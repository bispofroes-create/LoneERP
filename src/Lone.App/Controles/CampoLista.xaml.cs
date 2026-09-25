using Lone.Cliente.ViewModels.Comum;

namespace Lone.App.Controles;

/// <summary>
/// Escolha de um item de lista com autocompletar. Só aparência: o filtro e a regra "só vale o que foi escolhido"
/// ficam no SeletorDeLista (Lone.Cliente), que é o BindingContext deste controle.
/// </summary>
public partial class CampoLista : ContentView
{
    public static readonly BindableProperty RotuloProperty = BindableProperty.Create(
        nameof(Rotulo), typeof(string), typeof(CampoLista), string.Empty,
        propertyChanged: (b, _, n) => ((CampoLista)b).RotuloCampo.Text = (string)n);

    public CampoLista()
    {
        InitializeComponent();
    }

    public string Rotulo { get => (string)GetValue(RotuloProperty); set => SetValue(RotuloProperty, value); }

    /// <summary>Tocar numa sugestão escolhe o item (o item da lista não enxerga o comando do seletor).</summary>
    private void Sugestao_Clicked(object? sender, EventArgs e)
    {
        if (BindingContext is SeletorDeLista seletor && sender is BindableObject { BindingContext: ItemSeletor item })
            seletor.EscolherCommand.Execute(item);
    }
}
