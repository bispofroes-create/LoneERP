using static Lone.App.Controles.RecursosTela;

namespace Lone.App.Controles;

/// <summary>
/// Barra de ações da ficha (padrão aprovado em 03/10/2026, referência rodapé do SAP Fiori; docs/UX-ARQUITETURA.md):
/// estado à esquerda só quando há o que avisar ("● Alterações não salvas" / "Novo cadastro, ainda não salvo");
/// Salvar (destaque) e Descartar à direita, sempre no mesmo lugar, desligados sem alteração. Fechar fica no topo da ficha.
/// Liga-se aos nomes comuns de todas as telas de cadastro (CadastroViewModelBase): MostrarEstadoFicha, EstadoFicha,
/// SalvarCommand, PodeSalvarAgora, DescartarCommand, Livre e Ocupado. A ficha de Pessoas tem a sua (com o motivo).
/// Telas com ações próprias (Metas, Operações territoriais, Transferências) trocam o lado direito por <see cref="Acoes"/>.
/// </summary>
public sealed class BarraFicha : ContentView
{
    /// <summary>Botões do lado direito no lugar de Salvar e Descartar (ficha com ações próprias).</summary>
    public static readonly BindableProperty AcoesProperty = BindableProperty.Create(
        nameof(Acoes), typeof(View), typeof(BarraFicha), null,
        propertyChanged: (b, _, n) => { var barra = (BarraFicha)b; barra._direita.Content = (View?)n ?? barra._padrao; });

    /// <summary>
    /// "Salvar e novo" ao lado de Salvar (cadastros auxiliares, para cadastrar vários seguidos; padrão do botão Novo,
    /// 03/10/2026; como o "Save &amp; New" do Dynamics). Só abre o novo se salvou (a ficha ficou sem alterações).
    /// </summary>
    public static readonly BindableProperty MostrarSalvarENovoProperty = BindableProperty.Create(
        nameof(MostrarSalvarENovo), typeof(bool), typeof(BarraFicha), false,
        propertyChanged: (b, _, n) => ((BarraFicha)b)._salvarENovo.IsVisible = (bool)n);

    private readonly Button _salvarENovo = new() { Text = "Salvar e novo", VerticalOptions = LayoutOptions.Center, IsVisible = false };
    private readonly ContentView _direita = new() { VerticalOptions = LayoutOptions.Center };
    private readonly View _padrao;

    public View? Acoes { get => (View?)GetValue(AcoesProperty); set => SetValue(AcoesProperty, value); }
    public bool MostrarSalvarENovo { get => (bool)GetValue(MostrarSalvarENovoProperty); set => SetValue(MostrarSalvarENovoProperty, value); }

    public BarraFicha()
    {
        Cor(this, VisualElement.BackgroundColorProperty, "Superficie");

        var ocupado = new ActivityIndicator { VerticalOptions = LayoutOptions.Center, WidthRequest = 18, HeightRequest = 18 };
        ocupado.SetBinding(ActivityIndicator.IsRunningProperty, "Ocupado");
        ocupado.SetBinding(IsVisibleProperty, "Ocupado");

        var ponto = new Label { Text = "●", FontSize = 12, VerticalOptions = LayoutOptions.Center };
        Cor(ponto, Label.TextColorProperty, "Aviso", "AvisoFundo");
        var estado = new Label { VerticalOptions = LayoutOptions.Center };
        Estilo(estado, "Secundario");
        estado.SetBinding(Label.TextProperty, "EstadoFicha");
        var avisoEstado = new HorizontalStackLayout { Spacing = 6, VerticalOptions = LayoutOptions.Center, Children = { ponto, estado } };
        avisoEstado.SetBinding(IsVisibleProperty, "MostrarEstadoFicha");

        var salvar = new Button { Text = "Salvar", VerticalOptions = LayoutOptions.Center };
        salvar.SetBinding(Button.CommandProperty, "SalvarCommand");
        salvar.SetBinding(IsEnabledProperty, "PodeSalvarAgora");
        var descartar = new Button { Text = "Descartar", VerticalOptions = LayoutOptions.Center };
        Estilo(descartar, "BotaoSecundario");
        descartar.SetBinding(Button.CommandProperty, "DescartarCommand");
        descartar.SetBinding(IsEnabledProperty, "Livre");
        ToolTipProperties.SetText(descartar, "Desfaz as alterações não salvas (não exclui nada)");

        var linha = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            ColumnSpacing = 16,
            MinimumHeightRequest = 44
        };
        linha.Add(new HorizontalStackLayout { Spacing = 8, VerticalOptions = LayoutOptions.Center, Children = { ocupado, avisoEstado } });
        Estilo(_salvarENovo, "BotaoSecundario");
        _salvarENovo.SetBinding(IsEnabledProperty, "PodeSalvarAgora");
        ToolTipProperties.SetText(_salvarENovo, "Salva e já abre uma ficha nova");
        _salvarENovo.Clicked += async (_, _) =>
        {
            if (salvar.Command is CommunityToolkit.Mvvm.Input.IAsyncRelayCommand assincrono) await assincrono.ExecuteAsync(null);
            else salvar.Command?.Execute(null);
            if (BindingContext is Lone.Cliente.ViewModels.Cadastros.IMestreDetalhe tela && tela.Editando && !tela.TemAlteracoes && tela.PodeCriar)
                tela.NovoCommand.Execute(null);
        };
        _padrao = new HorizontalStackLayout { Spacing = 8, VerticalOptions = LayoutOptions.Center, Children = { salvar, _salvarENovo, descartar } };
        _direita.Content = _padrao;
        linha.Add(_direita, 1);

        var divisoria = new BoxView { HeightRequest = 1 };
        Cor(divisoria, BoxView.ColorProperty, "BordaSutil");
        Content = new VerticalStackLayout
        {
            Children =
            {
                divisoria,
                new ContentView { Padding = DeviceInfo.Idiom == DeviceIdiom.Phone ? new Thickness(16, 12) : new Thickness(32, 12), Content = linha }
            }
        };
    }
}
