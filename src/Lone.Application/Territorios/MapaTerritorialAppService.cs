using Lone.Application.Empresas;
using Lone.Application.Enderecos;
using Lone.Application.Papeis;
using Lone.Application.Seguranca;
using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Seguranca;
using Lone.Contracts.Territorios;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Territorios;
using Lone.Domain.Validacao;

namespace Lone.Application.Territorios;

public interface IMapaTerritorialAppService
{
    Task<List<MapaTerritorialDto>> ListarAsync(bool incluirInativos, CancellationToken ct = default);
    Task<MapaTerritorialDto?> ObterAsync(Guid id, CancellationToken ct = default);
    Task<MapaTerritorialDto> SalvarAsync(MapaTerritorialDto dto, CancellationToken ct = default);
    Task<MapaTerritorialDto> DesativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default);
    Task<MapaTerritorialDto> ReativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default);
}

/// <summary>
/// Mapas territoriais (Fase 2b-1a, decisões T1, T9, T16): permissão → normalização → referências (universo, finalidade,
/// empresa) → regras do domínio → gravação. Referência nova precisa existir e estar ativa; a que já estava gravada pode
/// continuar mesmo desativada no cadastro de origem.
/// </summary>
public sealed class MapaTerritorialAppService : IMapaTerritorialAppService
{
    private readonly IMapaTerritorialRepositorio _repositorio;
    private readonly IUsoTerritorial _uso;
    private readonly IPapelRepositorio _classificacoes;
    private readonly IFinalidadeEnderecoRepositorio _finalidades;
    private readonly IEmpresaConsultas _empresas;
    private readonly IAutorizacao _autorizacao;

    public MapaTerritorialAppService(IMapaTerritorialRepositorio repositorio, IUsoTerritorial uso, IPapelRepositorio classificacoes,
                                     IFinalidadeEnderecoRepositorio finalidades, IEmpresaConsultas empresas, IAutorizacao autorizacao)
    {
        _repositorio = repositorio;
        _uso = uso;
        _classificacoes = classificacoes;
        _finalidades = finalidades;
        _empresas = empresas;
        _autorizacao = autorizacao;
    }

    public async Task<List<MapaTerritorialDto>> ListarAsync(bool incluirInativos, CancellationToken ct = default)
    {
        AcessoTerritorios.ExigirLeitura(_autorizacao);
        var mapas = (await _repositorio.ListarAsync(ct)).Where(m => incluirInativos || m.Ativo)
            .OrderBy(m => m.Nome, StringComparer.CurrentCultureIgnoreCase).ToList();
        return await ParaDtosAsync(mapas, ct);
    }

    public async Task<MapaTerritorialDto?> ObterAsync(Guid id, CancellationToken ct = default)
    {
        AcessoTerritorios.ExigirLeitura(_autorizacao);
        return await _repositorio.ObterAsync(id, ct) is { } m ? (await ParaDtosAsync([m], ct))[0] : null;
    }

    public async Task<MapaTerritorialDto> SalvarAsync(MapaTerritorialDto dto, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Territorios.Configurar);
        var todos = await _repositorio.ListarAsync(ct);
        var anterior = dto.Id == Guid.Empty ? null : todos.FirstOrDefault(m => m.Id == dto.Id);
        // Código vazio num mapa gravado = "não mexi" (a tela não deixa editar); diferente do gravado, o domínio recusa.
        var codigo = RegrasCadastroTerritorial.NormalizarCodigo(dto.Codigo);
        var dados = new MapaTerritorial
        {
            Id = dto.Id == Guid.Empty ? IdSequencial.Novo() : dto.Id,
            Versao = dto.Versao,
            Codigo = codigo.Length == 0 && anterior is not null ? anterior.Codigo : codigo,
            Nome = RegrasCadastroTerritorial.Texto(dto.Nome),
            Descricao = RegrasCadastroTerritorial.TextoOpcional(dto.Descricao),
            EmpresaId = dto.EmpresaId == Guid.Empty ? null : dto.EmpresaId,
            Exclusivo = dto.Exclusivo,
            FinalidadeEnderecoReferenciaId = dto.FinalidadeEnderecoReferenciaId,
            RegistrarNosDocumentos = dto.RegistrarNosDocumentos,
            Ativo = anterior?.Ativo ?? true
        };
        dados.Classificacoes = RegrasMapaTerritorial.SincronizarClassificacoes(dados.Id, anterior?.Classificacoes ?? [], dto.Classificacoes ?? []);

        var emUso = anterior is not null && (await _uso.MapasEmUsoAsync(ct)).Contains(dados.Id);
        var erros = RegrasMapaTerritorial.Validar(dados, todos, anterior, emUso);
        erros.AddRange(await ValidarReferenciasAsync(dados, anterior, ct));
        if (erros.Count > 0) throw new ValidacaoException(erros.Distinct().ToList());

        if (anterior is null) dados.RegistrarEvento($"Mapa territorial '{dados.Nome}' criado.");
        else if (!string.Equals(anterior.Nome, dados.Nome, StringComparison.Ordinal))
            dados.RegistrarEvento($"Mapa territorial '{anterior.Nome}' renomeado para '{dados.Nome}'.");

