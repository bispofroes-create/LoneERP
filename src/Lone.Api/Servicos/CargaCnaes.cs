using Lone.Application.Fiscal;

namespace Lone.Api.Servicos;

/// <summary>
/// Em segundo plano, ao iniciar a API: carrega a tabela CNAE do IBGE se ela estiver vazia. Não atrasa nem derruba a
/// inicialização: sem internet, tenta de novo a cada 5 minutos (os cadastros continuam; só a descrição do CNAE falta).
/// </summary>
public sealed class CargaCnaes : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _escopos;
    private readonly ILogger<CargaCnaes> _log;

    public CargaCnaes(IServiceScopeFactory escopos, ILogger<CargaCnaes> log)
    {
        _escopos = escopos;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var escopo = _escopos.CreateAsyncScope();
                var servico = escopo.ServiceProvider.GetRequiredService<ServicoCnaes>();

                if (await servico.PrecisaCarregarAsync(ct))
                {
                    var resultado = await servico.AtualizarAsync(ct);
                    _log.LogInformation("Tabela CNAE carregada do IBGE: {Incluidos} subclasses.", resultado.Incluidos);
                }
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Não foi possível carregar a tabela CNAE agora. Nova tentativa em {Minutos} minutos.",
                    Intervalo.TotalMinutes);
            }

            try { await Task.Delay(Intervalo, ct); }
            catch (OperationCanceledException) { return; }
        }
    }
}
