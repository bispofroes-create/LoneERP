using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Comum;
using Lone.Contracts.Territorios;

namespace Lone.Cliente.Api;

/// <summary>Tipos de território, mapas territoriais e a árvore (Fase 2b-1a). A API confere permissões e regras.</summary>
public sealed class TerritoriosApi
{
    private readonly ClienteApi _api;

    public TerritoriosApi(ClienteApi api)
    {
        _api = api;
    }

    public Task<TerritoriosOpcoesDto> ListarOpcoesAsync(CancellationToken ct = default) =>
        _api.GetAsync<TerritoriosOpcoesDto>(Rotas.Territorios.Opcoes, ct);

    // ---- Tipos ----

    public Task<List<TipoTerritorioDto>> ListarTiposAsync(CancellationToken ct = default) =>
        _api.GetAsync<List<TipoTerritorioDto>>(Rotas.Estrutura.Listar(Rotas.Territorios.Tipos, true), ct);

    public Task<TipoTerritorioDto?> ObterTipoAsync(Guid id, CancellationToken ct = default) =>
        _api.GetOuNuloAsync<TipoTerritorioDto>(Rotas.Estrutura.PorId(Rotas.Territorios.Tipos, id), ct);

    public Task<TipoTerritorioDto> SalvarTipoAsync(TipoTerritorioDto item, CancellationToken ct = default) =>
        _api.PutAsync<TipoTerritorioDto>(Rotas.Estrutura.PorId(Rotas.Territorios.Tipos, item.Id), item, ct);

    // ---- Mapas ----

    public Task<List<MapaTerritorialDto>> ListarMapasAsync(CancellationToken ct = default) =>
        _api.GetAsync<List<MapaTerritorialDto>>(Rotas.Estrutura.Listar(Rotas.Territorios.Mapas, true), ct);

    public Task<MapaTerritorialDto?> ObterMapaAsync(Guid id, CancellationToken ct = default) =>
        _api.GetOuNuloAsync<MapaTerritorialDto>(Rotas.Estrutura.PorId(Rotas.Territorios.Mapas, id), ct);

    public Task<MapaTerritorialDto> SalvarMapaAsync(MapaTerritorialDto item, CancellationToken ct = default) =>
        _api.PutAsync<MapaTerritorialDto>(Rotas.Estrutura.PorId(Rotas.Territorios.Mapas, item.Id), item, ct);

    /// <summary>Desativa/reativa tipo ou mapa (<paramref name="grupo"/> = Rotas.Territorios.Tipos/Mapas).</summary>
    public Task<T> AlterarAtivoAsync<T>(string grupo, Guid id, bool ativar, byte[]? versao, CancellationToken ct = default) =>
        _api.PostAsync<T>(ativar ? Rotas.Estrutura.Reativar(grupo, id) : Rotas.Estrutura.Desativar(grupo, id),
                          new AlterarSituacaoRequisicao { Versao = versao }, ct: ct);

    // ---- Territórios ----

    public Task<ArvoreTerritorialDto> ListarDoMapaAsync(Guid mapaId, CancellationToken ct = default) =>
        _api.GetAsync<ArvoreTerritorialDto>(Rotas.Territorios.DoMapa(mapaId), ct);

    public Task<TerritorioDto?> ObterAsync(Guid id, CancellationToken ct = default) =>
        _api.GetOuNuloAsync<TerritorioDto>(Rotas.Territorios.PorId(id), ct);

    public Task<TerritorioDto> SalvarAsync(TerritorioDto item, CancellationToken ct = default) =>
        _api.PutAsync<TerritorioDto>(Rotas.Territorios.PorId(item.Id), item, ct);

    public Task<TerritorioDto> EncerrarAsync(Guid id, byte[]? versao, byte[]? versaoArvore, string? motivo, CancellationToken ct = default) =>
        _api.PostAsync<TerritorioDto>(Rotas.Territorios.Encerrar(id),
            new AlterarSituacaoTerritorioRequisicao { Versao = versao, VersaoArvore = versaoArvore, Motivo = motivo }, ct: ct);

    public Task<TerritorioDto> ReativarAsync(Guid id, byte[]? versao, byte[]? versaoArvore, CancellationToken ct = default) =>
        _api.PostAsync<TerritorioDto>(Rotas.Territorios.Reativar(id),
            new AlterarSituacaoTerritorioRequisicao { Versao = versao, VersaoArvore = versaoArvore }, ct: ct);
}
