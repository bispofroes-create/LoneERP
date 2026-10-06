using Lone.Api.Erros;
using Lone.Application.Infraestrutura;
using Lone.Application.Integracoes;
using Lone.Contracts.Comum;
using Lone.Contracts.Integracoes;

namespace Lone.Api.Endpoints;

/// <summary>Consultas externas (CEP, CNPJ) e a verificação de saúde da API.</summary>
public static class ConsultasEndpoints
{
    public static IEndpointRouteBuilder MapConsultas(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup(Rotas.Consultas.Grupo).WithTags("Consultas externas").RequireAuthorization();

        grupo.MapGet("cep/{cep}", async (string cep, IConsultasAppService servico, CancellationToken ct) =>
            await servico.ConsultarCepAsync(cep, ct) is { } dados
                ? Results.Ok(dados)
                : Problemas.Resultado(Problemas.NaoEncontrado("CEP não encontrado.")));

        // Conferência do CEP pelo motor (F2): só consulta e decide; nunca grava o endereço. Pedido inválido = 400; fonte
        // fora do ar = 200 com Resultado "FonteIndisponivel" (resultado, não erro).
        grupo.MapPost("cep/conferir", async (ConferirCepRequisicao requisicao, IConsultasAppService servico, CancellationToken ct) =>
            Results.Ok(await servico.ConferirCepAsync(requisicao, ct)));

        // Busca de CEP pelo endereço sem CEP (Checkpoint D): candidatos para o usuário escolher; nunca escolhe nem grava.
        // Dados insuficientes = 400; fonte fora do ar = 200 com Resultado "FonteIndisponivel".
        grupo.MapPost("cep/buscar-por-endereco",
            async (BuscarCepPorEnderecoRequisicao requisicao, IConsultasAppService servico, CancellationToken ct) =>
                Results.Ok(await servico.BuscarCepPorEnderecoAsync(requisicao, ct)));

        // Reconferência de CEPs em lote (F6): o aplicativo seleciona e manda blocos pequenos; cada bloco é processado no
        // ritmo da política (sem worker). Nunca altera endereço: só a situação, a fonte e a data da conferência.
        grupo.MapPost("cep/reconferencia/selecionar",
            async (FiltroReconferenciaCepDto filtro, Lone.Application.Integracoes.ConferenciaCep.IReconferenciaCepAppService servico, CancellationToken ct) =>
                Results.Ok(await servico.SelecionarAsync(filtro, ct)));
        grupo.MapPost("cep/reconferencia/processar",
            async (ProcessarReconferenciaCepRequisicao requisicao, Lone.Application.Integracoes.ConferenciaCep.IReconferenciaCepAppService servico,
                   CancellationToken ct) => Results.Ok(await servico.ProcessarAsync(requisicao, ct)));
        // Fim de uma execução da reconferência: um evento de auditoria por execução (sem lista de pessoas ou endereços).
        grupo.MapPost("cep/reconferencia/concluir",
            async (ConcluirReconferenciaCepRequisicao requisicao, Lone.Application.Integracoes.ConferenciaCep.IReconferenciaCepAppService servico,
                   CancellationToken ct) =>
            {
                await servico.ConcluirAsync(requisicao, ct);
                return Results.NoContent();
            });

        // Segunda opinião (Checkpoint G): só por pedido do usuário; compara duas fontes; nunca escolhe nem grava.
        grupo.MapPost("cep/segunda-opiniao", async (SegundaOpiniaoCepRequisicao requisicao, IConsultasAppService servico, CancellationToken ct) =>
            Results.Ok(await servico.ConsultarOutraFonteAsync(requisicao, ct)));

        grupo.MapPost("cep/historico/limpar",
            async (Lone.Application.Integracoes.ConferenciaCep.IReconferenciaCepAppService servico, CancellationToken ct) =>
                Results.Ok(await servico.LimparHistoricoAsync(ct)));

        grupo.MapGet("cnpj/{cnpj}", async (string cnpj, IConsultasAppService servico, CancellationToken ct) =>
            await servico.ConsultarCnpjAsync(cnpj, ct) is { } dados
                ? Results.Ok(dados)
                : Problemas.Resultado(Problemas.NaoEncontrado("CNPJ não encontrado.")));

        // Monitoramento: 200 com a API e o banco no ar; 503 se o banco não responde.
        app.MapGet(Rotas.Saude, async (IBancoDeDados banco, CancellationToken ct) =>
                await banco.DisponivelAsync(ct)
                    ? Results.Ok(new { situacao = "ok", banco = "ok" })
                    : Results.Json(new { situacao = "degradada", banco = "indisponível" }, statusCode: StatusCodes.Status503ServiceUnavailable))
            .WithTags("Saúde")
            .AllowAnonymous();

        return app;
    }
}
