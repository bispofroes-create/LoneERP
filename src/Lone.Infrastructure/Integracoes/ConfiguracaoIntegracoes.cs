using Lone.Application.Integracoes;
using Lone.Application.Integracoes.ConferenciaCep;
using Lone.Application.Municipios;
using Lone.Infrastructure.Integracoes.Ibge;
using Lone.Infrastructure.Integracoes.Cep;
using Lone.Infrastructure.Integracoes.Cnpj;
using Microsoft.Extensions.DependencyInjection;

namespace Lone.Infrastructure.Integracoes;

public static class ConfiguracaoIntegracoes
{
    /// <summary>Registra as consultas externas. Para trocar de provedor, troque a classe aqui.</summary>
    internal static IServiceCollection AddLoneIntegracoes(this IServiceCollection services)
    {
        services.AddHttpClient<ICnpjConsulta, BrasilApiCnpjConsulta>(c =>
        {
            c.BaseAddress = new Uri("https://brasilapi.com.br/api/");
            c.Timeout = TimeSpan.FromSeconds(20);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("Lone-ERP/1.0");
        });

        // Fonte complementar das inscrições estaduais: resposta rápida ou nada (não atrasa a consulta de CNPJ).
        services.AddHttpClient<IInscricaoEstadualConsulta, CnpjWsInscricaoConsulta>(c =>
        {
            c.BaseAddress = new Uri("https://publica.cnpj.ws/");
            c.Timeout = TimeSpan.FromSeconds(8);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("Lone-ERP/1.0");
        });

        services.AddLoneProvedoresCep(OpcoesResilienciaCep.Padrao);

        // Lista oficial de municípios (cerca de 1 MB; lida na primeira inicialização e quando o administrador pede).
        services.AddHttpClient<IMunicipiosOficiais, IbgeMunicipiosOficiais>(c =>
        {
            c.BaseAddress = new Uri("https://servicodados.ibge.gov.br/");
            c.Timeout = TimeSpan.FromSeconds(60);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("Lone-ERP/1.0");
        });

        // Subclasses CNAE (cerca de 400 KB; lidas na primeira inicialização e quando o administrador pede).
        services.AddHttpClient<Lone.Application.Fiscal.ICnaesOficiais, IbgeCnaesOficiais>(c =>
        {
            c.BaseAddress = new Uri("https://servicodados.ibge.gov.br/");
            c.Timeout = TimeSpan.FromSeconds(60);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("Lone-ERP/1.0");
        });

        return services;
    }

    /// <summary>
    /// Fontes de CEP do motor (F2, DM2): um cliente HTTP por fonte, cada um com o seu pipeline de resiliência (tempo por
    /// tentativa, nova tentativa e disjuntor independentes). A ordem de registro é a ordem da cadeia: ViaCEP, depois a
    /// BrasilAPI (reserva). O ViaCEP também atende o <see cref="ICepConsulta"/> antigo: um caminho só até o ViaCEP.
    /// <paramref name="ajustar"/>: só para testes (troca o handler HTTP primário).
    /// </summary>
    internal static IServiceCollection AddLoneProvedoresCep(this IServiceCollection services, OpcoesResilienciaCep opcoes,
                                                            Action<IHttpClientBuilder>? ajustar = null)
    {
        // O tempo é governado pelo pipeline (TempoTotalPorFonte); o HttpClient não corta antes (sem timeouts concorrentes).
        var viaCep = services.AddHttpClient<ViaCepProvedor>(c =>
        {
            c.BaseAddress = new Uri("https://viacep.com.br/ws/");
            c.Timeout = Timeout.InfiniteTimeSpan;
            c.DefaultRequestHeaders.UserAgent.ParseAdd("Lone-ERP/1.0");
        });
        viaCep.AddResilienceHandler("cep-viacep", p => ResilienciaCep.Configurar(p, opcoes));
        ajustar?.Invoke(viaCep);

        var brasilApi = services.AddHttpClient<BrasilApiCepProvedor>(c =>
        {
            c.BaseAddress = new Uri("https://brasilapi.com.br/api/");
            c.Timeout = Timeout.InfiniteTimeSpan;
            c.DefaultRequestHeaders.UserAgent.ParseAdd("Lone-ERP/1.0");
        });
        brasilApi.AddResilienceHandler("cep-brasilapi", p => ResilienciaCep.Configurar(p, opcoes));
        ajustar?.Invoke(brasilApi);

        services.AddTransient<ICepConsulta>(sp => sp.GetRequiredService<ViaCepProvedor>());
        services.AddTransient<IProvedorCep>(sp => sp.GetRequiredService<ViaCepProvedor>());
        services.AddTransient<IProvedorCep>(sp => sp.GetRequiredService<BrasilApiCepProvedor>());
        return services;
    }
}
