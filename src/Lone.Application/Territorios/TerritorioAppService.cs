using Lone.Application.Comercial;
using Lone.Application.Empresas;
using Lone.Application.Enderecos;
using Lone.Application.Metas;
using Lone.Application.Papeis;
using Lone.Application.Seguranca;
using Lone.Contracts.Colaboradores;
using Lone.Contracts.Comercial;
using Lone.Contracts.Enderecos;
using Lone.Contracts.Seguranca;
using Lone.Contracts.Territorios;
using Lone.Domain.Comercial;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Territorios;
using Lone.Domain.Validacao;

namespace Lone.Application.Territorios;

public interface ITerritorioAppService
{
    Task<ArvoreTerritorialDto> ListarDoMapaAsync(Guid mapaId, CancellationToken ct = default);
    Task<TerritorioDto?> ObterAsync(Guid id, CancellationToken ct = default);
    Task<TerritorioDto> SalvarAsync(TerritorioDto dto, CancellationToken ct = default);
    Task<TerritorioDto> EncerrarAsync(Guid id, AlterarSituacaoTerritorioRequisicao requisicao, CancellationToken ct = default);
    Task<TerritorioDto> ReativarAsync(Guid id, AlterarSituacaoTerritorioRequisicao requisicao, CancellationToken ct = default);
    Task<TerritoriosOpcoesDto> ListarOpcoesAsync(CancellationToken ct = default);
}

/// <summary>
/// Territórios (Fase 2b-1a): a árvore do mapa e os responsáveis. Ordem de toda mudança: permissão → lê o mapa, a árvore e
/// o uso operacional → monta → regras do domínio (árvore e responsáveis) → grava. Mudança de estrutura (criar, mover, mudar
/// o início, encerrar, reativar) exige a versão da árvore que a tela mostrava: se a árvore mudou, é recusada antes da
/// conferência, e a gravação troca a versão na mesma transação (duas mudanças simultâneas não passam as duas; seção 9.3 do
/// plano, D1 = B). Responsáveis e textos não mexem na estrutura e não serializam a árvore (só a versão do território).
/// </summary>
public sealed class TerritorioAppService : ITerritorioAppService
{
    private readonly ITerritorioRepositorio _repositorio;
    private readonly IMapaTerritorialRepositorio _mapas;
    private readonly ITipoTerritorioRepositorio _tipos;
    private readonly IUsoTerritorial _uso;
    private readonly ReferenciasComercial _comercial;
    private readonly IComercialConsultas _pessoas;
    private readonly IEquipeRepositorio _equipes;
    private readonly IEmpresaConsultas _empresas;
    private readonly IPapelRepositorio _classificacoes;
    private readonly IFinalidadeEnderecoRepositorio _finalidades;
    private readonly IAutorizacao _autorizacao;
    private readonly IMotivoDaOperacao _motivo;
    private readonly TimeProvider _relogio;

    public TerritorioAppService(ITerritorioRepositorio repositorio, IMapaTerritorialRepositorio mapas, ITipoTerritorioRepositorio tipos,
                                IUsoTerritorial uso, ReferenciasComercial comercial, IComercialConsultas pessoas, IEquipeRepositorio equipes,
                                IEmpresaConsultas empresas, IPapelRepositorio classificacoes, IFinalidadeEnderecoRepositorio finalidades,
                                IAutorizacao autorizacao, IMotivoDaOperacao motivo, TimeProvider relogio)
    {
        _repositorio = repositorio;
        _mapas = mapas;
        _tipos = tipos;
        _uso = uso;
        _comercial = comercial;
        _pessoas = pessoas;
        _equipes = equipes;
        _empresas = empresas;
        _classificacoes = classificacoes;
        _finalidades = finalidades;
        _autorizacao = autorizacao;
        _motivo = motivo;
        _relogio = relogio;
    }

    private DateOnly Hoje => DateOnly.FromDateTime(_relogio.GetLocalNow().DateTime);

    // ------------------------------------------------------------------ Leitura

