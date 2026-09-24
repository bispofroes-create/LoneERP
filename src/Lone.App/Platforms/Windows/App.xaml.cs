namespace Lone.App.WinUI;

/// <summary>Ponto de entrada no Windows. Só entrega o aplicativo MAUI montado em MauiProgram.</summary>
public partial class App : MauiWinUIApplication
{
    public App()
    {
        InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
