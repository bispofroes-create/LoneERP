using static Lone.App.Controles.RecursosTela;

namespace Lone.App.Controles;

/// <summary>
/// Ficha de uma tela de cadastro em página própria (padrão de tela de cadastro, 03/10/2026; docs/UX-ARQUITETURA.md):
/// "← Voltar para ..." no topo, título e situação do registro, mensagem, o formulário num painel com a largura da página
/// (mesma margem dos dois lados) e a barra de ações da ficha (BarraFicha) embaixo.
/// Liga-se aos nomes comuns: FecharFichaCommand, Formulario.Titulo, Formulario.SituacaoTexto, Mensagem e TipoMensagem
/// (o título e a situação podem vir de outro caminho: <see cref="CaminhoTitulo"/>, <see cref="CaminhoSituacao"/>).
/// O formulário é o conteúdo (Corpo) do controle em XAML.
/// </summary>
[ContentProperty(nameof(Corpo))]
public sealed class FichaCadastro : ContentView
{
    public static readonly BindableProperty TextoVoltarProperty = BindableProperty.Create(
        nameof(TextoVoltar), typeof(string), typeof(FichaCadastro), "Voltar",
        propertyChanged: (b, _, n) => ((FichaCadastro)b)._voltar.Text = "←  " + (string)n);

    public static readonly BindableProperty CorpoProperty = BindableProperty.Create(
        nameof(Corpo), typeof(View), typeof(FichaCadastro), null, propertyChanged: (b, _, n) => ((FichaCadastro)b)._painel.Content = (View?)n);

    /// <summary>Sem o painel em volta do formulário (ficha com seções em painéis próprios).</summary>
    public static readonly BindableProperty SemPainelProperty = BindableProperty.Create(
        nameof(SemPainel), typeof(bool), typeof(FichaCadastro), false, propertyChanged: (b, _, n) => ((FichaCadastro)b).TrocarPainel((bool)n));

    /// <summary>Mostra a barra Salvar/Descartar (telas cuja ficha só consulta ou tem ações próprias desligam).</summary>
    public static readonly BindableProperty MostrarBarraProperty = BindableProperty.Create(
        nameof(MostrarBarra), typeof(bool), typeof(FichaCadastro), true, propertyChanged: (b, _, n) => ((FichaCadastro)b)._barra.IsVisible = (bool)n);

    /// <summary>Botões da barra de baixo no lugar de Salvar e Descartar (ficha com ações próprias).</summary>
    public static readonly BindableProperty AcoesBarraProperty = BindableProperty.Create(
        nameof(AcoesBarra), typeof(View), typeof(FichaCadastro), null, propertyChanged: (b, _, n) => ((FichaCadastro)b)._barra.Acoes = (View?)n);

    /// <summary>De onde vem o título da ficha (padrão: Formulario.Titulo).</summary>
    public static readonly BindableProperty CaminhoTituloProperty = BindableProperty.Create(
        nameof(CaminhoTitulo), typeof(string), typeof(FichaCadastro), "Formulario.Titulo",
        propertyChanged: (b, _, n) => ((FichaCadastro)b).LigarTitulo((string)n));

    /// <summary>De onde vem a linha de situação abaixo do título (padrão: Formulario.SituacaoTexto).</summary>
    public static readonly BindableProperty CaminhoSituacaoProperty = BindableProperty.Create(
        nameof(CaminhoSituacao), typeof(string), typeof(FichaCadastro), "Formulario.SituacaoTexto",
        propertyChanged: (b, _, n) => ((FichaCadastro)b).LigarSituacao((string)n));

    /// <summary>Mostra "Salvar e novo" na barra (cadastros auxiliares).</summary>
    public static readonly BindableProperty MostrarSalvarENovoProperty = BindableProperty.Create(
        nameof(MostrarSalvarENovo), typeof(bool), typeof(FichaCadastro), false,
        propertyChanged: (b, _, n) => ((FichaCadastro)b)._barra.MostrarSalvarENovo = (bool)n);

