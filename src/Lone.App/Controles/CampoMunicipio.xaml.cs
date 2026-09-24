namespace Lone.App.Controles;

/// <summary>
/// Escolha de município (UF + autocompletar). Só aparência: a lista, o filtro e a regra "só vale o que foi
/// escolhido" ficam no SeletorMunicipio (Lone.Cliente), que é o BindingContext deste controle.
/// </summary>
public partial class CampoMunicipio : ContentView
{
    public static readonly BindableProperty RotuloProperty = BindableProperty.Create(
        nameof(Rotulo), typeof(string), typeof(CampoMunicipio), "Município",
        propertyChanged: (b, _, n) => ((CampoMunicipio)b).RotuloMunicipio.Text = (string)n);

    public CampoMunicipio()
    {
        InitializeComponent();
    }

    public string Rotulo { get => (string)GetValue(RotuloProperty); set => SetValue(RotuloProperty, value); }

    /// <summary>Tocar numa sugestão escolhe o município (o item da lista não enxerga o comando do seletor).</summary>
    private void Sugestao_Clicked(object? sender, EventArgs e)
    {
        if (BindingContext is Lone.Cliente.ViewModels.Comum.SeletorMunicipio seletor &&
            sender is BindableObject { BindingContext: Lone.Contracts.Municipios.MunicipioDto municipio })
            seletor.EscolherCommand.Execute(municipio);
    }
}
