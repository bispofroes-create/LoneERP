using Lone.Contracts.Comum;
using Lone.Contracts.Seguranca;

namespace Lone.Cliente.Api;

/// <summary>Chamadas do cadastro de perfis de acesso (exige a permissão de gerenciar perfis).</summary>
public sealed class PerfisApi
{
    private readonly ClienteApi _api;

    public PerfisApi(ClienteApi api)
    {
        _api = api;
    }

    public Task<List<PerfilResumo>> ListarAsync(CancellationToken ct = default) =>
        _api.GetAsync<List<PerfilResumo>>(Rotas.Perfis.Grupo, ct);

    public Task<PerfilDto?> ObterAsync(Guid id, CancellationToken ct = default) =>
        _api.GetOuNuloAsync<PerfilDto>(Rotas.Perfis.PorId(id), ct);

    public Task<PerfilDto> SalvarAsync(PerfilDto perfil, CancellationToken ct = default) =>
        _api.PutAsync<PerfilDto>(Rotas.Perfis.PorId(perfil.Id), perfil, ct);

    /// <summary>Catálogo de permissões com descrição, agrupado por módulo na tela.</summary>
    public Task<List<DefinicaoPermissao>> ListarPermissoesAsync(CancellationToken ct = default) =>
        _api.GetAsync<List<DefinicaoPermissao>>(Rotas.Perfis.Permissoes, ct);
}
