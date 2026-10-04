namespace Lone.App.Controles;

/// <summary>
/// Escolha de município (UF + autocompletar). Só aparência: a lista, o filtro e a regra "só vale o que foi
/// escolhido" ficam no SeletorMunicipio (Lone.Cliente), que é o BindingContext deste controle.
/// </summary>
public partial class CampoMunicipio : ContentView, ICampoValidavel
{
    public static readonly BindableProperty RotuloProperty = BindableProperty.Create(
        nameof(Rotulo), typeof(string), typeof(CampoMunicipio), "Município",
        propertyChanged: (b, _, n) => ((CampoMunicipio)b).RotuloMunicipio.Text = (string)n);

    /// <summary>UF em cima e município embaixo, cada um na largura toda (para espaços estreitos, como o painel de filtros).</summary>
    public static readonly BindableProperty EmpilhadoProperty = BindableProperty.Create(
        nameof(Empilhado), typeof(bool), typeof(CampoMunicipio), false,
        propertyChanged: (b, _, n) => ((CampoMunicipio)b).Arrumar((bool)n));

    public CampoMunicipio()
    {
        InitializeComponent();
        _marca = new MarcaDeErro(EntradaMunicipio);
        Pilha.Children.Insert(1, _marca.Rotulo); // logo abaixo da UF e do município
    }

    private readonly MarcaDeErro _marca;

    /// <summary>Marca de erro (Lone Contextual, Fase 1): borda no município e "⚠ mensagem" embaixo; nulo tira.</summary>
    public void MostrarErro(string? mensagem)
    {
        _marca.Mostrar(mensagem);
        EscolhaUf.MostrarErro(null); // a mensagem fica uma vez só, embaixo dos dois
    }

    /// <summary>Foco no município; sem UF escolhida (município desligado), na UF.</summary>
    public bool Focar() => EntradaMunicipio.IsEnabled ? EntradaMunicipio.Focus() : EscolhaUf.Focar();

    public string Rotulo { get => (string)GetValue(RotuloProperty); set => SetValue(RotuloProperty, value); }
    public bool Empilhado { get => (bool)GetValue(EmpilhadoProperty); set => SetValue(EmpilhadoProperty, value); }

    private void Arrumar(bool empilhado)
    {
        Linha.ColumnDefinitions = empilhado ? [new ColumnDefinition(GridLength.Star)] : [new ColumnDefinition(96), new ColumnDefinition(GridLength.Star)];
        Linha.RowDefinitions = empilhado ? [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto)] : [];
        Grid.SetColumn(BlocoMunicipio, empilhado ? 0 : 1);
        Grid.SetRow(BlocoMunicipio, empilhado ? 1 : 0);
        EscolhaUf.WidthRequest = empilhado ? 120 : -1;
        EscolhaUf.HorizontalOptions = empilhado ? LayoutOptions.Start : LayoutOptions.Fill;
    }

    /// <summary>Tocar numa sugestão escolhe o município (o item da lista não enxerga o comando do seletor).</summary>
    private void Sugestao_Clicked(object? sender, EventArgs e)
    {
        if (BindingContext is Lone.Cliente.ViewModels.Comum.SeletorMunicipio seletor &&
            sender is BindableObject { BindingContext: Lone.Contracts.Municipios.MunicipioDto municipio })
            seletor.EscolherCommand.Execute(municipio);
    }
}
