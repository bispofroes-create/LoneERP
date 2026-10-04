using Lone.App.Plataforma;
using Lone.Cliente.Formularios;
using Lone.Cliente.ViewModels.Comum;

namespace Lone.App.Controles;

/// <summary>
/// Rótulo + caixa de texto. Usado em todas as fichas para os campos ficarem iguais.
/// Com Mascara, formata enquanto o usuário digita (data, CPF, CNPJ, CEP, telefone).
/// Data: ícone 📅 dentro do campo, à direita, que abre o calendário (03/10/2026); digitar continua valendo.
/// </summary>
public sealed class Campo : ContentView
{
    public static readonly BindableProperty RotuloProperty = BindableProperty.Create(
        nameof(Rotulo), typeof(string), typeof(Campo), string.Empty,
        propertyChanged: (b, _, n) => ((Campo)b)._rotulo.Text = (string)n);

    public static readonly BindableProperty TextoProperty = BindableProperty.Create(
        nameof(Texto), typeof(string), typeof(Campo), string.Empty, BindingMode.TwoWay,
        propertyChanged: (b, _, n) => ((Campo)b)._entrada.Text = (string?)n ?? string.Empty);

    public static readonly BindableProperty DicaProperty = BindableProperty.Create(
        nameof(Dica), typeof(string), typeof(Campo), string.Empty,
        propertyChanged: (b, _, n) => ((Campo)b)._entrada.Placeholder = (string)n);

    public static readonly BindableProperty TecladoProperty = BindableProperty.Create(
        nameof(Teclado), typeof(Keyboard), typeof(Campo), Keyboard.Default,
        propertyChanged: (b, _, n) => ((Campo)b)._entrada.Keyboard = (Keyboard)n);

    public static readonly BindableProperty SomenteLeituraProperty = BindableProperty.Create(
        nameof(SomenteLeitura), typeof(bool), typeof(Campo), false,
        propertyChanged: (b, _, n) => { var c = (Campo)b; c._entrada.IsReadOnly = (bool)n; c.AtualizarCalendario(); });

    public static readonly BindableProperty MascaraProperty = BindableProperty.Create(
        nameof(Mascara), typeof(TipoMascara), typeof(Campo), TipoMascara.Nenhuma,
        propertyChanged: (b, o, n) => ((Campo)b).TrocarMascara((TipoMascara)o, (TipoMascara)n));

    private readonly Label _rotulo = new();
    private readonly Entry _entrada = new();
    private Button? _botaoCalendario;
    private Action? _fecharCalendario;

    /// <summary>Espaço à direita do texto para o ícone do calendário não ficar por cima do que se digita.</summary>
    private const double EspacoIcone = 36;

    public Campo()
    {
        _rotulo.SetDynamicResource(StyleProperty, "Rotulo");
        _entrada.TextChanged += Entrada_TextChanged;
        _entrada.HandlerChanged += (_, _) => AjustarTextoNativo();
        Content = new VerticalStackLayout { Spacing = 2, Children = { _rotulo, new Grid { Children = { _entrada } } } };
    }

    public string Rotulo { get => (string)GetValue(RotuloProperty); set => SetValue(RotuloProperty, value); }
    public string Texto { get => (string)GetValue(TextoProperty); set => SetValue(TextoProperty, value); }
    public string Dica { get => (string)GetValue(DicaProperty); set => SetValue(DicaProperty, value); }
    public Keyboard Teclado { get => (Keyboard)GetValue(TecladoProperty); set => SetValue(TecladoProperty, value); }
    public bool SomenteLeitura { get => (bool)GetValue(SomenteLeituraProperty); set => SetValue(SomenteLeituraProperty, value); }
    public TipoMascara Mascara { get => (TipoMascara)GetValue(MascaraProperty); set => SetValue(MascaraProperty, value); }

    private void Entrada_TextChanged(object? sender, TextChangedEventArgs e)
    {
        var texto = e.NewTextValue ?? string.Empty;
        var formatado = global::Lone.Cliente.ViewModels.Comum.Mascara.Aplicar(Mascara, texto);
        if (formatado != texto)
        {
            // Reescreve com a máscara; o novo TextChanged chega aqui já formatado e só atualiza o valor.
            _entrada.Text = formatado;
            _entrada.CursorPosition = formatado.Length;
            return;
        }
        // Só quando mudou: definir localmente o valor de uma propriedade ligada em OneWay (ex.: a idade calculada)
        // faz o MAUI desfazer a ligação, e o campo congelava no primeiro valor recebido.
        if (!string.Equals(Texto, texto, StringComparison.Ordinal)) Texto = texto;
    }

