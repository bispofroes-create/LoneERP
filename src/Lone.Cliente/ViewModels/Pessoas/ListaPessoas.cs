using CommunityToolkit.Mvvm.ComponentModel;
using Lone.Cliente.ViewModels.Comum;

namespace Lone.Cliente.ViewModels.Pessoas;

/// <summary>Atalho de filtro da lista ("Todos", "Pessoas físicas", "Clientes"...): um só marcado por vez.</summary>
public sealed partial class FiltroRapido : ObservableObject
{
    public FiltroRapido(string chave, string texto, string dica)
    {
        Chave = chave;
        Texto = texto;
        Dica = dica;
    }

    public string Chave { get; }
    public string Texto { get; }
    public string Dica { get; }

    [ObservableProperty] private bool _selecionado;

    public const string Todos = "todos";
    public const string Fisicas = "pf";
    public const string Juridicas = "pj";
    public const string Clientes = "clientes";
    public const string Fornecedores = "fornecedores";
    public const string Ativos = "ativos";
    public const string Inativos = "inativos";

    public static FiltroRapido[] Criar() =>
    [
        new(Todos, "Todos", "Cadastros em uso (ativos e em análise)") { Selecionado = true },
        new(Fisicas, "Pessoas físicas", "Só pessoas físicas"),
        new(Juridicas, "Pessoas jurídicas", "Só pessoas jurídicas"),
        new(Clientes, "Clientes", "Com o papel Cliente em vigor"),
        new(Fornecedores, "Fornecedores", "Com o papel Fornecedor em vigor"),
        new(Ativos, "Ativos", "Só a situação Ativo (sem os em análise)"),
        new(Inativos, "Inativos", "Só inativos e arquivados")
    ];
}

/// <summary>Botão da paginação: um número de página ou "…" (sem ação).</summary>
public sealed record PaginaItem(int? Numero, bool Atual)
{
    public string Texto => Numero?.ToString(TextoTela.Brasil) ?? "…";
    public bool Clicavel => Numero is not null && !Atual;
}

/// <summary>Cálculo da paginação (só números; a tela desenha).</summary>
public static class Paginacao
{
    /// <summary>Total de páginas (pelo menos 1).</summary>
    public static int Paginas(int total, int tamanho) => Math.Max(1, (int)Math.Ceiling(total / (double)Math.Max(1, tamanho)));

    /// <summary>"Mostrando 1–50 de 1.248" / "Nenhum registro".</summary>
    public static string Resumo(int pagina, int tamanho, int total, int naPagina)
    {
        if (total == 0 || naPagina == 0) return "Nenhum registro";
        var inicio = (pagina - 1) * tamanho + 1;
        var fim = inicio + naPagina - 1;
        return $"Mostrando {inicio.ToString("N0", TextoTela.Brasil)}–{fim.ToString("N0", TextoTela.Brasil)} de {total.ToString("N0", TextoTela.Brasil)}";
    }

    /// <summary>Janela de páginas: sempre a primeira e a última, e até duas de cada lado da atual ("1 … 4 5 6 7 8 … 25").</summary>
    public static IReadOnlyList<PaginaItem> Janela(int atual, int paginas)
    {
        var numeros = new SortedSet<int> { 1, paginas };
        for (var p = atual - 2; p <= atual + 2; p++)
            if (p >= 1 && p <= paginas) numeros.Add(p);

        var itens = new List<PaginaItem>();
        int? anterior = null;
        foreach (var n in numeros)
        {
            if (anterior is { } a && n - a > 1) itens.Add(new PaginaItem(null, false));
            itens.Add(new PaginaItem(n, n == atual));
            anterior = n;
        }
        return itens;
    }
}
