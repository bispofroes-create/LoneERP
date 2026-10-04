using System.Windows.Input;
using Lone.App.Plataforma;
using static Lone.App.Controles.RecursosTela;

namespace Lone.App.Controles;

/// <summary>
/// Campo de pesquisa com botão (ex.: "Cliente" + Buscar), no mesmo tamanho dos outros campos (03/10/2026): rótulo em
/// cima, caixa de 44 de altura com moldura única do Lone e o botão dentro da mesma linha. Referência: densidade "cozy" do
/// SAP Fiori (44 px) — todos os campos de uma página com a mesma altura, nunca densidades misturadas.
/// Ocupa uma coluna da grade de campos (regra: campo + botão = 1 coluna).
/// </summary>
public sealed class CampoPesquisa : ContentView
{
    public const double Altura = 44;

    public static readonly BindableProperty RotuloProperty = BindableProperty.Create(
        nameof(Rotulo), typeof(string), typeof(CampoPesquisa), string.Empty,
        propertyChanged: (b, _, n) => { var c = (CampoPesquisa)b; c._rotulo.Text = (string)n; c._rotulo.IsVisible = !string.IsNullOrEmpty((string)n); });

    public static readonly BindableProperty TextoProperty = BindableProperty.Create(
        nameof(Texto), typeof(string), typeof(CampoPesquisa), string.Empty, BindingMode.TwoWay,
        propertyChanged: (b, _, n) => { var c = (CampoPesquisa)b; if (c._busca.Text != (string?)n) c._busca.Text = (string?)n ?? string.Empty; });

    public static readonly BindableProperty DicaProperty = BindableProperty.Create(
        nameof(Dica), typeof(string), typeof(CampoPesquisa), string.Empty,
        propertyChanged: (b, _, n) => { var c = (CampoPesquisa)b; c._busca.Placeholder = (string)n; SemanticProperties.SetDescription(c._busca, (string)n); });

    public static readonly BindableProperty ComandoProperty = BindableProperty.Create(
        nameof(Comando), typeof(ICommand), typeof(CampoPesquisa), null,
        propertyChanged: (b, _, n) => { var c = (CampoPesquisa)b; c._busca.SearchCommand = (ICommand?)n; c._botao.Command = (ICommand?)n; });

    public static readonly BindableProperty TextoBotaoProperty = BindableProperty.Create(
        nameof(TextoBotao), typeof(string), typeof(CampoPesquisa), "Buscar", propertyChanged: (b, _, n) => ((CampoPesquisa)b)._botao.Text = (string)n);

    private readonly Label _rotulo = new() { IsVisible = false };
    private readonly SearchBar _busca = new() { BackgroundColor = Colors.Transparent, VerticalOptions = LayoutOptions.Center };
    private readonly Button _botao = new() { Text = "Buscar", VerticalOptions = LayoutOptions.Center };

    public CampoPesquisa()
    {
        Estilo(_rotulo, "Rotulo");
        Estilo(_botao, "BotaoSecundario");
        _botao.HeightRequest = Altura;
        _busca.TextChanged += (_, e) => { if (Texto != e.NewTextValue) Texto = e.NewTextValue ?? string.Empty; };
        CampoSemMoldura.Aplicar(_busca);
        var caixa = new Border
        {
            StrokeThickness = 1, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            Padding = new Thickness(4, 0), HeightRequest = Altura, Content = _busca
        };
        Cor(caixa, Border.StrokeProperty, "Borda");
        Cor(caixa, BackgroundColorProperty, "Superficie");
        var linha = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 8 };
        linha.Add(caixa);
        linha.Add(_botao, 1);
        Content = new VerticalStackLayout { Spacing = 2, Children = { _rotulo, linha } };
    }

    public string Rotulo { get => (string)GetValue(RotuloProperty); set => SetValue(RotuloProperty, value); }
    public string Texto { get => (string)GetValue(TextoProperty); set => SetValue(TextoProperty, value); }
    public string Dica { get => (string)GetValue(DicaProperty); set => SetValue(DicaProperty, value); }
    public ICommand? Comando { get => (ICommand?)GetValue(ComandoProperty); set => SetValue(ComandoProperty, value); }
    public string TextoBotao { get => (string)GetValue(TextoBotaoProperty); set => SetValue(TextoBotaoProperty, value); }
}
