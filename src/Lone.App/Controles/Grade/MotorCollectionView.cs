using System.Collections;
using System.Collections.Specialized;
using Lone.Cliente.Grade;

namespace Lone.App.Controles.Grade;

/// <summary>
/// Fronteira interna com o motor de virtualização: o único lugar que conhece o <see cref="CollectionView"/>. Trocar de
/// motor (CollectionView2, outro) mexe só aqui; as telas e a <see cref="GradeLista"/> não mudam. Seleção nativa
/// desligada: a seleção é do Lone, pela chave da linha.
/// </summary>
internal sealed class MotorCollectionView
{
    private TaskCompletionSource? _rolou;

    public MotorCollectionView()
    {
        Vista = new CollectionView
        {
            SelectionMode = SelectionMode.None,
            ItemsLayout = LinearItemsLayout.Vertical,
            ItemSizingStrategy = ItemSizingStrategy.MeasureAllItems,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Never,
        };
        Vista.Scrolled += AoRolar;
        Vista.HandlerChanged += (_, _) => AplicarCache();
    }

    /// <summary>
    /// Reserva do motor no Windows (<c>ItemsStackPanel.CacheLength</c>): 1 = meia tela pronta acima e meia abaixo da
    /// visível. O padrão do Windows (4 = duas telas acima e duas abaixo) criava ≈ 5× as linhas visíveis e deixava a
    /// abertura ≈ 2,5× mais lenta (medição da P2-B2).
    /// </summary>
    public const double ReservaPadrao = 1;

    /// <summary>
    /// Roda lateral sobre a lista (Shift + roda, ou a roda inclinada para o lado): o delta do Windows (120 por dente,
    /// positivo = para a esquerda). A lista é só vertical; quem rola para o lado é a grade.
    /// </summary>
#pragma warning disable CS0067 // só o Windows dispara (nas outras plataformas a roda lateral não existe aqui)
    public event Action<double>? RodaLateral;

    /// <summary>A lista rolou (o adaptador de rolagem registra a posição).</summary>
    public event Action? Rolou;
#pragma warning restore CS0067

    private void AplicarCache()
    {
#if WINDOWS
        if (Vista.Handler?.PlatformView is not Microsoft.UI.Xaml.Controls.ListViewBase lista) return;
        void Aplicar()
        {
            if (lista.ItemsPanelRoot is Microsoft.UI.Xaml.Controls.ItemsStackPanel painel && painel.CacheLength != ReservaPadrao)
                painel.CacheLength = ReservaPadrao;
        }
        if (lista.ItemsPanelRoot is not null) Aplicar();
        else lista.Loaded += (_, _) => Aplicar();
        if (!_rodaLigada)
        {
            _rodaLigada = true;
            lista.AddHandler(Microsoft.UI.Xaml.UIElement.PointerWheelChangedEvent,
                new Microsoft.UI.Xaml.Input.PointerEventHandler(AoRodar), true);
        }
#endif
    }

    public CollectionView Vista { get; }

    /// <summary>Índice do primeiro item visível informado pelo motor (exato no topo, segundo a P2-B1).</summary>
    public int PrimeiroVisivel { get; private set; }

    /// <summary>Último visível informado pelo motor (impreciso: só diagnóstico, nunca decisão).</summary>
    public int UltimoVisivel { get; private set; } = -1;

    public void DefinirMolde(DataTemplate molde) => Vista.ItemTemplate = molde;

    private ListaTrocavel _lista = new();

    /// <summary>
    /// Troca a lista inteira de uma vez: a fonte do motor é sempre a mesma e avisa um único "Reset", e o motor
    /// reaproveita as linhas visuais. Entregar uma lista nova a cada troca recriava todas as linhas (P2-B2: 2–3× mais lento).
    /// <para>
    /// Exceção medida na P2-B2 (Etapa 2A): no Windows, o painel nativo da lista (<c>ItemsStackPanel</c>) acumula
    /// linhas visuais a cada "Reset" e a cada salto longo da rolagem (≈ +2 por vez, todas carregadas e presas ao painel;
    /// não é coleta atrasada). Elas não saem nunca, deixam cada troca mais lenta e só são soltas quando a fonte do
    /// motor é substituída. Por isso, quando o painel passa do limite (3× as linhas que cabem na lista + 4; com a
    /// reserva 1 o normal é ≈ 2×), a troca seguinte usa uma fonte nova: o painel volta ao tamanho normal, ao custo de
    /// uma troca um pouco mais lenta. O limite segue a altura da lista e da linha (janela, escala, densidade).
    /// </para>
    /// </summary>
    public void DefinirItens(IReadOnlyList<ILinhaGrade> itens)
    {
        if (PainelInchado()) _lista = new ListaTrocavel();
        if (!ReferenceEquals(Vista.ItemsSource, _lista)) Vista.ItemsSource = _lista;
        _lista.Trocar(itens);
        PrimeiroVisivel = 0;
        UltimoVisivel = -1;
    }

