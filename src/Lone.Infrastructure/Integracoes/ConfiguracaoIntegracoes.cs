using Lone.Application.Integracoes;
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

        services.AddHttpClient<ICepConsulta, ViaCepConsulta>(c =>
        {
            c.BaseAddress = new Uri("https://viacep.com.br/ws/");
            c.Timeout = TimeSpan.FromSeconds(10);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("Lone-ERP/1.0");
        });

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
}
