using Lone.App.Plataforma;
using Lone.App.Views;
using Lone.Cliente;
using Lone.Cliente.Navegacao;
using Lone.Cliente.Plataforma;
using Microsoft.Extensions.Logging;

namespace Lone.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        // Implementações de plataforma usadas por Lone.Cliente.
        builder.Services.AddSingleton<IArmazenamentoSeguro, ArmazenamentoSeguroMaui>();
        builder.Services.AddSingleton<IPreferencias, PreferenciasMaui>();
        builder.Services.AddSingleton<IDispositivo, DispositivoMaui>();
        builder.Services.AddSingleton<INavegacao, NavegacaoMaui>();
        builder.Services.AddSingleton<IDialogos, DialogosMaui>();
        builder.Services.AddSingleton<IArquivos, ArquivosMaui>();
        builder.Services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromSeconds(30) });

        builder.Services.AddLoneCliente();

        // Telas (uma nova a cada abertura).
        builder.Services.AddTransient<AppShell>();
        builder.Services.AddTransient<CarregandoPage>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<PrimeiroAcessoPage>();
        builder.Services.AddTransient<EscolherEmpresaPage>();
        builder.Services.AddTransient<TrocarSenhaPage>();
        builder.Services.AddTransient<InicioPage>();
        builder.Services.AddTransient<EmConstrucaoPage>();
        builder.Services.AddTransient<UsuariosPage>();
        builder.Services.AddTransient<PerfisPage>();
        builder.Services.AddTransient<PessoasPage>();
        builder.Services.AddTransient<CamposPersonalizadosPage>();
        builder.Services.AddTransient<EtiquetasPage>();
        builder.Services.AddTransient<ProfissoesPage>();
        builder.Services.AddTransient<PapeisPage>();
        builder.Services.AddTransient<TiposMeioContatoPage>();
        builder.Services.AddTransient<TiposEnderecoPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
