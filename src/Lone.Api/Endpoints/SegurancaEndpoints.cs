using Lone.Api.Erros;
using Lone.Api.Seguranca;
using Lone.Application.Seguranca;
using Lone.Contracts.Comum;
using Lone.Contracts.Seguranca;

namespace Lone.Api.Endpoints;

/// <summary>Usuários, perfis e empresas do grupo. Toda alteração limpa o cache de permissões.</summary>
public static class SegurancaEndpoints
{
    public static IEndpointRouteBuilder MapSeguranca(this IEndpointRouteBuilder app)
    {
        MapUsuarios(app);
        MapPerfis(app);

        app.MapGet(Rotas.Empresas.DoGrupo,
                (IUsuarioAppService servico, CancellationToken ct) => servico.ListarEmpresasAsync(ct))
            .WithTags("Empresas")
            .RequireAuthorization();

        return app;
    }

    private static void MapUsuarios(IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup(Rotas.Usuarios.Grupo).WithTags("Usuários").RequireAuthorization();

        grupo.MapGet(string.Empty, (IUsuarioAppService servico, CancellationToken ct) => servico.ListarAsync(ct));

        grupo.MapGet("perfis-disponiveis", (IUsuarioAppService servico, CancellationToken ct) => servico.ListarPerfisAsync(ct));

        grupo.MapGet("pessoas", (string? texto, IUsuarioAppService servico, CancellationToken ct) => servico.BuscarPessoasAsync(texto, ct));

        grupo.MapGet("{id:guid}", async (Guid id, IUsuarioAppService servico, CancellationToken ct) =>
            await servico.ObterAsync(id, ct) is { } usuario
                ? Results.Ok(usuario)
                : Problemas.Resultado(Problemas.NaoEncontrado("Este usuário não existe.")));

        grupo.MapPut("{id:guid}", async (Guid id, SalvarUsuarioRequisicao requisicao, IUsuarioAppService servico,
                                         CacheAcesso cache, CancellationToken ct) =>
        {
            if (requisicao.Usuario.Id != Guid.Empty && requisicao.Usuario.Id != id)
                return Problemas.Resultado(Problemas.Validacao("O Id da URL não confere com o Id do usuário enviado."));

            requisicao.Usuario.Id = id;
            var salvo = await servico.SalvarAsync(requisicao, ct);
            cache.Invalidar();
            return Results.Ok(salvo);
        });

        grupo.MapPost("{id:guid}/desbloquear", async (Guid id, IUsuarioAppService servico, CancellationToken ct) =>
        {
            await servico.DesbloquearAsync(id, ct);
            return Results.NoContent();
        });
    }

    private static void MapPerfis(IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup(Rotas.Perfis.Grupo).WithTags("Perfis").RequireAuthorization();

        grupo.MapGet(string.Empty, (IPerfilAppService servico, CancellationToken ct) => servico.ListarAsync(ct));

        grupo.MapGet("permissoes", (IPerfilAppService servico) => servico.ListarPermissoes());

        grupo.MapGet("{id:guid}", async (Guid id, IPerfilAppService servico, CancellationToken ct) =>
            await servico.ObterAsync(id, ct) is { } perfil
                ? Results.Ok(perfil)
                : Problemas.Resultado(Problemas.NaoEncontrado("Este perfil não existe.")));

        grupo.MapPut("{id:guid}", async (Guid id, PerfilDto perfil, IPerfilAppService servico, CacheAcesso cache, CancellationToken ct) =>
        {
            if (perfil.Id != Guid.Empty && perfil.Id != id)
                return Problemas.Resultado(Problemas.Validacao("O Id da URL não confere com o Id do perfil enviado."));

            perfil.Id = id;
            var salvo = await servico.SalvarAsync(perfil, ct);
            cache.Invalidar();
            return Results.Ok(salvo);
        });
    }
}
