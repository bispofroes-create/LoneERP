using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Comum;
using Lone.Contracts.Papeis;

namespace Lone.Cliente.Api;

/// <summary>Cadastro de papéis. A API confere as permissões.</summary>
public sealed class PapeisApi
{
    private readonly ClienteApi _api;

    public PapeisApi(ClienteApi api)
    {
        _api = api;
    }

    public Task<List<PapelCadastroDto>> ListarAsync(bool incluirInativos, CancellationToken ct = default) =>
        _api.GetAsync<List<PapelCadastroDto>>(Rotas.Papeis.Listar(incluirInativos), ct);

    public Task<PapelCadastroDto?> ObterAsync(Guid id, CancellationToken ct = default) =>
        _api.GetOuNuloAsync<PapelCadastroDto>(Rotas.Papeis.PorId(id), ct);

    public Task<PapelCadastroDto> SalvarAsync(PapelCadastroDto papel, CancellationToken ct = default) =>
        _api.PutAsync<PapelCadastroDto>(Rotas.Papeis.PorId(papel.Id), papel, ct);

    public Task<PapelCadastroDto> DesativarAsync(Guid id, byte[]? versao, CancellationToken ct = default) =>
        _api.PostAsync<PapelCadastroDto>(Rotas.Papeis.Desativar(id), new AlterarSituacaoRequisicao { Versao = versao }, ct: ct);

    public Task<PapelCadastroDto> ReativarAsync(Guid id, byte[]? versao, CancellationToken ct = default) =>
        _api.PostAsync<PapelCadastroDto>(Rotas.Papeis.Reativar(id), new AlterarSituacaoRequisicao { Versao = versao }, ct: ct);
}