    private readonly Label _titulo = new() { LineBreakMode = LineBreakMode.TailTruncation };
    private readonly Label _situacao = new() { LineBreakMode = LineBreakMode.WordWrap };
    private readonly ScrollView _rolagem = new();
    private readonly Button _voltar = new() { Text = "←  Voltar", HorizontalOptions = LayoutOptions.Start };
    private readonly Border _painel = new() { Margin = new Thickness(0, 8, 0, 0) };
    private readonly BarraFicha _barra = new();

    public FichaCadastro()
    {
        Estilo(_voltar, "BotaoLink");
        _voltar.SetBinding(Button.CommandProperty, "FecharFichaCommand");

        Estilo(_titulo, "TituloPagina");
        LigarTitulo(CaminhoTitulo);
        Estilo(_situacao, "Secundario");
        LigarSituacao(CaminhoSituacao);
        var mensagem = new BarraMensagem();
        mensagem.SetBinding(BarraMensagem.MensagemProperty, "Mensagem");
        mensagem.SetBinding(BarraMensagem.TipoProperty, "TipoMensagem");
        Estilo(_painel, "PainelConteudo");

        var conteudo = new VerticalStackLayout { Padding = MargemPagina, Spacing = 10, Children = { _voltar, _titulo, _situacao, mensagem, _painel } };
        var pagina = new Grid { RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) } };
        _rolagem.Content = conteudo;
        pagina.Add(_rolagem);
        pagina.Add(_barra, 0, 1);
        Content = pagina;
    }

    public string CaminhoTitulo { get => (string)GetValue(CaminhoTituloProperty); set => SetValue(CaminhoTituloProperty, value); }
    public string CaminhoSituacao { get => (string)GetValue(CaminhoSituacaoProperty); set => SetValue(CaminhoSituacaoProperty, value); }

    /// <summary>Leva a ficha ao topo (ex.: quando aparece uma mensagem lá em cima).</summary>
    public Task RolarParaOTopoAsync() => _rolagem.ScrollToAsync(0, 0, animated: true);

    /// <summary>Caminho vazio: a ficha mostra o título dentro do próprio conteúdo (ex.: Transferências).</summary>
    private void LigarTitulo(string caminho)
    {
        _titulo.IsVisible = caminho.Length > 0;
        if (caminho.Length > 0) _titulo.SetBinding(Label.TextProperty, caminho);
        else _titulo.RemoveBinding(Label.TextProperty);
    }

    private void LigarSituacao(string caminho)
    {
        if (caminho.Length == 0)
        {
            _situacao.RemoveBinding(Label.TextProperty);
            _situacao.RemoveBinding(IsVisibleProperty);
            _situacao.IsVisible = false;
            return;
        }
        _situacao.SetBinding(Label.TextProperty, caminho);
        _situacao.SetBinding(IsVisibleProperty, new Binding(caminho, converter: new TemTexto()));
    }

    public string TextoVoltar { get => (string)GetValue(TextoVoltarProperty); set => SetValue(TextoVoltarProperty, value); }
    public View? Corpo { get => (View?)GetValue(CorpoProperty); set => SetValue(CorpoProperty, value); }
    public bool SemPainel { get => (bool)GetValue(SemPainelProperty); set => SetValue(SemPainelProperty, value); }
    public bool MostrarSalvarENovo { get => (bool)GetValue(MostrarSalvarENovoProperty); set => SetValue(MostrarSalvarENovoProperty, value); }
    public View? AcoesBarra { get => (View?)GetValue(AcoesBarraProperty); set => SetValue(AcoesBarraProperty, value); }
    public bool MostrarBarra { get => (bool)GetValue(MostrarBarraProperty); set => SetValue(MostrarBarraProperty, value); }

    private void TrocarPainel(bool semPainel)
    {
        if (semPainel)
        {
            _painel.ClearValue(StyleProperty);
            _painel.StrokeThickness = 0;
            _painel.Padding = 0;
            _painel.BackgroundColor = Colors.Transparent;
        }
        else Estilo(_painel, "PainelConteudo");
    }

    private sealed class TemTexto : IValueConverter
    {
        public object Convert(object? valor, Type tipo, object? parametro, System.Globalization.CultureInfo cultura) => valor is string s && s.Length > 0;
        public object ConvertBack(object? valor, Type tipo, object? parametro, System.Globalization.CultureInfo cultura) => throw new NotSupportedException();
    }
}
