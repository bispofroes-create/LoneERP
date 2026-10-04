using System.Windows.Input;
using Lone.Cliente.Grade;

namespace Lone.App.Controles.Grade;

/// <summary>
/// Grade de listagem do Lone (infraestrutura, sem conhecer nenhuma tela):
/// <list type="bullet">
/// <item><b>uma</b> lista virtualizada (motor interno <see cref="MotorCollectionView"/>), do tamanho da grade: a barra
/// vertical fica sempre na borda visível;</item>
/// <item><b>uma</b> rolagem lateral (barra própria embaixo, Shift + roda e roda lateral): desloca só a faixa das células
/// de cada linha e do cabeçalho (<c>TranslationX</c>, recortada); a parte fixa (molde da tela, ex.: o nome) não se mexe.
/// Sem segunda lista;</item>
/// <item>colunas com largura da própria coluna (<see cref="CalculadoraLarguras"/>), recalculada só quando a largura
/// da grade muda (tolerância de 1 ponto), sem reconstruir linhas;</item>
/// <item>altura da linha da grade (densidade), sem reconstruir linhas;</item>
/// <item>troca atômica: colunas e linhas chegam juntas em <see cref="Conteudo"/>;</item>
/// <item>seleção e destaque vêm do modelo (pela chave), nunca da seleção nativa do motor.</item>
/// </list>
/// Não lê dados, não pagina, não filtra e não guarda estado de navegação: repassa toques e ponteiro à tela.
/// </summary>
public sealed class GradeLista : ContentView
{
    public const double AlturaCabecalho = 40;
    public const double AlturaPadrao = 56;

    /// <summary>Altura da linha de filtro embaixo dos títulos (quando ligada).</summary>
    public const double AlturaLinhaFiltro = 46;

    /// <summary>Política de confirmação da rolagem (como a atual da navegação: até 40 tentativas de até 100 ms).</summary>
    private const int TentativasRolagem = 40;
    private const int EsperaRolagemMs = 100;

    public static readonly BindableProperty ConteudoProperty = BindableProperty.Create(
        nameof(Conteudo), typeof(ConteudoGrade), typeof(GradeLista), ConteudoGrade.Vazio,
        propertyChanged: (b, antigo, novo) => ((GradeLista)b).AoTrocarConteudo(antigo as ConteudoGrade, novo as ConteudoGrade ?? ConteudoGrade.Vazio));

    public static readonly BindableProperty ColunaFixaProperty = BindableProperty.Create(
        nameof(ColunaFixa), typeof(ColunaGradeDef), typeof(GradeLista), null,
        propertyChanged: (b, _, _) => ((GradeLista)b).AoTrocarParteFixa());

    public static readonly BindableProperty MoldeParteFixaProperty = BindableProperty.Create(
        nameof(MoldeParteFixa), typeof(DataTemplate), typeof(GradeLista), null,
        propertyChanged: (b, _, _) => ((GradeLista)b).AoTrocarParteFixa());

    public static readonly BindableProperty AlturaLinhaProperty = BindableProperty.Create(
        nameof(AlturaLinha), typeof(double), typeof(GradeLista), AlturaPadrao,
        propertyChanged: (b, _, novo) => ((GradeLista)b).AoTrocarAltura((double)novo));

    public static readonly BindableProperty MostrarColunasProperty = BindableProperty.Create(
        nameof(MostrarColunas), typeof(bool), typeof(GradeLista), true,
        propertyChanged: (b, _, novo) => ((GradeLista)b).AoTrocarModo((bool)novo));

    public static readonly BindableProperty ComandoTocarProperty = BindableProperty.Create(nameof(ComandoTocar), typeof(ICommand), typeof(GradeLista));
    public static readonly BindableProperty ComandoTocarDuasVezesProperty = BindableProperty.Create(nameof(ComandoTocarDuasVezes), typeof(ICommand), typeof(GradeLista));
    public static readonly BindableProperty ComandoPonteiroEntrouProperty = BindableProperty.Create(nameof(ComandoPonteiroEntrou), typeof(ICommand), typeof(GradeLista));
    public static readonly BindableProperty ComandoPonteiroSaiuProperty = BindableProperty.Create(nameof(ComandoPonteiroSaiu), typeof(ICommand), typeof(GradeLista));
    public static readonly BindableProperty ComandoOrdenarProperty = BindableProperty.Create(nameof(ComandoOrdenar), typeof(ICommand), typeof(GradeLista));

