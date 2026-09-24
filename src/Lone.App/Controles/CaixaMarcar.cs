namespace Lone.App.Controles;

/// <summary>Caixa de marcar com texto ao lado; tocar no texto também marca (alvo maior no celular).</summary>
public sealed class CaixaMarcar : ContentView
{
    public static readonly BindableProperty MarcadoProperty = BindableProperty.Create(
        nameof(Marcado), typeof(bool), typeof(CaixaMarcar), false, BindingMode.TwoWay,
        propertyChanged: (b, _, n) => ((CaixaMarcar)b)._caixa.IsChecked = (bool)n);

    public static readonly BindableProperty TextoProperty = BindableProperty.Create(
        nameof(Texto), typeof(string), typeof(CaixaMarcar), string.Empty,
        propertyChanged: (b, _, n) => ((CaixaMarcar)b)._texto.Text = (string)n);

    private readonly CheckBox _caixa = new() { VerticalOptions = LayoutOptions.Center };
    private readonly Label _texto = new() { VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.WordWrap };

    public CaixaMarcar()
    {
        _caixa.CheckedChanged += (_, e) => Marcado = e.Value;

        var toque = new TapGestureRecognizer();
        toque.Tapped += (_, _) => { if (IsEnabled) Marcado = !Marcado; };
        _texto.GestureRecognizers.Add(toque);

        Content = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 2,
            Children = { _caixa, _texto }
        };
        Grid.SetColumn(_texto, 1);
    }

    public bool Marcado { get => (bool)GetValue(MarcadoProperty); set => SetValue(MarcadoProperty, value); }
    public string Texto { get => (string)GetValue(TextoProperty); set => SetValue(TextoProperty, value); }
}
