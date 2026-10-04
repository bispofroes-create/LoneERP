using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Lone.Cliente.Grade;

/// <summary>
/// Linha de uma tela de cadastro na GradeLista (padrão de tela de cadastro, 03/10/2026): o título e o subtítulo vão na
/// parte fixa (à esquerda, como o nome em Pessoas); as demais colunas, nas células. <see cref="Item"/> é o registro.
/// </summary>
public sealed partial class LinhaCadastro : ObservableObject, ILinhaGrade
{
    public LinhaCadastro(Guid chave, object item, string titulo, string subtitulo, IReadOnlyList<CelulaGrade> celulas)
    {
        Chave = chave;
        Item = item;
        Titulo = titulo;
        Subtitulo = subtitulo;
        Celulas = celulas;
    }

    public Guid Chave { get; }
    public object Item { get; }
    public string Titulo { get; }
    public string Subtitulo { get; }
    public bool TemSubtitulo => Subtitulo.Length > 0;
    public IReadOnlyList<CelulaGrade> Celulas { get; }

    /// <summary>Recuo do título nas listas em árvore (ex.: territórios), em pontos; 0 nas listas comuns.</summary>
    public double Recuo { get; init; }

    /// <summary>Marcador antes do título nas listas em árvore ("▾" com filhos, "•" sem); vazio nas listas comuns.</summary>
    public string Marcador { get; init; } = string.Empty;
    public bool TemMarcador => Marcador.Length > 0;

    [ObservableProperty] private bool _destacada;
    [ObservableProperty] private bool _selecionada;
}

/// <summary>Uma coluna da lista de cadastro: a definição (largura, título) e como cada registro vira célula.</summary>
public sealed class ColunaCadastro<T>
{
    /// <param name="ordem">Valor usado para ordenar pela coluna (data, número...); nulo = o texto mostrado.</param>
    public ColunaCadastro(ColunaGradeDef definicao, Func<T, ColunaGradeDef, CelulaGrade> celula, Func<T, object?>? ordem = null)
    {
        Definicao = definicao;
        Celula = celula;
        Ordem = ordem;
    }

    public ColunaGradeDef Definicao { get; }
    public Func<T, ColunaGradeDef, CelulaGrade> Celula { get; }

    /// <summary>Chave de ordenação (clicar no título da coluna). Nula = o texto da célula.</summary>
    public Func<T, object?>? Ordem { get; }

    /// <summary>Coluna Situação: se o registro está ativo (a lista ganha o filtro Ativos / Inativos / Todos).</summary>
    public Func<T, bool>? Ativo { get; private init; }

    /// <summary>Texto livre: cresce com peso 1 a partir da mínima (regra de largura das listagens).</summary>
    public static ColunaCadastro<T> Texto(string chave, string titulo, Func<T, string?> valor, double minima = 160,
        Func<T, object?>? ordem = null) =>
        new(ColunaGradeDef.Proporcional(chave, titulo.ToUpperInvariant(), TipoCelula.Texto, peso: 1, minima: minima),
            (item, def) => CelulaGrade.DeTexto(def, valor(item)), ordem ?? (item => valor(item)));

    /// <summary>Dado curto (data, número, código, quantidade): largura fixa, não cresce. Datas e números: passe <paramref name="ordem"/>.</summary>
    public static ColunaCadastro<T> Curto(string chave, string titulo, Func<T, string?> valor, double largura = 120,
        Func<T, object?>? ordem = null) =>
        new(ColunaGradeDef.Fixa(chave, titulo.ToUpperInvariant(), TipoCelula.Texto, largura),
            (item, def) => CelulaGrade.DeTexto(def, valor(item)), ordem ?? (item => valor(item)));

    /// <summary>Selo (situação, tipo): largura fixa; o tom diz a cor ("Sucesso", "Aviso", "Neutro"...).</summary>
    public static ColunaCadastro<T> Selo(string chave, string titulo, Func<T, string?> valor, Func<T, string> tom, double largura = 130,
        Func<T, object?>? ordem = null) =>
        new(ColunaGradeDef.Fixa(chave, titulo.ToUpperInvariant(), TipoCelula.Selo, largura),
            (item, def) => CelulaGrade.DeSelo(def, valor(item), tom(item)), ordem ?? (item => valor(item)));

    /// <summary>Situação do cadastro auxiliar: "Ativo" (verde) ou "Inativo" (cinza); feminino = "Ativa"/"Inativa".</summary>
    public static ColunaCadastro<T> Situacao(Func<T, bool> ativo, bool feminino = false) =>
        new(Selo("situacao", "Situação", item => (ativo(item) ? "Ativ" : "Inativ") + (feminino ? "a" : "o"),
            item => ativo(item) ? "Sucesso" : "Neutro", 120)) { Ativo = ativo };

