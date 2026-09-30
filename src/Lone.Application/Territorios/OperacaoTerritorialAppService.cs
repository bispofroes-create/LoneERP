using Lone.Application.Consultas;
using Lone.Application.Municipios;
using Lone.Application.Seguranca;
using Lone.Contracts.Pessoas;
using Lone.Contracts.Seguranca;
using Lone.Contracts.Territorios;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Territorios;
using Lone.Domain.Validacao;

namespace Lone.Application.Territorios;

public interface IOperacaoTerritorialAppService
{
    Task<OperacoesTerritoriaisOpcoesDto> OpcoesAsync(CancellationToken ct = default);
    Task<List<OperacaoTerritorialResumoDto>> ListarAsync(Guid? mapaId, CancellationToken ct = default);
    Task<OperacaoTerritorialDto?> ObterAsync(Guid id, CancellationToken ct = default);
    Task<OperacaoTerritorialDto> CriarAsync(CriarOperacaoTerritorialRequisicao requisicao, CancellationToken ct = default);
    Task<OperacaoTerritorialDto> AlterarAsync(Guid id, AlterarOperacaoTerritorialRequisicao requisicao, CancellationToken ct = default);
    Task<OperacaoTerritorialDto> IncluirMudancaAsync(Guid id, IncluirMudancaTerritorialRequisicao requisicao, CancellationToken ct = default);
    Task<OperacaoTerritorialDto> MoverClienteAsync(Guid id, MoverClienteTerritorialRequisicao requisicao, CancellationToken ct = default);
    Task<OperacaoTerritorialDto> RetirarMudancaAsync(Guid id, Guid mudancaId, VersaoOperacaoTerritorialRequisicao requisicao, CancellationToken ct = default);
    Task<OperacaoTerritorialDto> SimularAsync(Guid id, VersaoOperacaoTerritorialRequisicao requisicao, CancellationToken ct = default);
    Task<List<SimulacaoTerritorialResumoDto>> SimulacoesAsync(Guid id, CancellationToken ct = default);
    Task<PaginaItensOperacaoTerritorialDto> ItensSimulacaoAsync(Guid simulacaoId, FiltroItensOperacaoTerritorialDto filtro, CancellationToken ct = default);
    Task<PaginaItensOperacaoTerritorialDto> ItensAplicadosAsync(Guid id, FiltroItensOperacaoTerritorialDto filtro, CancellationToken ct = default);
    Task<OperacaoTerritorialDto> AplicarAsync(Guid id, AplicarOperacaoTerritorialRequisicao requisicao, CancellationToken ct = default);
    Task<OperacaoTerritorialDto> CancelarAsync(Guid id, MotivoOperacaoTerritorialRequisicao requisicao, CancellationToken ct = default);
    Task<OperacaoTerritorialDto> DesfazerAsync(Guid id, MotivoOperacaoTerritorialRequisicao requisicao, CancellationToken ct = default);

    /// <summary>
    /// Divergências do mapa hoje (DN-14, seção N): quem o motor colocaria diferente do que está gravado, e por quê — sem
    /// operação, sem gravar nada. Para corrigir, cria-se uma operação (mesmo sem mudanças) e aplica-se.
    /// </summary>
    Task<DivergenciasTerritoriaisDto> DivergenciasAsync(Guid mapaId, FiltroItensOperacaoTerritorialDto filtro, CancellationToken ct = default);
}

/// <summary>
/// Operações territoriais TE- (Fase 2b-1b; plano, seções F a K). O ciclo é Rascunho → Simulada → Aplicada, com Cancelada e
/// Desfeita (DN-04); Agendada, Em vigor e Desatualizada são indicadores. Toda mudança de fato oficial passa por aqui:
/// simular calcula sem gravar fato nenhum e guarda a evidência (DN-03); aplicar repete o cálculo DENTRO da transação
/// travada e só grava se der exatamente o que foi mostrado (assinatura), tudo ou nada (T11, DN-12). PLANEJAR cria, edita e
/// simula (e cancela a própria operação: RT-2); APLICAR aplica, cancela e desfaz (DN-06); as duas exigem alcance Tudo.
/// </summary>
public sealed class OperacaoTerritorialAppService : IOperacaoTerritorialAppService
{
    private readonly IOperacaoTerritorialRepositorio _operacoes;
    private readonly IMotorTerritorialDados _dados;
    private readonly IMapaTerritorialRepositorio _mapas;
    private readonly ITerritorioRepositorio _territorios;
    private readonly IConsultasTerritoriais _consultas;
    private readonly IUsoTerritorial _uso;
    private readonly IConsultaPessoas _consultaPessoas;
    private readonly IMunicipioRepositorio _municipios;
    private readonly IAutorizacao _autorizacao;
    private readonly IUsuarioAtual _usuario;
    private readonly IAlcanceDoUsuario _alcance;
    private readonly IEscopoPessoas _escopo;
    private readonly IPessoasNoEscopo _noEscopo;
    private readonly Auditoria.IAuditoriaConsultas _auditoria;
    private readonly TimeProvider _relogio;

    public OperacaoTerritorialAppService(IOperacaoTerritorialRepositorio operacoes, IMotorTerritorialDados dados, IMapaTerritorialRepositorio mapas,
                                         ITerritorioRepositorio territorios, IConsultasTerritoriais consultas, IUsoTerritorial uso,
                                         IConsultaPessoas consultaPessoas, IMunicipioRepositorio municipios, IAutorizacao autorizacao,
                                         IUsuarioAtual usuario, IAlcanceDoUsuario alcance, IEscopoPessoas escopo, IPessoasNoEscopo noEscopo,
                                         Auditoria.IAuditoriaConsultas auditoria, TimeProvider relogio)
    {
        _operacoes = operacoes;
        _dados = dados;
        _mapas = mapas;
        _territorios = territorios;
        _consultas = consultas;
        _uso = uso;
        _consultaPessoas = consultaPessoas;
        _municipios = municipios;
        _autorizacao = autorizacao;
        _usuario = usuario;
        _alcance = alcance;
        _escopo = escopo;
        _noEscopo = noEscopo;
        _auditoria = auditoria;
        _relogio = relogio;
    }

    private DateOnly Hoje => DateOnly.FromDateTime(_relogio.GetLocalNow().DateTime);

    // ------------------------------------------------------------------ Permissões (DN-06, seção O)

    public const string MensagemAlcance =
        "Operações territoriais mexem no mapa inteiro: planejar e aplicar exigem alcance Tudo no seu perfil.";

    private bool Tudo => _alcance.Alcance == AlcanceComercial.Tudo;
    private bool PodePlanejar => _autorizacao.Possui(Permissoes.Territorios.Planejar) && Tudo;
    private bool PodeAplicar => _autorizacao.Possui(Permissoes.Territorios.Aplicar) && Tudo;

    private void ExigirPlanejar()
    {
        _autorizacao.Exigir(Permissoes.Territorios.Planejar);
        if (!Tudo) throw new AcessoNegadoException(Permissoes.Territorios.Planejar, MensagemAlcance);
    }

    private void ExigirAplicar()
    {
        _autorizacao.Exigir(Permissoes.Territorios.Aplicar);
        if (!Tudo) throw new AcessoNegadoException(Permissoes.Territorios.Aplicar, MensagemAlcance);
    }

    private async Task<OperacaoTerritorial> CarregarAsync(Guid id, CancellationToken ct) =>
        await _operacoes.ObterAsync(id, ct) ?? throw new ValidacaoException(["Esta operação territorial não existe mais."]);

    private async Task<MapaTerritorial> MapaAsync(Guid mapaId, CancellationToken ct) =>
        await _mapas.ObterAsync(mapaId, ct) ?? throw new ValidacaoException(["O mapa territorial da operação não existe mais."]);

    /// <summary>A versão que a tela abriu: sem ela, nada é gravado (nunca "última gravação vence").</summary>
    private static void ExigirVersao(OperacaoTerritorial op, byte[]? vista)
    {
        if (vista is null || op.Versao is null || !op.Versao.AsSpan().SequenceEqual(vista))
            throw new ConflitoDeEdicaoException(RegrasOperacaoTerritorial.MensagemOperacaoAlterada);
        op.Versao = vista;
    }

    // ------------------------------------------------------------------ Leitura

    public async Task<OperacoesTerritoriaisOpcoesDto> OpcoesAsync(CancellationToken ct = default)
    {
        AcessoTerritorios.ExigirLeituraDoMotor(_autorizacao);
        var mapas = await _mapas.ListarAsync(ct);
        var emUso = await _uso.MapasEmUsoAsync(ct);
        var parametros = await _dados.LerAsync(l => l.ParametrosAsync(ct), ct);
        var (campos, restritos) = await CamposRegraAsync(ct);
        return new OperacoesTerritoriaisOpcoesDto
        {
            Mapas = [.. mapas.OrderBy(m => m.Nome, StringComparer.CurrentCultureIgnoreCase)
                .Select(m => MapaTerritorialAppService.ParaDto(m, new Dictionary<Guid, string>(), new Dictionary<Guid, string>(), emUso.Contains(m.Id), 0))],
            CamposRegra = campos,
            CamposRestritos = restritos,
            Parametros = ParametrosTerritoriaisAppService.ParaDto(parametros),
            PodePlanejar = PodePlanejar,
            PodeAplicar = PodeAplicar
        };
    }

