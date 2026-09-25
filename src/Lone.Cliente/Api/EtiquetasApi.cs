using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Comum;
using Lone.Contracts.Etiquetas;

namespace Lone.Cliente.Api;

/// <summary>Cadastro de etiquetas. A API confere as permissões.</summary>
public sealed class EtiquetasApi
{
    private readonly ClienteApi _api;

    public EtiquetasApi(ClienteApi api)
    {
        _api = api;
    }

    public Task<List<EtiquetaDto>> ListarAsync(bool incluirInativas, CancellationToken ct = default) =>
        _api.GetAsync<List<EtiquetaDto>>(Rotas.Etiquetas.Listar(incluirInativas), ct);

    public Task<EtiquetaDto?> ObterAsync(Guid id, CancellationToken ct = default) =>
        _api.GetOuNuloAsync<EtiquetaDto>(Rotas.Etiquetas.PorId(id), ct);

    public Task<EtiquetaDto> SalvarAsync(EtiquetaDto etiqueta, CancellationToken ct = default) =>
        _api.PutAsync<EtiquetaDto>(Rotas.Etiquetas.PorId(etiqueta.Id), etiqueta, ct);

    public Task<EtiquetaDto> DesativarAsync(Guid id, byte[]? versao, CancellationToken ct = default) =>
        _api.PostAsync<EtiquetaDto>(Rotas.Etiquetas.Desativar(id), new AlterarSituacaoRequisicao { Versao = versao }, ct: ct);

    public Task<EtiquetaDto> ReativarAsync(Guid id, byte[]? versao, CancellationToken ct = default) =>
        _api.PostAsync<EtiquetaDto>(Rotas.Etiquetas.Reativar(id), new AlterarSituacaoRequisicao { Versao = versao }, ct: ct);

    public Task<ResultadoMesclarEtiqueta> MesclarAsync(Guid origemId, Guid destinoId, byte[]? versao, CancellationToken ct = default) =>
        _api.PostAsync<ResultadoMesclarEtiqueta>(Rotas.Etiquetas.Mesclar(origemId),
            new MesclarEtiquetaRequisicao { DestinoId = destinoId, Versao = versao }, ct: ct);
}
