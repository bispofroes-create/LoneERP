using System.Net;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.Timeout;

namespace Lone.Infrastructure.Integracoes.Cep;

/// <summary>
/// Parâmetros do pipeline de resiliência de cada fonte de CEP (DM2). Os do plano: 5 s por tentativa e 1 nova tentativa.
/// Os demais foram fixados na F2 de forma conservadora, para baixo volume (relatório da F2, §7).
/// </summary>
public sealed record OpcoesResilienciaCep
{
    /// <summary>Tempo de cada tentativa (plano §8: "timeout curto (5 s)").</summary>
    public TimeSpan TempoPorTentativa { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Novas tentativas depois da primeira, só em falha técnica transitória (plano: "uma nova tentativa").</summary>
    public int NovasTentativas { get; init; } = 1;

    public TimeSpan EsperaAntesDaNovaTentativa { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Teto de uma chamada a uma fonte, com a nova tentativa: 5 s + 0,5 s + 5 s cabe em 12 s.</summary>
    public TimeSpan TempoTotalPorFonte { get; init; } = TimeSpan.FromSeconds(12);

    /// <summary>Disjuntor: abre com metade ou mais de falhas técnicas...</summary>
    public double ProporcaoFalhas { get; init; } = 0.5;

    /// <summary>...em pelo menos 4 tentativas (2 conferências que falharam com a nova tentativa)...</summary>
    public int MinimoChamadas { get; init; } = 4;

    /// <summary>...dentro de 1 minuto...</summary>
    public TimeSpan JanelaAmostragem { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>...e fica aberto 2 minutos (plano: "para de chamar por alguns minutos").</summary>
    public TimeSpan DuracaoAbertura { get; init; } = TimeSpan.FromMinutes(2);

    public static OpcoesResilienciaCep Padrao { get; } = new();
}

/// <summary>
/// Pipeline de cada fonte (Microsoft.Extensions.Http.Resilience, sobre o IHttpClientFactory): teto da chamada → nova
/// tentativa → disjuntor → tempo por tentativa. Só trata falha <b>técnica</b>; resposta funcional (200, inclusive
/// "CEP não existe"; 400; 404) nunca é repetida nem conta para o disjuntor. Sem retry manual em outro lugar.
/// </summary>
public static class ResilienciaCep
{
    public static void Configurar(ResiliencePipelineBuilder<HttpResponseMessage> pipeline, OpcoesResilienciaCep opcoes)
    {
        pipeline
            .AddTimeout(opcoes.TempoTotalPorFonte)
            .AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = opcoes.NovasTentativas,
                Delay = opcoes.EsperaAntesDaNovaTentativa,
                BackoffType = DelayBackoffType.Constant,
                UseJitter = false,
                ShouldHandle = a => ValueTask.FromResult(DeveRepetir(a.Outcome))
            })
            .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                FailureRatio = opcoes.ProporcaoFalhas,
                MinimumThroughput = opcoes.MinimoChamadas,
                SamplingDuration = opcoes.JanelaAmostragem,
                BreakDuration = opcoes.DuracaoAbertura,
                ShouldHandle = a => ValueTask.FromResult(ContaParaDisjuntor(a.Outcome))
            })
            .AddTimeout(opcoes.TempoPorTentativa);
    }

    /// <summary>
    /// Nova tentativa só em falha técnica transitória: sem conexão, tempo da tentativa esgotado, HTTP 408 ou 5xx. Nunca:
    /// resposta 2xx (inclusive "CEP não existe"), 400/404 e demais 4xx, 429 (limite de taxa: repetir agrava) e
    /// cancelamento de quem chamou.
    /// </summary>
    public static bool DeveRepetir(Outcome<HttpResponseMessage> resultado) => resultado.Exception switch
    {
        HttpRequestException or TimeoutRejectedException => true,
        null => resultado.Result is { } r && (r.StatusCode == HttpStatusCode.RequestTimeout || (int)r.StatusCode >= 500),
        _ => false
    };

    /// <summary>Conta para abrir o disjuntor: as falhas que se repetem e também o 429 (a fonte pediu para parar).</summary>
    public static bool ContaParaDisjuntor(Outcome<HttpResponseMessage> resultado) =>
        DeveRepetir(resultado) || resultado.Result?.StatusCode == HttpStatusCode.TooManyRequests;
}
