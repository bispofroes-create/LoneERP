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
        MapOperacoes(app);

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

        // Fase 2b-1b: a aba "Regras e clientes" da ficha.
        grupo.MapGet("{id:guid}/motor", (Guid id, IConsultaTerritorialAppService servico, CancellationToken ct) => servico.DoTerritorioAsync(id, ct));

        return app;
    }

    /// <summary>
    /// Operações territoriais TE- (Fase 2b-1b). Permissões e alcance nos AppServices; conflitos (versão, mapa mudou,
    /// assinatura diferente) saem como 409 pelo tratamento geral de erros.
    /// </summary>
    private static void MapOperacoes(IEndpointRouteBuilder app)
    {
        // "Territórios do cliente na data" (contrato dos documentos futuros, seção L).
        app.MapGet(Rotas.Territorios.DoClienteBase + "/{pessoaId:guid}",
                (Guid pessoaId, DateOnly data, IConsultaTerritorialAppService servico, CancellationToken ct) => servico.DoClienteAsync(pessoaId, data, ct))
            .WithTags("Operações territoriais").RequireAuthorization();

        var parametros = app.MapGroup(Rotas.Territorios.Parametros).WithTags("Operações territoriais").RequireAuthorization();
        parametros.MapGet(string.Empty, (IParametrosTerritoriaisAppService servico, CancellationToken ct) => servico.ObterAsync(ct));
        parametros.MapPut(string.Empty, (ParametrosTerritoriaisDto dto, IParametrosTerritoriaisAppService servico, CancellationToken ct) =>
            servico.SalvarAsync(dto, ct));

        var grupo = app.MapGroup(Rotas.Territorios.Operacoes).WithTags("Operações territoriais").RequireAuthorization();

        grupo.MapGet(string.Empty, (Guid? mapaId, IOperacaoTerritorialAppService servico, CancellationToken ct) => servico.ListarAsync(mapaId, ct));
        grupo.MapGet("opcoes", (IOperacaoTerritorialAppService servico, CancellationToken ct) => servico.OpcoesAsync(ct));
        grupo.MapGet("{id:guid}", async (Guid id, IOperacaoTerritorialAppService servico, CancellationToken ct) =>
            await servico.ObterAsync(id, ct) is { } item
                ? Results.Ok(item)
                : Problemas.Resultado(Problemas.NaoEncontrado("Esta operação territorial não existe.")));
        grupo.MapPost(string.Empty, (CriarOperacaoTerritorialRequisicao requisicao, IOperacaoTerritorialAppService servico, CancellationToken ct) =>
            servico.CriarAsync(requisicao, ct));
        grupo.MapPut("{id:guid}", (Guid id, AlterarOperacaoTerritorialRequisicao requisicao, IOperacaoTerritorialAppService servico, CancellationToken ct) =>
            servico.AlterarAsync(id, requisicao, ct));
        grupo.MapPost("{id:guid}/mudancas", (Guid id, IncluirMudancaTerritorialRequisicao requisicao, IOperacaoTerritorialAppService servico, CancellationToken ct) =>
            servico.IncluirMudancaAsync(id, requisicao, ct));
        grupo.MapPost("{id:guid}/mudancas/{mudancaId:guid}/retirar",
            (Guid id, Guid mudancaId, VersaoOperacaoTerritorialRequisicao requisicao, IOperacaoTerritorialAppService servico, CancellationToken ct) =>
                servico.RetirarMudancaAsync(id, mudancaId, requisicao, ct));
        grupo.MapPost("{id:guid}/mover-cliente", (Guid id, MoverClienteTerritorialRequisicao requisicao, IOperacaoTerritorialAppService servico, CancellationToken ct) =>
            servico.MoverClienteAsync(id, requisicao, ct));
        grupo.MapPost("{id:guid}/simular", (Guid id, VersaoOperacaoTerritorialRequisicao requisicao, IOperacaoTerritorialAppService servico, CancellationToken ct) =>
            servico.SimularAsync(id, requisicao, ct));
        grupo.MapGet("{id:guid}/simulacoes", (Guid id, IOperacaoTerritorialAppService servico, CancellationToken ct) => servico.SimulacoesAsync(id, ct));
        grupo.MapPost("simulacoes/{simulacaoId:guid}/itens",
            (Guid simulacaoId, FiltroItensOperacaoTerritorialDto filtro, IOperacaoTerritorialAppService servico, CancellationToken ct) =>
                servico.ItensSimulacaoAsync(simulacaoId, filtro, ct));
        grupo.MapPost("divergencias/{mapaId:guid}", (Guid mapaId, FiltroItensOperacaoTerritorialDto filtro, IOperacaoTerritorialAppService servico, CancellationToken ct) =>
            servico.DivergenciasAsync(mapaId, filtro, ct));
        grupo.MapPost("{id:guid}/itens", (Guid id, FiltroItensOperacaoTerritorialDto filtro, IOperacaoTerritorialAppService servico, CancellationToken ct) =>
            servico.ItensAplicadosAsync(id, filtro, ct));
        grupo.MapPost("{id:guid}/aplicar", (Guid id, AplicarOperacaoTerritorialRequisicao requisicao, IOperacaoTerritorialAppService servico, CancellationToken ct) =>
            servico.AplicarAsync(id, requisicao, ct));
        grupo.MapPost("{id:guid}/cancelar", (Guid id, MotivoOperacaoTerritorialRequisicao requisicao, IOperacaoTerritorialAppService servico, CancellationToken ct) =>
            servico.CancelarAsync(id, requisicao, ct));
        grupo.MapPost("{id:guid}/desfazer", (Guid id, MotivoOperacaoTerritorialRequisicao requisicao, IOperacaoTerritorialAppService servico, CancellationToken ct) =>
            servico.DesfazerAsync(id, requisicao, ct));
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