    public static readonly BindableProperty ColunaOrdenadaProperty = BindableProperty.Create(
        nameof(ColunaOrdenada), typeof(string), typeof(GradeLista), null,
        propertyChanged: (b, _, _) => ((GradeLista)b).AtualizarSetas());

    public static readonly BindableProperty OrdemDecrescenteProperty = BindableProperty.Create(
        nameof(OrdemDecrescente), typeof(bool), typeof(GradeLista), false,
        propertyChanged: (b, _, _) => ((GradeLista)b).AtualizarSetas());

    public static readonly BindableProperty MostrarLinhaFiltroProperty = BindableProperty.Create(
        nameof(MostrarLinhaFiltro), typeof(bool), typeof(GradeLista), false,
        propertyChanged: (b, _, _) => ((GradeLista)b).AtualizarLinhaFiltro());

    private readonly MotorCollectionView _motor = new();
    private readonly ScrollView _rolagemLateral;
    private readonly BoxView _extensaoLateral = new() { Color = Colors.Transparent, HeightRequest = AlturaBarraLateral };
    private readonly Grid _cabecalho;
    private readonly ContentView _cabecalhoFixo;
    private readonly HorizontalStackLayout _faixaTitulos = new();
    private readonly Label _tituloFixo;
    private readonly Label _setaFixa;
    private readonly List<View> _titulos = [];
    private readonly List<(ColunaGradeDef Coluna, Label Seta, View Celula)> _setas = [];

    // Linha de filtro: parte fixa + faixa (recortada e deslocada como a dos títulos).
    private readonly ContentView _filtroFixo = new() { Padding = new Thickness(10, 0, 10, 6) };
    private readonly HorizontalStackLayout _faixaFiltros = new();
    private readonly List<View> _filtros = [];
    private Func<ColunaGradeDef, View?>? _criarFiltro;

    /// <summary>Altura da faixa da barra lateral (a barra do Windows fica sobre ela).</summary>
    private const double AlturaBarraLateral = 14;

    /// <summary>Quanto um "dente" da roda (120) desloca a tabela para o lado, em pontos.</summary>
    private const double PassoRodaLateral = 60;
    private readonly LinhasVivas _vivas = new();
    private IReadOnlyList<ColunaGradeDef> _colunasDoMolde = [];
    private double _disponivel = double.NaN;

