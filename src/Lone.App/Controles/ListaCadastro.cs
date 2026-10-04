using Lone.App.Controles.Grade;
using Lone.App.Plataforma;
using static Lone.App.Controles.RecursosTela;

namespace Lone.App.Controles;

/// <summary>
/// Lista de uma tela de cadastro (padrão de tela de cadastro, 03/10/2026; docs/UX-ARQUITETURA.md; referência: "List
/// Report" do SAP Fiori): título e explicação da página, botão de novo à direita, pesquisa (e filtros da tela ao lado),
/// mensagem, e a lista larga em colunas (GradeLista) com estado vazio. A ficha abre em página própria (FichaCadastro).
/// Liga-se aos nomes comuns de todas as telas de cadastro (CadastroViewModelBase): ConteudoLista, GradeDaLista,
/// AbrirRegistroDaLinhaCommand, Busca, NovoCommand, PodeCriar, Livre, Mensagem, TipoMensagem, ListaVazia e Ocupado.
/// </summary>
public sealed class ListaCadastro : ContentView
{
    public static readonly BindableProperty TituloProperty = BindableProperty.Create(
        nameof(Titulo), typeof(string), typeof(ListaCadastro), string.Empty, propertyChanged: (b, _, n) => ((ListaCadastro)b)._titulo.Text = (string)n);

    public static readonly BindableProperty SubtituloProperty = BindableProperty.Create(
        nameof(Subtitulo), typeof(string), typeof(ListaCadastro), string.Empty,
        propertyChanged: (b, _, n) => { var l = ((ListaCadastro)b)._subtitulo; l.Text = (string)n; l.IsVisible = !string.IsNullOrEmpty((string)n); });

    public static readonly BindableProperty TextoNovoProperty = BindableProperty.Create(
        nameof(TextoNovo), typeof(string), typeof(ListaCadastro), "+  Novo", propertyChanged: (b, _, n) => ((ListaCadastro)b).TrocarTextoNovo((string)n));

    public static readonly BindableProperty MostrarNovoProperty = BindableProperty.Create(
        nameof(MostrarNovo), typeof(bool), typeof(ListaCadastro), true,
        propertyChanged: (b, _, n) => { var l = (ListaCadastro)b; l._novo.IsVisible = (bool)n; l.AtualizarTextoVazio(); });

    public static readonly BindableProperty DicaBuscaProperty = BindableProperty.Create(
        nameof(DicaBusca), typeof(string), typeof(ListaCadastro), "Pesquisar",
        propertyChanged: (b, _, n) => { var l = (ListaCadastro)b; l._busca.Placeholder = (string)n; SemanticProperties.SetDescription(l._busca, (string)n); });

    public static readonly BindableProperty TituloVazioProperty = BindableProperty.Create(
        nameof(TituloVazio), typeof(string), typeof(ListaCadastro), "Nenhum cadastro", propertyChanged: (b, _, n) => ((ListaCadastro)b)._tituloVazio.Text = (string)n);

    public static readonly BindableProperty TextoVazioProperty = BindableProperty.Create(
        nameof(TextoVazio), typeof(string), typeof(ListaCadastro), string.Empty,
        propertyChanged: (b, _, _) => ((ListaCadastro)b).AtualizarTextoVazio());

    /// <summary>Filtros da tela, ao lado da pesquisa (ex.: "Mostrar encerrados", o mapa territorial).</summary>
    public static readonly BindableProperty FiltrosProperty = BindableProperty.Create(
        nameof(Filtros), typeof(View), typeof(ListaCadastro), null, propertyChanged: (b, _, n) =>
        {
            if (n is CaixaMarcar marcar) marcar.VerticalOptions = LayoutOptions.Center;
            if (n is View v) SemEspacoDeGrade(v);
            ((ListaCadastro)b).TrocarFiltros((View?)n);
        });

    /// <summary>Botões da página além do de novo (ficam à esquerda dele).</summary>
    public static readonly BindableProperty AcoesProperty = BindableProperty.Create(
        nameof(Acoes), typeof(View), typeof(ListaCadastro), null, propertyChanged: (b, _, n) => ((ListaCadastro)b)._acoes.Content = (View?)n);

