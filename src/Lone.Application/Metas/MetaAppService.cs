using System.Globalization;
using Lone.Application.Seguranca;
using Lone.Contracts.Metas;
using Lone.Contracts.Seguranca;
using Lone.Domain.Comercial;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Metas;
using Lone.Domain.Validacao;

namespace Lone.Application.Metas;

public interface IMetaAppService
{
    Task<List<MetaResumoDto>> ListarAsync(bool incluirInativas, CancellationToken ct = default);
    Task<MetaDto?> ObterAsync(Guid id, CancellationToken ct = default);
    Task<MetaDto> SalvarAsync(MetaDto dto, CancellationToken ct = default);
    Task<MetaDto> AlterarSituacaoAsync(Guid id, AlterarSituacaoMetaRequisicao requisicao, CancellationToken ct = default);
    Task<MetaDto> LancarRealizadoAsync(Guid id, LancarRealizadoRequisicao requisicao, CancellationToken ct = default);
    Task<ResultadoImportacaoRealizadoDto> ImportarRealizadoAsync(Guid id, ImportarRealizadoRequisicao requisicao, CancellationToken ct = default);
    Task<ApuracaoDto> ApurarAsync(Guid id, CancellationToken ct = default);
    Task<MetaOpcoesDto> ListarOpcoesAsync(CancellationToken ct = default);
    Task<MetaDto> DesativarAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// Motor de metas (D6): rascunho editável → publicada (estrutura travada; realizado informado pode ser lançado) →
/// em apuração → fechada (resultado congelado nos alvos e participantes, base das comissões). Reabrir exige a
/// permissão de fechar e um motivo. Nada é apagado: meta cancelada é desativada; tudo fica na auditoria.
/// </summary>
public sealed class MetaAppService : IMetaAppService
{
    private readonly IMetaRepositorio _repositorio;
    private readonly IIndicadorRepositorio _indicadores;
    private readonly IMetaConsultas _consultas;
    private readonly IFonteIndicadores _fontes;
    private readonly IAutorizacao _autorizacao;
    private readonly IUsuarioAtual _usuario;
    private readonly IMotivoDaOperacao _motivo;
    private readonly TimeProvider _relogio;
    private readonly IEscopoPessoas _escopo;

    public MetaAppService(IMetaRepositorio repositorio, IIndicadorRepositorio indicadores, IMetaConsultas consultas, IFonteIndicadores fontes,
                          IAutorizacao autorizacao, IUsuarioAtual usuario, IMotivoDaOperacao motivo, TimeProvider relogio,
                          IEscopoPessoas escopo)
    {
        _escopo = escopo;
        _repositorio = repositorio;
        _indicadores = indicadores;
        _consultas = consultas;
        _fontes = fontes;
        _autorizacao = autorizacao;
        _usuario = usuario;
        _motivo = motivo;
        _relogio = relogio;
    }

    private DateOnly Hoje => DateOnly.FromDateTime(_relogio.GetLocalNow().DateTime);
    private string Usuario => _usuario.Nome.Length > 100 ? _usuario.Nome[..100] : _usuario.Nome;

    public async Task<List<MetaResumoDto>> ListarAsync(bool incluirInativas, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Metas.Visualizar);
        var lista = await _repositorio.ListarAsync(incluirInativas, ct);
        var escopo = await _escopo.ObterAsync(ct);
        if (escopo.Tudo || lista.Count == 0) return lista;

        // Alcance (Fase 2a-3, E13): só as metas com participante no alcance, contando só os seus (meta sem participantes: só Tudo).
        var participantes = await _repositorio.ParticipantesAsync([.. lista.Select(m => m.Id)], ct);
        var visiveis = new List<MetaResumoDto>();
        foreach (var m in lista)
        {
            var todos = participantes.GetValueOrDefault(m.Id, []);
            var meus = todos.Count(p => RegrasEscopo.ParticipanteNoAlcance(escopo, p.Nivel, p.ReferenciaId));
            if (meus == 0) continue;
            m.Participantes = meus;
            visiveis.Add(m);
        }
        return visiveis;
    }

