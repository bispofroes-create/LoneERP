using Lone.Cliente.ViewModels.Cadastros;

namespace Lone.App.Views.Pessoas;

/// <summary>Aba da ficha de pessoa. Sem lógica: tudo está no PessoasViewModel e na PessoaFormulario.</summary>
public partial class SecaoAdicionaisView : ContentView
{
    public SecaoAdicionaisView()
    {
        InitializeComponent();
    }
}

/// <summary>Escolhe o controle de cada campo personalizado pelo jeito de desenhar (texto, texto longo, escolha).</summary>
public sealed class SeletorModeloCampo : DataTemplateSelector
{
    public DataTemplate? Texto { get; set; }
    public DataTemplate? TextoLongo { get; set; }
    public DataTemplate? Escolha { get; set; }

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container) =>
        (item as CampoPersonalizadoFormulario)?.Visual switch
        {
            VisualCampo.TextoLongo => TextoLongo!,
            VisualCampo.Escolha => Escolha!,
            _ => Texto!
        };
}
