using Lone.Aplicacao.Pessoas;
using Lone.Aplicacao.Seguranca;
using Microsoft.Extensions.DependencyInjection;

namespace Lone.Aplicacao;

public static class ConfiguracaoAplicacao
{
    /// <summary>Registra os serviços de aplicação. Repositórios e integrações são registrados pelos seus projetos.</summary>
    public static IServiceCollection AddLoneAplicacao(this IServiceCollection services)
    {
        // Sessão única por execução: é o usuário atual e também quem responde às permissões.
        services.AddSingleton<SessaoUsuario>();
        services.AddSingleton<ISessao>(sp => sp.GetRequiredService<SessaoUsuario>());
        services.AddSingleton<IUsuarioAtual>(sp => sp.GetRequiredService<SessaoUsuario>());
        services.AddSingleton<IEmpresaAtual>(sp => sp.GetRequiredService<SessaoUsuario>());
        services.AddSingleton<IAutorizacao>(sp => sp.GetRequiredService<SessaoUsuario>());

        services.AddSingleton<IHasherSenha, HasherSenhaPbkdf2>();
        services.AddSingleton<IAutenticador, AutenticadorLocal>();
        services.AddSingleton<IAutenticacaoService, AutenticacaoService>();
        services.AddSingleton<IUsuarioAppService, UsuarioAppService>();
        services.AddSingleton<IPerfilAppService, PerfilAppService>();

        services.AddSingleton<IPessoaAppService, PessoaAppService>();
        return services;
    }
}
