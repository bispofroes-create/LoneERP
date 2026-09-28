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
        // Diagnóstico de falhas: grava a exceção real (pilha completa) antes de o app fechar.
        RegistroFalhas.Ligar();

        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        // Caixas de marcar compactas (texto logo ao lado da caixa), em todas as telas.
        AjusteCaixaMarcar.Aplicar();

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
        builder.Services.AddTransient<ConfiguracoesPage>();
        builder.Services.AddTransient<EmConstrucaoPage>();
        builder.Services.AddTransient<UsuariosPage>();
        builder.Services.AddTransient<PerfisPage>();
        builder.Services.AddTransient<PessoasPage>();
        builder.Services.AddTransient<CamposPersonalizadosPage>();
        builder.Services.AddTransient<EtiquetasPage>();
        builder.Services.AddTransient<GruposEmpresariaisPage>();
        builder.Services.AddTransient<ProfissoesPage>();
        builder.Services.AddTransient<PapeisPage>();
        builder.Services.AddTransient<TiposMeioContatoPage>();
        builder.Services.AddTransient<TiposEnderecoPage>();
        builder.Services.AddTransient<FinalidadesTratamentoPage>();
        builder.Services.AddTransient<TiposDocumentoPage>();
        builder.Services.AddTransient<CargosPage>();
        builder.Services.AddTransient<DepartamentosPage>();
        builder.Services.AddTransient<EquipesPage>();
        builder.Services.AddTransient<IndicadoresPage>();
        builder.Services.AddTransient<MetasPage>();
        builder.Services.AddTransient<SetoresPage>();
        builder.Services.AddTransient<CentrosCustoPage>();
        builder.Services.AddTransient<PerfisComerciaisPage>();
        builder.Services.AddTransient<CondicoesPagamentoPage>();
        builder.Services.AddTransient<TiposCarteiraPage>();
        builder.Services.AddTransient<TiposAusenciaPage>();
        builder.Services.AddTransient<ParametrosComerciaisPage>();
        builder.Services.AddTransient<CoberturasPage>();
        builder.Services.AddTransient<CarteiraVencendoPage>();
        builder.Services.AddTransient<TransferenciasPage>();
        builder.Services.AddTransient<CarteiraEmDataPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
