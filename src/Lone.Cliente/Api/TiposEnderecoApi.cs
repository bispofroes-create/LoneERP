using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Comum;
using Lone.Contracts.Enderecos;

namespace Lone.Cliente.Api;

/// <summary>Tipos de endereço. A API confere as permissões.</summary>
public sealed class TiposEnderecoApi
{
    private readonly ClienteApi _api;

    public TiposEnderecoApi(ClienteApi api)
    {
        _api = api;
    }

    public Task<List<TipoEnderecoDto>> ListarAsync(bool incluirInativos, CancellationToken ct = default) =>
        _api.GetAsync<List<TipoEnderecoDto>>(Rotas.TiposEndereco.Listar(incluirInativos), ct);

    /// <summary>Cadastro de finalidades de endereço (todas; a ficha só oferece as ativas para associação nova).</summary>
    public Task<List<FinalidadeEnderecoDto>> ListarFinalidadesAsync(CancellationToken ct = default) =>
        _api.GetAsync<List<FinalidadeEnderecoDto>>(Rotas.FinalidadesEndereco.Grupo, ct);

    public Task<TipoEnderecoDto?> ObterAsync(Guid id, CancellationToken ct = default) =>
        _api.GetOuNuloAsync<TipoEnderecoDto>(Rotas.TiposEndereco.PorId(id), ct);

    public Task<TipoEnderecoDto> SalvarAsync(TipoEnderecoDto tipo, CancellationToken ct = default) =>
        _api.PutAsync<TipoEnderecoDto>(Rotas.TiposEndereco.PorId(tipo.Id), tipo, ct);

    public Task<TipoEnderecoDto> DesativarAsync(Guid id, byte[]? versao, CancellationToken ct = default) =>
        _api.PostAsync<TipoEnderecoDto>(Rotas.TiposEndereco.Desativar(id), new AlterarSituacaoRequisicao { Versao = versao }, ct: ct);

    public Task<TipoEnderecoDto> ReativarAsync(Guid id, byte[]? versao, CancellationToken ct = default) =>
        _api.PostAsync<TipoEnderecoDto>(Rotas.TiposEndereco.Reativar(id), new AlterarSituacaoRequisicao { Versao = versao }, ct: ct);
}
