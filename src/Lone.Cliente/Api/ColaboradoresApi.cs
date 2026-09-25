using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Colaboradores;
using Lone.Contracts.Comum;

namespace Lone.Cliente.Api;

/// <summary>Opções da aba "Colaborador" e os cadastros da estrutura organizacional. A API confere as permissões.</summary>
public sealed class ColaboradoresApi
{
    private readonly ClienteApi _api;

    public ColaboradoresApi(ClienteApi api)
    {
        _api = api;
    }

    public Task<ColaboradorOpcoesDto> ListarOpcoesAsync(CancellationToken ct = default) =>
        _api.GetAsync<ColaboradorOpcoesDto>(Rotas.Colaboradores.Opcoes, ct);

    public Task<List<T>> ListarAsync<T>(string grupo, bool incluirInativos, CancellationToken ct = default) =>
        _api.GetAsync<List<T>>(Rotas.Estrutura.Listar(grupo, incluirInativos), ct);

    public Task<T?> ObterAsync<T>(string grupo, Guid id, CancellationToken ct = default) where T : class =>
        _api.GetOuNuloAsync<T>(Rotas.Estrutura.PorId(grupo, id), ct);

    public Task<T> SalvarAsync<T>(string grupo, Guid id, T item, CancellationToken ct = default) where T : notnull =>
        _api.PutAsync<T>(Rotas.Estrutura.PorId(grupo, id), item, ct);

    public Task<T> DesativarAsync<T>(string grupo, Guid id, byte[]? versao, CancellationToken ct = default) =>
        _api.PostAsync<T>(Rotas.Estrutura.Desativar(grupo, id), new AlterarSituacaoRequisicao { Versao = versao }, ct: ct);

    public Task<T> ReativarAsync<T>(string grupo, Guid id, byte[]? versao, CancellationToken ct = default) =>
        _api.PostAsync<T>(Rotas.Estrutura.Reativar(grupo, id), new AlterarSituacaoRequisicao { Versao = versao }, ct: ct);
}
