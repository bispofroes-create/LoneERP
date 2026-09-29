using Lone.Api.Erros;
using Lone.Application.Territorios;
using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Comum;
using Lone.Contracts.Territorios;

namespace Lone.Api.Endpoints;

/// <summary>
/// Territórios (Motor Comercial, Fase 2b-1a): tipos, mapas territoriais e a árvore com os responsáveis. Nada é excluído:
/// tipos e mapas são desativados; territórios são encerrados. Permissões nos AppServices.
/// </summary>
public static class TerritoriosEndpoints
{
    public static IEndpointRouteBuilder MapTerritorios(this IEndpointRouteBuilder app)
    {
        MapTipos(app);
        MapMapas(app);

        var grupo = app.MapGroup(Rotas.Territorios.Grupo).WithTags("Territórios").RequireAuthorization();

        // ?mapaId=... : a árvore do mapa (ativos e encerrados) e a versão dela; a tela monta a hierarquia pelo PaiId.
        grupo.MapGet(string.Empty, (Guid mapaId, ITerritorioAppService servico, CancellationToken ct) => servico.ListarDoMapaAsync(mapaId, ct));

        grupo.MapGet("opcoes", (ITerritorioAppService servico, CancellationToken ct) => servico.ListarOpcoesAsync(ct));

        grupo.MapGet("{id:guid}", async (Guid id, ITerritorioAppService servico, CancellationToken ct) =>
            await servico.ObterAsync(id, ct) is { } item
                ? Results.Ok(item)
                : Problemas.Resultado(Problemas.NaoEncontrado("Este território não existe.")));

        grupo.MapPut("{id:guid}", async (Guid id, TerritorioDto item, ITerritorioAppService servico, CancellationToken ct) =>
        {
            if (item.Id != Guid.Empty && item.Id != id)
                return Problemas.Resultado(Problemas.Validacao("O Id da URL não confere com o Id enviado."));
            item.Id = id;
            return Results.Ok(await servico.SalvarAsync(item, ct));
        });

        // Encerrar e reativar mudam a estrutura: exigem a versão da árvore que a tela mostrava (D1 = B).
        grupo.MapPost("{id:guid}/encerrar", (Guid id, AlterarSituacaoTerritorioRequisicao requisicao, ITerritorioAppService servico, CancellationToken ct) =>
            servico.EncerrarAsync(id, requisicao, ct));

        grupo.MapPost("{id:guid}/reativar", (Guid id, AlterarSituacaoTerritorioRequisicao requisicao, ITerritorioAppService servico, CancellationToken ct) =>
            servico.ReativarAsync(id, requisicao, ct));

        return app;
    }

    private static void MapTipos(IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup(Rotas.Territorios.Tipos).WithTags("Tipos de território").RequireAuthorization();

        grupo.MapGet(string.Empty, (bool? incluirInativos, ITipoTerritorioAppService servico, CancellationToken ct) =>
            servico.ListarAsync(incluirInativos ?? false, ct));

        grupo.MapGet("{id:guid}", async (Guid id, ITipoTerritorioAppService servico, CancellationToken ct) =>
            await servico.ObterAsync(id, ct) is { } item
                ? Results.Ok(item)
                : Problemas.Resultado(Problemas.NaoEncontrado("Este tipo de território não existe.")));

        grupo.MapPut("{id:guid}", async (Guid id, TipoTerritorioDto item, ITipoTerritorioAppService servico, CancellationToken ct) =>
        {
            if (item.Id != Guid.Empty && item.Id != id)
                return Problemas.Resultado(Problemas.Validacao("O Id da URL não confere com o Id enviado."));
            item.Id = id;
            return Results.Ok(await servico.SalvarAsync(item, ct));
        });

        grupo.MapPost("{id:guid}/desativar", (Guid id, AlterarSituacaoRequisicao requisicao, ITipoTerritorioAppService servico, CancellationToken ct) =>
            servico.DesativarAsync(id, requisicao, ct));

        grupo.MapPost("{id:guid}/reativar", (Guid id, AlterarSituacaoRequisicao requisicao, ITipoTerritorioAppService servico, CancellationToken ct) =>
            servico.ReativarAsync(id, requisicao, ct));
    }

    private static void MapMapas(IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup(Rotas.Territorios.Mapas).WithTags("Mapas territoriais").RequireAuthorization();

        grupo.MapGet(string.Empty, (bool? incluirInativos, IMapaTerritorialAppService servico, CancellationToken ct) =>
            servico.ListarAsync(incluirInativos ?? false, ct));

        grupo.MapGet("{id:guid}", async (Guid id, IMapaTerritorialAppService servico, CancellationToken ct) =>
            await servico.ObterAsync(id, ct) is { } item
                ? Results.Ok(item)
                : Problemas.Resultado(Problemas.NaoEncontrado("Este mapa territorial não existe.")));

        grupo.MapPut("{id:guid}", async (Guid id, MapaTerritorialDto item, IMapaTerritorialAppService servico, CancellationToken ct) =>
        {
            if (item.Id != Guid.Empty && item.Id != id)
                return Problemas.Resultado(Problemas.Validacao("O Id da URL não confere com o Id enviado."));
            item.Id = id;
            return Results.Ok(await servico.SalvarAsync(item, ct));
        });

        grupo.MapPost("{id:guid}/desativar", (Guid id, AlterarSituacaoRequisicao requisicao, IMapaTerritorialAppService servico, CancellationToken ct) =>
            servico.DesativarAsync(id, requisicao, ct));

        grupo.MapPost("{id:guid}/reativar", (Guid id, AlterarSituacaoRequisicao requisicao, IMapaTerritorialAppService servico, CancellationToken ct) =>
            servico.ReativarAsync(id, requisicao, ct));
    }
}
