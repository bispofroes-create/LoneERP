using Lone.Application.Documentos;
using Lone.Application.Seguranca;
using Lone.Infrastructure.Arquivos;
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

        // Anexos de documentos: pasta no servidor (seção "Anexos": Pasta, TamanhoMaximoMb).
        var anexos = configuracao.GetSection(OpcoesAnexos.Secao).Get<OpcoesAnexos>() ?? new OpcoesAnexos();
        if (anexos.TamanhoMaximoMb <= 0) anexos.TamanhoMaximoMb = Lone.Domain.Documentos.RegrasAnexo.TamanhoMaximoMbPadrao;
        services.AddSingleton(anexos);
        services.AddSingleton<IArmazenamentoAnexos, ArmazenamentoAnexosEmPasta>();

        services.AddOptions<OpcoesJwt>()
            .Bind(configuracao.GetSection(OpcoesJwt.Secao))
            .Validate(o => o.Chave.Length >= OpcoesJwt.TamanhoMinimoChave,
                $"A chave JWT precisa ter pelo menos {OpcoesJwt.TamanhoMinimoChave} caracteres.")
            .ValidateOnStart();
        services.AddSingleton<IEmissorToken, EmissorTokenJwt>();

        return services;
    }
}