    private ColunaCadastro(ColunaCadastro<T> outra) : this(outra.Definicao, outra.Celula, outra.Ordem) { }
}

/// <summary>
/// A lista de uma tela de cadastro em colunas (padrão de tela de cadastro): parte fixa com título e subtítulo do
/// registro, mais as colunas. Monta o <see cref="ConteudoGrade"/> de uma vez e marca o registro aberto.
/// Ordenação (03/10/2026), como em Pessoas: clicar no título da coluna ordena crescente, de novo decrescente, e de novo
/// volta à ordem padrão (a do servidor). Textos em ordem natural ("TE-9" antes de "TE-10"), sem diferenciar maiúsculas;
/// vazios por último. Listas em árvore (com <see cref="Recuo"/>) não ordenam: a hierarquia é a ordem.
/// </summary>
public sealed partial class GradeCadastro<T> : ObservableObject where T : class
{
    private readonly Func<T, Guid> _chave;
    private readonly Func<T, string> _titulo;
    private readonly Func<T, string?> _subtitulo;
    private readonly IReadOnlyList<ColunaCadastro<T>> _colunas;

    /// <param name="tituloFixa">Título da parte fixa (ex.: "Titular", "Nome").</param>
    public GradeCadastro(string tituloFixa, Func<T, Guid> chave, Func<T, string> titulo, Func<T, string?> subtitulo,
        params ColunaCadastro<T>[] colunas)
    {
        _chave = chave;
        _titulo = titulo;
        _subtitulo = subtitulo;
        _colunas = colunas;
        // Parte fixa: a coluna que mais cresce (peso 3), como o nome em Pessoas.
        ColunaFixa = ColunaGradeDef.Proporcional(ChaveTitulo, tituloFixa.ToUpperInvariant(), TipoCelula.Texto, peso: 3, minima: 240);
        Colunas = [.. colunas.Select(c => c.Definicao)];
    }

    private const string ChaveTitulo = "titulo";

    /// <summary>Ordenação do título da parte fixa (ex.: o número da operação); nula = o próprio título.</summary>
    public Func<T, object?>? OrdemTitulo { get; init; }

    /// <summary>Coluna ordenada agora (nula = ordem padrão).</summary>
    [ObservableProperty] private string? _colunaOrdenadaChave;
    [ObservableProperty] private bool _ordemDecrescente;

    /// <summary>A ordem mudou: a tela monta a lista de novo.</summary>
    public event Action? OrdemMudou;

    /// <summary>Clicar no título da coluna: crescente → decrescente → ordem padrão.</summary>
    [RelayCommand]
    private void OrdenarColuna(ColunaGradeDef? coluna)
    {
        if (coluna is not { Ordenavel: true } || Recuo is not null) return;
        if (ColunaOrdenadaChave != coluna.Chave)
        {
            ColunaOrdenadaChave = coluna.Chave;
            OrdemDecrescente = false;
        }
        else if (!OrdemDecrescente) OrdemDecrescente = true;
        else
        {
            ColunaOrdenadaChave = null;
            OrdemDecrescente = false;
        }
        OrdemMudou?.Invoke();
    }

    /// <summary>Os itens na ordem escolhida (estável: empates ficam na ordem padrão; vazios sempre por último).</summary>
    public IReadOnlyList<T> Ordenar(IEnumerable<T> itens)
    {
        var lista = itens.ToList();
        if (ColunaOrdenadaChave is not { } chave || Recuo is not null) return lista;
        Func<T, object?>? valor = chave == ChaveTitulo
            ? OrdemTitulo ?? (item => _titulo(item))
            : _colunas.FirstOrDefault(c => c.Definicao.Chave == chave)?.Ordem;
        if (valor is null) return lista;
        var comparador = new ComparadorOrdem(OrdemDecrescente);
        return lista.Select((item, i) => (item, i, chaveItem: valor(item)))
            .OrderBy(t => t.chaveItem, comparador).ThenBy(t => t.i)
            .Select(t => t.item).ToList();
    }

    public ColunaGradeDef ColunaFixa { get; }

    /// <summary>Lista em árvore: recuo do título de cada registro (ex.: nível × 18).</summary>
    public Func<T, double>? Recuo { get; init; }

    /// <summary>Lista em árvore: marcador antes do título ("▾" / "•").</summary>
    public Func<T, string>? Marcador { get; init; }
    public IReadOnlyList<ColunaGradeDef> Colunas { get; }

