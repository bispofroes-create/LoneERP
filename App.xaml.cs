using System;
using Lone.Aplicacao;
using Lone.Data;
using Lone.Integracoes;
using Lone.Servicos;
using Lone.ViewModels;
using Lone.ViewModels.Pessoas;
using Lone.ViewModels.Seguranca;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace Lone
{
    public partial class App : Application
    {
        private Window? _window;

        /// <summary>Contêiner de injeção de dependência do aplicativo.</summary>
        public static IServiceProvider Services { get; private set; } = null!;

        public App()
        {
            InitializeComponent();
            Services = ConfigurarServicos();
        }

        private static IServiceProvider ConfigurarServicos()
        {
            var configuracao = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .Build();

            var conexao = configuracao.GetConnectionString("Lone")
                ?? throw new InvalidOperationException("ConnectionStrings:Lone não encontrada no appsettings.json.");

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuracao);
            services.AddLoneAplicacao();
            services.AddLoneDados(conexao);
            services.AddLoneIntegracoes();

            // Navegação da janela principal (primeiro acesso, login, sistema)
            services.AddSingleton<INavegacao, Navegacao>();

            // ViewModels (um novo a cada abertura de tela)
            services.AddTransient<LoginViewModel>();
            services.AddTransient<PrimeiroAcessoViewModel>();
            services.AddTransient<EscolherEmpresaViewModel>();
            services.AddTransient<TrocarSenhaViewModel>();
            services.AddTransient<ShellViewModel>();
            services.AddTransient<UsuariosViewModel>();
            services.AddTransient<PerfisViewModel>();
            services.AddTransient<PessoasViewModel>();

            return services.BuildServiceProvider();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            _window = new MainWindow();
            _window.Activate();
        }
    }
}
