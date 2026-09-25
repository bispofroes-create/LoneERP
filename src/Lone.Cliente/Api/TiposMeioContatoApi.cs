using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Comum;
using Lone.Contracts.Contatos;

namespace Lone.Cliente.Api;

/// <summary>Tipos de telefone/e-mail. A API confere as permissões.</summary>
public sealed class TiposMeioContatoApi
{
    private readonly ClienteApi _api;

    public TiposMeioContatoApi(ClienteApi api)
    {
        _api = api;
    }

    public Task<List<TipoMeioContatoDto>> ListarAsync(bool incluirInativos, CancellationToken ct = default) =>
        _api.GetAsync<List<TipoMeioContatoDto>>(Rotas.TiposMeioContato.Listar(incluirInativos), ct);

    public Task<TipoMeioContatoDto?> ObterAsync(Guid id, CancellationToken ct = default) =>
        _api.GetOuNuloAsync<TipoMeioContatoDto>(Rotas.TiposMeioContato.PorId(id), ct);

    public Task<TipoMeioContatoDto> SalvarAsync(TipoMeioContatoDto tipo, CancellationToken ct = default) =>
        _api.PutAsync<TipoMeioContatoDto>(Rotas.TiposMeioContato.PorId(tipo.Id), tipo, ct);

    public Task<TipoMeioContatoDto> DesativarAsync(Guid id, byte[]? versao, CancellationToken ct = default) =>
        _api.PostAsync<TipoMeioContatoDto>(Rotas.TiposMeioContato.Desativar(id), new AlterarSituacaoRequisicao { Versao = versao }, ct: ct);

    public Task<TipoMeioContatoDto> ReativarAsync(Guid id, byte[]? versao, CancellationToken ct = default) =>
        _api.PostAsync<TipoMeioContatoDto>(Rotas.TiposMeioContato.Reativar(id), new AlterarSituacaoRequisicao { Versao = versao }, ct: ct);
}
