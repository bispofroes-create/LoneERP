using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Comum;
using Lone.Contracts.Pessoas;

namespace Lone.Cliente.Api;

/// <summary>Finalidades de tratamento (LGPD). A API confere as permissões.</summary>
public sealed class FinalidadesTratamentoApi
{
    private readonly ClienteApi _api;

    public FinalidadesTratamentoApi(ClienteApi api)
    {
        _api = api;
    }

    public Task<List<FinalidadeTratamentoDto>> ListarAsync(bool incluirInativas, CancellationToken ct = default) =>
        _api.GetAsync<List<FinalidadeTratamentoDto>>(Rotas.FinalidadesTratamento.Listar(incluirInativas), ct);

    public Task<FinalidadeTratamentoDto?> ObterAsync(Guid id, CancellationToken ct = default) =>
        _api.GetOuNuloAsync<FinalidadeTratamentoDto>(Rotas.FinalidadesTratamento.PorId(id), ct);

    public Task<FinalidadeTratamentoDto> SalvarAsync(FinalidadeTratamentoDto finalidade, CancellationToken ct = default) =>
        _api.PutAsync<FinalidadeTratamentoDto>(Rotas.FinalidadesTratamento.PorId(finalidade.Id), finalidade, ct);

    public Task<FinalidadeTratamentoDto> DesativarAsync(Guid id, byte[]? versao, CancellationToken ct = default) =>
        _api.PostAsync<FinalidadeTratamentoDto>(Rotas.FinalidadesTratamento.Desativar(id), new AlterarSituacaoRequisicao { Versao = versao }, ct: ct);

    public Task<FinalidadeTratamentoDto> ReativarAsync(Guid id, byte[]? versao, CancellationToken ct = default) =>
        _api.PostAsync<FinalidadeTratamentoDto>(Rotas.FinalidadesTratamento.Reativar(id), new AlterarSituacaoRequisicao { Versao = versao }, ct: ct);
}