    /// <summary>Altura de cada linha (a grade informa; usada no limite do painel).</summary>
    public double AlturaLinha { get; set; } = 56;

    /// <summary>Limite de linhas no painel nativo antes de renovar a fonte (−1 = altura da lista ainda desconhecida).</summary>
    public int LimitePainel => Vista.Height > 0 && AlturaLinha > 0 ? 3 * (int)Math.Ceiling(Vista.Height / AlturaLinha) + 4 : -1;

    /// <summary>Linhas visuais presas ao painel nativo (−1 = indisponível).</summary>
    public int LinhasNoPainel
    {
        get
        {
#if WINDOWS
            // Só com a lista viva na tela: depois que a janela fecha, o controle nativo já foi destruído.
            if (!Vista.IsLoaded || Vista.Handler?.PlatformView is not Microsoft.UI.Xaml.Controls.ListViewBase lista) return -1;
            try { return lista.ItemsPanelRoot?.Children.Count ?? -1; }
            catch (Exception) { return -1; }
#else
            return -1;
#endif
        }
    }

    /// <summary>O painel nativo guarda mais linhas do que o limite para a altura atual.</summary>
    private bool PainelInchado()
    {
        var linhas = LinhasNoPainel;
        var limite = LimitePainel;
        return linhas >= 0 && limite > 0 && linhas > limite;
    }

    public void IrPara(int indice, ScrollToPosition posicao) => Vista.ScrollTo(indice, -1, posicao, false);

    /// <summary>Espera o próximo aviso de rolagem do motor, no máximo <paramref name="limiteMs"/>. Chamar antes de rolar.</summary>
    public Task EsperarRolagemAsync(int limiteMs)
    {
        var rolou = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _rolou = rolou;
        return Task.WhenAny(rolou.Task, Task.Delay(limiteMs));
    }

#if WINDOWS
    private bool _rodaLigada;

    private void AoRodar(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var ponto = e.GetCurrentPoint(sender as Microsoft.UI.Xaml.UIElement);
        var lateral = ponto.Properties.IsHorizontalMouseWheel;
        var shift = (e.KeyModifiers & global::Windows.System.VirtualKeyModifiers.Shift) != 0;
        if (!lateral && !shift) return;
        var delta = ponto.Properties.MouseWheelDelta;
        RodaLateral?.Invoke(lateral ? -delta : delta);
        e.Handled = true;
    }
#endif

    private void AoRolar(object? sender, ItemsViewScrolledEventArgs e)
    {
        PrimeiroVisivel = e.FirstVisibleItemIndex;
        UltimoVisivel = e.LastVisibleItemIndex;
        _rolou?.TrySetResult();
        Rolou?.Invoke();
    }
}

/// <summary>
/// Fonte fixa do motor: troca o conteúdo inteiro e avisa uma única vez (<see cref="NotifyCollectionChangedAction.Reset"/>).
/// Somente leitura para o motor.
/// </summary>
internal sealed class ListaTrocavel : IReadOnlyList<ILinhaGrade>, IList, INotifyCollectionChanged
{
    private static readonly NotifyCollectionChangedEventArgs Reinicio = new(NotifyCollectionChangedAction.Reset);
    private IReadOnlyList<ILinhaGrade> _itens = [];

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public void Trocar(IReadOnlyList<ILinhaGrade> itens)
    {
        _itens = itens;
        CollectionChanged?.Invoke(this, Reinicio);
    }

    public ILinhaGrade this[int index] => _itens[index];
    public int Count => _itens.Count;
    public IEnumerator<ILinhaGrade> GetEnumerator() => _itens.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    object? IList.this[int index] { get => _itens[index]; set => throw new NotSupportedException(); }
    bool IList.IsFixedSize => true;
    bool IList.IsReadOnly => true;
    bool ICollection.IsSynchronized => false;
    object ICollection.SyncRoot => this;
    int IList.Add(object? value) => throw new NotSupportedException();
    void IList.Clear() => throw new NotSupportedException();
    bool IList.Contains(object? value) => value is ILinhaGrade l && IndexOf(l) >= 0;
    int IList.IndexOf(object? value) => value is ILinhaGrade l ? IndexOf(l) : -1;
    void IList.Insert(int index, object? value) => throw new NotSupportedException();
    void IList.Remove(object? value) => throw new NotSupportedException();
    void IList.RemoveAt(int index) => throw new NotSupportedException();
    void ICollection.CopyTo(Array array, int index)
    {
        for (var i = 0; i < _itens.Count; i++) array.SetValue(_itens[i], index + i);
    }

    private int IndexOf(ILinhaGrade linha)
    {
        for (var i = 0; i < _itens.Count; i++)
            if (ReferenceEquals(_itens[i], linha)) return i;
        return -1;
    }
}
