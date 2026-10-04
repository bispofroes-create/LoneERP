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

    /// <summary>Preferência de uma tela (JSON que só a tela entende; vazio = nenhuma), guardada para o usuário.</summary>
    public async Task<string> ObterTelaAsync(string tela, CancellationToken ct = default) =>
        (await _api.GetAsync<PreferenciaTelaDto>(Rotas.Menu.Tela(tela), ct)).Conteudo;

    public Task SalvarTelaAsync(string tela, string conteudo, CancellationToken ct = default) =>
        _api.PutSemRespostaAsync(Rotas.Menu.Tela(tela), new PreferenciaTelaDto { Conteudo = conteudo }, ct);

    public Task RegistrarAcessoAsync(string rota, CancellationToken ct = default) =>
        _api.PostAsync(Rotas.Menu.Acessos, new AcessoMenuRequisicao { Rota = rota }, ct: ct);
}