    /// <summary>
    /// Os campos da lista fechada (DN-07) com as escolhas, no mesmo formato do filtro de Pessoas (a tela reaproveita o
    /// painel). Os que pedem permissão que o usuário não tem vêm em <c>restritos</c>: ele vê a condição como "restrita".
    /// </summary>
    private async Task<(List<CampoFiltroDto> Campos, List<string> Restritos)> CamposRegraAsync(CancellationToken ct)
    {
        var opcoes = await _consultaPessoas.OpcoesAsync(Hoje, ct);
        var doBanco = await _consultaPessoas.OpcoesFiltroAsync(ct);
        static List<OpcaoFiltroDto> Da(List<OpcaoConsultaDto> lista) => lista.Select(o => new OpcaoFiltroDto(o.Id.ToString("D"), o.Nome)).ToList();
        var campos = new List<CampoFiltroDto>();
        var restritos = new List<string>();
        foreach (var d in CatalogoFiltrosPessoas.CamposRegraTerritorio)
        {
            if ((d.Permissao is { } p && !_autorizacao.Possui(p)) || (d.PermissaoEmRegraTerritorio is { } q && !_autorizacao.Possui(q)))
            {
                restritos.Add(d.Id);
                continue;
            }
            campos.Add(new CampoFiltroDto
            {
                Id = d.Id, Grupo = d.Grupo, Nome = d.Nome, Tipo = d.Tipo, Operadores = [.. d.Operadores], ValePara = d.ValePara,
                Dica = EhEndereco(d.Id) ? "Endereço de referência do mapa (o principal da finalidade escolhida no mapa)." : d.Dica,
                TextoLivre = d.TextoLivre, SomenteDigitos = d.SomenteDigitos, TamanhoMinimo = d.TamanhoMinimo, TamanhoMaximo = d.TamanhoMaximo,
                Opcoes = d.FonteOpcoes switch
                {
                    CatalogoFiltrosPessoas.FontePapeis => Da(opcoes.Papeis),
                    CatalogoFiltrosPessoas.FonteEtiquetas => Da(opcoes.Etiquetas),
                    { } fonte when doBanco.TryGetValue(fonte, out var lista) => lista,
                    _ => d.Opcoes?.ToList() ?? []
                },
                OpcoesSobDemanda = d.FonteOpcoes == CatalogoFiltrosPessoas.FonteMunicipios ? CatalogoFiltrosPessoas.FonteMunicipios : null
            });
        }
        return (campos, restritos);
    }

    private static bool EhEndereco(string campo) =>
        campo is CamposFiltroPessoas.Uf or CamposFiltroPessoas.Municipio or CamposFiltroPessoas.Cidade or CamposFiltroPessoas.Bairro or CamposFiltroPessoas.Cep;

    public async Task<List<OperacaoTerritorialResumoDto>> ListarAsync(Guid? mapaId, CancellationToken ct = default)
    {
        AcessoTerritorios.ExigirLeituraDoMotor(_autorizacao);
        var operacoes = await _operacoes.ListarAsync(mapaId, ct);
        var mapas = (await _mapas.ListarAsync(ct)).ToDictionary(m => m.Id, m => m.Nome);
        var hoje = Hoje;
        return [.. operacoes.Select(o =>
        {
            var dto = new OperacaoTerritorialResumoDto();
            PreencherResumo(dto, o, mapas.GetValueOrDefault(o.MapaId), hoje);
            return dto;
        })];
    }

    private static void PreencherResumo(OperacaoTerritorialResumoDto dto, OperacaoTerritorial o, string? mapa, DateOnly hoje)
    {
        dto.Id = o.Id;
        dto.Numero = o.Numero;
        dto.MapaId = o.MapaId;
        dto.Mapa = mapa;
        dto.EfeitoEm = o.EfeitoEm;
        dto.Motivo = o.Motivo;
        dto.Situacao = o.Situacao;
        dto.Agendada = o.Agendada(hoje);
        dto.EmVigor = o.EmVigor(hoje);
        dto.SituacaoNome = o.Situacao switch
        {
            SituacaoOperacaoTerritorial.Rascunho => "Rascunho",
            SituacaoOperacaoTerritorial.Simulada => "Simulada",
            SituacaoOperacaoTerritorial.Aplicada => o.Agendada(hoje) ? "Agendada" : "Em vigor",
            SituacaoOperacaoTerritorial.Cancelada => "Cancelada",
            SituacaoOperacaoTerritorial.Desfeita => "Desfeita",
            _ => o.Situacao.ToString()
        };
        dto.CriadaPor = o.CriadaPor;
        dto.CriadaEm = o.CriadoEm;
        dto.AplicadaPor = o.AplicadaPor;
        dto.AplicadaEm = o.AplicadaEm;
        dto.QuantidadeMudancas = o.Mudancas.Count;
        dto.Entraram = o.Entraram;
        dto.Sairam = o.Sairam;
        dto.Mudaram = o.Mudaram;
    }

    public async Task<OperacaoTerritorialDto?> ObterAsync(Guid id, CancellationToken ct = default)
    {
        AcessoTerritorios.ExigirLeituraDoMotor(_autorizacao);
        return await _operacoes.ObterAsync(id, ct) is { } op ? await ParaDtoAsync(op, ct) : null;
    }