    /// <summary>Conteúdo entre a pesquisa e a lista (ex.: um aviso fixo da tela).</summary>
    public static readonly BindableProperty AvisoProperty = BindableProperty.Create(
        nameof(Aviso), typeof(View), typeof(ListaCadastro), null,
        propertyChanged: (b, _, n) => { var l = (ListaCadastro)b; l._aviso.Content = (View?)n; l._aviso.IsVisible = n is not null; });

    private readonly Label _titulo = new() { MaxLines = 1, LineBreakMode = LineBreakMode.TailTruncation };
    private readonly Label _subtitulo = new() { MaxLines = 2, LineBreakMode = LineBreakMode.TailTruncation, IsVisible = false };
    private readonly Button _novo = new() { VerticalOptions = LayoutOptions.Center };
    private readonly SearchBar _busca = new() { BackgroundColor = Colors.Transparent, Placeholder = "Pesquisar", VerticalOptions = LayoutOptions.Center };
    // Filtros ao lado da pesquisa: alinhados pela base, como na barra de filtros do SAP Fiori (os campos com rótulo em cima
    // ficam com a caixa na mesma linha da pesquisa); com 44 de altura no mínimo, a caixa de marcar fica centrada nela.
    private readonly ContentView _filtros = new() { VerticalOptions = LayoutOptions.End, MinimumHeightRequest = CampoPesquisa.Altura, IsVisible = false };
    private Border? _caixaBusca;
    private readonly ContentView _acoes = new() { VerticalOptions = LayoutOptions.Center };
    private readonly ContentView _aviso = new() { IsVisible = false };
    private readonly Label _tituloVazio = new() { Text = "Nenhum cadastro", HorizontalOptions = LayoutOptions.Center };
    private readonly Label _textoVazio = new() { HorizontalTextAlignment = TextAlignment.Center, IsVisible = false };

    public ListaCadastro()
    {
        Estilo(_titulo, "TituloPagina");
        Estilo(_subtitulo, "SubtituloPagina");
        Estilo(_tituloVazio, "SecaoTitulo");
        Estilo(_textoVazio, "SubtituloPagina");

        _novo.SetBinding(Button.CommandProperty, "NovoCommand");
        _novo.SetBinding(IsEnabledProperty, "Livre");
        TrocarTextoNovo(TextoNovo);
        // Sem permissão para criar, o "+ Novo" some (PodeCriar da base; 03/10/2026).
        SetBinding(MostrarNovoProperty, new Binding("PodeCriar"));

        var cabecalho = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 16 };
        cabecalho.Add(new VerticalStackLayout { Spacing = 4, Children = { _titulo, _subtitulo } });
        // Botões do cabeçalho embaixo, rente à linha da pesquisa: a distância até ela é a mesma da pesquisa até a lista (03/10/2026).
        cabecalho.Add(new HorizontalStackLayout { Spacing = 12, VerticalOptions = LayoutOptions.End, Children = { _acoes, _novo } }, 1);

