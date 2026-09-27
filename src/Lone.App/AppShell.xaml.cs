using Lone.Cliente.ViewModels;
using Lone.Cliente.ViewModels.Cadastros;

namespace Lone.App;

public partial class AppShell : Shell
{
    public AppShell(MenuViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        Unloaded += (_, _) => viewModel.Dispose();

        // Tela grande: menu fixo ao lado (vários painéis). Celular: menu recolhido para sobrar espaço.
        FlyoutBehavior = DeviceInfo.Current.Idiom == DeviceIdiom.Phone ? FlyoutBehavior.Flyout : FlyoutBehavior.Locked;

        // Cola de interface: o menu (ViewModel) pede a rota; o Shell navega e avisa qual tela ficou aberta.
        viewModel.Navegar = async rota =>
        {
            await GoToAsync("//" + rota);
            if (FlyoutBehavior == FlyoutBehavior.Flyout) FlyoutIsPresented = false;
        };
        Navigated += (_, _) => viewModel.DefinirRotaAtual(CurrentState?.Location?.OriginalString);

        // Favoritos e recentes do usuário vêm da API; o menu já funciona antes da resposta.
        viewModel.CarregarPreferenciasCommand.Execute(null);

        // Windows: empresa e usuário no canto direito da barra de título (a navegação instala ao abrir o sistema).
        if (DeviceInfo.Current.Platform == DevicePlatform.WinUI)
            BarraDeTitulo = new TitleBar
            {
                Title = "Lone ERP",
                HeightRequest = 48,
                BackgroundColor = Cor("MenuFundo"),
                ForegroundColor = Colors.White,
                TrailingContent = new Controles.BarraTituloSistema { BindingContext = viewModel }
            };
    }

    /// <summary>Barra de título da janela enquanto o sistema está aberto (nula fora do Windows).</summary>
    public TitleBar? BarraDeTitulo { get; }

    private static Color Cor(string chave) =>
        Application.Current?.Resources.TryGetValue(chave, out var valor) == true && valor is Color cor ? cor : Colors.Black;

    /// <summary>
    /// Trocar de tela pelo menu com uma ficha alterada e não salva: pergunta antes (mesma regra do Fechar/Descartar).
    /// </summary>
    protected override async void OnNavigating(ShellNavigatingEventArgs args)
    {
        base.OnNavigating(args);
        if (args.Cancelled || CurrentPage?.BindingContext is not IMestreDetalhe { TemAlteracoes: true } tela)
            return;

        var adiamento = args.GetDeferral();
        try
        {
            if (!await tela.PodeSairAsync())
                args.Cancel();
        }
        finally
        {
            adiamento.Complete();
        }
    }
}
