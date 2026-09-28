using System.Threading.RateLimiting;
using Lone.Api.Erros;
using Lone.Api.Seguranca;
using Lone.Application.Seguranca;
using Lone.Contracts.Comum;
using Lone.Infrastructure.Seguranca;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Lone.Api;

/// <summary>Registro dos serviços próprios da API: autenticação, usuário da requisição, erros, JSON e limites.</summary>
public static class ConfiguracaoApi
{
    /// <summary>Limite de tentativas nos endpoints de login (por endereço IP).</summary>
    public const string PoliticaLogin = "login";

    /// <summary>Chaves JWT de exemplo começam assim; fora do ambiente de desenvolvimento a API se recusa a iniciar com elas.</summary>
    public const string PrefixoChaveDesenvolvimento = "DESENVOLVIMENTO-";

    public static IServiceCollection AddLoneApi(this IServiceCollection services, IConfiguration configuracao, IHostEnvironment ambiente)
    {
        AdicionarAutenticacao(services, configuracao, ambiente);

        // O usuário da requisição responde às três perguntas que os casos de uso fazem.
        services.AddScoped<UsuarioDaRequisicao>();
        services.AddScoped<IUsuarioAtual>(sp => sp.GetRequiredService<UsuarioDaRequisicao>());
        services.AddScoped<IMotivoDaOperacao>(sp => sp.GetRequiredService<UsuarioDaRequisicao>());
        services.AddScoped<IEmpresaAtual>(sp => sp.GetRequiredService<UsuarioDaRequisicao>());
        services.AddScoped<IAutorizacao>(sp => sp.GetRequiredService<UsuarioDaRequisicao>());
        services.AddScoped<IAlcanceDoUsuario>(sp => sp.GetRequiredService<UsuarioDaRequisicao>());
        services.AddMemoryCache();
        services.AddSingleton<CacheAcesso>();

        services.AddProblemDetails(o => o.CustomizeProblemDetails = c =>
            c.ProblemDetails.Extensions.TryAdd("rastreio", c.HttpContext.TraceIdentifier));
        services.AddExceptionHandler<TratadorDeErros>();

        services.ConfigureHttpJsonOptions(o => OpcoesJson.Configurar(o.SerializerOptions));

        services.AddRateLimiter(o =>
        {
            o.AddPolicy(PoliticaLogin, contexto => RateLimitPartition.GetFixedWindowLimiter(
                contexto.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            o.OnRejected = (contexto, _) =>
            {
                contexto.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                return new ValueTask(Problemas.Escrever(contexto.HttpContext, Problemas.MuitasTentativas()));
            };
        });

        // Carga da tabela de municípios (IBGE) e conciliação dos textos antigos, sem atrasar a inicialização.
        if (configuracao.GetValue("Municipios:CarregarAoIniciar", true))
            services.AddHostedService<Servicos.CargaMunicipios>();

        // Carga da tabela CNAE (IBGE), também sem atrasar a inicialização.
        if (configuracao.GetValue("Cnaes:CarregarAoIniciar", true))
            services.AddHostedService<Servicos.CargaCnaes>();

        services.AddOpenApi();
        return services;
    }

    private static void AdicionarAutenticacao(IServiceCollection services, IConfiguration configuracao, IHostEnvironment ambiente)
    {
        var jwt = configuracao.GetSection(OpcoesJwt.Secao).Get<OpcoesJwt>() ?? new OpcoesJwt();
        jwt.Validar();
        if (!ambiente.IsDevelopment() && jwt.Chave.StartsWith(PrefixoChaveDesenvolvimento, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "A chave JWT de desenvolvimento não pode ser usada fora do ambiente de desenvolvimento. " +
                "Defina uma chave própria na variável de ambiente Jwt__Chave.");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.MapInboundClaims = false; // mantém os nomes do token ("sub", "empresa"...)
                o.TokenValidationParameters = jwt.ParametrosValidacao();
                o.Events = new JwtBearerEvents
                {
                    // Sem token ou token inválido/vencido: resposta no mesmo formato dos outros erros.
                    OnChallenge = async contexto =>
                    {
                        contexto.HandleResponse();
                        var mensagem = contexto.AuthenticateFailure is null
                            ? "Entre no sistema para continuar."
                            : "Sua sessão expirou. Entre novamente.";
                        await Problemas.Escrever(contexto.HttpContext, Problemas.NaoAutenticado(mensagem));
                    }
                };
            });

        services.AddAuthorization();
    }
}
