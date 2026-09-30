using Lone.Cliente.Navegacao;
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

        // Tela grande: menu fixo ao lado (vários painéis). Celular: menu recolhido para sobrar espaço. No Windows, a
        // janela estreita também recolhe o menu (AjustarMenu).
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
        {
            _botaoMenu = new Button
            {
                Text = "☰", FontSize = 16, TextColor = Colors.White, BackgroundColor = Colors.Transparent, BorderWidth = 0,
                Padding = new Thickness(0), WidthRequest = 40, HeightRequest = 32, MinimumHeightRequest = 32,
                VerticalOptions = LayoutOptions.Center, Margin = new Thickness(4, 0, 0, 0), IsVisible = false
            };
            ToolTipProperties.SetText(_botaoMenu, "Mostrar o menu");
            SemanticProperties.SetDescription(_botaoMenu, "Mostrar o menu");
            _botaoMenu.Clicked += (_, _) => FlyoutIsPresented = !FlyoutIsPresented;

            BarraDeTitulo = new TitleBar
            {
                Title = "Lone ERP",
                HeightRequest = 48,
                BackgroundColor = Cor("MenuFundo"),
                ForegroundColor = Colors.White,
                LeadingContent = _botaoMenu,
                TrailingContent = new Controles.BarraTituloSistema { BindingContext = viewModel }
            };

            // Janela estreita: o menu recolhe (☰ na barra de título) para a tela ficar com a largura; larga, fica fixo.
            // Mede pela janela (o SizeChanged do próprio Shell não chega no Windows); solta a janela ao sair do sistema.
            Loaded += (_, _) => LigarJanela();
            PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(Window)) LigarJanela();
            };
            Unloaded += (_, _) => SoltarJanela();
        }
    }

    /// <summary>Botão ☰ da barra de título (Windows): só aparece com o menu recolhido (<see cref="MenuLateral"/>).</summary>
    private readonly Button? _botaoMenu;

    private Window? _janela;

    private void LigarJanela()
    {
        if (ReferenceEquals(Window, _janela)) { AjustarMenu(); return; }
        SoltarJanela();
        if (Window is not { } janela) return;
        _janela = janela;
        janela.SizeChanged += JanelaMudouDeTamanho;
        AjustarMenu();
    }

    private void SoltarJanela()
    {
        if (_janela is not null) _janela.SizeChanged -= JanelaMudouDeTamanho;
        _janela = null;
    }

    private void JanelaMudouDeTamanho(object? sender, EventArgs e) => AjustarMenu();

    private void AjustarMenu()
    {
        var largura = _janela?.Width ?? Width;
        if (_botaoMenu is null || largura <= 0) return;
        var recolher = MenuLateral.Recolhido(largura);
        var comportamento = recolher ? FlyoutBehavior.Flyout : FlyoutBehavior.Locked;
        if (FlyoutBehavior == comportamento) return;
        FlyoutIsPresented = false; // ao recolher, começa fechado; ao fixar, o Shell mostra o menu ao lado
        FlyoutBehavior = comportamento;
        _botaoMenu.IsVisible = recolher;
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
