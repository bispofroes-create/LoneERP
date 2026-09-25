using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Comum;
using Lone.Contracts.Documentos;

namespace Lone.Cliente.Api;

/// <summary>Tipos de documento. A API confere as permissões.</summary>
public sealed class TiposDocumentoApi
{
    private readonly ClienteApi _api;

    public TiposDocumentoApi(ClienteApi api)
    {
        _api = api;
    }

    public Task<List<TipoDocumentoDto>> ListarAsync(bool incluirInativos, CancellationToken ct = default) =>
        _api.GetAsync<List<TipoDocumentoDto>>(Rotas.TiposDocumento.Listar(incluirInativos), ct);

    public Task<TipoDocumentoDto?> ObterAsync(Guid id, CancellationToken ct = default) =>
        _api.GetOuNuloAsync<TipoDocumentoDto>(Rotas.TiposDocumento.PorId(id), ct);

    public Task<TipoDocumentoDto> SalvarAsync(TipoDocumentoDto tipo, CancellationToken ct = default) =>
        _api.PutAsync<TipoDocumentoDto>(Rotas.TiposDocumento.PorId(tipo.Id), tipo, ct);

    public Task<TipoDocumentoDto> DesativarAsync(Guid id, byte[]? versao, CancellationToken ct = default) =>
        _api.PostAsync<TipoDocumentoDto>(Rotas.TiposDocumento.Desativar(id), new AlterarSituacaoRequisicao { Versao = versao }, ct: ct);

    public Task<TipoDocumentoDto> ReativarAsync(Guid id, byte[]? versao, CancellationToken ct = default) =>
        _api.PostAsync<TipoDocumentoDto>(Rotas.TiposDocumento.Reativar(id), new AlterarSituacaoRequisicao { Versao = versao }, ct: ct);
}
