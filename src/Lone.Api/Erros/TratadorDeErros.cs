using System.Diagnostics;
using Lone.Contracts.Comum;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Lone.Api.Erros;

/// <summary>
/// Transforma exceções em respostas HTTP. Erros de regra viram 400/403/409...; erros inesperados viram 500
/// com um código de rastreio (o detalhe técnico vai só para o log, nunca para o aplicativo).
/// </summary>
public sealed class TratadorDeErros : IExceptionHandler
{
    private readonly IProblemDetailsService _problemas;
    private readonly ILogger<TratadorDeErros> _log;

    public TratadorDeErros(IProblemDetailsService problemas, ILogger<TratadorDeErros> log)
    {
        _problemas = problemas;
        _log = log;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext contexto, Exception erro, CancellationToken ct)
    {
        // Cliente desistiu da requisição: nada a responder.
        if (erro is OperationCanceledException && contexto.RequestAborted.IsCancellationRequested)
            return true;

        var problema = Problemas.De(erro);
        if (problema is null)
        {
            var rastreio = Activity.Current?.Id ?? contexto.TraceIdentifier;
            _log.LogError(erro, "Erro inesperado em {Metodo} {Caminho} (rastreio {Rastreio})",
                contexto.Request.Method, contexto.Request.Path, rastreio);

            problema = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Erro interno",
                Detail = $"Ocorreu um erro inesperado no servidor. Informe ao suporte o código {rastreio}."
            };
            problema.Extensions[ErrosApi.CampoCodigo] = "erro_interno";
        }

        contexto.Response.StatusCode = problema.Status ?? StatusCodes.Status500InternalServerError;
        return await _problemas.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = contexto,
            ProblemDetails = problema,
            Exception = erro
        });
    }
}