    /// <summary>Máscaras que mudam o texto (Número e E-mail só escolhem o teclado).</summary>
    private static bool Formata(TipoMascara m) => m is not (TipoMascara.Nenhuma or TipoMascara.Numero or TipoMascara.Email);

    /// <summary>
    /// Teclado adequado à máscara (números no celular), a menos que a tela tenha escolhido outro.
    /// Só reformata o que já estava mascarado (ex.: telefone → celular). Texto livre (e-mail, identificação
    /// estrangeira) não é convertido, para não virar outra coisa nem ser apagado ao trocar o tipo.
    /// </summary>
    private void TrocarMascara(TipoMascara anterior, TipoMascara mascara)
    {
        if (IsSet(TecladoProperty) is false)
            _entrada.Keyboard = mascara switch
            {
                TipoMascara.Data or TipoMascara.Cpf or TipoMascara.Cep or TipoMascara.Hora or TipoMascara.DataHora
                    or TipoMascara.Numero => Keyboard.Numeric,
                TipoMascara.Telefone => Keyboard.Telephone,
                TipoMascara.Email => Keyboard.Email,
                _ => Keyboard.Default
            };
        if (Formata(anterior) && Formata(mascara))
            _entrada.Text = global::Lone.Cliente.ViewModels.Comum.Mascara.Aplicar(mascara, _entrada.Text);
        AtualizarCalendario();
    }

    /// <summary>Ícone 📅 só em campo de data editável (e onde a plataforma tem popover).</summary>
    private bool MostraCalendario => Mascara == TipoMascara.Data && !SomenteLeitura && Popover.Disponivel;

    private void AtualizarCalendario()
    {
        if (MostraCalendario && _botaoCalendario is null)
        {
            _botaoCalendario = new Button
            {
                Text = "📅", FontSize = 15, Padding = 0, WidthRequest = EspacoIcone, MinimumWidthRequest = 0, MinimumHeightRequest = 0,
                BackgroundColor = Colors.Transparent, BorderWidth = 0, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Fill
            };
            SemanticProperties.SetDescription(_botaoCalendario, "Abrir calendário");
            ToolTipProperties.SetText(_botaoCalendario, "Escolher no calendário");
            _botaoCalendario.Clicked += (_, _) => AbrirCalendario();
            ((Grid)((VerticalStackLayout)Content).Children[1]).Children.Add(_botaoCalendario);
        }
        if (_botaoCalendario is not null) _botaoCalendario.IsVisible = MostraCalendario;
        AjustarTextoNativo();
    }

    private void AbrirCalendario()
    {
        if (_botaoCalendario is null) return;
        TextoTela.TentarData(_entrada.Text, out var atual);
        var calendario = new Calendario(atual, podeLimpar: !string.IsNullOrWhiteSpace(_entrada.Text));
        calendario.Escolheu += (_, data) =>
        {
            _fecharCalendario?.Invoke();
            _entrada.Text = data is { } d ? MesCalendario.Texto(d) : string.Empty;
            _entrada.Focus();
        };
        _fecharCalendario = Popover.Mostrar(_botaoCalendario, calendario);
    }

    /// <summary>
    /// No Windows: deixa à direita do texto o espaço do ícone e tira o "X" de apagar do WinUI, que ficaria embaixo dele.
    /// </summary>
    private void AjustarTextoNativo()
    {
#if WINDOWS
        if (_entrada.Handler?.PlatformView is not Microsoft.UI.Xaml.Controls.TextBox texto) return;
        // Só depois de carregado: antes disso o estilo do WinUI ainda não deu o espaçamento interno, e mexer nele zerava
        // a margem esquerda do texto (03/10/2026). Campo sem calendário fica como o WinUI deixou.
        void Ajustar()
        {
            if (!MostraCalendario && _paddingOriginal is null) return;
            var p = texto.Padding;
            _paddingOriginal ??= p.Right;
            texto.Padding = new Microsoft.UI.Xaml.Thickness(p.Left, p.Top, MostraCalendario ? EspacoIcone : _paddingOriginal.Value, p.Bottom);
            if (Descendente(texto, "DeleteButton") is Microsoft.UI.Xaml.FrameworkElement apagar)
                apagar.Width = MostraCalendario ? 0 : double.NaN;
        }
        if (texto.IsLoaded) Ajustar();
        else texto.Loaded += (_, _) => Ajustar();
#endif
    }

#if WINDOWS
    private double? _paddingOriginal;

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