    public async Task<MetaDto?> ObterAsync(Guid id, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Metas.Visualizar);
        var escopo = await _escopo.ObterAsync(ct);
        return await _repositorio.ObterAsync(id, ct) is { } meta && RegrasEscopo.MetaVisivel(escopo, meta)
            ? await ParaDtoAsync(meta, escopo, ct)
            : null;
    }

    /// <summary>A meta como está gravada, se o usuário a vê (senão, como se não existisse).</summary>
    private async Task<(Meta Meta, EscopoResolvido Escopo)> ObterVisivelAsync(Guid id, CancellationToken ct)
    {
        var escopo = await _escopo.ObterAsync(ct);
        var meta = await _repositorio.ObterAsync(id, ct);
        // Com alcance restrito, "não existe" e "fora do alcance" respondem igual (não revela quais existem).
        if (meta is null && escopo.Tudo) throw new ValidacaoException(["Esta meta não existe mais."]);
        if (meta is null || !RegrasEscopo.MetaVisivel(escopo, meta)) throw new ForaDoEscopoException();
        return (meta, escopo);
    }

    /// <summary>
    /// E13: mudar a estrutura, a situação ou cancelar exige a permissão (conferida por quem chama) e a meta inteira no alcance,
    /// para não mexer em alvos de quem o usuário não vê.
    /// </summary>
    private static void ExigirMetaInteira(EscopoResolvido escopo, Meta meta)
    {
        if (!RegrasEscopo.MetaInteiraNoAlcance(escopo, meta))
            throw new ValidacaoException(["Esta meta tem participantes fora do seu alcance: só quem alcança todos pode mudar a estrutura, a situação ou cancelá-la."]);
    }

    public async Task<MetaDto> SalvarAsync(MetaDto dto, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Metas.Gerenciar);
        var escopo = await _escopo.ObterAsync(ct);
        var anterior = dto.Id == Guid.Empty ? null : await _repositorio.ObterAsync(dto.Id, ct);
        if (anterior is not null && !RegrasEscopo.MetaVisivel(escopo, anterior)) throw new ForaDoEscopoException();
        if (anterior is not null) ExigirMetaInteira(escopo, anterior);
        if (dto.Participantes.Any(p => !RegrasEscopo.ParticipanteNoAlcance(escopo, p.Nivel, p.ReferenciaId)))
            throw new ValidacaoException(["Há participante fora do seu alcance: escolha só colaboradores e equipes que você gerencia."]);
        if (escopo.Restrito && dto.Participantes.Count == 0)
            throw new ValidacaoException(["Inclua ao menos um participante do seu alcance (colaborador ou equipe) antes de salvar."]);
        if (anterior is not null && !RegrasMeta.PodeEditarEstrutura(anterior))
            throw new ValidacaoException([$"A meta está \"{RegrasMeta.Nome(anterior.Situacao)}\": a estrutura só muda no rascunho (volte a meta para rascunho, se ainda não foi apurada)."]);

        var meta = ParaEntidade(dto);
        meta.Situacao = SituacaoMeta.Rascunho;
        meta.Ativo = anterior?.Ativo ?? true;
        if (!meta.Ativo) throw new ValidacaoException(["Esta meta foi cancelada."]);

        var indicadores = (await _indicadores.ListarAsync(ct)).ToDictionary(i => i.Id);
        var erros = RegrasMeta.Validar(meta, indicadores, completa: false);
        foreach (var item in meta.Itens)
            if (indicadores.TryGetValue(item.IndicadorId, out var ind) && !ind.Ativo && anterior?.Itens.All(i => i.IndicadorId != ind.Id) != false)
                erros.Add($"O indicador \"{ind.Nome}\" está desativado.");
        if (erros.Count > 0) throw new ValidacaoException(erros);

        if (anterior is null) meta.RegistrarEvento($"Meta '{meta.Nome}' criada.");
        await _repositorio.SalvarAsync(meta, anterior is null, ct);
        return (await ObterAsync(meta.Id, ct))!;
    }

    public async Task<MetaDto> AlterarSituacaoAsync(Guid id, AlterarSituacaoMetaRequisicao requisicao, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Metas.Visualizar);
        var (meta, escopo) = await ObterVisivelAsync(id, ct);
        var de = meta.Situacao;
        var para = requisicao.Situacao;
        if (!meta.Ativo) throw new ValidacaoException(["Esta meta foi cancelada."]);
        if (RegrasMeta.ErroTransicao(de, para) is { } erro) throw new ValidacaoException([erro]);

        var fechamento = para == SituacaoMeta.Fechada || de == SituacaoMeta.Fechada;
        _autorizacao.Exigir(fechamento ? Permissoes.Metas.Fechar : Permissoes.Metas.Gerenciar);
        ExigirMetaInteira(escopo, meta); // depois da permissão: sem ela, o motivo da recusa é a permissão
        var motivo = RegrasMeta.Texto(requisicao.Motivo);
        if (de == SituacaoMeta.Fechada && motivo is null) throw new ValidacaoException(["Informe o motivo para reabrir a apuração."]);
        if (motivo is { Length: > 250 }) throw new ValidacaoException(["O motivo pode ter no máximo 250 caracteres."]);
        _motivo.Motivo = motivo;
        meta.Versao = requisicao.Versao ?? meta.Versao;

        var indicadores = (await _indicadores.ListarAsync(ct)).ToDictionary(i => i.Id);
        if (para == SituacaoMeta.Publicada && de == SituacaoMeta.Rascunho &&
            RegrasMeta.Validar(meta, indicadores, completa: true) is { Count: > 0 } erros)
            throw new ValidacaoException(erros);
        if (para == SituacaoMeta.Rascunho && meta.Alvos.Any(a => a.Realizado is not null))
            throw new ValidacaoException(["Já há realizado lançado: a meta não pode voltar a rascunho."]);

        if (para == SituacaoMeta.Fechada)
            await CongelarAsync(meta, indicadores, ct);
        if (de == SituacaoMeta.Fechada)
            Descongelar(meta);

        meta.Situacao = para;
        meta.RegistrarEvento($"Meta '{meta.Nome}': {RegrasMeta.Nome(de)} → {RegrasMeta.Nome(para)}" + (motivo is null ? "." : $" ({motivo})."));
        await _repositorio.SalvarAsync(meta, novo: false, ct);
        return (await ObterAsync(id, ct))!;
    }

    /// <summary>Fechamento: grava o realizado calculado nos alvos e a nota/faixa/prêmio de cada participante (resultado congelado).</summary>
    private async Task CongelarAsync(Meta meta, Dictionary<Guid, Indicador> indicadores, CancellationToken ct)
    {
        var faltando = meta.Alvos.Count(a => a.Realizado is null && EhInformado(meta, a.ItemId, indicadores));
        if (faltando > 0)
            throw new ValidacaoException([$"Faltam {faltando} realizado(s) informado(s). Lance (ou lance zero) antes de fechar."]);

        var calculados = await CalcularAsync(meta, indicadores, ct);
        var agora = _relogio.GetUtcNow().UtcDateTime;
        foreach (var alvo in meta.Alvos.Where(a => !EhInformado(meta, a.ItemId, indicadores)))
        {
            alvo.Realizado = calculados.GetValueOrDefault((alvo.ParticipanteId, alvo.ItemId)) ?? 0;
            alvo.OrigemRealizado = OrigemRealizado.Calculado;
            alvo.RealizadoEm = agora;
            alvo.RealizadoPor = "sistema";
        }

        var resultado = RegrasMeta.Apurar(meta, indicadores, meta.Alvos.ToDictionary(a => (a.ParticipanteId, a.ItemId), a => a.Realizado));
        foreach (var r in resultado)
        {
            var p = meta.Participantes.First(x => x.Id == r.ParticipanteId);
            p.NotaFinal = r.Nota;
            p.Faixa = r.Faixa?.Nome;
            p.PercentualPremio = r.Faixa?.PercentualPremio ?? 0;
        }
        meta.FechadaEm = agora;
        meta.FechadaPor = Usuario;
    }

    /// <summary>Reabertura: volta a calcular (limpa o calculado congelado; o informado continua).</summary>
    private static void Descongelar(Meta meta)
    {
        foreach (var alvo in meta.Alvos.Where(a => a.OrigemRealizado == OrigemRealizado.Calculado))
        {
            alvo.Realizado = null;
            alvo.OrigemRealizado = null;
            alvo.RealizadoEm = null;
            alvo.RealizadoPor = null;
        }
        foreach (var p in meta.Participantes)
        {
            p.NotaFinal = null;
            p.Faixa = null;
            p.PercentualPremio = null;
        }
        meta.FechadaEm = null;
        meta.FechadaPor = null;
    }

    private static bool EhInformado(Meta meta, Guid itemId, IReadOnlyDictionary<Guid, Indicador> indicadores) =>
        meta.Itens.FirstOrDefault(i => i.Id == itemId) is { } item &&
        (!indicadores.TryGetValue(item.IndicadorId, out var ind) || ind.Fonte == FonteIndicador.Informado);

    public async Task<MetaDto> LancarRealizadoAsync(Guid id, LancarRealizadoRequisicao requisicao, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Metas.LancarRealizado);
        var (meta, escopo) = await ObterVisivelAsync(id, ct);
        var indicadores = (await _indicadores.ListarAsync(ct)).ToDictionary(i => i.Id);
        // Só os participantes no alcance recebem lançamento (os outros nem aparecem para ele).
        var meus = meta.Participantes.Where(p => RegrasEscopo.ParticipanteNoAlcance(escopo, p.Nivel, p.ReferenciaId)).Select(p => p.Id).ToHashSet();
        if (requisicao.Lancamentos.Any(l => !meus.Contains(l.ParticipanteId)))
            throw new ValidacaoException(["Lançamento para participante ou indicador que não está na meta."]);
        Aplicar(meta, indicadores, requisicao.Lancamentos, OrigemRealizado.Informado);
        meta.Versao = requisicao.Versao ?? meta.Versao;
        _motivo.Motivo = RegrasMeta.Texto(requisicao.Motivo);
        await _repositorio.SalvarAsync(meta, novo: false, ct);
        return (await ObterAsync(id, ct))!;
    }

    private void Aplicar(Meta meta, IReadOnlyDictionary<Guid, Indicador> indicadores, IEnumerable<LancamentoRealizadoDto> lancamentos, OrigemRealizado origem)
    {
        if (!meta.Ativo || !RegrasMeta.PodeLancarRealizado(meta))
            throw new ValidacaoException([$"A meta está \"{RegrasMeta.Nome(meta.Situacao)}\": o realizado só é lançado com ela publicada ou em apuração."]);
        var agora = _relogio.GetUtcNow().UtcDateTime;
        var erros = new List<string>();
        foreach (var l in lancamentos)
        {
            var alvo = meta.Alvos.FirstOrDefault(a => a.ParticipanteId == l.ParticipanteId && a.ItemId == l.ItemId);
            if (alvo is null) { erros.Add("Lançamento para participante ou indicador que não está na meta."); continue; }
            if (!EhInformado(meta, l.ItemId, indicadores)) { erros.Add("Indicadores calculados pelo cadastro não recebem lançamento."); continue; }
            if (l.Valor is < 0) { erros.Add("O realizado não pode ser negativo."); continue; }
            alvo.Realizado = l.Valor is { } v ? Math.Round(v, 4) : null;
            alvo.OrigemRealizado = l.Valor is null ? null : origem;
            alvo.RealizadoEm = l.Valor is null ? null : agora;
            alvo.RealizadoPor = l.Valor is null ? null : Usuario;
        }
        if (erros.Count > 0) throw new ValidacaoException(erros.Distinct().ToList());
    }

    public async Task<ResultadoImportacaoRealizadoDto> ImportarRealizadoAsync(Guid id, ImportarRealizadoRequisicao requisicao, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Metas.LancarRealizado);
        var (meta, escopo) = await ObterVisivelAsync(id, ct);
        if (meta.Itens.All(i => i.Id != requisicao.ItemId)) throw new ValidacaoException(["Escolha o indicador da importação."]);

        // Só os participantes no alcance (Fase 2a-3): nome de outro é "não está na meta", sem revelar que está.
        var participantes = meta.Participantes.Where(p => RegrasEscopo.ParticipanteNoAlcance(escopo, p.Nivel, p.ReferenciaId)).ToList();
        var nomes = await _consultas.NomesAsync(participantes.Select(p => (p.Nivel, p.ReferenciaId)), ct);
        var porNome = participantes
            .GroupBy(p => TextoBusca.Normalizar(nomes.GetValueOrDefault((p.Nivel, p.ReferenciaId), string.Empty)))
            .ToDictionary(g => g.Key, g => g.ToList());

        var resultado = new ResultadoImportacaoRealizadoDto();
        var linhas = (requisicao.Conteudo ?? string.Empty).Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < linhas.Length; i++)
        {
            var texto = linhas[i].Trim();
            if (texto.Length == 0) continue;
            var partes = texto.Split(';');
            var linha = new LinhaImportacaoRealizadoDto { Linha = i + 1, Participante = partes[0].Trim() };
            if (partes.Length < 2) linha.Erro = "Use \"participante;valor\".";
            else if (!decimal.TryParse(partes[1].Trim(), NumberStyles.Number, new CultureInfo("pt-BR"), out var valor) || valor < 0)
            {
                // Primeira linha pode ser o cabeçalho.
                if (i == 0 && resultado.Linhas.Count == 0) continue;
                linha.Erro = "Valor inválido (use vírgula para decimais: 1234,56).";
            }
            else
            {
                linha.Valor = valor;
                var chave = TextoBusca.Normalizar(linha.Participante);
                if (!porNome.TryGetValue(chave, out var achados)) linha.Erro = "Participante não está na meta (confira o nome).";
                else if (achados.Count > 1) linha.Erro = "Nome repetido entre os participantes: ajuste na tela.";
                else linha.ParticipanteId = achados[0].Id;
            }
            resultado.Linhas.Add(linha);
        }
        if (resultado.Linhas.Where(l => l.ParticipanteId is not null).GroupBy(l => l.ParticipanteId).Any(g => g.Count() > 1))
            foreach (var l in resultado.Linhas.Where(l => l.ParticipanteId is not null).GroupBy(l => l.ParticipanteId).Where(g => g.Count() > 1).SelectMany(g => g))
                l.Erro = "Participante repetido no arquivo.";
        resultado.Validas = resultado.Linhas.Count(l => l.Erro is null);

        if (requisicao.Confirmar)
        {
            if (resultado.Linhas.Any(l => l.Erro is not null))
                throw new ValidacaoException(["Corrija as linhas com erro antes de gravar (nada foi gravado)."]);
            var indicadores = (await _indicadores.ListarAsync(ct)).ToDictionary(x => x.Id);
            Aplicar(meta, indicadores, resultado.Linhas.Select(l => new LancamentoRealizadoDto
            {
                ParticipanteId = l.ParticipanteId!.Value, ItemId = requisicao.ItemId, Valor = l.Valor
            }), OrigemRealizado.Importado);
            meta.Versao = requisicao.Versao ?? meta.Versao;
            _motivo.Motivo = RegrasMeta.Texto(requisicao.Motivo) ?? "Importação de realizado (CSV)";
            await _repositorio.SalvarAsync(meta, novo: false, ct);
            resultado.Gravado = true;
        }
        return resultado;
    }

    public async Task<ApuracaoDto> ApurarAsync(Guid id, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Metas.Visualizar);
        var (meta, escopo) = await ObterVisivelAsync(id, ct);
        var indicadores = (await _indicadores.ListarAsync(ct)).ToDictionary(i => i.Id);
        var congelada = meta.Situacao == SituacaoMeta.Fechada;
        var meus = meta.Participantes.Where(p => RegrasEscopo.ParticipanteNoAlcance(escopo, p.Nivel, p.ReferenciaId)).Select(p => p.Id).ToHashSet();

        var realizados = meta.Alvos.ToDictionary(a => (a.ParticipanteId, a.ItemId), a => a.Realizado);
        if (!congelada && meta.Situacao != SituacaoMeta.Rascunho)
            foreach (var (chave, valor) in await CalcularAsync(meta, indicadores, ct))
                realizados[chave] = valor;

        var nomes = await _consultas.NomesAsync(meta.Participantes.Select(p => (p.Nivel, p.ReferenciaId)), ct);
        var apuracao = new ApuracaoDto { MetaId = meta.Id, Situacao = meta.Situacao, Congelada = congelada };
        // A apuração é da meta inteira (a nota de cada um não depende dos outros), mas só os do alcance aparecem.
        foreach (var r in RegrasMeta.Apurar(meta, indicadores, realizados).Where(r => meus.Contains(r.ParticipanteId)))
        {
            var p = meta.Participantes.First(x => x.Id == r.ParticipanteId);
            apuracao.Participantes.Add(new ApuracaoParticipanteDto
            {
                ParticipanteId = p.Id,
                Nome = nomes.GetValueOrDefault((p.Nivel, p.ReferenciaId), "(participante)"),
                Nivel = p.Nivel,
                Nota = congelada ? p.NotaFinal ?? r.Nota : r.Nota,
                Faixa = congelada ? p.Faixa : r.Faixa?.Nome,
                PercentualPremio = congelada ? p.PercentualPremio : r.Faixa?.PercentualPremio ?? 0,
                Itens = meta.Itens.OrderBy(i => i.Ordem).Select(i =>
                {
                    var alvo = meta.Alvos.FirstOrDefault(a => a.ParticipanteId == p.Id && a.ItemId == i.Id);
                    return new ApuracaoItemDto
                    {
                        ItemId = i.Id,
                        Indicador = indicadores.TryGetValue(i.IndicadorId, out var ind) ? ind.Nome : "(indicador)",
                        Alvo = alvo?.Alvo,
                        Realizado = realizados.GetValueOrDefault((p.Id, i.Id)),
                        Origem = alvo?.OrigemRealizado ?? (EhInformado(meta, i.Id, indicadores) ? null : OrigemRealizado.Calculado),
                        Atingimento = r.AtingimentoPorItem.GetValueOrDefault(i.Id)
                    };
                }).ToList()
            });
        }
        var faltando = meta.Alvos.Count(a => meus.Contains(a.ParticipanteId) && a.Realizado is null && EhInformado(meta, a.ItemId, indicadores));
        if (faltando > 0 && meta.Situacao != SituacaoMeta.Rascunho)
            apuracao.Avisos.Add($"{faltando} realizado(s) informado(s) ainda não lançado(s): contam zero até serem lançados.");
        if (!congelada && meta.FimEm >= Hoje)
            apuracao.Avisos.Add("O período ainda não terminou: o resultado é parcial.");
        apuracao.Participantes = apuracao.Participantes.OrderByDescending(x => x.Nota).ToList();
        return apuracao;
    }

    private async Task<Dictionary<(Guid, Guid), decimal?>> CalcularAsync(Meta meta, IReadOnlyDictionary<Guid, Indicador> indicadores, CancellationToken ct)
    {
        var resultado = new Dictionary<(Guid, Guid), decimal?>();
        foreach (var item in meta.Itens)
        {
            if (!indicadores.TryGetValue(item.IndicadorId, out var ind) || ind.Fonte == FonteIndicador.Informado) continue;
            var valores = await _fontes.CalcularAsync(ind.Fonte, meta.InicioEm, meta.FimEm, meta.Participantes, ct);
            foreach (var p in meta.Participantes) resultado[(p.Id, item.Id)] = valores.GetValueOrDefault(p.Id);
        }
        return resultado;
    }

    public async Task<MetaOpcoesDto> ListarOpcoesAsync(CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Metas.Visualizar);
        var escopo = await _escopo.ObterAsync(ct);
        return new MetaOpcoesDto
        {
            Indicadores = (await _indicadores.ListarAsync(ct)).OrderBy(i => i.Nome).Select(IndicadorAppService.ParaDto).ToList(),
            // Participantes que ele pode pôr numa meta: os do alcance (Fase 2a-3, E13).
            Participantes = [.. (await _consultas.OpcoesAsync(Hoje, ct)).Where(p => RegrasEscopo.ParticipanteNoAlcance(escopo, p.Nivel, p.Id))]
        };
    }

    public async Task<MetaDto> DesativarAsync(Guid id, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Metas.Gerenciar);
        var (meta, escopo) = await ObterVisivelAsync(id, ct);
        ExigirMetaInteira(escopo, meta);
        if (meta.Situacao != SituacaoMeta.Rascunho) throw new ValidacaoException(["Só um rascunho pode ser cancelado."]);
        if (meta.Ativo)
        {
            meta.Ativo = false;
            meta.RegistrarEvento($"Meta '{meta.Nome}' cancelada.");
            await _repositorio.SalvarAsync(meta, novo: false, ct);
        }
        return (await ObterAsync(id, ct))!;
    }

    // ---------------------------------------------------------------- Conversão

    private static Meta ParaEntidade(MetaDto d)
    {
        var id = d.Id == Guid.Empty ? IdSequencial.Novo() : d.Id;
        Guid Novo(Guid g) => g == Guid.Empty ? IdSequencial.Novo() : g;
        var meta = new Meta
        {
            Id = id,
            Versao = d.Versao,
            Nome = RegrasMeta.Texto(d.Nome) ?? string.Empty,
            Descricao = RegrasMeta.Texto(d.Descricao),
            InicioEm = d.InicioEm,
            FimEm = d.FimEm,
            LimiteAtingimento = d.LimiteAtingimento,
            Itens = d.Itens.Select((i, n) => new MetaItem { Id = Novo(i.Id), MetaId = id, IndicadorId = i.IndicadorId, Peso = i.Peso, Ordem = n }).ToList(),
            Faixas = d.Faixas.Select(f => new MetaFaixa
            {
                Id = Novo(f.Id), MetaId = id, InicioPercentual = f.InicioPercentual, Nome = RegrasMeta.Texto(f.Nome) ?? string.Empty, PercentualPremio = f.PercentualPremio
            }).ToList(),
            Participantes = d.Participantes.Select(p => new MetaParticipante { Id = Novo(p.Id), MetaId = id, Nivel = p.Nivel, ReferenciaId = p.ReferenciaId }).ToList()
        };
        // Alvos só dos participantes e itens que continuam na meta.
        meta.Alvos = d.Alvos
            .Where(a => meta.Participantes.Any(p => p.Id == a.ParticipanteId) && meta.Itens.Any(i => i.Id == a.ItemId))
            .Select(a => new MetaAlvo { Id = Novo(a.Id), MetaId = id, ParticipanteId = a.ParticipanteId, ItemId = a.ItemId, Alvo = a.Alvo })
            .ToList();
        return meta;
    }

    /// <summary>A meta para a tela, só com os participantes (e alvos) no alcance; marca quando faltou alguém (E13).</summary>
    private async Task<MetaDto> ParaDtoAsync(Meta completa, EscopoResolvido escopo, CancellationToken ct)
    {
        var meus = completa.Participantes.Where(p => RegrasEscopo.ParticipanteNoAlcance(escopo, p.Nivel, p.ReferenciaId)).ToList();
        var m = completa;
        if (meus.Count < completa.Participantes.Count)
        {
            var ids = meus.Select(p => p.Id).ToHashSet();
            m = new Meta
            {
                Id = completa.Id, Versao = completa.Versao, Nome = completa.Nome, Descricao = completa.Descricao,
                InicioEm = completa.InicioEm, FimEm = completa.FimEm, Situacao = completa.Situacao,
                LimiteAtingimento = completa.LimiteAtingimento, Ativo = completa.Ativo,
                FechadaEm = completa.FechadaEm, FechadaPor = completa.FechadaPor,
                Itens = completa.Itens, Faixas = completa.Faixas, Participantes = meus,
                Alvos = [.. completa.Alvos.Where(a => ids.Contains(a.ParticipanteId))]
            };
        }
        var dto = await ParaDtoAsync(m, ct);
        dto.ParcialPorAlcance = meus.Count < completa.Participantes.Count;
        return dto;
    }

    private async Task<MetaDto> ParaDtoAsync(Meta m, CancellationToken ct)
    {
        var nomes = await _consultas.NomesAsync(m.Participantes.Select(p => (p.Nivel, p.ReferenciaId)), ct);
        return new MetaDto
        {
            Id = m.Id, Versao = m.Versao, Nome = m.Nome, Descricao = m.Descricao, InicioEm = m.InicioEm, FimEm = m.FimEm,
            Situacao = m.Situacao, LimiteAtingimento = m.LimiteAtingimento, Ativo = m.Ativo,
            FechadaEm = m.FechadaEm is { } f ? DateTime.SpecifyKind(f, DateTimeKind.Utc) : null, FechadaPor = m.FechadaPor,
            Itens = m.Itens.OrderBy(i => i.Ordem).Select(i => new MetaItemDto { Id = i.Id, IndicadorId = i.IndicadorId, Peso = i.Peso, Ordem = i.Ordem }).ToList(),
            Faixas = m.Faixas.OrderBy(f => f.InicioPercentual)
                .Select(f => new MetaFaixaDto { Id = f.Id, InicioPercentual = f.InicioPercentual, Nome = f.Nome, PercentualPremio = f.PercentualPremio }).ToList(),
            Participantes = m.Participantes.OrderBy(p => p.Nivel).ThenBy(p => nomes.GetValueOrDefault((p.Nivel, p.ReferenciaId)))
                .Select(p => new MetaParticipanteDto
                {
                    Id = p.Id, Nivel = p.Nivel, ReferenciaId = p.ReferenciaId, Nome = nomes.GetValueOrDefault((p.Nivel, p.ReferenciaId)),
                    NotaFinal = p.NotaFinal, Faixa = p.Faixa, PercentualPremio = p.PercentualPremio
                }).ToList(),
            Alvos = m.Alvos.Select(a => new MetaAlvoDto
            {
                Id = a.Id, ParticipanteId = a.ParticipanteId, ItemId = a.ItemId, Alvo = a.Alvo, Realizado = a.Realizado,
                OrigemRealizado = a.OrigemRealizado, RealizadoEm = a.RealizadoEm is { } r ? DateTime.SpecifyKind(r, DateTimeKind.Utc) : null,
                RealizadoPor = a.RealizadoPor
            }).ToList()
        };
    }
}