    public async Task<ArvoreTerritorialDto> ListarDoMapaAsync(Guid mapaId, CancellationToken ct = default)
    {
        AcessoTerritorios.ExigirLeitura(_autorizacao);
        if (await _mapas.ObterAsync(mapaId, ct) is null) throw new ValidacaoException(["Este mapa territorial não existe mais."]);
        // A versão ANTES dos territórios: se a árvore mudar entre as duas leituras, a tela fica com a versão velha e a
        // próxima mudança de estrutura é recusada (lado seguro).
        var versaoArvore = await _repositorio.ObterVersaoArvoreAsync(mapaId, ct);
        var doMapa = await _repositorio.ListarDoMapaAsync(mapaId, ct);
        var tipos = (await _tipos.ListarAsync(ct)).ToDictionary(t => t.Id, t => t.Nome);
        var comUso = await _uso.TerritoriosComUsoAsync(mapaId, ct);
        var nomes = await NomesAsync(doMapa, ct);
        var hoje = Hoje;
        return new ArvoreTerritorialDto
        {
            VersaoArvore = versaoArvore,
            Territorios = [.. doMapa.Select(t => new TerritorioResumoDto
            {
                Id = t.Id, MapaId = t.MapaId, Codigo = t.Codigo, Nome = t.Nome, TipoId = t.TipoId, Tipo = tipos.GetValueOrDefault(t.TipoId),
                PaiId = t.PaiId, Situacao = t.Situacao, InicioEm = RegrasArvoreTerritorial.InicioDe(t), FimEm = t.FimEm,
                ResponsaveisHoje = ResponsaveisHoje(t, nomes, hoje), ComUso = comUso.Contains(t.Id)
            })]
        };
    }

    public async Task<TerritorioDto?> ObterAsync(Guid id, CancellationToken ct = default)
    {
        AcessoTerritorios.ExigirLeitura(_autorizacao);
        return await ReleAsync(id, ct);
    }

    private async Task<TerritorioDto?> ReleAsync(Guid id, CancellationToken ct)
    {
        if (await _repositorio.ObterAsync(id, ct) is not { } t) return null;
        var doMapa = await _repositorio.ListarDoMapaAsync(t.MapaId, ct);
        var comUso = await _uso.TerritoriosComUsoAsync(t.MapaId, ct);
        return ParaDto(t, doMapa, await NomesAsync([t], ct), comUso.Contains(t.Id));
    }

    public async Task<TerritoriosOpcoesDto> ListarOpcoesAsync(CancellationToken ct = default)
    {
        AcessoTerritorios.ExigirLeitura(_autorizacao);
        var funcoes = (await _comercial.TiposAsync(ct)).Values.OrderBy(p => p.Ordem).ThenBy(p => p.Nome).ToList();
        var usosTipos = await _tipos.ContarUsosAsync(ct);
        var mapas = await _mapas.ListarAsync(ct);
        var empresas = await _empresas.ListarEmpresasAsync(ct);
        var finalidades = await _finalidades.ListarAsync(ct);
        var emUso = await _uso.MapasEmUsoAsync(ct);
        var ativosPorMapa = await _mapas.ContarTerritoriosAtivosAsync(ct);
        var nomesEmpresas = empresas.ToDictionary(e => e.Id, e => e.Nome);
        var nomesFinalidades = finalidades.ToDictionary(f => f.Id, f => f.Nome);
        return new TerritoriosOpcoesDto
        {
            Tipos = [.. (await _tipos.ListarAsync(ct)).OrderBy(t => t.Ordem).ThenBy(t => t.Nome).Select(t => TipoTerritorioAppService.ParaDto(t, usosTipos.GetValueOrDefault(t.Id)))],
            Mapas = [.. mapas.OrderBy(m => m.Nome, StringComparer.CurrentCultureIgnoreCase)
                .Select(m => MapaTerritorialAppService.ParaDto(m, nomesEmpresas, nomesFinalidades, emUso.Contains(m.Id), ativosPorMapa.GetValueOrDefault(m.Id)))],
            Funcoes = [.. funcoes.Select(p => TipoCarteiraAppService.ParaDto(p, 0))],
            Pessoas = await _pessoas.ListarAtendentesAsync([.. funcoes.Where(p => p.Ativo).SelectMany(p => p.ClassificacoesAceitas).Distinct()], ct),
            Equipes = [.. (await _equipes.ListarAsync(ct)).Where(e => e.Ativo).OrderBy(e => e.Nome, StringComparer.CurrentCultureIgnoreCase)
                .Select(e => new PessoaOpcaoDto(e.Id, e.Nome))],
            Empresas = empresas,
            Classificacoes = [.. (await _classificacoes.ListarAsync(incluirInativos: true, ct)).Select(p => new ClassificacaoOpcaoDto(p.Id, p.Nome, p.Ativo))],
            Finalidades = [.. finalidades.OrderBy(f => f.Ordem).ThenBy(f => f.Nome)
                .Select(f => new FinalidadeEnderecoDto { Id = f.Id, Codigo = f.Codigo, Nome = f.Nome, Ordem = f.Ordem, Ativo = f.Ativo })]
        };
    }

    // ------------------------------------------------------------------ Gravação

