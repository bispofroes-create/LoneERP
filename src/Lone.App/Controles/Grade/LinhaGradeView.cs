using System.ComponentModel;
using Lone.Cliente.Grade;

namespace Lone.App.Controles.Grade;

/// <summary>
/// O visual de uma linha da <see cref="GradeLista"/> (reciclado pelo motor). Molde enxuto (F1), montado uma vez por
/// conjunto de colunas: a parte fixa (molde da tela) e uma célula por coluna, com o molde escolhido pelo tipo da coluna,
/// sem alternativas ocultas. Ao receber outra linha, só troca textos e cores; a largura vem da coluna e o deslocamento
/// lateral e a altura vêm da grade. A linha tem a largura da grade: a parte fixa fica parada à esquerda e só a faixa
/// das células (recortada) é deslocada pela rolagem lateral.
/// </summary>
internal sealed class LinhaGradeView : Grid
{
    private readonly GradeLista _grade;
    private readonly IReadOnlyList<ColunaGradeDef> _colunas;
    private readonly HorizontalStackLayout _celulas = new();
    private readonly List<CelulaView> _slots = [];
    private readonly AbsoluteLayout _areaCelulas;
    private ILinhaGrade? _linha;
    private bool _inscrita;

    public LinhaGradeView(GradeLista grade, IReadOnlyList<ColunaGradeDef> colunas)
    {
        _grade = grade;
        _colunas = colunas;
        grade.B2ContarCriada(); // B2Temp
        grade.B2Nasceu(this); // B2Temp

        RowDefinitions.Add(new RowDefinition(GridLength.Star));
        RowDefinitions.Add(new RowDefinition(new GridLength(1)));
        ColumnDefinitions.Add(new ColumnDefinition(new GridLength(0)));
        ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        HeightRequest = grade.AlturaLinha;

        // Parte fixa (molde da tela) parada à esquerda; as células numa faixa recortada, deslocada pela rolagem lateral.
        Fixa = new ContentView();
        if (grade.MoldeParteFixa?.CreateContent() is View parteFixa) Fixa.Content = parteFixa;
        foreach (var coluna in colunas)
        {
            var slot = CelulaView.Criar(coluna.Tipo);
            _slots.Add(slot);
            _celulas.Add(slot.Raiz);
        }
        _areaCelulas = GradeLista.FaixaRecortada(_celulas);
        this.Add(Fixa, 0, 0);
        this.Add(_areaCelulas, 1, 0);
        var separador = new BoxView { HeightRequest = 1 };
        separador.SetAppThemeColor(BoxView.ColorProperty, CoresGrade.Cor("BordaSutil"), CoresGrade.Cor("BordaSutilEscuro"));
        this.Add(separador, 0, 1);
        SetColumnSpan((IView)separador, 2);

        var toque = new TapGestureRecognizer();
        toque.Tapped += (_, _) => _grade.Tocou(_linha);
        var duplo = new TapGestureRecognizer { NumberOfTapsRequired = 2 };
        duplo.Tapped += (_, _) => _grade.TocouDuasVezes(_linha);
        var ponteiro = new PointerGestureRecognizer();
        ponteiro.PointerEntered += (_, _) => _grade.PonteiroEntrou(_linha);
        ponteiro.PointerExited += (_, _) => _grade.PonteiroSaiu(_linha);
        GestureRecognizers.Add(toque);
        GestureRecognizers.Add(duplo);
        GestureRecognizers.Add(ponteiro);

        Loaded += (_, _) =>
        {
            Inscrever();
            _grade.Registrar(this);
            AjustarAGrade();
        };
        Unloaded += (_, _) =>
        {
            _grade.Remover(this);
            Desinscrever();
        };
        // O motor pode descartar a linha sem Unloaded: ao perder o controle nativo, solta o modelo e sai do registro.
        HandlerChanged += (_, _) =>
        {
            if (Handler is not null) return;
            _grade.Remover(this);
            Desinscrever();
        };
        AjustarAGrade();
        PintarEstado();
    }

    /// <summary>A parte fixa (molde da tela): parada à esquerda.</summary>
    internal ContentView Fixa { get; }

    /// <summary>B2Temp: a faixa das células (deslocada), para o laboratório conferir o alinhamento.</summary>
    internal View B2Faixa => _celulas;
    internal bool B2Inscrita => _inscrita;

