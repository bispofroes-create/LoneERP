using Lone.Api.Erros;
using Lone.Application.Metas;
using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Comum;
using Lone.Contracts.Metas;

namespace Lone.Api.Endpoints;

/// <summary>Motor de metas: equipes, indicadores e metas (rascunho → publicada → em apuração → fechada). Permissões nos AppServices.</summary>
public static class MetasEndpoints
{
    public static IEndpointRouteBuilder MapMetas(this IEndpointRouteBuilder app)
    {
        MapEquipes(app);
        MapIndicadores(app);

        var grupo = app.MapGroup(Rotas.Metas.Grupo).WithTags("Metas").RequireAuthorization();

        grupo.MapGet(string.Empty, (bool? incluirInativas, IMetaAppService servico, CancellationToken ct) =>
            servico.ListarAsync(incluirInativas ?? false, ct));

        grupo.MapGet("opcoes", (IMetaAppService servico, CancellationToken ct) => servico.ListarOpcoesAsync(ct));

        grupo.MapGet("{id:guid}", async (Guid id, IMetaAppService servico, CancellationToken ct) =>
            await servico.ObterAsync(id, ct) is { } item
                ? Results.Ok(item)
                : Problemas.Resultado(Problemas.NaoEncontrado("Esta meta não existe.")));

        grupo.MapPut("{id:guid}", async (Guid id, MetaDto item, IMetaAppService servico, CancellationToken ct) =>
        {
            if (item.Id != Guid.Empty && item.Id != id)
                return Problemas.Resultado(Problemas.Validacao("O Id da URL não confere com o Id enviado."));
            item.Id = id;
            return Results.Ok(await servico.SalvarAsync(item, ct));
        });

        grupo.MapPost("{id:guid}/situacao", (Guid id, AlterarSituacaoMetaRequisicao requisicao, IMetaAppService servico, CancellationToken ct) =>
            servico.AlterarSituacaoAsync(id, requisicao, ct));

        grupo.MapPost("{id:guid}/realizado", (Guid id, LancarRealizadoRequisicao requisicao, IMetaAppService servico, CancellationToken ct) =>
            servico.LancarRealizadoAsync(id, requisicao, ct));

        grupo.MapPost("{id:guid}/realizado/importar", (Guid id, ImportarRealizadoRequisicao requisicao, IMetaAppService servico, CancellationToken ct) =>
            servico.ImportarRealizadoAsync(id, requisicao, ct));

        grupo.MapGet("{id:guid}/apuracao", (Guid id, IMetaAppService servico, CancellationToken ct) => servico.ApurarAsync(id, ct));

        grupo.MapPost("{id:guid}/desativar", (Guid id, IMetaAppService servico, CancellationToken ct) => servico.DesativarAsync(id, ct));

        return app;
    }

    private static void MapEquipes(IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup(Rotas.Metas.Equipes).WithTags("Equipes").RequireAuthorization();

        grupo.MapGet(string.Empty, (bool? incluirInativos, IEquipeAppService servico, CancellationToken ct) =>
            servico.ListarAsync(incluirInativos ?? false, ct));

        grupo.MapGet("{id:guid}", async (Guid id, IEquipeAppService servico, CancellationToken ct) =>
            await servico.ObterAsync(id, ct) is { } item
                ? Results.Ok(item)
                : Problemas.Resultado(Problemas.NaoEncontrado("Esta equipe não existe.")));

        grupo.MapPut("{id:guid}", async (Guid id, EquipeDto item, IEquipeAppService servico, CancellationToken ct) =>
        {
            if (item.Id != Guid.Empty && item.Id != id)
                return Problemas.Resultado(Problemas.Validacao("O Id da URL não confere com o Id enviado."));
            item.Id = id;
            return Results.Ok(await servico.SalvarAsync(item, ct));
        });

        grupo.MapPost("{id:guid}/desativar", (Guid id, AlterarSituacaoRequisicao requisicao, IEquipeAppService servico, CancellationToken ct) =>
            servico.DesativarAsync(id, requisicao, ct));

        grupo.MapPost("{id:guid}/reativar", (Guid id, AlterarSituacaoRequisicao requisicao, IEquipeAppService servico, CancellationToken ct) =>
            servico.ReativarAsync(id, requisicao, ct));
    }

    private static void MapIndicadores(IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup(Rotas.Metas.Indicadores).WithTags("Indicadores").RequireAuthorization();

        grupo.MapGet(string.Empty, (bool? incluirInativos, IIndicadorAppService servico, CancellationToken ct) =>
            servico.ListarAsync(incluirInativos ?? false, ct));

        grupo.MapGet("{id:guid}", async (Guid id, IIndicadorAppService servico, CancellationToken ct) =>
            await servico.ObterAsync(id, ct) is { } item
                ? Results.Ok(item)
                : Problemas.Resultado(Problemas.NaoEncontrado("Este indicador não existe.")));

        grupo.MapPut("{id:guid}", async (Guid id, IndicadorDto item, IIndicadorAppService servico, CancellationToken ct) =>
        {
            if (item.Id != Guid.Empty && item.Id != id)
                return Problemas.Resultado(Problemas.Validacao("O Id da URL não confere com o Id enviado."));
            item.Id = id;
            return Results.Ok(await servico.SalvarAsync(item, ct));
        });

        grupo.MapPost("{id:guid}/desativar", (Guid id, AlterarSituacaoRequisicao requisicao, IIndicadorAppService servico, CancellationToken ct) =>
            servico.DesativarAsync(id, requisicao, ct));

        grupo.MapPost("{id:guid}/reativar", (Guid id, AlterarSituacaoRequisicao requisicao, IIndicadorAppService servico, CancellationToken ct) =>
            servico.ReativarAsync(id, requisicao, ct));
    }
}
