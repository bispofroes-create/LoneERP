using static Lone.App.Controles.RecursosTela;

namespace Lone.App.Controles;

/// <summary>
/// Campo de número inteiro com setas ▲▼ dentro, à direita (03/10/2026; como o NumberBox do Windows e o "step input" do
/// SAP Fiori): digitar continua valendo; as setas (e as teclas ↑ ↓ no computador) somam ou tiram <see cref="Passo"/>,
/// sem passar de <see cref="Minimo"/> e <see cref="Maximo"/>. Vazio + seta começa em <see cref="ValorInicial"/>.
/// Mesmo tamanho dos outros campos (rótulo em cima, caixa de 44).
/// </summary>
public sealed class CampoNumero : ContentView, ICampoValidavel
{
    private const double LarguraSetas = 30;

    public static readonly BindableProperty RotuloProperty = BindableProperty.Create(
        nameof(Rotulo), typeof(string), typeof(CampoNumero), string.Empty, propertyChanged: (b, _, n) => ((CampoNumero)b)._rotulo.Text = (string)n);

    public static readonly BindableProperty TextoProperty = BindableProperty.Create(
        nameof(Texto), typeof(string), typeof(CampoNumero), string.Empty, BindingMode.TwoWay,
        propertyChanged: (b, _, n) => { var c = (CampoNumero)b; if (c._entrada.Text != (string?)n) c._entrada.Text = (string?)n ?? string.Empty; });

    public static readonly BindableProperty DicaProperty = BindableProperty.Create(
        nameof(Dica), typeof(string), typeof(CampoNumero), string.Empty, propertyChanged: (b, _, n) => ((CampoNumero)b)._entrada.Placeholder = (string)n);

    public static readonly BindableProperty MinimoProperty = BindableProperty.Create(nameof(Minimo), typeof(int), typeof(CampoNumero), 0);
    public static readonly BindableProperty MaximoProperty = BindableProperty.Create(nameof(Maximo), typeof(int), typeof(CampoNumero), 99999);
    public static readonly BindableProperty PassoProperty = BindableProperty.Create(nameof(Passo), typeof(int), typeof(CampoNumero), 1);
    public static readonly BindableProperty ValorInicialProperty = BindableProperty.Create(nameof(ValorInicial), typeof(int), typeof(CampoNumero), 0);

    private readonly Label _rotulo = new();
    private readonly Entry _entrada = new() { Keyboard = Keyboard.Numeric, MaxLength = 6 };
    private readonly Button _mais = Seta("▲", "Aumentar");
    private readonly Button _menos = Seta("▼", "Diminuir");

    public CampoNumero()
    {
        Estilo(_rotulo, "Rotulo");
        _entrada.TextChanged += (_, e) =>
        {
            var texto = e.NewTextValue ?? string.Empty;
            var digitos = new string(texto.Where(char.IsDigit).ToArray());
            if (digitos != texto) { _entrada.Text = digitos; return; } // só números
            if (Texto != digitos) Texto = digitos;
        };
        _entrada.HandlerChanged += (_, _) => AjustarTextoNativo();
        _mais.Clicked += (_, _) => Somar(+1);
        _menos.Clicked += (_, _) => Somar(-1);
        var setas = new VerticalStackLayout
        {
            Spacing = 0, WidthRequest = LarguraSetas, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Center,
            Children = { _mais, _menos }
        };
        _marca = new MarcaDeErro(_entrada);
        Content = new VerticalStackLayout { Spacing = 2, Children = { _rotulo, new Grid { Children = { _entrada, setas } }, _marca.Mensagens } };
    }

    private readonly MarcaDeErro _marca;

    /// <summary>Marca de erro (Lone Contextual, Fase 1): borda e "⚠ mensagem" embaixo; nulo tira.</summary>
    public void MostrarErro(string? mensagem) => _marca.Mostrar(mensagem);

    /// <summary>Destaque de alteração: fundo azul-claro e "● Veio da Receita... · antes: ..." embaixo; nulo tira.</summary>
    public void MostrarDestaque(global::Lone.Cliente.ViewModels.Comum.DestaqueCampo? destaque) => _marca.MostrarDestaque(destaque);

    public bool Focar() => _entrada.IsEnabled && _entrada.Focus();

    public string Rotulo { get => (string)GetValue(RotuloProperty); set => SetValue(RotuloProperty, value); }
    public string Texto { get => (string)GetValue(TextoProperty); set => SetValue(TextoProperty, value); }
    public string Dica { get => (string)GetValue(DicaProperty); set => SetValue(DicaProperty, value); }
    public int Minimo { get => (int)GetValue(MinimoProperty); set => SetValue(MinimoProperty, value); }
    public int Maximo { get => (int)GetValue(MaximoProperty); set => SetValue(MaximoProperty, value); }
    public int Passo { get => (int)GetValue(PassoProperty); set => SetValue(PassoProperty, value); }
    public int ValorInicial { get => (int)GetValue(ValorInicialProperty); set => SetValue(ValorInicialProperty, value); }

    /// <summary>Soma ou tira um passo (vazio começa no valor inicial), dentro do mínimo e do máximo.</summary>
    private void Somar(int sentido)
    {
        var atual = int.TryParse(_entrada.Text, out var n) ? n : (int?)null;
        var novo = atual is { } a ? a + sentido * Math.Max(1, Passo) : ValorInicial;
        _entrada.Text = Math.Clamp(novo, Minimo, Maximo).ToString();
        _entrada.CursorPosition = _entrada.Text.Length;
    }

    private static Button Seta(string texto, string descricao)
    {
        var botao = new Button
        {
            Text = texto, FontSize = 9, Padding = 0, HeightRequest = 18, WidthRequest = LarguraSetas, MinimumHeightRequest = 0,
            MinimumWidthRequest = 0, BackgroundColor = Colors.Transparent, BorderWidth = 0
        };
        Cor(botao, Button.TextColorProperty, "TextoSecundario");
        SemanticProperties.SetDescription(botao, descricao);
        ToolTipProperties.SetText(botao, descricao);
        return botao;
    }

    /// <summary>No Windows: espaço para as setas, sem o "X" de apagar do WinUI, e as teclas ↑ ↓ somando/tirando.</summary>
    private void AjustarTextoNativo()
    {
#if WINDOWS
        if (_entrada.Handler?.PlatformView is not Microsoft.UI.Xaml.Controls.TextBox texto) return;
        texto.KeyDown += (_, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.Up) { Somar(+1); e.Handled = true; }
            else if (e.Key == Windows.System.VirtualKey.Down) { Somar(-1); e.Handled = true; }
        };
        void Ajustar()
        {
            var p = texto.Padding;
            texto.Padding = new Microsoft.UI.Xaml.Thickness(p.Left, p.Top, LarguraSetas + 4, p.Bottom);
            if (Descendente(texto, "DeleteButton") is Microsoft.UI.Xaml.FrameworkElement apagar) apagar.Width = 0;
        }
        if (texto.IsLoaded) Ajustar();
        else texto.Loaded += (_, _) => Ajustar();
#endif
    }

#if WINDOWS
    private static Microsoft.UI.Xaml.DependencyObject? Descendente(Microsoft.UI.Xaml.DependencyObject raiz, string nome)
    {
        for (var i = 0; i < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(raiz); i++)
        {
            var filho = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(raiz, i);
            if (filho is Microsoft.UI.Xaml.FrameworkElement { Name: var n } && n == nome) return filho;
            if (Descendente(filho, nome) is { } achado) return achado;
        }
        return null;
    }
#endif
}