    /// <summary>
    /// Altura da linha: 56 com subtítulo (título + subtítulo), 44 sem (uma linha só, a altura dos campos — Fiori cozy;
    /// 04/10/2026). Decidida a cada montagem pelo que a lista mostra.
    /// </summary>
    [ObservableProperty] private double _alturaLinha = AlturaComSubtitulo;

    public const double AlturaComSubtitulo = 56;
    public const double AlturaSemSubtitulo = 44;

    /// <summary>A lista tem a coluna Situação (e então o filtro Ativos / Inativos / Todos).</summary>
    public bool TemSituacao => _colunas.Any(c => c.Ativo is not null);

    /// <summary>Se o registro está ativo, pela coluna Situação; nulo sem a coluna.</summary>
    public bool? AtivoDe(T item) => _colunas.FirstOrDefault(c => c.Ativo is not null)?.Ativo is { } ativo ? ativo(item) : null;

    public ConteudoGrade Montar(IEnumerable<T> itens, T? aberto)
    {
        var linhas = Ordenar(itens).Select(item => (ILinhaGrade)new LinhaCadastro(
                _chave(item), item, _titulo(item), _subtitulo(item) ?? string.Empty,
                [.. _colunas.Select(c => c.Celula(item, c.Definicao))])
            {
                Selecionada = ReferenceEquals(item, aberto),
                Recuo = Recuo?.Invoke(item) ?? 0,
                Marcador = Marcador?.Invoke(item) ?? string.Empty
            })
            .ToList();
        if (linhas.Count > 0)
            AlturaLinha = linhas.OfType<LinhaCadastro>().Any(l => l.TemSubtitulo) ? AlturaComSubtitulo : AlturaSemSubtitulo;
        return new ConteudoGrade(Colunas, linhas);
    }

    [RelayCommand]
    private void EntrarNaLinha(ILinhaGrade? linha)
    {
        if (linha is LinhaCadastro l) l.Destacada = true;
    }

    [RelayCommand]
    private void SairDaLinha(ILinhaGrade? linha)
    {
        if (linha is LinhaCadastro l) l.Destacada = false;
    }
}

/// <summary>
/// Compara valores de ordenação: textos em ordem natural (números dentro do texto por valor) sem diferenciar maiúsculas e
/// acentos; datas e números pelo valor. Vazios (nulo ou texto em branco) ficam por último nas duas direções.
/// </summary>
internal sealed class ComparadorOrdem(bool decrescente) : IComparer<object?>
{
    private static readonly System.Globalization.CompareInfo Comparacao = new System.Globalization.CultureInfo("pt-BR").CompareInfo;
    private const System.Globalization.CompareOptions Opcoes =
        System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace;

    public int Compare(object? x, object? y)
    {
        var vazioX = x is null || x is string sx && string.IsNullOrWhiteSpace(sx);
        var vazioY = y is null || y is string sy && string.IsNullOrWhiteSpace(sy);
        if (vazioX || vazioY) return vazioX == vazioY ? 0 : vazioX ? 1 : -1;
        var r = x is string a && y is string b ? Natural(a, b)
            : x is IComparable c && x.GetType() == y!.GetType() ? c.CompareTo(y)
            : Natural(x!.ToString() ?? string.Empty, y!.ToString() ?? string.Empty);
        return decrescente ? -r : r;
    }

    /// <summary>"TE-9" &lt; "TE-10"; "2 mudanças" &lt; "12 mudanças".</summary>
    internal static int Natural(string a, string b)
    {
        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
            {
                int fimA = i, fimB = j;
                while (fimA < a.Length && char.IsDigit(a[fimA])) fimA++;
                while (fimB < b.Length && char.IsDigit(b[fimB])) fimB++;
                var na = a[i..fimA].TrimStart('0');
                var nb = b[j..fimB].TrimStart('0');
                var r = na.Length != nb.Length ? na.Length.CompareTo(nb.Length) : string.CompareOrdinal(na, nb);
                if (r != 0) return r;
                i = fimA;
                j = fimB;
            }
            else
            {
                int fimA = i, fimB = j;
                while (fimA < a.Length && !char.IsDigit(a[fimA])) fimA++;
                while (fimB < b.Length && !char.IsDigit(b[fimB])) fimB++;
                var r = Comparacao.Compare(a, i, fimA - i, b, j, fimB - j, Opcoes);
                if (r != 0) return r;
                i = fimA;
                j = fimB;
            }
        }
        return (a.Length - i).CompareTo(b.Length - j);
    }
}
