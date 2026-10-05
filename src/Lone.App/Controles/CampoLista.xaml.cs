using Lone.Cliente.ViewModels.Comum;

namespace Lone.App.Controles;

/// <summary>
/// Escolha de um item de lista com autocompletar. Só aparência: o filtro e a regra "só vale o que foi escolhido"
/// ficam no SeletorDeLista (Lone.Cliente), que é o BindingContext deste controle.
/// </summary>
public partial class CampoLista : ContentView, ICampoValidavel
{
    public static readonly BindableProperty RotuloProperty = BindableProperty.Create(
        nameof(Rotulo), typeof(string), typeof(CampoLista), string.Empty,
        propertyChanged: (b, _, n) => ((CampoLista)b).RotuloCampo.Text = (string)n);

    public CampoLista()
    {
        InitializeComponent();
        _marca = new MarcaDeErro(Entrada);
        Pilha.Children.Insert(2, _marca.Mensagens); // logo abaixo da caixa, antes do aviso e das sugestões
    }

    private readonly MarcaDeErro _marca;

    /// <summary>Marca de erro (Lone Contextual, Fase 1): borda e "⚠ mensagem" embaixo; nulo tira.</summary>
    public void MostrarErro(string? mensagem) => _marca.Mostrar(mensagem);

    /// <summary>Destaque de alteração: fundo azul-claro e "● Veio da Receita... · antes: ..." embaixo; nulo tira.</summary>
    public void MostrarDestaque(global::Lone.Cliente.ViewModels.Comum.DestaqueCampo? destaque) => _marca.MostrarDestaque(destaque);

    public bool Focar() => Entrada.IsEnabled && Entrada.Focus();

    public string Rotulo { get => (string)GetValue(RotuloProperty); set => SetValue(RotuloProperty, value); }

    /// <summary>Tocar numa sugestão escolhe o item (o item da lista não enxerga o comando do seletor).</summary>
    private void Sugestao_Clicked(object? sender, EventArgs e)
    {
        if (BindingContext is SeletorDeLista seletor && sender is BindableObject { BindingContext: ItemSeletor item })
            seletor.EscolherCommand.Execute(item);
    }
}
