namespace Lone.App.Controles;

/// <summary>
/// Caixa de marcar com texto ao lado; tocar no texto também marca (alvo maior no celular). É a única forma de caixa de
/// marcar nas telas do Lone: o espaço entre a caixa e o texto é o mesmo em todas (ver Plataforma/AjusteCaixaMarcar).
/// </summary>
public sealed class CaixaMarcar : ContentView
{
    /// <summary>Espaço entre a caixa e o texto (decisão do usuário: só "um espaço").</summary>
    public const double EspacoTexto = 4;

    public static readonly BindableProperty MarcadoProperty = BindableProperty.Create(
        nameof(Marcado), typeof(bool), typeof(CaixaMarcar), false, BindingMode.TwoWay,
        propertyChanged: (b, _, n) => ((CaixaMarcar)b)._caixa.IsChecked = (bool)n);

    public static readonly BindableProperty TextoProperty = BindableProperty.Create(
        nameof(Texto), typeof(string), typeof(CaixaMarcar), string.Empty,
        propertyChanged: (b, _, n) => ((CaixaMarcar)b)._texto.Text = (string)n);

    /// <summary>Conteúdo embaixo do texto, alinhado com ele (ex.: o resumo do Prazo; 03/10/2026).</summary>
    public static readonly BindableProperty DetalheProperty = BindableProperty.Create(
        nameof(Detalhe), typeof(View), typeof(CaixaMarcar), null,
        propertyChanged: (b, _, n) => { var c = (CaixaMarcar)b; c._detalhe.Content = (View?)n; c._detalhe.IsVisible = n is not null; });

    private readonly ContentView _detalhe = new() { IsVisible = false };
    private readonly CheckBox _caixa = new() { VerticalOptions = LayoutOptions.Center };
    private readonly Label _texto = new() { VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.WordWrap };

    public CaixaMarcar()
    {
        _caixa.CheckedChanged += (_, e) => Marcado = e.Value;

        var toque = new TapGestureRecognizer();
        toque.Tapped += (_, _) => { if (IsEnabled) Marcado = !Marcado; };
        _texto.GestureRecognizers.Add(toque);

        var grade = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) },
            ColumnSpacing = EspacoTexto,
            Children = { _caixa, _texto, _detalhe }
        };
        Grid.SetColumn(_texto, 1);
        Grid.SetColumn(_detalhe, 1);
        Grid.SetRow(_detalhe, 1);
        Content = grade;
    }

    public bool Marcado { get => (bool)GetValue(MarcadoProperty); set => SetValue(MarcadoProperty, value); }
    public string Texto { get => (string)GetValue(TextoProperty); set => SetValue(TextoProperty, value); }
    public View? Detalhe { get => (View?)GetValue(DetalheProperty); set => SetValue(DetalheProperty, value); }
}