        _busca.SetBinding(SearchBar.TextProperty, "Busca");
        CampoSemMoldura.Aplicar(_busca);
        var caixaBusca = new Border { StrokeThickness = 1, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 }, Padding = new Thickness(4, 0), Content = _busca,
            HeightRequest = CampoPesquisa.Altura, VerticalOptions = LayoutOptions.End }; // mesma altura dos campos (44)
        Cor(caixaBusca, Border.StrokeProperty, "Borda");
        Cor(caixaBusca, BackgroundColorProperty, "Superficie");
        var pesquisa = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 16 };
        pesquisa.Add(caixaBusca);
        pesquisa.Add(_filtros, 1);
        _caixaBusca = caixaBusca;
        Grid.SetColumnSpan(caixaBusca, 2); // sem filtros, a pesquisa vai até a borda da lista (sem o espaço da coluna vazia)

        var mensagem = new BarraMensagem();
        mensagem.SetBinding(BarraMensagem.MensagemProperty, "Mensagem");
        mensagem.SetBinding(BarraMensagem.TipoProperty, "TipoMensagem");

        var grade = new GradeLista { MoldeParteFixa = new DataTemplate(MoldeTitulo) };
        grade.SetBinding(GradeLista.ConteudoProperty, "ConteudoLista");
        grade.SetBinding(GradeLista.ColunaFixaProperty, "GradeDaLista.ColunaFixa");
        grade.SetBinding(GradeLista.AlturaLinhaProperty, "GradeDaLista.AlturaLinha");
        grade.SetBinding(GradeLista.ComandoTocarProperty, "AbrirRegistroDaLinhaCommand");
        grade.SetBinding(GradeLista.ComandoPonteiroEntrouProperty, "GradeDaLista.EntrarNaLinhaCommand");
        grade.SetBinding(GradeLista.ComandoPonteiroSaiuProperty, "GradeDaLista.SairDaLinhaCommand");
        // Ordenação ao clicar no título da coluna (GradeCadastro: crescente → decrescente → padrão).
        grade.SetBinding(GradeLista.ComandoOrdenarProperty, "GradeDaLista.OrdenarColunaCommand");
        grade.SetBinding(GradeLista.ColunaOrdenadaProperty, "GradeDaLista.ColunaOrdenadaChave");
        grade.SetBinding(GradeLista.OrdemDecrescenteProperty, "GradeDaLista.OrdemDecrescente");

        var vazio = new VerticalStackLayout
        {
            Spacing = 8, Padding = new Thickness(32, 48), Margin = new Thickness(0, 40, 0, 0),
            HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Start,
            Children = { _tituloVazio, _textoVazio }
        };
        vazio.SetBinding(IsVisibleProperty, "ListaVazia");

        var ocupado = new ActivityIndicator { VerticalOptions = LayoutOptions.Start, HorizontalOptions = LayoutOptions.Center, Margin = new Thickness(0, 56) };
        ocupado.SetBinding(ActivityIndicator.IsRunningProperty, "Ocupado");
        ocupado.SetBinding(IsVisibleProperty, "Ocupado");

        var painel = new Border { Padding = 0, Content = new Grid { Children = { grade, vazio, ocupado } } };
        Estilo(painel, "PainelConteudo");

        // Cabeçalho, pesquisa, aviso e mensagem numa pilha: o que está escondido não deixa espaço sobrando (numa grade, cada
        // linha vazia ainda somava o espaçamento, e a lista descia nas telas sem aviso — 03/10/2026).
        var topo = new VerticalStackLayout { Spacing = 16, Children = { cabecalho, pesquisa, _aviso, mensagem } };
        var pagina = new Grid
        {
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) },
            RowSpacing = 16,
            Padding = MargemPagina
        };
        pagina.Add(topo, 0, 0);
        pagina.Add(painel, 0, 1);
        Content = pagina;
    }

    public string Titulo { get => (string)GetValue(TituloProperty); set => SetValue(TituloProperty, value); }
    public string Subtitulo { get => (string)GetValue(SubtituloProperty); set => SetValue(SubtituloProperty, value); }
    public string TextoNovo { get => (string)GetValue(TextoNovoProperty); set => SetValue(TextoNovoProperty, value); }
    public bool MostrarNovo { get => (bool)GetValue(MostrarNovoProperty); set => SetValue(MostrarNovoProperty, value); }
    public string DicaBusca { get => (string)GetValue(DicaBuscaProperty); set => SetValue(DicaBuscaProperty, value); }
    public string TituloVazio { get => (string)GetValue(TituloVazioProperty); set => SetValue(TituloVazioProperty, value); }
    public string TextoVazio { get => (string)GetValue(TextoVazioProperty); set => SetValue(TextoVazioProperty, value); }
    public View? Filtros { get => (View?)GetValue(FiltrosProperty); set => SetValue(FiltrosProperty, value); }
    public View? Acoes { get => (View?)GetValue(AcoesProperty); set => SetValue(AcoesProperty, value); }
    public View? Aviso { get => (View?)GetValue(AvisoProperty); set => SetValue(AvisoProperty, value); }

    /// <summary>
    /// Os campos trazem do estilo o espaço da grade de campos (12 à direita, 8 embaixo; Estilos.xaml). Na linha da pesquisa
    /// esse espaço desalinhava a caixa do filtro da caixa de pesquisa (03/10/2026): aqui ele sai, e o espaçamento fica com a
    /// linha. Mesmo padrão do campo com botão nas fichas (o campo com Padding="0" dentro de um contêiner).
    /// </summary>
    private static void SemEspacoDeGrade(View raiz)
    {
        if (raiz is Campo or CampoEscolha or CaixaMarcar or CampoPesquisa) ((ContentView)raiz).Padding = 0;
        if (raiz is Layout layout)
            foreach (var filho in layout.OfType<View>()) SemEspacoDeGrade(filho);
    }

    /// <summary>
    /// Lista vazia: só o título e uma orientação, sem repetir o botão (um único "+ Novo" por tela, o do alto — decisão de
    /// 03/10/2026, como o "no data" do SAP Fiori). Sem texto próprio da tela, orienta a usar o botão do alto.
    /// </summary>
    private void AtualizarTextoVazio()
    {
        var texto = TextoVazio;
        if (string.IsNullOrEmpty(texto) && MostrarNovo)
        {
            var botao = TextoNovo.Trim().Replace("+  ", "+ ");
            var primeiro = botao.TrimStart('+', ' ').StartsWith("Nova", StringComparison.Ordinal) ? "a primeira" : "o primeiro";
            texto = $"Use \"{botao}\", no alto da página, para incluir {primeiro}.";
        }
        _textoVazio.Text = texto;
        _textoVazio.IsVisible = !string.IsNullOrEmpty(texto);
    }

    /// <summary>Com filtros, a pesquisa divide a linha com eles; sem filtros, ocupa a largura toda (alinhada à lista).</summary>
    private void TrocarFiltros(View? filtros)
    {
        _filtros.Content = filtros;
        _filtros.IsVisible = filtros is not null;
        if (_caixaBusca is not null) Grid.SetColumnSpan(_caixaBusca, filtros is null ? 2 : 1);
    }

    private void TrocarTextoNovo(string texto)
    {
        _novo.Text = texto;
        AtualizarTextoVazio();
        SemanticProperties.SetDescription(_novo, texto.TrimStart('+', ' '));
        ToolTipProperties.SetText(_novo, texto.TrimStart('+', ' ') + " (Ctrl+N)");
    }

    /// <summary>
    /// Parte fixa da linha: título em negrito e subtítulo (LinhaCadastro), como o nome e o código em Pessoas. Nas listas em
    /// árvore, o recuo e o marcador ("▾" / "•") vêm antes do título.
    /// </summary>
    private static object MoldeTitulo()
    {
        var titulo = new Label { FontAttributes = FontAttributes.Bold, LineBreakMode = LineBreakMode.TailTruncation };
        Estilo(titulo, "TextoTabela");
        titulo.SetBinding(Label.TextProperty, "Titulo");
        titulo.SetBinding(ToolTipProperties.TextProperty, "Titulo");
        var subtitulo = new Label { FontSize = 13, LineBreakMode = LineBreakMode.TailTruncation };
        Estilo(subtitulo, "TextoTabelaSecundario");
        subtitulo.SetBinding(Label.TextProperty, "Subtitulo");
        subtitulo.SetBinding(IsVisibleProperty, "TemSubtitulo");
        var recuo = new BoxView { HeightRequest = 1, Color = Colors.Transparent };
        recuo.SetBinding(WidthRequestProperty, "Recuo");
        var marcador = new Label { VerticalOptions = LayoutOptions.Center, Margin = new Thickness(0, 0, 6, 0) };
        Estilo(marcador, "TextoTabelaSecundario");
        marcador.SetBinding(Label.TextProperty, "Marcador");
        marcador.SetBinding(IsVisibleProperty, "TemMarcador");
        var textos = new VerticalStackLayout { VerticalOptions = LayoutOptions.Center, Spacing = 2, Children = { titulo, subtitulo } };
        var linha = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
            Padding = new Thickness(16, 0, 8, 0)
        };
        linha.Add(recuo, 0);
        linha.Add(marcador, 1);
        linha.Add(textos, 2);
        return linha;
    }
}