        await _repositorio.SalvarAsync(dados, anterior is null, ct);
        return await ObterAsync(dados.Id, ct) ?? throw new ConflitoDeEdicaoException();
    }

    /// <summary>Classificações novas do universo, finalidade e empresa escolhidas agora precisam existir e estar ativas.</summary>
    private async Task<List<string>> ValidarReferenciasAsync(MapaTerritorial dados, MapaTerritorial? anterior, CancellationToken ct)
    {
        var erros = new List<string>();
        var jaAceitas = anterior?.ClassificacoesAceitas.ToHashSet() ?? [];
        var novas = dados.ClassificacoesAceitas.Where(id => !jaAceitas.Contains(id)).ToList();
        if (novas.Count > 0)
        {
            var cadastro = await _classificacoes.ObterVariosAsync(novas, ct);
            if (novas.Any(id => !cadastro.TryGetValue(id, out var papel) || !papel.Ativo))
                erros.Add("Universo: uma classificação escolhida não existe mais ou está desativada. Reabra o cadastro e escolha de novo.");
        }

        if (dados.FinalidadeEnderecoReferenciaId != Guid.Empty && dados.FinalidadeEnderecoReferenciaId != anterior?.FinalidadeEnderecoReferenciaId)
        {
            var finalidade = (await _finalidades.ListarAsync(ct)).FirstOrDefault(f => f.Id == dados.FinalidadeEnderecoReferenciaId);
            if (finalidade is not { Ativo: true })
                erros.Add("A finalidade do endereço de referência escolhida não existe mais ou está desativada.");
        }

        if (dados.EmpresaId is { } empresaId && empresaId != anterior?.EmpresaId &&
            !(await _empresas.ListarEmpresasAsync(ct)).Any(e => e.Id == empresaId && e.Ativa))
            erros.Add("A empresa escolhida não é uma empresa ativa do grupo.");
        return erros;
    }

    public Task<MapaTerritorialDto> DesativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default) =>
        AlterarAsync(id, requisicao, desativar: true, ct);

    public Task<MapaTerritorialDto> ReativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default) =>
        AlterarAsync(id, requisicao, desativar: false, ct);

    /// <summary>Mapa desativado: a árvore fica só para consulta (nada muda nele até reativar).</summary>
    private async Task<MapaTerritorialDto> AlterarAsync(Guid id, AlterarSituacaoRequisicao requisicao, bool desativar, CancellationToken ct)
    {
        _autorizacao.Exigir(Permissoes.Territorios.Configurar);
        var mapa = await _repositorio.ObterAsync(id, ct) ?? throw new ValidacaoException(["Este mapa territorial não existe mais."]);
        if (desativar)
        {
            var erros = RegrasMapaTerritorial.ValidarDesativacao(mapa, (await _uso.MapasEmUsoAsync(ct)).Contains(id)).ToList();
            if (erros.Count > 0) throw new ValidacaoException(erros);
        }
        mapa.Versao = requisicao.Versao ?? mapa.Versao;
        if (desativar) mapa.Desativar();
        else mapa.Reativar();
        await _repositorio.SalvarAsync(mapa, novo: false, ct);
        return await ObterAsync(id, ct) ?? throw new ConflitoDeEdicaoException();
    }

    private async Task<List<MapaTerritorialDto>> ParaDtosAsync(IReadOnlyList<MapaTerritorial> mapas, CancellationToken ct)
    {
        if (mapas.Count == 0) return [];
        var empresas = mapas.Any(m => m.EmpresaId is not null)
            ? (await _empresas.ListarEmpresasAsync(ct)).ToDictionary(e => e.Id, e => e.Nome)
            : new Dictionary<Guid, string>();
        var finalidades = (await _finalidades.ListarAsync(ct)).ToDictionary(f => f.Id, f => f.Nome);
        var emUso = await _uso.MapasEmUsoAsync(ct);
        var territorios = await _repositorio.ContarTerritoriosAtivosAsync(ct);
        return mapas.Select(m => ParaDto(m, empresas, finalidades, emUso.Contains(m.Id), territorios.GetValueOrDefault(m.Id))).ToList();
    }

    public static MapaTerritorialDto ParaDto(MapaTerritorial m, IReadOnlyDictionary<Guid, string> empresas, IReadOnlyDictionary<Guid, string> finalidades,
                                             bool emUso, int territoriosAtivos) => new()
    {
        Id = m.Id, Versao = m.Versao, Codigo = m.Codigo, Nome = m.Nome, Descricao = m.Descricao, EmpresaId = m.EmpresaId,
        Empresa = m.EmpresaId is { } e ? empresas.GetValueOrDefault(e) : null, Exclusivo = m.Exclusivo,
        FinalidadeEnderecoReferenciaId = m.FinalidadeEnderecoReferenciaId,
        FinalidadeEnderecoReferencia = finalidades.GetValueOrDefault(m.FinalidadeEnderecoReferenciaId),
        Classificacoes = [.. m.ClassificacoesAceitas], Ativo = m.Ativo, EmUso = emUso, TerritoriosAtivos = territoriosAtivos,
        RegistrarNosDocumentos = m.RegistrarNosDocumentos
    };
}
