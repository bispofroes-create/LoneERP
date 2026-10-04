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

        // Motor global de navegação: este Shell passa a ser a plataforma dele (histórico recomeça a cada Shell novo —
        // entrada, outra empresa). O menu diz o nome de cada tela e quais o perfil ainda pode abrir.
        _plataforma = new Plataforma.NavegacaoShell(this);
        _navegacao.RotaPermitida = viewModel.PodeAbrir;
        _navegacao.TituloDaRota = viewModel.TituloDaRota;
        _navegacao.Conectar(_plataforma);
        Unloaded += (_, _) => _navegacao.Desconectar(_plataforma);

        // Tela grande: menu fixo ao lado (vários painéis). Celular: menu recolhido para sobrar espaço. No Windows, a
        // janela estreita também recolhe o menu (AjustarMenu).
        FlyoutBehavior = DeviceInfo.Current.Idiom == DeviceIdiom.Phone ? FlyoutBehavior.Flyout : FlyoutBehavior.Locked;

        // Cola de interface: o menu (ViewModel) pede a rota ao motor de navegação (navegação global: muda de área); o Shell
        // navega e avisa qual tela ficou aberta (menu destaca o item; o motor registra no histórico).
        viewModel.Navegar = async rota =>
        {
            await _navegacao.IrParaTelaAsync(rota, OrigemNavegacao.Menu);
            if (FlyoutBehavior == FlyoutBehavior.Flyout) FlyoutIsPresented = false;
        };
        Navigated += (_, _) =>
        {
            viewModel.DefinirRotaAtual(CurrentState?.Location?.OriginalString);
            _navegacao.AoChegarNaTela();
        };

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

            // Voltar global: fixo na barra de título (fora de qualquer rolagem, igual em todas as telas), com a dica
            // dizendo para onde vai. Mesmo caminho do Alt+←, do botão "voltar" do mouse e do voltar do celular.
            _botaoVoltar = new Button
            {
                Text = "←", FontSize = 18, TextColor = Colors.White, BackgroundColor = Colors.Transparent, BorderWidth = 0,
                Padding = new Thickness(0), WidthRequest = 40, HeightRequest = 32, MinimumHeightRequest = 32,
                VerticalOptions = LayoutOptions.Center, Margin = new Thickness(4, 0, 0, 0), IsEnabled = false
            };
            _botaoVoltar.Clicked += (_, _) => _ = _navegacao.VoltarAsync();
            AtualizarVoltar();

            BarraDeTitulo = new TitleBar
            {
                Title = "Lone ERP",
                HeightRequest = 48,
                BackgroundColor = Cor("MenuFundo"),
                ForegroundColor = Colors.White,
                LeadingContent = new HorizontalStackLayout { Spacing = 0, Children = { _botaoMenu, _botaoVoltar } },
                TrailingContent = new Controles.BarraTituloSistema { BindingContext = viewModel }
            };

            // Janela estreita: o menu recolhe (☰ na barra de título) para a tela ficar com a largura; larga, fica fixo.
            // Mede pela janela (o SizeChanged do próprio Shell não chega no Windows); solta a janela ao sair do sistema.
            Loaded += (_, _) =>
            {
                LigarJanela();
                _navegacao.PropertyChanged -= NavegacaoMudou; // o motor é do aplicativo: a inscrição sai com o Shell
                _navegacao.PropertyChanged += NavegacaoMudou;
                AtualizarVoltar();
            };
            PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(Window)) LigarJanela();
            };
            Unloaded += (_, _) =>
            {
                SoltarJanela();
                _navegacao.PropertyChanged -= NavegacaoMudou;
            };
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
        _atalhos = Plataforma.AtalhosNavegacao.Ligar(janela, _navegacao.VoltarAsync, NovoNaTelaAberta);
        AjustarMenu();
    }

    /// <summary>Ctrl+N: "+ Novo" da tela de cadastro aberta, se ela permite criar e está livre.</summary>
    private static void NovoNaTelaAberta()
    {
        if (Current?.CurrentPage?.BindingContext is not Lone.Cliente.ViewModels.Cadastros.IMestreDetalhe tela) return;
        if (tela.PodeCriar && tela.Livre && tela.NovoCommand.CanExecute(null)) tela.NovoCommand.Execute(null);
    }

    private void SoltarJanela()
    {
        if (_janela is not null) _janela.SizeChanged -= JanelaMudouDeTamanho;
        _janela = null;
        _atalhos?.Dispose();
        _atalhos = null;
    }

    // ---- Motor global de navegação ----

    private readonly GerenciadorNavegacao _navegacao = GerenciadorNavegacao.Padrao;
    private readonly Plataforma.NavegacaoShell _plataforma;
    private readonly Button? _botaoVoltar;
    private Plataforma.AtalhosNavegacao? _atalhos;

    private void NavegacaoMudou(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => AtualizarVoltar();

    /// <summary>Voltar habilitado só com para onde voltar; a dica e o leitor de tela dizem o destino.</summary>
    private void AtualizarVoltar()
    {
        if (_botaoVoltar is null) return;
        var pode = _navegacao.PodeVoltar && !_navegacao.Navegando;
        _botaoVoltar.IsEnabled = pode;
        _botaoVoltar.Opacity = pode ? 1 : 0.35;
        var texto = _navegacao.PodeVoltar ? $"{_navegacao.DescricaoVoltar} (Alt+←)" : "Nada para voltar";
        ToolTipProperties.SetText(_botaoVoltar, texto);
        SemanticProperties.SetDescription(_botaoVoltar, _navegacao.DescricaoVoltar);
    }

    /// <summary>
    /// Voltar do celular (botão ou gesto): o mesmo Voltar do motor. Menu aberto por cima: fecha o menu primeiro. Sem
    /// histórico, o comportamento de antes (a tela fecha a ficha; na tela inicial, o sistema decide).
    /// </summary>
    protected override bool OnBackButtonPressed()
    {
        if (FlyoutBehavior == FlyoutBehavior.Flyout && FlyoutIsPresented)
        {
            FlyoutIsPresented = false;
            return true;
        }
        if (_navegacao.PodeVoltar)
        {
            _ = _navegacao.VoltarAsync();
            return true;
        }
        return base.OnBackButtonPressed();
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
