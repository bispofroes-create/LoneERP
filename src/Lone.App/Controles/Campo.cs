using Lone.App.Plataforma;
using Lone.Cliente.Formularios;
using Lone.Cliente.ViewModels.Comum;

namespace Lone.App.Controles;

/// <summary>
/// Rótulo + caixa de texto. Usado em todas as fichas para os campos ficarem iguais.
/// Com Mascara, formata enquanto o usuário digita (data, CPF, CNPJ, CEP, telefone).
/// Data: ícone 📅 dentro do campo, à direita, que abre o calendário (03/10/2026); digitar continua valendo.
/// </summary>
public sealed class Campo : ContentView, ICampoValidavel
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

    /// <summary>
    /// Bloco G: quantos caracteres cabem (o tamanho da coluna do banco; a API confere de novo). Zero = sem limite.
    /// </summary>
    public static readonly BindableProperty TamanhoMaximoProperty = BindableProperty.Create(
        nameof(TamanhoMaximo), typeof(int), typeof(Campo), 0,
        propertyChanged: (b, _, n) => ((Campo)b)._entrada.MaxLength = (int)n > 0 ? (int)n : int.MaxValue);

    public static readonly BindableProperty AoLadoProperty = BindableProperty.Create(
        nameof(AoLado), typeof(View), typeof(Campo), null, propertyChanged: (b, o, n) => ((Campo)b).TrocarAoLado(o as View, n as View));

    private readonly Label _rotulo = new();
    private readonly Entry _entrada = new();
    private readonly Grid _linhaEntrada = new() { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 8 };
    private Button? _botaoCalendario;
    private Action? _fecharCalendario;

    /// <summary>Espaço à direita do texto para o ícone do calendário não ficar por cima do que se digita.</summary>
    private const double EspacoIcone = 36;

    public Campo()
    {
        _rotulo.SetDynamicResource(StyleProperty, "Rotulo");
        _entrada.TextChanged += Entrada_TextChanged;
        _entrada.HandlerChanged += (_, _) => AjustarTextoNativo();
        _marca = new MarcaDeErro(_entrada);
        _linhaEntrada.Add(_entrada);
        Content = new VerticalStackLayout { Spacing = 2, Children = { _rotulo, _linhaEntrada, _marca.Mensagens } };
    }

    private readonly MarcaDeErro _marca;

    /// <summary>Marca de erro (Lone Contextual, Fase 1): borda e "⚠ mensagem" embaixo; nulo tira.</summary>
    public void MostrarErro(string? mensagem) => _marca.Mostrar(mensagem);

    /// <summary>Destaque de alteração: fundo azul-claro e "● Veio da Receita... · antes: ..." embaixo; nulo tira.</summary>
    public void MostrarDestaque(global::Lone.Cliente.ViewModels.Comum.DestaqueCampo? destaque) => _marca.MostrarDestaque(destaque);

    public bool Focar() => !SomenteLeitura && _entrada.IsEnabled && _entrada.Focus();

    /// <summary>
    /// Ação na mesma linha da caixa (ex.: "Consultar CNPJ"): fica alinhada à caixa, e a mensagem de erro aparece embaixo das
    /// duas sem empurrar o botão.
    /// </summary>
    public View? AoLado { get => (View?)GetValue(AoLadoProperty); set => SetValue(AoLadoProperty, value); }

    private void TrocarAoLado(View? antigo, View? novo)
    {
        if (antigo is not null) _linhaEntrada.Remove(antigo);
        if (novo is null) return;
        novo.VerticalOptions = LayoutOptions.Center;
        _linhaEntrada.Add(novo, 1);
    }

    public string Rotulo { get => (string)GetValue(RotuloProperty); set => SetValue(RotuloProperty, value); }
    public string Texto { get => (string)GetValue(TextoProperty); set => SetValue(TextoProperty, value); }
    public string Dica { get => (string)GetValue(DicaProperty); set => SetValue(DicaProperty, value); }
    public Keyboard Teclado { get => (Keyboard)GetValue(TecladoProperty); set => SetValue(TecladoProperty, value); }
    public bool SomenteLeitura { get => (bool)GetValue(SomenteLeituraProperty); set => SetValue(SomenteLeituraProperty, value); }
    public TipoMascara Mascara { get => (TipoMascara)GetValue(MascaraProperty); set => SetValue(MascaraProperty, value); }
    public int TamanhoMaximo { get => (int)GetValue(TamanhoMaximoProperty); set => SetValue(TamanhoMaximoProperty, value); }

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
            _linhaEntrada.Add(_botaoCalendario);
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
