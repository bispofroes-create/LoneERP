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

        return app;
    }
}
