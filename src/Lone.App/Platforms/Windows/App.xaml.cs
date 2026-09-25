namespace Lone.App.WinUI;

/// <summary>Ponto de entrada no Windows. Só entrega o aplicativo MAUI montado em MauiProgram.</summary>
public partial class App : MauiWinUIApplication
{
    public App()
    {
        InitializeComponent();

        // Exceções da interface do WinUI (layout, controles nativos) não passam pelo AppDomain: grava aqui também.
        // Handled continua falso: o comportamento do app não muda, só fica o registro para achar a causa.
        UnhandledException += (_, e) =>
            Lone.App.Plataforma.RegistroFalhas.Registrar("WinUI.UnhandledException", e.Exception, e.Message);
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