    /// <summary>
    /// A operação na tela, com os indicadores calculados agora (DN-04): simulação desatualizada (versão do motor, base de
    /// mudança, janela do T8), avisos entre rascunhos (seção K), bloqueio pela operação futura (RT-1) e o que o usuário pode
    /// fazer.
    /// </summary>
    private async Task<OperacaoTerritorialDto> ParaDtoAsync(OperacaoTerritorial op, CancellationToken ct)
    {
        var hoje = Hoje;
        var mapa = await MapaAsync(op.MapaId, ct);
        var territorios = (await _territorios.ListarDoMapaAsync(op.MapaId, ct)).ToDictionary(t => t.Id);
        var (motor, parametros, estado, versoes) = await _dados.LerAsync(async l =>
        {
            var m = await l.MotorAsync(op.MapaId, ct);
            var p = await l.ParametrosAsync(ct);
            EstadoTerritorialEmD? e = op.Aberta ? await l.EstadoAsync(mapa, op.EfeitoEm, comAtribuicoes: false, ct) : null;
            var v = op.Aberta
                ? await l.VersoesDasBasesAsync([.. op.Mudancas.Select(x => x.RegraBaseId).OfType<Guid>()],
                                                [.. op.Mudancas.Select(x => x.ExcecaoBaseId).OfType<Guid>()], ct)
                : new Dictionary<Guid, byte[]>();
            return (m, p, e, v);
        }, ct);
        var pessoas = await _consultas.NomesDePessoasAsync([.. op.Mudancas.Select(m => m.PessoaId).OfType<Guid>().Distinct()], ct);
        var simulacoes = op.SimulacaoAtualId is null ? [] : await _operacoes.SimulacoesAsync(op.Id, ct);
        var atual = simulacoes.FirstOrDefault(s => s.Id == op.SimulacaoAtualId);

        var dto = new OperacaoTerritorialDto
        {
            Versao = op.Versao, Observacao = op.Observacao, MapaExclusivo = mapa.Exclusivo,
            CanceladaPor = op.CanceladaPor, CanceladaEm = op.CanceladaEm, CanceladaMotivo = op.CanceladaMotivo,
            DesfeitaPor = op.DesfeitaPor, DesfeitaEm = op.DesfeitaEm, DesfeitaMotivo = op.DesfeitaMotivo, OrigemAtualizada = op.OrigemAtualizada,
            SimulacaoAtual = atual is null ? null : ParaDto(atual, atual: true)
        };
        PreencherResumo(dto, op, mapa.Nome, hoje);

        // Bases velhas (L5): conferidas contra o estado de agora na data de efeito.
        var basesVelhas = estado is null ? new Dictionary<Guid, string>() : BasesVelhas(op, estado, versoes);
        foreach (var m in op.Mudancas.OrderBy(m => m.Ordem))
            dto.Mudancas.Add(ParaDto(m, territorios, pessoas, basesVelhas.GetValueOrDefault(m.Id)));

        var ultimaNumero = motor?.UltimaOperacaoId is { } u ? (await _operacoes.NumerosAsync([u], ct)).GetValueOrDefault(u) : null;
        var acimaDoLimite = false;
        if (op.Aberta)
        {
            var estrutural = op.Mudancas.Any(m => RegrasOperacaoTerritorial.Estrutural(m.Tipo));
            var errosData = RegrasOperacaoTerritorial.ValidarEfeito(op.EfeitoEm, hoje, parametros.DiasRetroativosMaximo, motor?.UltimoEfeitoEm,
                                                                    ultimaNumero, estrutural);
            if (op.Situacao == SituacaoOperacaoTerritorial.Simulada && atual is not null)
            {
                if (motor?.Versao is null || !motor.Versao.AsSpan().SequenceEqual(atual.VersaoMotor))
                    dto.MotivosDesatualizada.Add("O mapa mudou depois da simulação (outra operação aplicada, árvore alterada ou mapa desativado/reativado).");
                if (basesVelhas.Count > 0) dto.MotivosDesatualizada.Add("Há mudança planejada sobre uma base que não é mais a vigente.");
                if (errosData.Count > 0) dto.MotivosDesatualizada.Add("A data de efeito saiu da janela permitida: " + string.Join(" ", errosData));
                dto.Desatualizada = dto.MotivosDesatualizada.Count > 0;

                // DN-12: o volume vem da simulação atual; acima do limite não aplica, entre o aviso e o limite só avisa.
                var gravados = RegrasOperacaoTerritorial.ClientesGravados(atual.Entram, atual.Saem, atual.Mudam, atual.OrigemAtualizada);
                if (RegrasOperacaoTerritorial.AcimaDoLimite(gravados) is { } limite)
                {
                    dto.Avisos.Add(limite);
                    acimaDoLimite = true;
                }
                else if (RegrasOperacaoTerritorial.AvisoDeVolume(gravados) is { } volume)
                    dto.Avisos.Add(volume);
            }
            // RT-1: uma operação aplicada com efeito futuro bloqueia as de efeito anterior neste mapa.
            if (motor?.UltimoEfeitoEm is { } ultimoEfeito && ultimoEfeito > hoje && op.EfeitoEm < ultimoEfeito && motor.UltimaOperacaoId is { } bloqueio)
            {
                dto.BloqueadaPorId = bloqueio;
                dto.BloqueadaPor = ultimaNumero;
                dto.PodeDesfazerBloqueio = PodeAplicar;
                dto.Avisos.Add($"Esta operação não pode ser aplicada porque existe a {ultimaNumero} com efeito futuro neste mapa. " +
                               $"Para realizar esta correção, primeiro desfaça a {ultimaNumero}.");
            }
            if (op.EfeitoEm > hoje)
                dto.Avisos.Add("Efeito futuro: depois de aplicada, esta operação impede aplicar neste mapa qualquer operação com efeito " +
                               "anterior a ela, até ser desfeita ou entrar em vigor.");
            await AvisosEntreRascunhosAsync(op, territorios, pessoas, dto, ct);
        }

        // A história da operação: os eventos de negócio (as alterações campo a campo ficam no histórico completo da auditoria).
        // História (revisão de 29/09): eventos de negócio + o antes → depois dos campos que o planejador edita (data, motivo,
        // observação). As mudanças não são editadas, só incluídas e retiradas, e cada uma tem o seu evento com o texto dela.
        var registros = await _auditoria.ListarPorRaizAsync(nameof(OperacaoTerritorial), op.Id, 500, null, ct);
        dto.Historico = [.. registros.Where(r => r.Acao == AcaoAuditoria.Evento || EdicaoDeCampo(r))];
        // Autoria das edições: quem, além de quem criou, editou o rascunho. A auditoria guarda o nome de quem gravou (não o
        // Id); os eventos de edição já dizem "X alterou a operação criada por Y" comparando pelo Id.
        var edicoes = registros.Where(r => EdicaoDeCampo(r) || r.Entidade == nameof(OperacaoTerritorialMudanca)).ToList();
        if (edicoes.Count > 0)
        {
            dto.UltimaEdicaoPor = edicoes[0].Usuario;
            dto.UltimaEdicaoEm = edicoes[0].DataHora;
        }
        dto.EditadaTambemPor = [.. edicoes.Select(r => r.Usuario).Where(u => !string.Equals(u, op.CriadaPor, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.CurrentCulture)];

        var podeCancelar = op.Aberta && (PodeAplicar || (PodePlanejar && _usuario.Id is { } eu && op.CriadaPorId == eu));
        dto.PodeEditar = op.Aberta && PodePlanejar;
        dto.PodeSimular = op.Aberta && PodePlanejar;
        dto.PodeAplicar = op.Situacao == SituacaoOperacaoTerritorial.Simulada && PodeAplicar && !dto.Desatualizada && dto.BloqueadaPorId is null &&
                          atual is { Inconsistencias: 0 } && !acimaDoLimite;
        dto.PodeCancelar = podeCancelar;
        dto.PodeDesfazer = op.Situacao == SituacaoOperacaoTerritorial.Aplicada && PodeAplicar && op.EfeitoEm > hoje && motor?.UltimaOperacaoId == op.Id;
        return dto;
    }

    /// <summary>Outra operação aberta no mesmo mapa mexe no mesmo território ou cliente (seção K: convivem, com aviso).</summary>
    private async Task AvisosEntreRascunhosAsync(OperacaoTerritorial op, IReadOnlyDictionary<Guid, Territorio> territorios,
                                                 IReadOnlyDictionary<Guid, string> pessoas, OperacaoTerritorialDto dto, CancellationToken ct)
    {
        var outras = (await _operacoes.ListarAsync(op.MapaId, ct)).Where(o => o.Id != op.Id && o.Aberta).ToList();
        foreach (var outra in outras)
        {
            var mesmosTerritorios = outra.Mudancas.Select(m => m.TerritorioId).OfType<Guid>()
                .Intersect(op.Mudancas.Select(m => m.TerritorioId).OfType<Guid>()).ToList();
            var mesmosClientes = outra.Mudancas.Select(m => m.PessoaId).OfType<Guid>()
                .Intersect(op.Mudancas.Select(m => m.PessoaId).OfType<Guid>()).ToList();
            foreach (var t in mesmosTerritorios)
                dto.Avisos.Add($"\"{territorios.GetValueOrDefault(t)?.Nome ?? "?"}\" também está na {outra.Numero} ({RegrasOperacaoTerritorial.Nome(outra.Situacao)}).");
            foreach (var p in mesmosClientes)
                dto.Avisos.Add($"O cliente {pessoas.GetValueOrDefault(p) ?? "?"} também está na {outra.Numero} ({RegrasOperacaoTerritorial.Nome(outra.Situacao)}).");
        }
    }

    /// <summary>Mudanças cuja base (a versão que o usuário viu) não é mais a vigente: mudança → mensagem.</summary>
    private static Dictionary<Guid, string> BasesVelhas(OperacaoTerritorial op, EstadoTerritorialEmD estado, IReadOnlyDictionary<Guid, byte[]> versoes)
    {
        var resultado = new Dictionary<Guid, string>();
        var regraVigente = estado.RegrasVigentes.Where(r => r.VigenteEm(estado.Efeito)).ToDictionary(r => r.TerritorioId);
        var excecoes = estado.ExcecoesVigentes.Where(x => x.VigenteEm(estado.Efeito)).ToDictionary(x => x.Id);
        foreach (var m in op.Mudancas)
        {
            string? velha = null;
            switch (m.Tipo)
            {
                case TipoMudancaTerritorial.NovaVersaoRegra or TipoMudancaTerritorial.EncerrarRegra:
                    var vigente = m.TerritorioId is { } t ? regraVigente.GetValueOrDefault(t) : null;
                    if (vigente?.Id != m.RegraBaseId || (m.RegraBaseId is { } r && m.BaseVersao is { } bv &&
                                                          versoes.TryGetValue(r, out var atual) && !atual.AsSpan().SequenceEqual(bv)))
                        velha = RegrasOperacaoTerritorial.MensagemBaseVelha("a regra do território");
                    break;
                case TipoMudancaTerritorial.EncerrarExcecao:
                    if (m.ExcecaoBaseId is not { } e || !excecoes.ContainsKey(e) ||
                        (m.BaseVersao is { } bx && versoes.TryGetValue(e, out var ax) && !ax.AsSpan().SequenceEqual(bx)))
                        velha = RegrasOperacaoTerritorial.MensagemBaseVelha("a exceção");
                    break;
                case TipoMudancaTerritorial.MoverTerritorio or TipoMudancaTerritorial.EncerrarTerritorio:
                    var aberta = m.TerritorioId is { } tp && estado.Territorios.TryGetValue(tp, out var terr) ? RegrasArvoreTerritorial.PosicaoAberta(terr) : null;
                    if (aberta is not { FimEm: null } || aberta.Id != m.PosicaoBaseId)
                        velha = RegrasOperacaoTerritorial.MensagemBaseVelha("a posição do território");
                    break;
            }
            if (velha is not null) resultado[m.Id] = velha;
        }
        return resultado;
    }

    private static MudancaTerritorialDto ParaDto(OperacaoTerritorialMudanca m, IReadOnlyDictionary<Guid, Territorio> territorios,
                                                 IReadOnlyDictionary<Guid, string> pessoas, string? baseVelha)
    {
        var dados = DadosMudancaTerritorial.DeJson(m.Depois);
        string? Nome(Guid? id) => id is { } x ? territorios.GetValueOrDefault(x)?.Nome : null;
        var territorio = Nome(m.TerritorioId) ?? "?";
        var pessoa = m.PessoaId is { } p ? pessoas.GetValueOrDefault(p) ?? "?" : null;
        var descricao = m.Tipo switch
        {
            TipoMudancaTerritorial.NovaVersaoRegra => $"Nova versão da regra de {territorio}" + (dados.Prioridade is { } pr ? $" (prioridade {pr})" : " (sem prioridade)"),
            TipoMudancaTerritorial.EncerrarRegra => $"Encerrar a regra de {territorio}",
            TipoMudancaTerritorial.Fixar => $"Fixar {pessoa} em {territorio}",
            TipoMudancaTerritorial.Retirar => $"Retirar {pessoa} de {territorio}",
            TipoMudancaTerritorial.EncerrarExcecao => $"Encerrar a exceção de {pessoa} em {territorio}",
            TipoMudancaTerritorial.MoverTerritorio => $"Mover {territorio} para {Nome(dados.NovoPaiId) ?? "o primeiro nível"}",
            TipoMudancaTerritorial.EncerrarTerritorio => $"Encerrar o território {territorio}",
            TipoMudancaTerritorial.ReativarTerritorio => $"Reativar o território {territorio}",
            _ => TextoRegraTerritorio.NomeTipo(m.Tipo)
        };
        return new MudancaTerritorialDto
        {
            Id = m.Id, Ordem = m.Ordem, Tipo = m.Tipo, TipoNome = TextoRegraTerritorio.NomeTipo(m.Tipo), TerritorioId = m.TerritorioId,
            Territorio = Nome(m.TerritorioId), PessoaId = m.PessoaId, Pessoa = pessoa, RegraBaseId = m.RegraBaseId, ExcecaoBaseId = m.ExcecaoBaseId,
            PosicaoBaseId = m.PosicaoBaseId,
            Grupos = dados.Grupos is null ? null : TextoRegraTerritorio.DeJson(dados.Grupos), Criterios = dados.Criterios, Prioridade = dados.Prioridade,
            Motivo = dados.Motivo, FimEm = dados.FimEm, NovoPaiId = dados.NovoPaiId, NovoPai = Nome(dados.NovoPaiId),
            Descricao = descricao, Antes = m.Antes, BaseVelha = baseVelha
        };
    }

    private static readonly HashSet<string> CamposEditaveis =
        [nameof(OperacaoTerritorial.EfeitoEm), nameof(OperacaoTerritorial.Motivo), nameof(OperacaoTerritorial.Observacao)];

    private static bool EdicaoDeCampo(Lone.Contracts.Auditoria.RegistroHistorico r) =>
        r.Acao == AcaoAuditoria.Alteracao && r.Entidade == nameof(OperacaoTerritorial) && r.Campo is { } c && CamposEditaveis.Contains(c);

    /// <summary>O texto da mudança como a tela mostra ("Fixar João em SP"), para o evento de quem a retirou.</summary>
    private async Task<string> DescreverAsync(OperacaoTerritorial op, OperacaoTerritorialMudanca m, CancellationToken ct)
    {
        var territorios = (await _territorios.ListarDoMapaAsync(op.MapaId, ct)).ToDictionary(t => t.Id);
        var pessoas = m.PessoaId is { } p ? await _consultas.NomesDePessoasAsync([p], ct) : new Dictionary<Guid, string>();
        return ParaDto(m, territorios, pessoas, null).Descricao;
    }

    private static SimulacaoTerritorialResumoDto ParaDto(OperacaoTerritorialSimulacao s, bool atual) => new()
    {
        Id = s.Id, SimuladaEm = s.SimuladaEm, SimuladaPor = s.SimuladaPor, EfeitoEm = s.EfeitoEm, AtributosAvaliadosEm = s.AtributosAvaliadosEm,
        Atual = atual, Entram = s.Entram, Saem = s.Saem, Mudam = s.Mudam, OrigemAtualizada = s.OrigemAtualizada, EmConflito = s.EmConflito,
        Inconsistencias = s.Inconsistencias, DaOperacao = s.DaOperacao, Divergencias = s.Divergencias
    };

    public async Task<List<SimulacaoTerritorialResumoDto>> SimulacoesAsync(Guid id, CancellationToken ct = default)
    {
        AcessoTerritorios.ExigirLeituraDoMotor(_autorizacao);
        var op = await CarregarAsync(id, ct);
        return [.. (await _operacoes.SimulacoesAsync(id, ct)).Select(s => ParaDto(s, s.Id == op.SimulacaoAtualId))];
    }

    // ------------------------------------------------------------------ Rascunho

    public async Task<OperacaoTerritorialDto> CriarAsync(CriarOperacaoTerritorialRequisicao requisicao, CancellationToken ct = default)
    {
        ExigirPlanejar();
        var mapa = await MapaAsync(requisicao.MapaId, ct);
        var erros = RegrasOperacaoTerritorial.ValidarTextos(requisicao.Motivo, requisicao.Observacao);
        if (!mapa.Ativo) erros.Add($"O mapa \"{mapa.Nome}\" está desativado: reative-o para planejar operações.");
        if (requisicao.EfeitoEm == default) erros.Add("Informe a data de efeito (o primeiro dia em que as mudanças valem).");
        if (erros.Count > 0) throw new ValidacaoException(erros);

        var op = new OperacaoTerritorial
        {
            Id = IdSequencial.Novo(), MapaId = mapa.Id, EfeitoEm = requisicao.EfeitoEm, Motivo = requisicao.Motivo.Trim(),
            Observacao = RegrasCadastroTerritorial.TextoOpcional(requisicao.Observacao), CriadaPorId = _usuario.Id, CriadaPor = _usuario.Nome
        };
        await _operacoes.CriarAsync(op, Hoje.Year,
            o => o.RegistrarEvento($"Operação {o.Numero} criada no mapa '{mapa.Nome}' com efeito em {RegrasCadastroTerritorial.Data(o.EfeitoEm)}."), ct);
        return await ObterAsync(op.Id, ct) ?? throw new ConflitoDeEdicaoException();
    }

    public async Task<OperacaoTerritorialDto> AlterarAsync(Guid id, AlterarOperacaoTerritorialRequisicao requisicao, CancellationToken ct = default)
    {
        ExigirPlanejar();
        var op = await CarregarAsync(id, ct);
        RegrasOperacaoTerritorial.ExigirAberta(op);
        ExigirVersao(op, requisicao.Versao);
        var erros = RegrasOperacaoTerritorial.ValidarTextos(requisicao.Motivo, requisicao.Observacao);
        if (requisicao.EfeitoEm == default) erros.Add("Informe a data de efeito (o primeiro dia em que as mudanças valem).");
        if (erros.Count > 0) throw new ValidacaoException(erros);

        var mudouData = op.EfeitoEm != requisicao.EfeitoEm;
        var motivo = requisicao.Motivo.Trim();
        var observacao = RegrasCadastroTerritorial.TextoOpcional(requisicao.Observacao);
        var oQue = new List<string>();
        if (mudouData)
            oQue.Add($"data de efeito de {RegrasCadastroTerritorial.Data(op.EfeitoEm)} para {RegrasCadastroTerritorial.Data(requisicao.EfeitoEm)}");
        if (op.Motivo != motivo) oQue.Add("motivo alterado");
        if (op.Observacao != observacao) oQue.Add("observação alterada");
        if (mudouData) RegrasOperacaoTerritorial.VoltarARascunho(op); // a simulação era de outra data
        op.EfeitoEm = requisicao.EfeitoEm;
        op.Motivo = motivo;
        op.Observacao = observacao;
        if (oQue.Count > 0) op.RegistrarEvento(RegrasOperacaoTerritorial.EventoDeEdicao(op, _usuario.Id, _usuario.Nome, string.Join("; ", oQue)));
        await _operacoes.SalvarAsync(op, ct);
        return await ObterAsync(id, ct) ?? throw new ConflitoDeEdicaoException();
    }

    public async Task<OperacaoTerritorialDto> RetirarMudancaAsync(Guid id, Guid mudancaId, VersaoOperacaoTerritorialRequisicao requisicao, CancellationToken ct = default)
    {
        ExigirPlanejar();
        var op = await CarregarAsync(id, ct);
        RegrasOperacaoTerritorial.ExigirAberta(op);
        ExigirVersao(op, requisicao.Versao);
        var mudanca = op.Mudancas.FirstOrDefault(m => m.Id == mudancaId) ?? throw new ValidacaoException(["Esta mudança não está mais na operação."]);
        var descricao = await DescreverAsync(op, mudanca, ct);
        RegrasOperacaoTerritorial.VoltarARascunho(op);
        op.Mudancas.Remove(mudanca);
        // A ordem segue contínua (1, 2, 3...): é a ordem em que as mudanças de estrutura são conferidas e aplicadas.
        var ordem = 1;
        foreach (var m in op.Mudancas.OrderBy(m => m.Ordem)) m.Ordem = ordem++;
        op.RegistrarEvento(RegrasOperacaoTerritorial.EventoDeEdicao(op, _usuario.Id, _usuario.Nome, $"mudança {mudanca.Ordem} retirada — {descricao}"));
        await _operacoes.SalvarAsync(op, ct);
        return await ObterAsync(id, ct) ?? throw new ConflitoDeEdicaoException();
    }

    public async Task<OperacaoTerritorialDto> MoverClienteAsync(Guid id, MoverClienteTerritorialRequisicao requisicao, CancellationToken ct = default)
    {
        // Atalho (seção G): Retirar de A + Fixar em B, as duas conferidas juntas como qualquer mudança.
        if (requisicao.DeTerritorioId == requisicao.ParaTerritorioId)
            throw new ValidacaoException(["Escolha territórios diferentes para mover o cliente."]);
        return await IncluirAsync(id, requisicao.Versao,
        [
            new IncluirMudancaTerritorialRequisicao { Tipo = TipoMudancaTerritorial.Retirar, TerritorioId = requisicao.DeTerritorioId,
                PessoaId = requisicao.PessoaId, Motivo = requisicao.Motivo, FimEm = requisicao.FimEm },
            new IncluirMudancaTerritorialRequisicao { Tipo = TipoMudancaTerritorial.Fixar, TerritorioId = requisicao.ParaTerritorioId,
                PessoaId = requisicao.PessoaId, Motivo = requisicao.Motivo, FimEm = requisicao.FimEm }
        ], ct);
    }

    public Task<OperacaoTerritorialDto> IncluirMudancaAsync(Guid id, IncluirMudancaTerritorialRequisicao requisicao, CancellationToken ct = default) =>
        IncluirAsync(id, requisicao.Versao, [requisicao], ct);

    /// <summary>
    /// Inclui mudanças: monta cada uma com a base vigente AGORA na data de efeito (id, versão e foto: L5), confere a mudança
    /// na sequência da operação (a árvore já com as mudanças de estrutura anteriores) e o conjunto, e volta a operação para
    /// Rascunho (a simulação anterior deixa de valer).
    /// </summary>
    private async Task<OperacaoTerritorialDto> IncluirAsync(Guid id, byte[]? versao, IReadOnlyList<IncluirMudancaTerritorialRequisicao> pedidos,
                                                            CancellationToken ct)
    {
        ExigirPlanejar();
        var op = await CarregarAsync(id, ct);
        RegrasOperacaoTerritorial.ExigirAberta(op);
        ExigirVersao(op, versao);
        var mapa = await MapaAsync(op.MapaId, ct);
        var hoje = Hoje;
        var (estado, universo, comUso) = await _dados.LerAsync(async l =>
        {
            var e = await l.EstadoAsync(mapa, op.EfeitoEm, comAtribuicoes: false, ct);
            var u = await l.UniversoAsync(mapa, ct);
            return (e, u, await _uso.TerritoriosComUsoAsync(mapa.Id, ct));
        }, ct);
        var regraVigente = estado.RegrasVigentes.Where(r => r.VigenteEm(op.EfeitoEm)).ToDictionary(r => r.TerritorioId);
        var excecoes = estado.ExcecoesVigentes.Where(x => x.VigenteEm(op.EfeitoEm)).ToDictionary(x => x.Id);

        var novas = new List<OperacaoTerritorialMudanca>();
        var erros = new List<string>();
        var ordem = op.Mudancas.Count == 0 ? 1 : op.Mudancas.Max(m => m.Ordem) + 1;
        foreach (var pedido in pedidos)
        {
            var (mudanca, errosMontagem) = await MontarAsync(op, mapa, pedido, ordem++, estado, regraVigente, excecoes, ct);
            erros.AddRange(errosMontagem);
            if (mudanca is not null) novas.Add(mudanca);
        }
        if (erros.Count > 0) throw new ValidacaoException(erros.Distinct().ToList());

        var todas = op.Mudancas.Concat(novas).OrderBy(m => m.Ordem).ToList();
        var contexto = new RegrasOperacaoTerritorial.ContextoMudanca(mapa, estado.Territorios, comUso, regraVigente, excecoes,
                                                                     universo.Contains, op.EfeitoEm);
        // Só os erros das mudanças novas e do conjunto: uma mudança antiga com base velha não impede incluir outra.
        var ordensNovas = novas.Select(m => $"Mudança {m.Ordem}:").ToList();
        erros.AddRange(RegrasOperacaoTerritorial.ValidarTodas(todas, contexto)
            .Where(e => !e.StartsWith("Mudança ", StringComparison.Ordinal) || ordensNovas.Any(o => e.StartsWith(o, StringComparison.Ordinal))));
        var parametros = await _dados.LerAsync(l => l.ParametrosAsync(ct), ct);
        if (novas.Any(m => RegrasOperacaoTerritorial.Estrutural(m.Tipo)) && op.EfeitoEm > hoje)
            erros.AddRange(RegrasOperacaoTerritorial.ValidarEfeito(op.EfeitoEm, hoje, parametros.DiasRetroativosMaximo, null, null, true));
        if (erros.Count > 0) throw new ValidacaoException(erros.Distinct().ToList());

        RegrasOperacaoTerritorial.VoltarARascunho(op);
        var pessoas = await _consultas.NomesDePessoasAsync([.. novas.Select(m => m.PessoaId).OfType<Guid>().Distinct()], ct);
        foreach (var m in novas)
        {
            op.Mudancas.Add(m);
            var descricao = ParaDto(m, estado.Territorios, pessoas, null).Descricao;
            op.RegistrarEvento(RegrasOperacaoTerritorial.EventoDeEdicao(op, _usuario.Id, _usuario.Nome, $"mudança {m.Ordem} incluída — {descricao}"));
        }
        await _operacoes.SalvarAsync(op, ct);
        return await ObterAsync(id, ct) ?? throw new ConflitoDeEdicaoException();
    }

    /// <summary>Uma mudança a partir do pedido: alvo, base vigente (id, versão, foto) e o proposto em JSON de forma fechada.</summary>
    private async Task<(OperacaoTerritorialMudanca? Mudanca, List<string> Erros)> MontarAsync(
        OperacaoTerritorial op, MapaTerritorial mapa, IncluirMudancaTerritorialRequisicao p, int ordem, EstadoTerritorialEmD estado,
        IReadOnlyDictionary<Guid, RegraTerritorio> regraVigente, IReadOnlyDictionary<Guid, ExcecaoTerritorio> excecoes, CancellationToken ct)
    {
        var erros = new List<string>();
        var m = new OperacaoTerritorialMudanca
        {
            Id = IdSequencial.Novo(), OperacaoId = op.Id, MapaId = op.MapaId, Ordem = ordem, Tipo = p.Tipo,
            TerritorioId = p.TerritorioId == Guid.Empty ? null : p.TerritorioId, PessoaId = p.PessoaId == Guid.Empty ? null : p.PessoaId
        };
        if (!Enum.IsDefined(p.Tipo)) return (null, ["Tipo de mudança desconhecido."]);
        var dados = new DadosMudancaTerritorial();
        switch (p.Tipo)
        {
            case TipoMudancaTerritorial.NovaVersaoRegra:
                erros.AddRange(TextoRegraTerritorio.Validar(p.Grupos, _autorizacao.Possui));
                if (erros.Count > 0) return (null, erros);
                dados.Grupos = TextoRegraTerritorio.ParaJson(p.Grupos!);
                dados.Criterios = TextoRegraTerritorio.Texto(p.Grupos!, await NomesDasOpcoesAsync(p.Grupos!, ct));
                dados.Prioridade = p.Prioridade;
                if (m.TerritorioId is { } t && regraVigente.TryGetValue(t, out var vigente))
                {
                    m.RegraBaseId = vigente.Id;
                    m.BaseVersao = vigente.Versao;
                    m.Antes = System.Text.Json.JsonSerializer.Serialize(new { versao = vigente.Numero, criterios = vigente.Criterios, prioridade = vigente.Prioridade });
                }
                break;

            case TipoMudancaTerritorial.EncerrarRegra:
                if (m.TerritorioId is { } te && regraVigente.TryGetValue(te, out var aEncerrar))
                {
                    m.RegraBaseId = aEncerrar.Id;
                    m.BaseVersao = aEncerrar.Versao;
                    m.Antes = System.Text.Json.JsonSerializer.Serialize(new { versao = aEncerrar.Numero, criterios = aEncerrar.Criterios, prioridade = aEncerrar.Prioridade });
                }
                break;

            case TipoMudancaTerritorial.Fixar or TipoMudancaTerritorial.Retirar:
                dados.Motivo = p.Motivo?.Trim();
                dados.FimEm = p.FimEm;
                // Base: as exceções vigentes do cliente no mapa (foto), para a revisão ver o que existia ao planejar.
                if (m.PessoaId is { } pessoa)
                {
                    var doCliente = estado.ExcecoesVigentes.Where(x => x.PessoaId == pessoa && x.VigenteEm(op.EfeitoEm))
                        .OrderBy(x => x.InicioEm).Select(x => new { excecao = x.Id, territorio = x.TerritorioId, tipo = x.Tipo.ToString(), inicio = x.InicioEm, fim = x.FimEm })
                        .ToList();
                    if (doCliente.Count > 0) m.Antes = System.Text.Json.JsonSerializer.Serialize(doCliente);
                }
                break;

            case TipoMudancaTerritorial.EncerrarExcecao:
                var excecaoId = p.ExcecaoId == Guid.Empty ? null : p.ExcecaoId;
                m.ExcecaoBaseId = excecaoId;
                if (excecaoId is { } eid && excecoes.TryGetValue(eid, out var excecao))
                {
                    m.TerritorioId = excecao.TerritorioId;
                    m.PessoaId = excecao.PessoaId;
                    m.BaseVersao = excecao.Versao;
                    m.Antes = System.Text.Json.JsonSerializer.Serialize(new { tipo = excecao.Tipo.ToString(), inicio = excecao.InicioEm, fim = excecao.FimEm, motivo = excecao.Motivo });
                }
                break;

            case TipoMudancaTerritorial.MoverTerritorio or TipoMudancaTerritorial.EncerrarTerritorio or TipoMudancaTerritorial.ReativarTerritorio:
                if (p.Tipo == TipoMudancaTerritorial.MoverTerritorio) dados.NovoPaiId = p.NovoPaiId == Guid.Empty ? null : p.NovoPaiId;
                if (m.TerritorioId is { } ts && estado.Territorios.TryGetValue(ts, out var territorio))
                {
                    if (RegrasArvoreTerritorial.PosicaoAberta(territorio) is { FimEm: null } aberta) m.PosicaoBaseId = aberta.Id;
                    m.Antes = System.Text.Json.JsonSerializer.Serialize(new { pai = territorio.PaiId, situacao = territorio.Situacao.ToString(), fim = territorio.FimEm });
                }
                break;
        }
        m.Depois = dados.ParaJson();
        return (m, erros);
    }

    /// <summary>Nomes do dia de cada valor citado (texto congelado da regra).</summary>
    private async Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>> NomesDasOpcoesAsync(GruposRegraTerritorioDto grupos, CancellationToken ct)
    {
        var (campos, _) = await CamposRegraAsync(ct);
        var nomes = new Dictionary<string, IReadOnlyDictionary<string, string>>();
        foreach (var c in campos.Where(c => c.Opcoes.Count > 0))
            nomes[c.Id] = c.Opcoes.GroupBy(o => o.Valor).ToDictionary(g => g.Key, g => g.First().Texto);
        var municipios = TextoRegraTerritorio.Municipios(grupos);
        if (municipios.Count > 0)
            nomes[CamposFiltroPessoas.Municipio] = (await _municipios.ObterAsync(municipios, ct))
                .ToDictionary(kv => kv.Key.ToString(System.Globalization.CultureInfo.InvariantCulture), kv => $"{kv.Value.Nome}/{kv.Value.Uf}");
        return nomes;
    }

    // ------------------------------------------------------------------ Simulação (seção I)

    /// <summary>O resultado de um cálculo do mapa inteiro em D, com o que foi lido.</summary>
    private sealed record Calculo(EstadoTerritorialEmD Estado, ResultadoSimulacaoTerritorial Resultado, byte[] VersaoMotor, byte[] VersaoArvore,
                                  Dictionary<Guid, string> Atributos);

    /// <summary>
    /// O cálculo que a simulação faz e a aplicação repete (o mesmo código): estado em D, universo, candidatos de cada regra
    /// (uma consulta por versão da regra, reaproveitada entre "com" e "sem" a operação), o motor duas vezes e a comparação
    /// com as atribuições vigentes (DN-14), mais os atributos lidos dos clientes afetados.
    /// </summary>
    /// <param name="versaoMotorAssinada">Na aplicação, a versão do motor que a simulação assinou (a trava já trocou a rowversion).</param>
    private static async Task<Calculo> CalcularAsync(ILeitorTerritorial l, OperacaoTerritorial op, MapaTerritorial mapa, DateOnly hoje, CancellationToken ct,
                                                byte[]? versaoMotorAssinada = null)
    {
        var motor = await l.MotorAsync(mapa.Id, ct) ?? throw new InvalidOperationException("O mapa não tem o controle do motor.");
        var arvore = await l.VersaoArvoreAsync(mapa.Id, ct) ?? [];
        var estado = await l.EstadoAsync(mapa, op.EfeitoEm, comAtribuicoes: true, ct);
        var universo = await l.UniversoAsync(mapa, ct);
        var com = CenarioTerritorial.Montar(estado, op.Mudancas);
        var sem = CenarioTerritorial.Montar(estado, []);

        var porRegra = new Dictionary<Guid, IReadOnlySet<Guid>>();
        async Task<IReadOnlyDictionary<Guid, IReadOnlySet<Guid>>> CandidatosAsync(CenarioTerritorial cenario)
        {
            var porTerritorio = new Dictionary<Guid, IReadOnlySet<Guid>>();
            foreach (var regra in cenario.Regras.Values)
            {
                if (!porRegra.TryGetValue(regra.RegraId, out var pessoas))
                    porRegra[regra.RegraId] = pessoas = await l.CandidatosAsync(mapa, TextoRegraTerritorio.DeJson(regra.Grupos), hoje, ct);
                porTerritorio[regra.TerritorioId] = pessoas;
            }
            return porTerritorio;
        }

        var atribuicoes = estado.AtribuicoesVigentes.Where(a => a.VigenteEm(op.EfeitoEm)).ToList();
        var decisoesCom = com.Resolver(universo, await CandidatosAsync(com), atribuicoes);
        var decisoesSem = sem.Resolver(universo, await CandidatosAsync(sem), atribuicoes);
        var resultado = SimulacaoTerritorial.Comparar(decisoesCom, decisoesSem, atribuicoes, op.EfeitoEm, versaoMotorAssinada ?? motor.Versao ?? []);

        var campos = com.Regras.Values.Concat(sem.Regras.Values)
            .SelectMany(r => TextoRegraTerritorio.Campos(TextoRegraTerritorio.DeJson(r.Grupos))).ToHashSet(StringComparer.Ordinal);
        var afetados = resultado.Afetados.Select(a => a.Decisao.PessoaId).ToList();
        var atributos = afetados.Count == 0 || campos.Count == 0 ? new Dictionary<Guid, string>() : await l.AtributosAsync(mapa, afetados, campos, ct);
        return new Calculo(estado, resultado, motor.Versao ?? [], arvore, atributos);
    }

    /// <summary>Tudo que impede simular (as bases velhas não impedem: marcam a mudança; seção I, passo 1).</summary>
    private static async Task<List<string>> ConferirAsync(ILeitorTerritorial l, OperacaoTerritorial op, MapaTerritorial mapa, EstadoTerritorialEmD estado,
                                                   DateOnly hoje, bool paraAplicar, CancellationToken ct)
    {
        var erros = new List<string>();
        if (!mapa.Ativo) erros.Add($"O mapa \"{mapa.Nome}\" está desativado: reative-o para simular ou aplicar operações.");
        // Sem mudanças também vale: a operação só corrige as divergências que já existiam (DN-14; tela Divergências).
        var parametros = await l.ParametrosAsync(ct);
        var motor = await l.MotorAsync(mapa.Id, ct);
        var ultimaNumero = motor?.UltimaOperacaoId is { } u ? await l.NumeroOperacaoAsync(u, ct) : null;
        erros.AddRange(RegrasOperacaoTerritorial.ValidarEfeito(op.EfeitoEm, hoje, parametros.DiasRetroativosMaximo, motor?.UltimoEfeitoEm,
            ultimaNumero, op.Mudancas.Any(m => RegrasOperacaoTerritorial.Estrutural(m.Tipo))));

        var regraVigente = estado.RegrasVigentes.Where(r => r.VigenteEm(op.EfeitoEm)).ToDictionary(r => r.TerritorioId);
        var excecoes = estado.ExcecoesVigentes.Where(x => x.VigenteEm(op.EfeitoEm)).ToDictionary(x => x.Id);
        var universo = await l.UniversoAsync(mapa, ct);
        var comUso = new HashSet<Guid>(); // a conferência da operação não depende do uso (a mudança de estrutura por operação vale com ou sem)
        var contexto = new RegrasOperacaoTerritorial.ContextoMudanca(mapa, estado.Territorios, comUso, regraVigente, excecoes, universo.Contains, op.EfeitoEm);
        var errosMudancas = RegrasOperacaoTerritorial.ValidarTodas(op.Mudancas.OrderBy(m => m.Ordem).ToList(), contexto);

        var versoes = await l.VersoesDasBasesAsync([.. op.Mudancas.Select(x => x.RegraBaseId).OfType<Guid>()],
                                                   [.. op.Mudancas.Select(x => x.ExcecaoBaseId).OfType<Guid>()], ct);
        var velhas = BasesVelhas(op, estado, versoes);
        if (paraAplicar)
        {
            // Aplicar: base velha recusa, nomeando a mudança (seção J, passo 4).
            erros.AddRange(errosMudancas);
            foreach (var m in op.Mudancas.OrderBy(m => m.Ordem).Where(m => velhas.ContainsKey(m.Id)))
                erros.Add($"Mudança {m.Ordem}: {velhas[m.Id]}");
        }
        else
        {
            var textosBase = new[]
            {
                RegrasOperacaoTerritorial.MensagemBaseVelha("a regra do território"),
                RegrasOperacaoTerritorial.MensagemBaseVelha("a posição do território")
            };
            erros.AddRange(errosMudancas.Where(e => !textosBase.Any(b => e.EndsWith(b, StringComparison.Ordinal))));
        }
        return erros.Distinct().ToList();
    }

    public async Task<OperacaoTerritorialDto> SimularAsync(Guid id, VersaoOperacaoTerritorialRequisicao requisicao, CancellationToken ct = default)
    {
        ExigirPlanejar();
        var op = await CarregarAsync(id, ct);
        RegrasOperacaoTerritorial.ExigirAberta(op);
        ExigirVersao(op, requisicao.Versao);
        var hoje = Hoje;

        // Foto coerente sem SNAPSHOT (desligado no LoneERP): versão do motor antes e depois; se mudou no meio, refaz uma vez.
        Calculo? calculo = null;
        for (var tentativa = 1; tentativa <= 2 && calculo is null; tentativa++)
        {
            calculo = await _dados.LerAsync(async l =>
            {
                var mapa = await l.MapaAsync(op.MapaId, ct) ?? throw new ValidacaoException(["O mapa territorial da operação não existe mais."]);
                var antes = (await l.MotorAsync(op.MapaId, ct))?.Versao;
                var estado = await l.EstadoAsync(mapa, op.EfeitoEm, comAtribuicoes: false, ct);
                var erros = await ConferirAsync(l, op, mapa, estado, hoje, paraAplicar: false, ct);
                if (erros.Count > 0) throw new ValidacaoException(erros);
                var c = await CalcularAsync(l, op, mapa, hoje, ct);
                var depois = (await l.MotorAsync(op.MapaId, ct))?.Versao;
                return antes is not null && depois is not null && antes.AsSpan().SequenceEqual(depois) && c.VersaoMotor.AsSpan().SequenceEqual(antes) ? c : null;
            }, ct);
        }
        if (calculo is null)
            throw new ConflitoDeEdicaoException("O mapa está sendo alterado neste momento (outra operação ou a árvore). Nada foi gravado: simule de novo em instantes.");

        var r = calculo.Resultado;
        var agora = DateTime.UtcNow;
        var simulacao = new OperacaoTerritorialSimulacao
        {
            Id = IdSequencial.Novo(), OperacaoId = op.Id, EfeitoEm = op.EfeitoEm, SimuladaEm = agora, SimuladaPorId = _usuario.Id,
            SimuladaPor = _usuario.Nome, VersaoMotor = calculo.VersaoMotor, VersaoArvore = calculo.VersaoArvore, Assinatura = r.Assinatura,
            AtributosAvaliadosEm = agora, Entram = r.Entram, Saem = r.Saem, Mudam = r.Mudam, OrigemAtualizada = r.OrigemAtualizada,
            EmConflito = r.EmConflito, Inconsistencias = r.Inconsistencias, DaOperacao = r.DaOperacao, Divergencias = r.Divergencias
        };
        foreach (var a in r.Afetados)
        {
            simulacao.Itens.Add(new OperacaoTerritorialSimulacaoItem
            {
                Id = IdSequencial.Novo(), SimulacaoId = simulacao.Id, PessoaId = a.Decisao.PessoaId, Resultado = a.Decisao.Resultado, Efeito = a.Efeito,
                OrigemEfeito = a.Origem, TerritorioAtualId = a.Atuais.Keys.Order().Cast<Guid?>().FirstOrDefault(),
                TerritorioPropostoId = a.Decisao.Territorios.Select(t => t.TerritorioId).Order().Cast<Guid?>().FirstOrDefault(),
                Explicacao = SimulacaoTerritorial.Explicacao(a, Atributos(calculo.Atributos, a.Decisao.PessoaId))
            });
        }
        RegrasOperacaoTerritorial.MarcarSimulada(op, simulacao.Id);
        await _operacoes.SalvarSimulacaoAsync(op, simulacao, ct);
        return await ObterAsync(id, ct) ?? throw new ConflitoDeEdicaoException();
    }

    private static IReadOnlyDictionary<string, string?>? Atributos(IReadOnlyDictionary<Guid, string> lidos, Guid pessoa) =>
        lidos.TryGetValue(pessoa, out var texto) ? new Dictionary<string, string?> { ["lidos"] = texto } : null;

    // ------------------------------------------------------------------ Aplicação (seção J)

    public async Task<OperacaoTerritorialDto> AplicarAsync(Guid id, AplicarOperacaoTerritorialRequisicao requisicao, CancellationToken ct = default)
    {
        ExigirAplicar();
        var op = await CarregarAsync(id, ct);
        if (op.Situacao != SituacaoOperacaoTerritorial.Simulada)
            throw new ValidacaoException([$"Só uma operação simulada pode ser aplicada (a {op.Numero} está {RegrasOperacaoTerritorial.Nome(op.Situacao)})."]);
        ExigirVersao(op, requisicao.Versao);
        if (op.SimulacaoAtualId != requisicao.SimulacaoId)
            throw new ConflitoDeEdicaoException("A simulação conferida na tela não é mais a atual desta operação. Nada foi gravado: recarregue e confira.");
        var simulacao = await _operacoes.ObterSimulacaoAsync(requisicao.SimulacaoId, ct)
                        ?? throw new ConflitoDeEdicaoException(RegrasOperacaoTerritorial.MensagemMapaMudou);

        var estruturais = op.Mudancas.Where(m => RegrasOperacaoTerritorial.Estrutural(m.Tipo)).Select(m => m.TerritorioId).OfType<Guid>().Distinct().ToList();
        var pedido = new PedidoAplicacaoTerritorial(op.Id, op.MapaId, requisicao.Versao!, simulacao.VersaoMotor,
                                                     estruturais.Count > 0 ? simulacao.VersaoArvore : null, estruturais);
        var hoje = Hoje;
        try
        {
            // DN-12: recusa pelo volume antes de travar o mapa. A contagem da simulação basta: dentro da trava, a assinatura igual
            // garante que os afetados são os mesmos.
            if (RegrasOperacaoTerritorial.AcimaDoLimite(RegrasOperacaoTerritorial.ClientesGravados(simulacao.Entram, simulacao.Saem, simulacao.Mudam,
                                                                                                simulacao.OrigemAtualizada)) is { } limite)
                throw new ValidacaoException([limite]);

            await _dados.AplicarAsync(pedido, async l =>
            {
                // Tudo daqui em diante roda com o mapa travado: o que é conferido é o que será gravado.
                var mapa = await l.MapaAsync(op.MapaId, ct) ?? throw new ValidacaoException(["O mapa territorial da operação não existe mais."]);
                var estadoAgora = await l.EstadoAsync(mapa, op.EfeitoEm, comAtribuicoes: false, ct);
                var erros = await ConferirAsync(l, op, mapa, estadoAgora, hoje, paraAplicar: true, ct);
                if (erros.Count > 0) throw new ValidacaoException(erros);

                // A trava do motor (UPDATE ... WHERE Versao = vista) já provou que a versão é a simulada, mas o próprio UPDATE
                // gera uma rowversion nova. Por isso a assinatura é recalculada com a versão que a simulação assinou: o que se
                // compara é o conteúdo (efeito + afetados), não o carimbo que a trava acabou de trocar.
                var calculo = await CalcularAsync(l, op, mapa, hoje, ct, simulacao.VersaoMotor);
                if (calculo.Resultado.Bloqueada)
                    throw new ValidacaoException([$"Há {calculo.Resultado.Inconsistencias} inconsistência(s) nas exceções (conflito de fixação ou fixação " +
                                                  "inválida): resolva na própria operação e simule de novo."]);
                if (!calculo.Resultado.Assinatura.AsSpan().SequenceEqual(simulacao.Assinatura))
                    throw new ConflitoDeEdicaoException(RegrasOperacaoTerritorial.MensagemAssinaturaDiferente);
                if (op.Mudancas.Count == 0 && calculo.Resultado.Afetados.Count == 0)
                    throw new ValidacaoException(["Nada a aplicar: a operação não tem mudanças e o mapa não tem divergências."]);

                var encerrados = op.Mudancas.Where(m => m.Tipo == TipoMudancaTerritorial.EncerrarTerritorio).Select(m => m.TerritorioId).OfType<Guid>().ToList();
                var plano = AplicacaoTerritorial.Planejar(op, calculo.Estado, calculo.Resultado, await l.UltimaVersaoDasRegrasAsync(mapa.Id, ct),
                    encerrados.Count == 0 ? [] : await l.ResponsaveisAsync(encerrados, op.EfeitoEm, ct),
                    p => calculo.Atributos.GetValueOrDefault(p));

                var motor = await l.MotorAsync(mapa.Id, ct);
                var r = calculo.Resultado;
                op.Entraram = r.Entram;
                op.Sairam = r.Saem;
                op.Mudaram = r.Mudam;
                op.OrigemAtualizada = r.OrigemAtualizada;
                RegrasOperacaoTerritorial.MarcarAplicada(op, _usuario.Id, _usuario.Nome, DateTime.UtcNow, motor?.UltimaOperacaoId, motor?.UltimoEfeitoEm);
                return new AplicacaoCalculada(plano, op);
            }, ct);
        }
        catch (Exception ex) when (ex is ValidacaoException or ConflitoDeEdicaoException)
        {
            // A transação caiu inteira; a recusa fica na história da operação por uma gravação à parte (seção F).
            var motivo = ex is ValidacaoException v ? string.Join(" ", v.Erros) : ex.Message;
            await _operacoes.RegistrarEventoAsync(op.Id, $"Tentativa de aplicar a {op.Numero} recusada: {motivo}", ct);
            throw;
        }
        return await ObterAsync(id, ct) ?? throw new ConflitoDeEdicaoException();
    }

    // ------------------------------------------------------------------ Cancelar e desfazer (DN-05, DN-06, RT-2)

    public async Task<OperacaoTerritorialDto> CancelarAsync(Guid id, MotivoOperacaoTerritorialRequisicao requisicao, CancellationToken ct = default)
    {
        if (!_autorizacao.Possui(Permissoes.Territorios.Aplicar)) ExigirPlanejar();
        else if (!Tudo) throw new AcessoNegadoException(Permissoes.Territorios.Aplicar, MensagemAlcance);
        var op = await CarregarAsync(id, ct);
        var erros = RegrasOperacaoTerritorial.ValidarCancelamento(op, _usuario.Id, PodeAplicar, PodePlanejar, requisicao.Motivo);
        if (erros.Count > 0) throw new ValidacaoException(erros);
        ExigirVersao(op, requisicao.Versao);
        RegrasOperacaoTerritorial.Cancelar(op, _usuario.Id, _usuario.Nome, requisicao.Motivo, DateTime.UtcNow);
        await _operacoes.SalvarAsync(op, ct);
        return await ObterAsync(id, ct) ?? throw new ConflitoDeEdicaoException();
    }

    public async Task<OperacaoTerritorialDto> DesfazerAsync(Guid id, MotivoOperacaoTerritorialRequisicao requisicao, CancellationToken ct = default)
    {
        ExigirAplicar();
        var op = await CarregarAsync(id, ct);
        var motor = await _dados.LerAsync(l => l.MotorAsync(op.MapaId, ct), ct);
        var ultimaNumero = motor?.UltimaOperacaoId is { } u ? (await _operacoes.NumerosAsync([u], ct)).GetValueOrDefault(u) : null;
        var erros = RegrasOperacaoTerritorial.ValidarDesfazer(op, Hoje, motor?.UltimaOperacaoId, ultimaNumero, requisicao.Motivo);
        if (erros.Count > 0) throw new ValidacaoException(erros);
        if (requisicao.Versao is null) throw new ConflitoDeEdicaoException(RegrasOperacaoTerritorial.MensagemOperacaoAlterada);
        RegrasOperacaoTerritorial.Desfazer(op, _usuario.Id, _usuario.Nome, requisicao.Motivo, DateTime.UtcNow);
        await _dados.DesfazerAsync(op, requisicao.Versao, ct);
        return await ObterAsync(id, ct) ?? throw new ConflitoDeEdicaoException();
    }

    // ------------------------------------------------------------------ Divergências (DN-14)

    public async Task<DivergenciasTerritoriaisDto> DivergenciasAsync(Guid mapaId, FiltroItensOperacaoTerritorialDto filtro, CancellationToken ct = default)
    {
        AcessoTerritorios.ExigirLeituraDoMotor(_autorizacao);
        var hoje = Hoje;
        var resultado = await _dados.LerAsync(async l =>
        {
            var mapa = await l.MapaAsync(mapaId, ct) ?? throw new ValidacaoException(["Este mapa territorial não existe mais."]);
            return (await CalcularAsync(l, new OperacaoTerritorial { MapaId = mapaId, EfeitoEm = hoje }, mapa, hoje, ct)).Resultado;
        }, ct);

        IEnumerable<EfeitoCliente> afetados = resultado.Afetados;
        if (!Tudo)
        {
            var escopo = await _escopo.ObterAsync(ct);
            var meus = await _noEscopo.ClientesDiretosAsync([.. resultado.Afetados.Select(a => a.Decisao.PessoaId)], escopo, ct);
            afetados = afetados.Where(a => meus.Contains(a.Decisao.PessoaId));
        }
        var f = Limitar(filtro);
        if (f.Efeito is { } e) afetados = afetados.Where(a => a.Efeito == e);
        if (f.SoProblemas) afetados = afetados.Where(a => a.Efeito == EfeitoNoCliente.Bloqueado ||
                                                          a.Decisao.Resultado is ResultadoAtribuicao.Conflito or ResultadoAtribuicao.PermaneceEmConflito);
        var lista = afetados.OrderBy(a => a.Efeito == EfeitoNoCliente.Bloqueado ? 0 : 1).ThenBy(a => a.Efeito).ThenBy(a => a.Decisao.PessoaId).ToList();
        var pagina = await PaginaAsync(mapaId, lista.Count, !Tudo, lista.Skip(f.Pular).Take(f.Quantidade).Select(a =>
            (a.Decisao.PessoaId, a.Decisao.Resultado, a.Efeito, (OrigemEfeitoSimulado?)null,
             a.Atuais.Keys.Order().Cast<Guid?>().FirstOrDefault(), a.Decisao.Territorios.Select(t => t.TerritorioId).Order().Cast<Guid?>().FirstOrDefault(),
             SimulacaoTerritorial.Explicacao(a))), ct);
        return new DivergenciasTerritoriaisDto
        {
            MapaId = mapaId, Data = hoje, Entram = resultado.Entram, Saem = resultado.Saem, Mudam = resultado.Mudam,
            OrigemAtualizada = resultado.OrigemAtualizada, EmConflito = resultado.EmConflito, Inconsistencias = resultado.Inconsistencias,
            Itens = pagina
        };
    }

    // ------------------------------------------------------------------ Itens (simulação e resultado aplicado)

    public async Task<PaginaItensOperacaoTerritorialDto> ItensSimulacaoAsync(Guid simulacaoId, FiltroItensOperacaoTerritorialDto filtro, CancellationToken ct = default)
    {
        AcessoTerritorios.ExigirLeituraDoMotor(_autorizacao);
        var simulacao = await _operacoes.ObterSimulacaoAsync(simulacaoId, ct) ?? throw new ValidacaoException(["Esta simulação não existe."]);
        var op = await CarregarAsync(simulacao.OperacaoId, ct);
        var restritos = await RestringirAsync(simulacaoId, null, ct);
        var (total, itens) = await _operacoes.ItensSimulacaoAsync(simulacaoId, Limitar(filtro), restritos, ct);
        return await PaginaAsync(op.MapaId, total, restritos is not null,
            itens.Select(i => (i.PessoaId, i.Resultado, i.Efeito, (OrigemEfeitoSimulado?)i.OrigemEfeito, i.TerritorioAtualId, i.TerritorioPropostoId, i.Explicacao)), ct);
    }

    public async Task<PaginaItensOperacaoTerritorialDto> ItensAplicadosAsync(Guid id, FiltroItensOperacaoTerritorialDto filtro, CancellationToken ct = default)
    {
        AcessoTerritorios.ExigirLeituraDoMotor(_autorizacao);
        var op = await CarregarAsync(id, ct);
        var restritos = await RestringirAsync(null, id, ct);
        var (total, itens) = await _operacoes.ItensAplicadosAsync(id, Limitar(filtro), restritos, ct);
        return await PaginaAsync(op.MapaId, total, restritos is not null,
            itens.Select(i => (i.PessoaId, i.Resultado, i.Efeito, (OrigemEfeitoSimulado?)null, i.TerritorioAnteriorId, i.TerritorioNovoId, i.Explicacao)), ct);
    }

    private static FiltroItensOperacaoTerritorialDto Limitar(FiltroItensOperacaoTerritorialDto? f)
    {
        f ??= new();
        f.Pular = Math.Max(0, f.Pular);
        f.Quantidade = Math.Clamp(f.Quantidade, 1, FiltroItensOperacaoTerritorialDto.QuantidadeMaxima);
        f.Texto = string.IsNullOrWhiteSpace(f.Texto) ? null : f.Texto.Trim();
        return f;
    }

    /// <summary>Com alcance restrito, só os clientes do alcance (seção O); alcance Tudo: nulo = todos.</summary>
    private async Task<IReadOnlySet<Guid>?> RestringirAsync(Guid? simulacaoId, Guid? operacaoId, CancellationToken ct)
    {
        if (Tudo) return null;
        var escopo = await _escopo.ObterAsync(ct);
        var pessoas = await _operacoes.PessoasDosItensAsync(simulacaoId, operacaoId, ct);
        return await _noEscopo.ClientesDiretosAsync(pessoas, escopo, ct);
    }

    private async Task<PaginaItensOperacaoTerritorialDto> PaginaAsync(
        Guid mapaId, int total, bool filtrado,
        IEnumerable<(Guid Pessoa, ResultadoAtribuicao Resultado, EfeitoNoCliente Efeito, OrigemEfeitoSimulado? Origem, Guid? Atual, Guid? Proposto, string Explicacao)> itens,
        CancellationToken ct)
    {
        var lista = itens.ToList();
        var territorios = (await _territorios.ListarDoMapaAsync(mapaId, ct)).ToDictionary(t => t.Id, t => t.Nome);
        var ids = lista.Select(i => i.Pessoa).Distinct().ToList();
        var nomes = await _consultas.NomesDePessoasAsync(ids, ct);
        var codigos = await _consultas.CodigosDePessoasAsync(ids, ct);
        return new PaginaItensOperacaoTerritorialDto
        {
            Total = total,
            FiltradoPeloAlcance = filtrado,
            Itens = [.. lista.Select(i => new ItemOperacaoTerritorialDto
            {
                PessoaId = i.Pessoa, Codigo = codigos.TryGetValue(i.Pessoa, out var c) ? c : null, Pessoa = nomes.GetValueOrDefault(i.Pessoa),
                Resultado = i.Resultado, ResultadoNome = TextoRegraTerritorio.NomeResultado(i.Resultado),
                Efeito = i.Efeito, EfeitoNome = TextoRegraTerritorio.NomeEfeito(i.Efeito), OrigemEfeito = i.Origem,
                TerritorioAtualId = i.Atual, TerritorioAtual = i.Atual is { } a ? territorios.GetValueOrDefault(a) : null,
                TerritorioPropostoId = i.Proposto, TerritorioProposto = i.Proposto is { } p ? territorios.GetValueOrDefault(p) : null,
                Motivo = MotivoDoItem(i.Explicacao), Explicacao = i.Explicacao
            })]
        };
    }

    /// <summary>O passo que decidiu, lido da explicação congelada.</summary>
    private static string? MotivoDoItem(string explicacao)
    {
        try
        {
            using var json = System.Text.Json.JsonDocument.Parse(explicacao);
            return json.RootElement.TryGetProperty("passo", out var passo) && Enum.TryParse<PassoDecisaoTerritorial>(passo.GetString(), out var p)
                ? TextoRegraTerritorio.NomePasso(p)
                : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
