using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Comum;
using Lone.Contracts.GruposEmpresariais;

namespace Lone.Cliente.Api;

/// <summary>Cadastro de grupos empresariais. A API confere as permissões.</summary>
public sealed class GruposEmpresariaisApi
{
    private readonly ClienteApi _api;

    public GruposEmpresariaisApi(ClienteApi api)
    {
        _api = api;
    }

    public Task<List<GrupoEmpresarialDto>> ListarAsync(bool incluirInativos, CancellationToken ct = default) =>
        _api.GetAsync<List<GrupoEmpresarialDto>>(Rotas.GruposEmpresariais.Listar(incluirInativos), ct);

    public Task<GrupoEmpresarialDto?> ObterAsync(Guid id, CancellationToken ct = default) =>
        _api.GetOuNuloAsync<GrupoEmpresarialDto>(Rotas.GruposEmpresariais.PorId(id), ct);

    public Task<List<EmpresaDoGrupoEmpresarialDto>> ListarEmpresasAsync(Guid id, CancellationToken ct = default) =>
        _api.GetAsync<List<EmpresaDoGrupoEmpresarialDto>>(Rotas.GruposEmpresariais.Empresas(id), ct);

    public Task<GrupoEmpresarialDto> SalvarAsync(GrupoEmpresarialDto grupo, CancellationToken ct = default) =>
        _api.PutAsync<GrupoEmpresarialDto>(Rotas.GruposEmpresariais.PorId(grupo.Id), grupo, ct);

    public Task<GrupoEmpresarialDto> DesativarAsync(Guid id, byte[]? versao, CancellationToken ct = default) =>
        _api.PostAsync<GrupoEmpresarialDto>(Rotas.GruposEmpresariais.Desativar(id), new AlterarSituacaoRequisicao { Versao = versao }, ct: ct);

    public Task<GrupoEmpresarialDto> ReativarAsync(Guid id, byte[]? versao, CancellationToken ct = default) =>
        _api.PostAsync<GrupoEmpresarialDto>(Rotas.GruposEmpresariais.Reativar(id), new AlterarSituacaoRequisicao { Versao = versao }, ct: ct);
}