    public GradeLista()
    {
        _tituloFixo = new Label { VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 1 };
        if (CoresGrade.Recurso("CabecalhoTabela") is Style estilo) _tituloFixo.Style = estilo;
        _setaFixa = NovaSeta();
        _cabecalhoFixo = new ContentView { Padding = new Thickness(16, 0, 12, 0), Content = TituloComSeta(_tituloFixo, _setaFixa) };
        var ordenarFixa = new TapGestureRecognizer();
        ordenarFixa.Tapped += (_, _) => Ordenar(ColunaFixa);
        _cabecalhoFixo.GestureRecognizers.Add(ordenarFixa);
        _cabecalhoFixo.GestureRecognizers.Add(SetaAoPassar(_cabecalhoFixo));

        // Cabeçalho = parte fixa + faixa dos títulos (recortada e deslocada junto com as células); embaixo, a linha
        // de filtro (opcional), com a mesma divisão e o mesmo deslocamento.
        _cabecalho = new Grid();
        _cabecalho.RowDefinitions.Add(new RowDefinition(new GridLength(AlturaCabecalho)));
        _cabecalho.RowDefinitions.Add(new RowDefinition(new GridLength(0)));
        _cabecalho.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(0)));
        _cabecalho.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        _cabecalho.SetAppThemeColor(BackgroundColorProperty, CoresGrade.Cor("SuperficieRealce"), CoresGrade.Cor("SuperficieRealceEscuro"));
        _cabecalho.Add(_cabecalhoFixo, 0, 0);
        _cabecalho.Add(FaixaRecortada(_faixaTitulos), 1, 0);
        _cabecalho.Add(_filtroFixo, 0, 1);
        _cabecalho.Add(FaixaRecortada(_faixaFiltros), 1, 1);
        _filtroFixo.IsVisible = false;
        _faixaFiltros.IsVisible = false;

        // Barra lateral própria, embaixo da área das células: a única rolagem horizontal da grade.
        _rolagemLateral = new ScrollView
        {
            Orientation = ScrollOrientation.Horizontal,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Always,
            HeightRequest = AlturaBarraLateral,
            IsVisible = false,
            Content = _extensaoLateral
        };
        _rolagemLateral.Scrolled += AoRolarLateral;
        _motor.RodaLateral += delta => _ = RolarLateralAsync(DeslocamentoLateral - delta / 120 * PassoRodaLateral);

        var raiz = new Grid();
        raiz.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        raiz.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        raiz.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        raiz.Add(_cabecalho, 0, 0);
        raiz.Add(_motor.Vista, 0, 1);
        raiz.Add(_rolagemLateral, 0, 2);
        Content = raiz;

        _motor.AlturaLinha = AlturaLinha;
        MontarMolde();
        SizeChanged += (_, _) => RecalcularLarguras();
        Unloaded += (_, _) => Soltar();
    }

    /// <summary>
    /// Área recortada onde uma faixa mais larga que ela (títulos ou células) é deslocada para o lado. A faixa tem a
    /// largura das colunas (posição absoluta), então não é espremida pela área.
    /// </summary>
    internal static AbsoluteLayout FaixaRecortada(View faixa)
    {
        var area = new AbsoluteLayout { IsClippedToBounds = true };
        AbsoluteLayout.SetLayoutFlags(faixa, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.HeightProportional);
        AbsoluteLayout.SetLayoutBounds(faixa, new Rect(0, 0, 0, 1));
        area.Add(faixa);
        return area;
    }

    /// <summary>Largura da faixa das células (soma das colunas, sem a parte fixa).</summary>
    internal static void DefinirLarguraFaixa(View faixa, double largura) =>
        AbsoluteLayout.SetLayoutBounds(faixa, new Rect(0, 0, largura, 1));

    /// <summary>Colunas e linhas, trocadas juntas e de uma vez.</summary>
    public ConteudoGrade Conteudo
    {
        get => (ConteudoGrade)GetValue(ConteudoProperty);
        set => SetValue(ConteudoProperty, value);
    }

    /// <summary>Regra de largura da parte fixa (ex.: Nome, fixa 340, mínima 260). O título vai no cabeçalho.</summary>
    public ColunaGradeDef? ColunaFixa
    {
        get => (ColunaGradeDef?)GetValue(ColunaFixaProperty);
        set => SetValue(ColunaFixaProperty, value);
    }

    /// <summary>Molde da parte fixa de cada linha (fornecido pela tela; o contexto é a linha).</summary>
    public DataTemplate? MoldeParteFixa
    {
        get => (DataTemplate?)GetValue(MoldeParteFixaProperty);
        set => SetValue(MoldeParteFixaProperty, value);
    }

    /// <summary>Altura de todas as linhas (densidade). Mudar não reconstrói linhas.</summary>
    public double AlturaLinha
    {
        get => (double)GetValue(AlturaLinhaProperty);
        set => SetValue(AlturaLinhaProperty, value);
    }

    /// <summary>Falso: só a parte fixa, na largura toda (modo cartão / janela estreita).</summary>
    public bool MostrarColunas
    {
        get => (bool)GetValue(MostrarColunasProperty);
        set => SetValue(MostrarColunasProperty, value);
    }

    public ICommand? ComandoTocar { get => (ICommand?)GetValue(ComandoTocarProperty); set => SetValue(ComandoTocarProperty, value); }
    public ICommand? ComandoTocarDuasVezes { get => (ICommand?)GetValue(ComandoTocarDuasVezesProperty); set => SetValue(ComandoTocarDuasVezesProperty, value); }
    public ICommand? ComandoPonteiroEntrou { get => (ICommand?)GetValue(ComandoPonteiroEntrouProperty); set => SetValue(ComandoPonteiroEntrouProperty, value); }
    public ICommand? ComandoPonteiroSaiu { get => (ICommand?)GetValue(ComandoPonteiroSaiuProperty); set => SetValue(ComandoPonteiroSaiuProperty, value); }

    /// <summary>Toque no título de uma coluna (parâmetro: a <see cref="ColunaGradeDef"/>). A tela decide o que fazer.</summary>
    public ICommand? ComandoOrdenar { get => (ICommand?)GetValue(ComandoOrdenarProperty); set => SetValue(ComandoOrdenarProperty, value); }

    /// <summary>Chave da coluna que ordena a lista agora (nula = ordem padrão). O título dela mostra ▲ ou ▼.</summary>
    public string? ColunaOrdenada
    {
        get => (string?)GetValue(ColunaOrdenadaProperty);
        set => SetValue(ColunaOrdenadaProperty, value);
    }

    /// <summary>A coluna ordenada está em ordem decrescente (▼); falso = crescente (▲).</summary>
    public bool OrdemDecrescente
    {
        get => (bool)GetValue(OrdemDecrescenteProperty);
        set => SetValue(OrdemDecrescenteProperty, value);
    }

    /// <summary>Mostra a linha de filtro embaixo dos títulos (os campos vêm de <see cref="CriarFiltro"/>).</summary>
    public bool MostrarLinhaFiltro
    {
        get => (bool)GetValue(MostrarLinhaFiltroProperty);
        set => SetValue(MostrarLinhaFiltroProperty, value);
    }

    /// <summary>
    /// A tela monta o campo de filtro de uma coluna (pela <see cref="ColunaGradeDef.Chave"/>, com o contexto que ela
    /// mesma escolher); nulo = coluna sem filtro. A grade só posiciona o campo na largura da coluna, desloca junto com a
    /// rolagem lateral e, quando um campo fora da vista recebe o foco (Tab), rola até ele.
    /// </summary>
    public Func<ColunaGradeDef, View?>? CriarFiltro
    {
        get => _criarFiltro;
        set
        {
            _criarFiltro = value;
            MontarCabecalho();
            AoTrocarParteFixa();
        }
    }

    /// <summary>Quanto a tabela está rolada para o lado (pontos de tela).</summary>
    public double DeslocamentoLateral { get; private set; }

    /// <summary>Largura total da tabela (parte fixa + colunas). Maior que a grade = há rolagem lateral.</summary>
    public double LarguraConteudo { get; private set; }

    /// <summary>Largura da parte fixa agora.</summary>
    public double LarguraParteFixa => ColunaFixa?.LarguraEfetiva ?? 0;

    /// <summary>Índice do primeiro registro visível (âncora lógica da rolagem).</summary>
    public int PrimeiroVisivel => _motor.PrimeiroVisivel;

    /// <summary>
    /// Leva o registro <paramref name="indice"/> ao topo e confirma pelo primeiro visível. Perto do fim (a lista não
    /// sobe mais) também conta como certo. Desiste sem travar depois de <see cref="TentativasRolagem"/> tentativas.
    /// </summary>
    public async Task<bool> IrParaAsync(int indice, CancellationToken cancelamento = default)
    {
        if (AncoraLogica.Limitar(indice, Conteudo.Linhas.Count) is not { } alvo) return true;
        int? anterior = null;
        for (var tentativa = 1; tentativa <= TentativasRolagem; tentativa++)
        {
            cancelamento.ThrowIfCancellationRequested();
            var espera = _motor.EsperarRolagemAsync(EsperaRolagemMs);
            _motor.IrPara(alvo, ScrollToPosition.Start);
            await espera;
            await Task.Delay(16, cancelamento);
            var primeiro = _motor.PrimeiroVisivel;
            B2UltimasTentativas = tentativa; // B2Temp
            if (AncoraLogica.Confirmada(alvo, primeiro, listaNoFim: anterior == primeiro && primeiro < alvo)) return true;
            anterior = primeiro;
        }
        return false;
    }

    /// <summary>Largura da faixa das células (todas as colunas, sem a parte fixa).</summary>
    public double LarguraCelulas => Math.Max(0, LarguraConteudo - LarguraParteFixa);

    /// <summary>Quanto a tabela pode rolar para o lado (0 = cabe inteira).</summary>
    public double MaximoLateral => Math.Max(0, LarguraCelulas - Math.Max(0, Width - LarguraParteFixa));

    /// <summary>Rola a tabela para o lado (pontos de tela), dentro do limite.</summary>
    public Task RolarLateralAsync(double x) => _rolagemLateral.ScrollToAsync(Math.Clamp(x, 0, MaximoLateral), 0, false);

    // ---------- Repasse à tela ----------

    internal void Tocou(ILinhaGrade? linha) => Executar(ComandoTocar, linha);
    internal void TocouDuasVezes(ILinhaGrade? linha) => Executar(ComandoTocarDuasVezes, linha);
    internal void PonteiroEntrou(ILinhaGrade? linha) => Executar(ComandoPonteiroEntrou, linha);
    internal void PonteiroSaiu(ILinhaGrade? linha) => Executar(ComandoPonteiroSaiu, linha);
    private void Ordenar(ColunaGradeDef? coluna)
    {
        if (coluna is { Ordenavel: true }) Executar(ComandoOrdenar, coluna);
    }

    private static void Executar(ICommand? comando, object? parametro)
    {
        if (parametro is not null && comando?.CanExecute(parametro) == true) comando.Execute(parametro);
    }

    // ---------- Linhas vivas (do próprio componente; limpas ao sair da tela) ----------

    internal void Registrar(LinhaGradeView linha) => _vivas.Adicionar(linha);
    internal void Remover(LinhaGradeView linha) => _vivas.Remover(linha);

    private void Soltar() => _vivas.Limpar();

    // ---------- Conteúdo, molde, larguras ----------

    private void AoTrocarConteudo(ConteudoGrade? antigo, ConteudoGrade novo)
    {
        if (!novo.MesmasColunas(antigo))
        {
            _colunasDoMolde = novo.Colunas;
            MontarCabecalho();
            MontarMolde();
            _disponivel = double.NaN;
            RecalcularLarguras();
        }
        B2TrocasItens++; // B2Temp
        _motor.DefinirItens(novo.Linhas, renovarFonte: B2RenovarACada > 0 && B2TrocasItens % B2RenovarACada == 0); // B2Temp: experimento (a)
    }

    private void AoTrocarParteFixa()
    {
        _tituloFixo.Text = ColunaFixa?.Titulo ?? string.Empty;
        _filtroFixo.Content = ColunaFixa is { } fixa && _criarFiltro?.Invoke(fixa) is { } campo ? Revelando(campo, null) : null;
        AtualizarSetas();
        AtualizarLinhaFiltro();
        MontarMolde();
        _disponivel = double.NaN;
        RecalcularLarguras();
    }

    private void AoTrocarAltura(double altura)
    {
        _motor.AlturaLinha = altura;
        foreach (var linha in _vivas) linha.AplicarAltura(altura);
    }

    private void AoTrocarModo(bool mostrar)
    {
        _faixaTitulos.IsVisible = mostrar;
        AtualizarLinhaFiltro();
        foreach (var linha in _vivas) linha.AplicarModo(mostrar);
        _disponivel = double.NaN;
        RecalcularLarguras();
    }

    /// <summary>Um molde por conjunto de colunas (a célula i é sempre da coluna i).</summary>
    private void MontarMolde()
    {
        var colunas = _colunasDoMolde;
        _motor.DefinirMolde(new DataTemplate(() => new LinhaGradeView(this, colunas)));
    }

    private void MontarCabecalho()
    {
        foreach (var titulo in _titulos) _faixaTitulos.Remove(titulo);
        _titulos.Clear();
        foreach (var filtro in _filtros) _faixaFiltros.Remove(filtro);
        _filtros.Clear();
        _setas.Clear();
        var estilo = CoresGrade.Recurso("CabecalhoTabela") as Style;
        foreach (var coluna in _colunasDoMolde)
        {
            var rotulo = new Label { Text = coluna.Titulo, VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 1 };
            if (estilo is not null) rotulo.Style = estilo;
            var seta = NovaSeta();
            var celula = new ContentView { Padding = new Thickness(12, 0), WidthRequest = coluna.LarguraEfetiva, Content = TituloComSeta(rotulo, seta) };
            if (coluna.Ordenavel)
            {
                var toque = new TapGestureRecognizer();
                toque.Tapped += (_, _) => Ordenar(coluna);
                celula.GestureRecognizers.Add(toque);
                celula.GestureRecognizers.Add(SetaAoPassar(celula));
            }
            else seta.IsVisible = false;
            _titulos.Add(celula);
            _faixaTitulos.Add(celula);
            _setas.Add((coluna, seta, celula));

            // Filtro da coluna (ou espaço vazio, para manter o alinhamento com os títulos).
            var campo = _criarFiltro?.Invoke(coluna);
            var lugar = new ContentView { Padding = new Thickness(10, 0, 10, 6), WidthRequest = coluna.LarguraEfetiva, Content = campo is null ? null : Revelando(campo, coluna) };
            _filtros.Add(lugar);
            _faixaFiltros.Add(lugar);
        }
        AtualizarSetas();
        AtualizarLinhaFiltro();
    }

    // ---------- Seta de ordenação e linha de filtro ----------

    private static Label NovaSeta()
    {
        var seta = new Label { FontSize = 11, VerticalOptions = LayoutOptions.Center, Margin = new Thickness(4, 0, 0, 0) };
        if (CoresGrade.Recurso("CabecalhoTabela") is Style estilo) seta.Style = estilo;
        seta.FontSize = 11;
        return seta;
    }

    private static Grid TituloComSeta(Label titulo, Label seta)
    {
        var grade = new Grid { ColumnSpacing = 0 };
        grade.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        grade.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        grade.Add(titulo, 0, 0);
        grade.Add(seta, 1, 0);
        return grade;
    }

    /// <summary>Título com o mouse em cima (mostra o "↕" que diz que dá para ordenar).</summary>
    private View? _tituloSobMouse;

    private PointerGestureRecognizer SetaAoPassar(View titulo)
    {
        var passar = new PointerGestureRecognizer();
        passar.PointerEntered += (_, _) => { _tituloSobMouse = titulo; AtualizarSetas(); };
        passar.PointerExited += (_, _) => { if (ReferenceEquals(_tituloSobMouse, titulo)) _tituloSobMouse = null; AtualizarSetas(); };
        return passar;
    }

    /// <summary>
    /// ▲ crescente, ▼ decrescente só na coluna ordenada; nas outras, o "↕" aparece apenas com o mouse em cima (04/10/2026:
    /// indicador discreto, como no Fiori e no Dynamics). A seta guarda o espaço (opacidade), então o título não pula.
    /// A dica diz o que o toque faz.
    /// </summary>
    private void AtualizarSetas()
    {
        Pintar(ColunaFixa, _setaFixa, _cabecalhoFixo);
        foreach (var (coluna, seta, celula) in _setas) Pintar(coluna, seta, celula);

        void Pintar(ColunaGradeDef? coluna, Label seta, View alvo)
        {
            if (coluna is null) return;
            var ordenando = coluna.Chave == ColunaOrdenada;
            seta.Text = !ordenando ? "↕" : OrdemDecrescente ? "▼" : "▲";
            seta.Opacity = ordenando ? 1 : coluna.Ordenavel && ReferenceEquals(_tituloSobMouse, alvo) ? 0.45 : 0;
            if (!coluna.Ordenavel) { alvo.ClearValue(ToolTipProperties.TextProperty); return; }
            ToolTipProperties.SetText(alvo, !ordenando ? $"Ordenar por {coluna.Titulo}"
                : OrdemDecrescente ? $"{coluna.Titulo}: ordem decrescente. Toque para voltar à ordem padrão."
                : $"{coluna.Titulo}: ordem crescente. Toque para decrescente.");
        }
    }

    private void AtualizarLinhaFiltro()
    {
        var mostrar = MostrarLinhaFiltro && _criarFiltro is not null;
        _cabecalho.RowDefinitions[1].Height = new GridLength(mostrar ? AlturaLinhaFiltro : 0);
        _filtroFixo.IsVisible = mostrar;
        _faixaFiltros.IsVisible = mostrar && MostrarColunas;
    }

    /// <summary>
    /// Campo de filtro que, ao receber o foco (Tab), traz a coluna para a vista. Inscreve todos os elementos do campo
    /// (ex.: a borda e o Entry dentro dela). A parte fixa (coluna nula) não rola.
    /// </summary>
    private View Revelando(View campo, ColunaGradeDef? coluna)
    {
        if (coluna is { } alvo) InscreverFoco(campo, alvo);
        return campo;
    }

    private void InscreverFoco(Element elemento, ColunaGradeDef coluna)
    {
        if (elemento is VisualElement visual) visual.Focused += (_, _) => _ = RevelarAsync(coluna);
        switch (elemento)
        {
            case Layout layout:
                foreach (var filho in layout.Children.OfType<Element>()) InscreverFoco(filho, coluna);
                break;
            case ContentView vista when vista.Content is { } conteudo:
                InscreverFoco(conteudo, coluna);
                break;
            case Border borda when borda.Content is { } dentro:
                InscreverFoco(dentro, coluna);
                break;
        }
    }

    /// <summary>Rola para o lado só o necessário para a coluna aparecer inteira (ou o começo dela, se não couber).</summary>
    private Task RevelarAsync(ColunaGradeDef coluna)
    {
        var inicio = 0.0;
        foreach (var c in _colunasDoMolde)
        {
            if (ReferenceEquals(c, coluna)) break;
            inicio += c.LarguraEfetiva;
        }
        var visivel = Math.Max(0, Width - LarguraParteFixa);
        var fim = inicio + coluna.LarguraEfetiva;
        if (inicio < DeslocamentoLateral) return RolarLateralAsync(inicio);
        if (fim > DeslocamentoLateral + visivel) return RolarLateralAsync(Math.Min(inicio, fim - visivel));
        return Task.CompletedTask;
    }

    /// <summary>Só quando a largura da grade muda de verdade (janela, prévia, painel, escala), com tolerância de 1 ponto.</summary>
    private void RecalcularLarguras()
    {
        var largura = Width;
        if (!double.IsFinite(largura) || largura <= 0) return;
        if (!double.IsNaN(_disponivel) && Math.Abs(largura - _disponivel) < 1) return;
        _disponivel = largura;

        var colunas = new List<ColunaGradeDef>(_colunasDoMolde.Count + 1);
        if (ColunaFixa is { } fixa) colunas.Add(fixa);
        if (MostrarColunas) colunas.AddRange(_colunasDoMolde);
        var resultado = CalculadoraLarguras.Calcular(colunas, largura);
        CalculadoraLarguras.Aplicar(colunas, resultado);
        LarguraConteudo = resultado.Total;
        B2Recalculos++; // B2Temp

        _cabecalho.ColumnDefinitions[0].Width = new GridLength(LarguraParteFixa);
        DefinirLarguraFaixa(_faixaTitulos, LarguraCelulas);
        _faixaTitulos.IsVisible = MostrarColunas;
        for (var i = 0; i < _titulos.Count; i++) _titulos[i].WidthRequest = _colunasDoMolde[i].LarguraEfetiva;
        DefinirLarguraFaixa(_faixaFiltros, LarguraCelulas);
        for (var i = 0; i < _filtros.Count; i++) _filtros[i].WidthRequest = _colunasDoMolde[i].LarguraEfetiva;
        AtualizarLinhaFiltro();
        foreach (var linha in _vivas) linha.AplicarLarguras();

        // Barra lateral: só quando a tabela não cabe; alinhada à área das células; a extensão é a largura das colunas.
        var maximo = MaximoLateral;
        _rolagemLateral.Margin = new Thickness(LarguraParteFixa, 0, 0, 0);
        _extensaoLateral.WidthRequest = LarguraCelulas;
        _rolagemLateral.IsVisible = maximo > 0.5;
        if (DeslocamentoLateral > maximo)
        {
            Deslocar(maximo);
            _ = RolarLateralAsync(maximo);
        }
    }

    private void AoRolarLateral(object? sender, ScrolledEventArgs e) => Deslocar(Math.Clamp(e.ScrollX, 0, MaximoLateral));

    /// <summary>Desloca a faixa das células (linhas vivas e cabeçalho); a parte fixa não se mexe.</summary>
    private void Deslocar(double x)
    {
        if (x == DeslocamentoLateral) return;
        DeslocamentoLateral = x;
        _faixaTitulos.TranslationX = -x;
        _faixaFiltros.TranslationX = -x;
        foreach (var linha in _vivas) linha.AplicarDeslocamento(x);
    }

    #region B2Temp: instrumentação do laboratório da P2-B2 (Etapa 2). Remover ao final.
    internal int B2Criadas { get; private set; }
    internal int B2Reciclagens { get; private set; }
    internal int B2TrocasItens { get; private set; }
    internal int B2Recalculos { get; private set; }
    internal int B2UltimasTentativas { get; private set; }
    internal int B2UltimoVisivel => _motor.UltimoVisivel;
    internal IReadOnlyCollection<LinhaGradeView> B2Vivas => _vivas.ToList();
    private readonly List<WeakReference<LinhaGradeView>> _b2Todas = [];
    /// <summary>Linhas visuais ainda existentes na memória (chamar depois de coletar o lixo).</summary>
    internal int B2VivasReais => _b2Todas.Count(w => w.TryGetTarget(out _));
    internal void B2Nasceu(LinhaGradeView linha) => _b2Todas.Add(new(linha));
    internal View B2FaixaTitulos => _faixaTitulos;
    /// <summary>Experimento (b): linhas sem se inscrever no modelo (destaque/seleção deixam de ser pintados).</summary>
    internal bool B2SemInscricao { get; set; }
    /// <summary>Experimento (a): renovar a fonte do motor a cada K trocas (0 = nunca).</summary>
    internal int B2RenovarACada { get; set; }
    internal IReadOnlyList<LinhaGradeView> B2TodasVivas()
    {
        var vivas = new List<LinhaGradeView>();
        foreach (var w in _b2Todas)
            if (w.TryGetTarget(out var linha)) vivas.Add(linha);
        return vivas;
    }
    internal void B2ContarCriada() => B2Criadas++;
    internal void B2ContarReciclagem() => B2Reciclagens++;
    internal int B2Vinculos { get; private set; }
    internal void B2ContarVinculo() => B2Vinculos++;
    internal void B2RolarAte(int indice, ScrollToPosition posicao) => _motor.IrPara(indice, posicao);
    internal string B2Nativo => _motor.Nativo;
    internal int B2Renovacoes => _motor.Renovacoes;
    internal int B2LinhasNoPainel => _motor.LinhasNoPainel;
    internal int B2LimitePainel => _motor.LimitePainel;
    internal CollectionView B2Vista => _motor.Vista;
    #endregion
}

/// <summary>
/// As linhas visuais vivas da grade, <b>sem segurá-las</b>: o motor pode descartar uma linha sem avisar (sem
/// <c>Unloaded</c>), e uma lista comum a manteria viva para sempre (medido na P2-B2: +2 linhas e ≈ +0,8 MB a cada troca).
/// </summary>
internal sealed class LinhasVivas : IEnumerable<LinhaGradeView>
{
    private readonly List<WeakReference<LinhaGradeView>> _itens = [];

    public void Adicionar(LinhaGradeView linha)
    {
        foreach (var existente in this)
            if (ReferenceEquals(existente, linha)) return;
        _itens.Add(new WeakReference<LinhaGradeView>(linha));
    }

    public void Remover(LinhaGradeView linha) =>
        _itens.RemoveAll(w => !w.TryGetTarget(out var existente) || ReferenceEquals(existente, linha));

    public void Limpar() => _itens.Clear();

    public IEnumerator<LinhaGradeView> GetEnumerator()
    {
        var vivas = new List<LinhaGradeView>(_itens.Count);
        _itens.RemoveAll(w => !w.TryGetTarget(out _));
        foreach (var w in _itens)
            if (w.TryGetTarget(out var linha)) vivas.Add(linha);
        return vivas.GetEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
