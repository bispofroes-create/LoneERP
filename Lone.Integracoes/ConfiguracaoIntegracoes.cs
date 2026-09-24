using Lone.Aplicacao.Integracoes;
using Lone.Integracoes.Cep;
using Lone.Integracoes.Cnpj;
using Microsoft.Extensions.DependencyInjection;

namespace Lone.Integracoes;

public static class ConfiguracaoIntegracoes
{
    /// <summary>Registra as consultas externas. Para trocar de provedor, troque a classe aqui.</summary>
    public static IServiceCollection AddLoneIntegracoes(this IServiceCollection services)
    {
        services.AddHttpClient<ICnpjConsulta, BrasilApiCnpjConsulta>(c =>
        {
            c.BaseAddress = new Uri("https://brasilapi.com.br/api/");
            c.Timeout = TimeSpan.FromSeconds(20);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("Lone-ERP/1.0");
        });

        services.AddHttpClient<ICepConsulta, ViaCepConsulta>(c =>
        {
            c.BaseAddress = new Uri("https://viacep.com.br/ws/");
            c.Timeout = TimeSpan.FromSeconds(10);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("Lone-ERP/1.0");
        });

        return services;
    }
}
