using Lone.Contracts.Comum;
using Lone.Contracts.Menu;

namespace Lone.Cliente.Api;

/// <summary>Favoritos e recentes do menu do usuário logado (guardados na API, acompanham o usuário em qualquer aparelho).</summary>
public sealed class MenuUsuarioApi
{
    private readonly ClienteApi _api;

    public MenuUsuarioApi(ClienteApi api) => _api = api;

    public Task<PreferenciasMenuDto> ObterAsync(CancellationToken ct = default) =>
        _api.GetAsync<PreferenciasMenuDto>(Rotas.Menu.Preferencias, ct);

    public Task DefinirFavoritoAsync(string rota, bool favorito, CancellationToken ct = default) =>
        _api.PutSemRespostaAsync(Rotas.Menu.Favoritos, new FavoritoMenuRequisicao { Rota = rota, Favorito = favorito }, ct);

    public Task RegistrarAcessoAsync(string rota, CancellationToken ct = default) =>
        _api.PostAsync(Rotas.Menu.Acessos, new AcessoMenuRequisicao { Rota = rota }, ct: ct);
}
