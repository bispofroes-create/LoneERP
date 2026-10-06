using System.Net;
using System.Text.Json;
using Lone.Application.Integracoes.ConferenciaCep;
using Lone.Domain.Enderecos.ConferenciaCep;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Lone.Infrastructure.Integracoes.Cep;

/// <summary>
/// A chamada HTTP de uma fonte de CEP, já traduzida: JSON lido, ou a falha técnica em texto (para o log). Cancelamento de
/// quem chamou sobe como cancelamento; tempo esgotado, disjuntor aberto, sem conexão e status técnico viram falha.
/// </summary>
internal static class ChamadaCep
{
    internal sealed record Resposta(HttpStatusCode Status, JsonDocument? Json, string? Falha, bool Invalida)
    {
        public bool Tecnica => Falha is not null;
    }

    public static async Task<Resposta> GetAsync(HttpClient http, string rota, CancellationToken ct)
    {
        HttpResponseMessage resposta;
        try
        {
            resposta = await http.GetAsync(rota, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutRejectedException)
        {
            return Falhou("tempo esgotado");
        }
        catch (BrokenCircuitException)
        {
            return Falhou("disjuntor aberto (a fonte falhou há pouco)");
        }
        catch (ExecutionRejectedException ex)
        {
            return Falhou("chamada recusada: " + ex.GetType().Name);
        }
        catch (HttpRequestException ex)
        {
            return Falhou("sem conexão: " + ex.Message);
        }
        catch (OperationCanceledException)
        {
            return Falhou("tempo esgotado (cliente HTTP)");
        }

        using (resposta)
        {
            var status = resposta.StatusCode;
            if (status == HttpStatusCode.RequestTimeout || status == HttpStatusCode.TooManyRequests || (int)status >= 500)
                return Falhou($"HTTP {(int)status}", status);
            if (!resposta.IsSuccessStatusCode)
                return new Resposta(status, null, null, Invalida: false); // 4xx funcional: quem chama interpreta

            try
            {
                await using var fluxo = await resposta.Content.ReadAsStreamAsync(ct);
                return new Resposta(status, await JsonDocument.ParseAsync(fluxo, cancellationToken: ct), null, Invalida: false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (JsonException ex)
            {
                return new Resposta(status, null, "JSON inválido: " + ex.Message, Invalida: true);
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or OperationCanceledException)
            {
                return Falhou("resposta interrompida: " + ex.Message, status);
            }
        }
    }

    private static Resposta Falhou(string falha, HttpStatusCode status = 0) => new(status, null, falha, Invalida: false);

    /// <summary>Falha técnica da resposta como resultado da consulta (e no log).</summary>
    public static ResultadoProvedorCep FalhaConsulta(ILogger log, CepFonte fonte, Resposta r, string cep)
    {
        log.LogWarning("Consulta do CEP {Cep} na fonte {Fonte} falhou: {Falha}", cep, fonte, r.Falha);
        return r.Invalida ? ResultadoProvedorCep.Invalida(fonte, r.Falha!) : ResultadoProvedorCep.Indisponivel(fonte, r.Falha!);
    }

    public static ResultadoProvedorCep InvalidaConsulta(ILogger log, CepFonte fonte, string cep, string motivo)
    {
        log.LogWarning("Consulta do CEP {Cep} na fonte {Fonte}: resposta fora do contrato ({Motivo})", cep, fonte, motivo);
        return ResultadoProvedorCep.Invalida(fonte, motivo);
    }
}
