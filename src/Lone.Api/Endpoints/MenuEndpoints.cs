using Lone.Application.Menu;
using Lone.Contracts.Comum;
using Lone.Contracts.Menu;

namespace Lone.Api.Endpoints;

/// <summary>Favoritos e recentes do menu do usuário logado. Só login: cada um mexe apenas nos próprios dados.</summary>
public static class MenuEndpoints
{
    public static IEndpointRouteBuilder MapMenu(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup(Rotas.Menu.Grupo).WithTags("Menu do usuário").RequireAuthorization();

        grupo.MapGet("preferencias", (IMenuUsuarioAppService servico, CancellationToken ct) => servico.ObterAsync(ct));

        grupo.MapPut("favoritos", async (FavoritoMenuRequisicao requisicao, IMenuUsuarioAppService servico, CancellationToken ct) =>
        {
            await servico.DefinirFavoritoAsync(requisicao, ct);
            return Results.NoContent();
        });

        grupo.MapPost("acessos", async (AcessoMenuRequisicao requisicao, IMenuUsuarioAppService servico, CancellationToken ct) =>
        {
            await servico.RegistrarAcessoAsync(requisicao, ct);
            return Results.NoContent();
        });

        // Preferência de uma tela (ex.: colunas da lista de pessoas): JSON que só a tela entende.
        grupo.MapGet("telas/{tela}", (string tela, IMenuUsuarioAppService servico, CancellationToken ct) =>
            servico.ObterTelaAsync(tela, ct));

        grupo.MapPut("telas/{tela}", async (string tela, PreferenciaTelaDto preferencia, IMenuUsuarioAppService servico, CancellationToken ct) =>
        {
            await servico.DefinirTelaAsync(tela, preferencia, ct);
            return Results.NoContent();
        });

        return app;
    }
}