    internal ILinhaGrade? Linha => _linha;

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        var nova = BindingContext as ILinhaGrade;
        if (ReferenceEquals(nova, _linha)) return;
        if (_linha is not null && nova is not null) _grade.B2ContarReciclagem(); // B2Temp
        if (nova is not null) _grade.B2ContarVinculo(); // B2Temp
        Desinscrever();
        _linha = nova;
        if (IsLoaded) Inscrever();
        Preencher();
        PintarEstado();
        AjustarAGrade();
    }

    /// <summary>Deslocamento lateral, larguras, altura e modo atuais da grade (linha nova ou reciclada).</summary>
    internal void AjustarAGrade()
    {
        AplicarDeslocamento(_grade.DeslocamentoLateral);
        AplicarLarguras();
        AplicarAltura(_grade.AlturaLinha);
        _areaCelulas.IsVisible = _grade.MostrarColunas;
    }

    internal void AplicarDeslocamento(double x)
    {
        if (_celulas.TranslationX != -x) _celulas.TranslationX = -x;
    }

    internal void AplicarLarguras()
    {
        var fixa = _grade.LarguraParteFixa;
        if (ColumnDefinitions[0].Width.Value != fixa) ColumnDefinitions[0].Width = new GridLength(fixa);
        GradeLista.DefinirLarguraFaixa(_celulas, _grade.LarguraCelulas);
        for (var i = 0; i < _slots.Count; i++)
        {
            var largura = _colunas[i].LarguraEfetiva;
            if (_slots[i].Raiz.WidthRequest != largura) _slots[i].Raiz.WidthRequest = largura;
        }
    }

    internal void AplicarAltura(double altura)
    {
        if (HeightRequest != altura) HeightRequest = altura;
    }

    internal void AplicarModo(bool mostrarColunas) => _areaCelulas.IsVisible = mostrarColunas;

    private void Preencher()
    {
        var celulas = _linha?.Celulas;
        for (var i = 0; i < _slots.Count; i++)
        {
            // Durante uma troca de colunas o motor pode desenhar uma linha antiga com o molde novo: não mostra nada torto.
            var celula = celulas is not null && i < celulas.Count && ReferenceEquals(celulas[i].Coluna, _colunas[i]) ? celulas[i] : null;
            if (celula is null) _slots[i].Limpar();
            else _slots[i].Preencher(celula);
        }
    }

    private void Inscrever()
    {
        if (_inscrita || _linha is null || _grade.B2SemInscricao) return; // B2Temp: experimento (b)
        _linha.PropertyChanged += AoMudarLinha;
        _inscrita = true;
    }

    private void Desinscrever()
    {
        if (!_inscrita || _linha is null) return;
        _linha.PropertyChanged -= AoMudarLinha;
        _inscrita = false;
    }

    private void AoMudarLinha(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ILinhaGrade.Destacada) or nameof(ILinhaGrade.Selecionada) or null or "")
            PintarEstado();
    }

    /// <summary>Destaque (mouse) e seleção vêm do modelo; a parte fixa é opaca para cobrir as células que passam por baixo.</summary>
    private void PintarEstado()
    {
        var chave = _linha?.Selecionada == true ? "Selecao" : _linha?.Destacada == true ? "SuperficieRealce" : null;
        BackgroundColor = chave is null ? Colors.Transparent : CoresGrade.DoTema(chave);
    }

    internal Color CorDoEstado => BackgroundColor; // B2Temp: conferência da seleção/destaque pelo laboratório
}

/// <summary>
/// Uma célula do molde F1, um molde por tipo e sem alternativas ocultas: texto = 1 <see cref="Label"/>;
/// selo = contêiner + <see cref="SeloTom"/>; pílulas = pilha + <see cref="SeloTom"/> por papel.
/// </summary>
internal abstract class CelulaView
{
    protected static readonly Thickness Margens = new(12, 0);

    public abstract View Raiz { get; }

    public static CelulaView Criar(TipoCelula tipo) => tipo switch
    {
        TipoCelula.Selo => new CelulaSelo(),
        TipoCelula.Pilulas => new CelulaPilulas(),
        _ => new CelulaTexto()
    };

    public abstract void Preencher(CelulaGrade celula);
    public abstract void Limpar();

    /// <summary>No máximo uma dica por célula; sem valor, sem dica.</summary>
    protected static void Dica(BindableObject alvo, string? texto)
    {
        if (texto is null) alvo.ClearValue(ToolTipProperties.TextProperty);
        else ToolTipProperties.SetText(alvo, texto);
    }

    private sealed class CelulaTexto : CelulaView
    {
        private readonly Label _rotulo = new()
        {
            Padding = Margens,
            VerticalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 1
        };
        // Cor aplicada agora: null = a do estilo; "vazia" = secundária; ou o tom do aviso. Só mexe quando muda (reciclagem).
        private string? _cor;

