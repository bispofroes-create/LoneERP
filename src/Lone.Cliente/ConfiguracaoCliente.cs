using Lone.Cliente.Api;
using Lone.Cliente.Navegacao;
using Lone.Cliente.Sessao;
using Lone.Cliente.ViewModels;
using Lone.Cliente.ViewModels.Cadastros;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Cliente.ViewModels.Seguranca;
using Microsoft.Extensions.DependencyInjection;

namespace Lone.Cliente;

public static class ConfiguracaoCliente
{
    /// <summary>
    /// Registra sessão, cliente da API, serviços e ViewModels. O aplicativo registra antes as implementações
    /// de plataforma (IArmazenamentoSeguro, IPreferencias, IDispositivo, INavegacao) e um HttpClient.
    /// </summary>
    public static IServiceCollection AddLoneCliente(this IServiceCollection services)
    {
        services.AddSingleton<SessaoCliente>();
        services.AddSingleton<ConfiguracaoServidor>();
        services.AddSingleton<ClienteApi>();
        services.AddSingleton<ServicoAutenticacao>();
        services.AddSingleton<PessoasApi>();
        services.AddSingleton<UsuariosApi>();
        services.AddSingleton<PerfisApi>();
        services.AddSingleton<ConsultasApi>();
        services.AddSingleton<MunicipiosApi>();
        services.AddSingleton<CamposPersonalizadosApi>();
        services.AddSingleton<EtiquetasApi>();
        services.AddSingleton<ProfissoesApi>();
        services.AddSingleton<PapeisApi>();
        services.AddSingleton<TiposMeioContatoApi>();
        services.AddSingleton<TiposEnderecoApi>();
        services.AddSingleton<TiposDocumentoApi>();
        services.AddSingleton<FluxoDeEntrada>();

        // Um ViewModel novo a cada abertura de tela.
        services.AddTransient<LoginViewModel>();
        services.AddTransient<PrimeiroAcessoViewModel>();
        services.AddTransient<EscolherEmpresaViewModel>();
        services.AddTransient<TrocarSenhaViewModel>();
        services.AddTransient<MenuViewModel>();
        services.AddTransient<InicioViewModel>();
        services.AddTransient<UsuariosViewModel>();
        services.AddTransient<PerfisViewModel>();
        services.AddTransient<PessoasViewModel>();
        services.AddTransient<CamposPersonalizadosViewModel>();
        services.AddTransient<EtiquetasViewModel>();
        services.AddTransient<ProfissoesViewModel>();
        services.AddTransient<PapeisViewModel>();
        services.AddTransient<TiposMeioContatoViewModel>();
        services.AddTransient<TiposEnderecoViewModel>();
        services.AddTransient<TiposDocumentoViewModel>();

        return services;
    }
}
