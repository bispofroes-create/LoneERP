using System.Windows.Input;
using static Lone.App.Controles.RecursosTela;

namespace Lone.App.Controles;

/// <summary>
/// Atalho com contagem ("3  Até 7 dias"), em pílula, na barra de uma lista de consulta (03/10/2026; como os atalhos de
/// Pessoas): tocar executa <see cref="Comando"/> com <see cref="Parametro"/>; o selecionado fica na cor primária.
/// </summary>
public sealed class AtalhoContagem : ContentView
{
    public static readonly BindableProperty TextoProperty = BindableProperty.Create(
        nameof(Texto), typeof(string), typeof(AtalhoContagem), string.Empty, propertyChanged: (b, _, n) => ((AtalhoContagem)b)._texto.Text = (string)n);

    public static readonly BindableProperty QuantidadeProperty = BindableProperty.Create(
        nameof(Quantidade), typeof(int), typeof(AtalhoContagem), 0, propertyChanged: (b, _, n) => ((AtalhoContagem)b)._numero.Text = ((int)n).ToString("N0"));

    public static readonly BindableProperty SelecionadoProperty = BindableProperty.Create(
        nameof(Selecionado), typeof(bool), typeof(AtalhoContagem), false, propertyChanged: (b, _, _) => ((AtalhoContagem)b).Pintar());

    /// <summary>Sem o número (atalho só com texto, ex.: os períodos da lista vazia).</summary>
    public static readonly BindableProperty MostrarQuantidadeProperty = BindableProperty.Create(
        nameof(MostrarQuantidade), typeof(bool), typeof(AtalhoContagem), true, propertyChanged: (b, _, n) => ((AtalhoContagem)b)._numero.IsVisible = (bool)n);

    public static readonly BindableProperty ComandoProperty = BindableProperty.Create(nameof(Comando), typeof(ICommand), typeof(AtalhoContagem));
    public static readonly BindableProperty ParametroProperty = BindableProperty.Create(nameof(Parametro), typeof(object), typeof(AtalhoContagem));

    private readonly Label _numero = new() { Text = "0", FontAttributes = FontAttributes.Bold, FontSize = 13, VerticalOptions = LayoutOptions.Center };
    private readonly Label _texto = new() { FontSize = 13, VerticalOptions = LayoutOptions.Center };
    private readonly Border _pilula;

    public AtalhoContagem()
    {
        _pilula = new Border
        {
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 }, StrokeThickness = 1,
            Padding = new Thickness(12, 6), MinimumHeightRequest = 32,
            Content = new HorizontalStackLayout { Spacing = 6, Children = { _numero, _texto } }
        };
        var toque = new TapGestureRecognizer();
        toque.Tapped += (_, _) => { if (Comando?.CanExecute(Parametro) == true) Comando.Execute(Parametro); };
        _pilula.GestureRecognizers.Add(toque);
        SemanticProperties.SetHint(this, "Filtra a lista");
        Content = _pilula;
        Pintar();
    }

    public string Texto { get => (string)GetValue(TextoProperty); set => SetValue(TextoProperty, value); }
    public int Quantidade { get => (int)GetValue(QuantidadeProperty); set => SetValue(QuantidadeProperty, value); }
    public bool Selecionado { get => (bool)GetValue(SelecionadoProperty); set => SetValue(SelecionadoProperty, value); }
    public bool MostrarQuantidade { get => (bool)GetValue(MostrarQuantidadeProperty); set => SetValue(MostrarQuantidadeProperty, value); }
    public ICommand? Comando { get => (ICommand?)GetValue(ComandoProperty); set => SetValue(ComandoProperty, value); }
    public object? Parametro { get => GetValue(ParametroProperty); set => SetValue(ParametroProperty, value); }

    private void Pintar()
    {
        if (Selecionado)
        {
            Cor(_pilula, BackgroundColorProperty, "Primaria");
            Cor(_pilula, Border.StrokeProperty, "Primaria");
            Cor(_numero, Label.TextColorProperty, "SobrePrimaria");
            Cor(_texto, Label.TextColorProperty, "SobrePrimaria");
        }
        else
        {
            Cor(_pilula, BackgroundColorProperty, "Superficie");
            Cor(_pilula, Border.StrokeProperty, "Borda");
            Cor(_numero, Label.TextColorProperty, "Texto");
            Cor(_texto, Label.TextColorProperty, "TextoSecundario");
        }
    }
}
