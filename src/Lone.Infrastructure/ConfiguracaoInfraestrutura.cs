using Lone.Application.Seguranca;
using Lone.Infrastructure.Integracoes;
using Lone.Infrastructure.Persistencia;
using Lone.Infrastructure.Seguranca;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lone.Infrastructure;

public static class ConfiguracaoInfraestrutura
{
    /// <summary>
    /// Registra banco (SQL Server), repositórios, consultas externas e emissão de tokens.
    /// Lê "ConnectionStrings:Lone" e a seção "Jwt" da configuração da API.
    /// </summary>
    public static IServiceCollection AddLoneInfrastructure(this IServiceCollection services, IConfiguration configuracao)
    {
        var conexao = configuracao.GetConnectionString("Lone")
            ?? throw new InvalidOperationException("ConnectionStrings:Lone não encontrada na configuração da API.");

        services.AddLoneDados(conexao);
        services.AddLoneIntegracoes();

        services.AddOptions<OpcoesJwt>()
            .Bind(configuracao.GetSection(OpcoesJwt.Secao))
            .Validate(o => o.Chave.Length >= OpcoesJwt.TamanhoMinimoChave,
                $"A chave JWT precisa ter pelo menos {OpcoesJwt.TamanhoMinimoChave} caracteres.")
            .ValidateOnStart();
        services.AddSingleton<IEmissorToken, EmissorTokenJwt>();

        return services;
    }
}
