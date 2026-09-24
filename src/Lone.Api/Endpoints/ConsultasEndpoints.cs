using Lone.Api.Erros;
using Lone.Application.Infraestrutura;
using Lone.Application.Integracoes;
using Lone.Contracts.Comum;

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