    public async Task<TerritorioDto> SalvarAsync(TerritorioDto dto, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Territorios.Configurar);
        var anterior = dto.Id == Guid.Empty ? null : await _repositorio.ObterAsync(dto.Id, ct);
        var mapaId = anterior?.MapaId ?? dto.MapaId;
        var hoje = Hoje;
        var paiId = dto.PaiId == Guid.Empty ? null : dto.PaiId;
        var inicio = dto.InicioEm ?? (anterior is null ? hoje : RegrasArvoreTerritorial.InicioDe(anterior) ?? hoje);
        var estrutural = anterior is null || anterior.PaiId != paiId || RegrasArvoreTerritorial.InicioDe(anterior) != inicio;
        if (estrutural) await ExigirArvoreVistaAsync(mapaId, dto.VersaoArvore, ct); // antes de conferir contra outra árvore

        var mapa = await _mapas.ObterAsync(mapaId, ct);
        var doMapa = await _repositorio.ListarDoMapaAsync(mapaId, ct);
        var tipos = (await _tipos.ListarAsync(ct)).ToDictionary(t => t.Id);
        var comUso = await _uso.TerritoriosComUsoAsync(mapaId, ct);

        var id = dto.Id == Guid.Empty ? IdSequencial.Novo() : dto.Id;
        var codigo = RegrasCadastroTerritorial.NormalizarCodigo(dto.Codigo);
        var dados = new Territorio
        {
            Id = id,
            Versao = dto.Versao,
            MapaId = mapaId,
            Codigo = codigo,
            Nome = RegrasCadastroTerritorial.Texto(dto.Nome),
            TipoId = dto.TipoId,
            PaiId = paiId,
            Descricao = RegrasCadastroTerritorial.TextoOpcional(dto.Descricao),
            Situacao = anterior?.Situacao ?? SituacaoTerritorio.Ativo,
            FimEm = anterior?.FimEm
        };
        var usoNaSubarvore = anterior is not null && RegrasArvoreTerritorial.UsoNaSubarvore(doMapa, id, comUso);
        RegrasArvoreTerritorial.AjustarPosicoes(dados, anterior, inicio, usoNaSubarvore);
        dados.Responsaveis = MesclarResponsaveis(id, dto.Responsaveis, anterior);

        var erros = RegrasArvoreTerritorial.Validar(dados, anterior, doMapa, mapa, tipos, comUso, hoje);
        erros.AddRange(await ValidarResponsaveisAsync(dados, anterior, hoje, ct));
        if (erros.Count > 0) throw new ValidacaoException(erros.Distinct().ToList());