        public CelulaTexto()
        {
            if (CoresGrade.Recurso("TextoTabela") is Style estilo) _rotulo.Style = estilo;
        }

        public override View Raiz => _rotulo;

        public override void Preencher(CelulaGrade celula)
        {
            _rotulo.Text = celula.Texto;
            Dica(_rotulo, celula.Vazia ? null : celula.Texto);
            var cor = celula.Vazia ? "vazia" : celula.Tom;
            if (cor == _cor) return;
            _cor = cor;
            switch (cor)
            {
                case null:
                    // Cor normal sempre explícita (a mesma do estilo TextoTabela). Limpar a cor (ClearValue/RemoveBinding)
                    // não devolvia a do estilo: a linha reciclada ficava com a cor do aviso ou a cinza da pessoa anterior.
                    _rotulo.SetAppThemeColor(Label.TextColorProperty, CoresGrade.Cor("Texto"), CoresGrade.Cor("TextoEscuro"));
                    _rotulo.FontAttributes = FontAttributes.None;
                    break;
                case "vazia":
                    _rotulo.SetAppThemeColor(Label.TextColorProperty, CoresGrade.Cor("TextoSecundario"), CoresGrade.Cor("TextoSecundarioEscuro"));
                    _rotulo.FontAttributes = FontAttributes.None;
                    break;
                default:
                    // Aviso no próprio texto (mesmo molde): cor do tom no tema claro; no escuro, o fundo claro do tom (legível).
                    // Tom sem cor nos recursos: usa o do aviso (nunca texto transparente).
                    var tom = CoresGrade.Recurso(cor) is Color ? cor : "Aviso";
                    _rotulo.SetAppThemeColor(Label.TextColorProperty, CoresGrade.Cor(tom), CoresGrade.Cor(tom + "Fundo"));
                    _rotulo.FontAttributes = FontAttributes.Bold;
                    break;
            }
        }

        public override void Limpar()
        {
            _rotulo.Text = string.Empty;
            Dica(_rotulo, null);
        }
    }

    private sealed class CelulaSelo : CelulaView
    {
        private readonly Grid _conteiner = new() { Padding = Margens };
        private readonly SeloTom _selo = new() { HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Center };

        public CelulaSelo() => _conteiner.Add(_selo);

        public override View Raiz => _conteiner;

        public override void Preencher(CelulaGrade celula)
        {
            _selo.IsVisible = true;
            _selo.Texto = celula.Texto;
            _selo.Tom = celula.Tom ?? "Neutro";
        }

        public override void Limpar() => _selo.IsVisible = false;
    }

    private sealed class CelulaPilulas : CelulaView
    {
        // Recortada: papéis que não cabem na largura da coluna não invadem a coluna seguinte (a dica mostra todos).
        private readonly HorizontalStackLayout _pilha = new() { Padding = Margens, Spacing = 4, VerticalOptions = LayoutOptions.Center, IsClippedToBounds = true };
        private readonly List<SeloTom> _selos = [];

        public override View Raiz => _pilha;

        public override void Preencher(CelulaGrade celula)
        {
            // Reaproveita os selos já criados; só cria quando a linha tem mais papéis que as anteriores.
            while (_selos.Count < celula.Selos.Count)
            {
                var novo = new SeloTom();
                _selos.Add(novo);
                _pilha.Add(novo);
            }
            for (var i = 0; i < _selos.Count; i++)
            {
                var usar = i < celula.Selos.Count;
                if (usar)
                {
                    _selos[i].Texto = celula.Selos[i].Texto;
                    _selos[i].Tom = celula.Selos[i].Tom;
                }
                if (_selos[i].IsVisible != usar) _selos[i].IsVisible = usar;
            }
            Dica(_pilha, celula.Vazia ? null : celula.Texto);
        }

        public override void Limpar()
        {
            foreach (var selo in _selos) selo.IsVisible = false;
            Dica(_pilha, null);
        }
    }
}

/// <summary>Cores e estilos do tema, lidos dos recursos do aplicativo (sem estado próprio).</summary>
internal static class CoresGrade
{
    public static object? Recurso(string chave) =>
        Application.Current?.Resources.TryGetValue(chave, out var valor) == true ? valor : null;

    public static Color Cor(string chave) => Recurso(chave) as Color ?? Colors.Transparent;

    /// <summary>A cor no tema atual ("Selecao" no claro, "SelecaoEscuro" no escuro).</summary>
    public static Color DoTema(string chave) =>
        Cor(Application.Current?.RequestedTheme == AppTheme.Dark ? chave + "Escuro" : chave);
}
