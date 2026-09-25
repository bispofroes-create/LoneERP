using Lone.Api.Erros;
using Lone.Application.CamposPersonalizados;
using Lone.Application.Etiquetas;
using Lone.Application.Municipios;
using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Comum;
using Lone.Contracts.Etiquetas;
using Lone.Domain.Enums;

namespace Lone.Api.Endpoints;

/// <summary>Tabelas de apoio dos cadastros: municípios do IBGE, campos personalizados e etiquetas. Permissões nos AppServices.</summary>
public static class CadastrosEndpoints
{
    public static IEndpointRouteBuilder MapCadastros(this IEndpointRouteBuilder app)
    {
        MapMunicipios(app);
        MapCamposPersonalizados(app);
        MapEtiquetas(app);
        return app;
    }

    private static void MapMunicipios(IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup(Rotas.Municipios.Grupo).WithTags("Municípios").RequireAuthorization();

        // ?uf=MG — a lista inteira da UF (até ~850 itens); o aplicativo filtra enquanto o usuário digita.
        grupo.MapGet(string.Empty, (string uf, IMunicipioAppService servico, CancellationToken ct) => servico.ListarDaUfAsync(uf, ct));

        grupo.MapGet("situacao", (IMunicipioAppService servico, CancellationToken ct) => servico.ObterSituacaoAsync(ct));

        grupo.MapPost("atualizar", (IMunicipioAppService servico, CancellationToken ct) => servico.AtualizarAsync(ct));
    }

    private static void MapEtiquetas(IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup(Rotas.Etiquetas.Grupo).WithTags("Etiquetas").RequireAuthorization();

        // Lista pequena (dezenas a centenas): vem inteira e o aplicativo filtra no aparelho.
        grupo.MapGet(string.Empty, (bool? incluirInativas, IEtiquetaAppService servico, CancellationToken ct) =>
            servico.ListarAsync(incluirInativas ?? false, ct));

        grupo.MapGet("{id:guid}", async (Guid id, IEtiquetaAppService servico, CancellationToken ct) =>
            await servico.ObterAsync(id, ct) is { } etiqueta
                ? Results.Ok(etiqueta)
                : Problemas.Resultado(Problemas.NaoEncontrado("Esta etiqueta não existe.")));

        // Inclui ou altera (o Id vem do aparelho).
        grupo.MapPut("{id:guid}", async (Guid id, EtiquetaDto etiqueta, IEtiquetaAppService servico, CancellationToken ct) =>
        {
            if (etiqueta.Id != Guid.Empty && etiqueta.Id != id)
                return Problemas.Resultado(Problemas.Validacao("O Id da URL não confere com o Id da etiqueta enviada."));
            etiqueta.Id = id;
            return Results.Ok(await servico.SalvarAsync(etiqueta, ct));
        });

        grupo.MapPost("{id:guid}/desativar",
            (Guid id, AlterarSituacaoRequisicao requisicao, IEtiquetaAppService servico, CancellationToken ct) =>
                servico.DesativarAsync(id, requisicao, ct));

        grupo.MapPost("{id:guid}/reativar",
            (Guid id, AlterarSituacaoRequisicao requisicao, IEtiquetaAppService servico, CancellationToken ct) =>
                servico.ReativarAsync(id, requisicao, ct));

        grupo.MapPost("{id:guid}/mesclar",
            (Guid id, MesclarEtiquetaRequisicao requisicao, IEtiquetaAppService servico, CancellationToken ct) =>
                servico.MesclarAsync(id, requisicao, ct));
    }

    private static void MapCamposPersonalizados(IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup(Rotas.CamposPersonalizados.Grupo).WithTags("Campos personalizados").RequireAuthorization();

        grupo.MapGet(string.Empty,
            (EntidadePersonalizavel? entidade, bool? incluirInativos, ICampoPersonalizadoAppService servico, CancellationToken ct) =>
                servico.ListarAsync(entidade ?? EntidadePersonalizavel.Pessoa, incluirInativos ?? false, ct));

        grupo.MapGet("{id:guid}", async (Guid id, ICampoPersonalizadoAppService servico, CancellationToken ct) =>
            await servico.ObterAsync(id, ct) is { } campo
                ? Results.Ok(campo)
                : Problemas.Resultado(Problemas.NaoEncontrado("Este campo não existe.")));

        // Inclui ou altera (o Id vem do aparelho).
        grupo.MapPut("{id:guid}", async (Guid id, CampoPersonalizadoDto campo, ICampoPersonalizadoAppService servico, CancellationToken ct) =>
        {
            if (campo.Id != Guid.Empty && campo.Id != id)
                return Problemas.Resultado(Problemas.Validacao("O Id da URL não confere com o Id do campo enviado."));
            campo.Id = id;
            return Results.Ok(await servico.SalvarAsync(campo, ct));
        });

        grupo.MapPost("{id:guid}/desativar",
            (Guid id, AlterarSituacaoRequisicao requisicao, ICampoPersonalizadoAppService servico, CancellationToken ct) =>
                servico.DesativarAsync(id, requisicao, ct));

        grupo.MapPost("{id:guid}/reativar",
            (Guid id, AlterarSituacaoRequisicao requisicao, ICampoPersonalizadoAppService servico, CancellationToken ct) =>
                servico.ReativarAsync(id, requisicao, ct));

        grupo.MapPut("ordem", async (EntidadePersonalizavel? entidade, List<Guid> ids, ICampoPersonalizadoAppService servico, CancellationToken ct) =>
        {
            await servico.ReordenarAsync(entidade ?? EntidadePersonalizavel.Pessoa, ids, ct);
            return Results.NoContent();
        });
    }
}