        RegistrarEventos(dados, anterior, doMapa);
        await _repositorio.SalvarAsync(dados, anterior is null, estrutural ? dto.VersaoArvore : null, estrutural ? comUso : null, ct);
        return await ReleAsync(id, ct) ?? throw new ConflitoDeEdicaoException();
    }

    public Task<TerritorioDto> EncerrarAsync(Guid id, AlterarSituacaoTerritorioRequisicao requisicao, CancellationToken ct = default) =>
        AlterarSituacaoAsync(id, requisicao, encerrar: true, ct);

    public Task<TerritorioDto> ReativarAsync(Guid id, AlterarSituacaoTerritorioRequisicao requisicao, CancellationToken ct = default) =>
        AlterarSituacaoAsync(id, requisicao, encerrar: false, ct);

    /// <summary>Encerrar e reativar sem operação: só sem uso operacional (na 2b-1b, com uso, só por operação territorial).</summary>
    private async Task<TerritorioDto> AlterarSituacaoAsync(Guid id, AlterarSituacaoTerritorioRequisicao requisicao, bool encerrar, CancellationToken ct)
    {
        _autorizacao.Exigir(Permissoes.Territorios.Configurar);
        var territorio = await _repositorio.ObterAsync(id, ct) ?? throw new ValidacaoException(["Este território não existe mais."]);
        await ExigirArvoreVistaAsync(territorio.MapaId, requisicao.VersaoArvore, ct);
        var mapa = await _mapas.ObterAsync(territorio.MapaId, ct);
        var doMapa = await _repositorio.ListarDoMapaAsync(territorio.MapaId, ct);
        var comUso = await _uso.TerritoriosComUsoAsync(territorio.MapaId, ct);

        var erros = encerrar
            ? RegrasArvoreTerritorial.ValidarEncerramento(territorio, doMapa, mapa, comUso)
            : RegrasArvoreTerritorial.ValidarReativacao(territorio, doMapa, mapa, comUso);
        if (erros.Count > 0) throw new ValidacaoException(erros);

        territorio.Versao = requisicao.Versao ?? territorio.Versao;
        if (encerrar) RegrasArvoreTerritorial.Encerrar(territorio, Hoje);
        else RegrasArvoreTerritorial.Reativar(territorio);
        _motivo.Motivo = RegrasCadastroTerritorial.TextoOpcional(requisicao.Motivo);
        await _repositorio.SalvarAsync(territorio, novo: false, requisicao.VersaoArvore, comUso, ct);
        return await ReleAsync(id, ct) ?? throw new ConflitoDeEdicaoException();
    }

    /// <summary>
    /// Mudança de estrutura: a árvore precisa ser a mesma que a tela mostrava. Conferido aqui para responder cedo (sem
    /// conferir a mudança contra uma árvore que o usuário não viu); a garantia é a gravação, que exige a mesma versão na
    /// transação.
    /// </summary>
    private async Task ExigirArvoreVistaAsync(Guid mapaId, byte[]? vista, CancellationToken ct)
    {
        if (vista is null) throw new ValidacaoException([RegrasArvoreTerritorial.MensagemSemVersaoArvore]);
        var atual = await _repositorio.ObterVersaoArvoreAsync(mapaId, ct)
                    ?? throw new ValidacaoException(["O mapa territorial escolhido não existe mais."]);
        if (!atual.AsSpan().SequenceEqual(vista)) throw new ConflitoDeEdicaoException(RegrasArvoreTerritorial.MensagemArvoreAlterada);
    }

    // ------------------------------------------------------------------ Responsáveis

    /// <summary>
    /// Os responsáveis enviados + os gravados que não vieram (nunca somem). Id vazio = novo. Pessoa e equipe: só uma
    /// (o que vier vazio vira nulo).
    /// </summary>
    private static List<TerritorioResponsavel> MesclarResponsaveis(Guid territorioId, IEnumerable<TerritorioResponsavelDto>? enviados, Territorio? anterior)
    {
        var lista = (enviados ?? []).Select(r => new TerritorioResponsavel
        {
            Id = r.Id == Guid.Empty ? IdSequencial.Novo() : r.Id,
            TerritorioId = territorioId,
            PessoaId = r.PessoaId == Guid.Empty ? null : r.PessoaId,
            EquipeId = r.EquipeId == Guid.Empty ? null : r.EquipeId,
            TipoCarteiraId = r.TipoCarteiraId,
            InicioEm = r.InicioEm,
            FimEm = r.FimEm,
            Observacao = RegrasCadastroTerritorial.TextoOpcional(r.Observacao),
            Ativo = r.Ativo
        }).ToList();
        foreach (var gravado in anterior?.Responsaveis ?? [])
        {
            if (lista.FirstOrDefault(r => r.Id == gravado.Id) is { } enviado)
            {
                enviado.CriadoEm = gravado.CriadoEm;
                enviado.AtualizadoEm = gravado.AtualizadoEm;
            }
            else lista.Add(gravado);
        }
        return lista;
    }

    private async Task<List<string>> ValidarResponsaveisAsync(Territorio dados, Territorio? anterior, DateOnly hoje, CancellationToken ct)
    {
        if (dados.Responsaveis.Count == 0) return [];
        var funcoes = await _comercial.TiposAsync(ct);
        var idsPessoas = dados.Responsaveis.Select(r => r.PessoaId).OfType<Guid>().Distinct().ToList();
        var pessoas = idsPessoas.Count > 0 ? await _pessoas.PessoasElegiveisAsync(idsPessoas, ct) : new Dictionary<Guid, PessoaElegivel>();
        var equipes = dados.Responsaveis.Any(r => r.EquipeId is not null)
            ? (await _equipes.ListarAsync(ct)).ToDictionary(e => e.Id)
            : new Dictionary<Guid, Equipe>();
        var citados = new List<Territorio> { dados };
        if (anterior is not null) citados.Add(anterior);
        var nomes = await NomesAsync(citados, ct);
        var inicioTerritorio = RegrasArvoreTerritorial.InicioDe(dados) ?? hoje;
        return RegrasResponsavelTerritorio.Validar(dados, anterior, inicioTerritorio, hoje, funcoes, pessoas, equipes, nomes);
    }

    // ------------------------------------------------------------------ Apoio

    /// <summary>Eventos de negócio em linguagem do usuário (vão para o histórico junto com a gravação).</summary>
    private static void RegistrarEventos(Territorio dados, Territorio? anterior, IReadOnlyCollection<Territorio> doMapa)
    {
        string NomeDe(Guid? paiId) => paiId is { } p ? doMapa.FirstOrDefault(t => t.Id == p)?.Nome ?? "?" : "primeiro nível";
        if (anterior is null)
        {
            dados.RegistrarEvento($"Território '{dados.Nome}' criado em {NomeDe(dados.PaiId)}.");
            return;
        }
        if (!string.Equals(anterior.Nome, dados.Nome, StringComparison.Ordinal))
            dados.RegistrarEvento($"Território '{anterior.Nome}' renomeado para '{dados.Nome}'.");
        if (anterior.PaiId != dados.PaiId)
            dados.RegistrarEvento($"Território '{dados.Nome}' movido de {NomeDe(anterior.PaiId)} para {NomeDe(dados.PaiId)}.");
    }

    /// <summary>Nomes das pessoas, equipes e funções citadas nos responsáveis.</summary>
    private async Task<Dictionary<Guid, string>> NomesAsync(IEnumerable<Territorio> territorios, CancellationToken ct)
    {
        var responsaveis = territorios.SelectMany(t => t.Responsaveis).ToList();
        var nomes = new Dictionary<Guid, string>();
        if (responsaveis.Count == 0) return nomes;
        var idsPessoas = responsaveis.Select(r => r.PessoaId).OfType<Guid>().Distinct().ToList();
        if (idsPessoas.Count > 0)
            foreach (var (id, nome) in await _pessoas.NomesAsync(idsPessoas, ct)) nomes[id] = nome;
        if (responsaveis.Any(r => r.EquipeId is not null))
            foreach (var e in await _equipes.ListarAsync(ct)) nomes[e.Id] = e.Nome;
        foreach (var (id, funcao) in await _comercial.TiposAsync(ct)) nomes[id] = funcao.Nome;
        return nomes;
    }

    /// <summary>"Vendedor: João · Supervisor: Equipe Sul" (os vigentes hoje).</summary>
    private static string? ResponsaveisHoje(Territorio t, IReadOnlyDictionary<Guid, string> nomes, DateOnly hoje)
    {
        var partes = t.Responsaveis.Where(r => r.VigenteEm(hoje))
            .Select(r => $"{nomes.GetValueOrDefault(r.TipoCarteiraId, "?")}: {nomes.GetValueOrDefault(r.PessoaId ?? r.EquipeId ?? Guid.Empty, "?")}")
            .Order(StringComparer.CurrentCultureIgnoreCase).ToList();
        return partes.Count == 0 ? null : string.Join(" · ", partes);
    }

    private static TerritorioDto ParaDto(Territorio t, IReadOnlyCollection<Territorio> doMapa, IReadOnlyDictionary<Guid, string> nomes, bool comUso)
    {
        var porId = doMapa.ToDictionary(x => x.Id);
        var caminho = RegrasArvoreTerritorial.Caminho(porId, t.PaiId);
        caminho.Reverse();
        return new TerritorioDto
        {
            Id = t.Id, Versao = t.Versao, MapaId = t.MapaId, Codigo = t.Codigo, Nome = t.Nome, TipoId = t.TipoId, PaiId = t.PaiId,
            Descricao = t.Descricao, InicioEm = RegrasArvoreTerritorial.InicioDe(t), Situacao = t.Situacao, FimEm = t.FimEm,
            Caminho = caminho.Count == 0 ? null : string.Join(" › ", caminho.Select(c => c.Nome)),
            ComUso = comUso,
            Posicoes = [.. t.Posicoes.OrderByDescending(p => p.InicioEm).ThenByDescending(p => p.Ativo).Select(p => new TerritorioPosicaoDto
            {
                PaiId = p.PaiId, Pai = p.PaiId is { } pai ? porId.GetValueOrDefault(pai)?.Nome : null, InicioEm = p.InicioEm, FimEm = p.FimEm, Ativo = p.Ativo
            })],
            Responsaveis = [.. t.Responsaveis.OrderByDescending(r => r.Ativo).ThenBy(r => r.FimEm is not null).ThenByDescending(r => r.InicioEm)
                .Select(r => new TerritorioResponsavelDto
                {
                    Id = r.Id, PessoaId = r.PessoaId, EquipeId = r.EquipeId, Nome = nomes.GetValueOrDefault(r.PessoaId ?? r.EquipeId ?? Guid.Empty),
                    TipoCarteiraId = r.TipoCarteiraId, Funcao = nomes.GetValueOrDefault(r.TipoCarteiraId), InicioEm = r.InicioEm, FimEm = r.FimEm,
                    Observacao = r.Observacao, Ativo = r.Ativo
                })]
        };
    }
}
