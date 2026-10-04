using System.ComponentModel;
using Lone.Cliente.ViewModels.Comum;

namespace Lone.App.Controles;

/// <summary>
/// Resumo de erros fixo de um formulário (Lone Contextual, Fase 1), fora da rolagem: "Não foi possível salvar a pessoa ·
/// Corrija N informações" e a lista. Linha com campo = link (→, toque, Enter ou Espaço) que leva ao campo; linha sem campo
/// = erro geral, só texto. Mais de 4 erros: a lista rola dentro do resumo. Some sem erros. O BindingContext é o
/// <see cref="ResumoValidacao"/>; o Narrador anuncia quando aparece.
/// </summary>
public sealed class BarraValidacao : ContentView
{
    private const double AlturaListaLonga = 4 * 30;

    private readonly ScrollView _rolagemLista = new();
    private ResumoValidacao? _resumo;

    public BarraValidacao()
    {
        var titulo = new Label { FontAttributes = FontAttributes.Bold, FontSize = 14, LineBreakMode = LineBreakMode.WordWrap };
        titulo.SetBinding(Label.TextProperty, new Binding(nameof(ResumoValidacao.Titulo), stringFormat: "⚠  {0}"));
        Cor(titulo, "Erro");
        var contagem = new Label { FontSize = 13 };
        contagem.SetBinding(Label.TextProperty, nameof(ResumoValidacao.Contagem));

        var lista = new VerticalStackLayout { Spacing = 0 };
        lista.SetBinding(BindableLayout.ItemsSourceProperty, nameof(ResumoValidacao.Itens));
        BindableLayout.SetItemTemplate(lista, new DataTemplate(Linha));
        _rolagemLista.Content = lista;

        var borda = new Border
        {
            Padding = new Thickness(16, 10),
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            Content = new VerticalStackLayout { Spacing = 2, Children = { titulo, contagem, _rolagemLista } }
        };
        borda.SetAppThemeColor(BackgroundColorProperty, Recurso("ErroFundo") ?? Colors.MistyRose, Recurso("ErroFundoEscuro") ?? Colors.Maroon);
        borda.SetAppThemeColor(Border.StrokeProperty, Recurso("Erro") ?? Colors.DarkRed, Recurso("Erro") ?? Colors.DarkRed);
        Content = borda;
        SemanticProperties.SetHeadingLevel(titulo, SemanticHeadingLevel.Level2);
        SetBinding(IsVisibleProperty, new Binding(nameof(ResumoValidacao.Visivel)));
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        if (_resumo is not null) _resumo.PropertyChanged -= Resumo_PropertyChanged;
        _resumo = BindingContext as ResumoValidacao;
        if (_resumo is not null) _resumo.PropertyChanged += Resumo_PropertyChanged;
        AjustarAltura();
    }

    private void Resumo_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ResumoValidacao.ListaLonga)) AjustarAltura();
        if (e.PropertyName == nameof(ResumoValidacao.Visivel) && _resumo is { Visivel: true } resumo)
            SemanticScreenReader.Announce($"{resumo.Titulo}. {resumo.Contagem}");
    }

    private void AjustarAltura() => _rolagemLista.MaximumHeightRequest = _resumo?.ListaLonga == true ? AlturaListaLonga : double.PositiveInfinity;

    /// <summary>
    /// Uma linha: a mensagem com a seta logo depois do texto (perto de onde se lê, não no fim da faixa) e, se tiver campo, um
    /// botão transparente por cima (foco, Enter, Narrador).
    /// </summary>
    private View Linha()
    {
        var texto = new Span();
        texto.SetBinding(Span.TextProperty, new Binding(nameof(ItemValidacao.Mensagem), stringFormat: "•  {0}"));
        var seta = new Span { FontAttributes = FontAttributes.Bold };
        seta.SetBinding(Span.TextProperty, nameof(ItemValidacao.Seta));
        if (Recurso("Primaria") is { } link) seta.TextColor = link;
        var mensagem = new Label
        {
            FontSize = 13, LineBreakMode = LineBreakMode.WordWrap, VerticalOptions = LayoutOptions.Center,
            FormattedText = new FormattedString { Spans = { texto, seta } }
        };

        var botao = new Button
        {
            Text = string.Empty, BackgroundColor = Colors.Transparent, BorderWidth = 0, Padding = 0, CornerRadius = 0,
            MinimumHeightRequest = 0, MinimumWidthRequest = 0, HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill
        };
        botao.SetBinding(IsVisibleProperty, nameof(ItemValidacao.TemCampo));
        botao.SetBinding(SemanticProperties.DescriptionProperty, nameof(ItemValidacao.Descricao));
        botao.SetBinding(ToolTipProperties.TextProperty, nameof(ItemValidacao.Descricao));
        botao.Clicked += (s, _) =>
        {
            if (s is BindableObject { BindingContext: ItemValidacao item }) _resumo?.IrParaCommand.Execute(item);
        };

        // A linha ocupa só a largura do texto: o toque fica onde está a mensagem.
        var linha = new Grid { Padding = new Thickness(0, 4), HorizontalOptions = LayoutOptions.Start };
        linha.Add(mensagem);
        linha.Add(botao);
        return linha;
    }

    private static void Cor(Label rotulo, string chave)
    {
        if (Recurso(chave) is { } cor) rotulo.TextColor = cor;
    }

    private static Color? Recurso(string chave) =>
        Application.Current?.Resources.TryGetValue(chave, out var valor) == true && valor is Color cor ? cor : null;
}
