using Lone.Application.Municipios;

namespace Lone.Api.Servicos;

/// <summary>
/// Em segundo plano, ao iniciar a API: carrega a tabela de municípios do IBGE se ela estiver vazia e concilia os
/// textos antigos de município. Não atrasa nem derruba a inicialização: sem internet, tenta de novo a cada
/// 5 minutos (os cadastros continuam abrindo; só a escolha de município fica indisponível até a carga).
/// </summary>
public sealed class CargaMunicipios : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _escopos;
    private readonly ILogger<CargaMunicipios> _log;

    public CargaMunicipios(IServiceScopeFactory escopos, ILogger<CargaMunicipios> log)
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
                var servico = escopo.ServiceProvider.GetRequiredService<ServicoMunicipios>();

                if (await servico.PrecisaCarregarAsync(ct))
                {
                    var resultado = await servico.AtualizarAsync(ct);
                    _log.LogInformation(
                        "Tabela de municípios carregada do IBGE: {Incluidos} municípios. Textos antigos conciliados: {Resolvidas}; a corrigir: {Abertas}.",
                        resultado.Incluidos, resultado.PendenciasResolvidas, resultado.PendenciasAbertas);
                }
                else
                {
                    var (resolvidas, abertas) = await servico.ConciliarAsync(ct);
                    if (resolvidas + abertas > 0)
                        _log.LogInformation("Municípios: {Resolvidas} textos antigos conciliados; {Abertas} a corrigir no cadastro.", resolvidas, abertas);
                }
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Não foi possível carregar a tabela de municípios agora. Nova tentativa em {Minutos} minutos.",
                    Intervalo.TotalMinutes);
            }

            try { await Task.Delay(Intervalo, ct); }
            catch (OperationCanceledException) { return; }
        }
    }
}
