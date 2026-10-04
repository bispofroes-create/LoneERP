using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Papeis;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enums;
using Lone.Domain.Papeis;

namespace Lone.Cliente.ViewModels.Pessoas;

/// <summary>
/// Aba acima da lista (um só marcada por vez): "Todos", uma natureza, um papel do cadastro de papéis ou uma visão salva
/// (Ids em <see cref="AbasPessoas"/>). O usuário escolhe quais aparecem e em que ordem (<see cref="EditorAbas"/>).
/// </summary>
public sealed partial class FiltroRapido : ObservableObject
{
    public FiltroRapido(string chave, string texto, string dica)
    {
        Chave = chave;
        Texto = texto;
        Dica = dica;
        Natureza = AbasPessoas.NaturezaDe(chave);
        PapelId = AbasPessoas.PapelDe(chave);
        VisaoId = AbasPessoas.VisaoDe(chave);
    }

    public string Chave { get; }
    public string Texto { get; }
    public string Dica { get; }

    /// <summary>Aba de natureza (PF, PJ, estrangeiro): filtra a lista por ela.</summary>
    public NaturezaPessoa? Natureza { get; }

    /// <summary>Aba de papel (Cliente, Transportadora, papel criado pelo usuário...): só quem tem o papel ativo.</summary>
    public Guid? PapelId { get; }

    /// <summary>Aba de visão salva: tocar aplica a visão (troca os filtros da tela).</summary>
    public Guid? VisaoId { get; }

    public bool EhVisao => VisaoId is not null;
    public bool EhTodos => Chave == Todos;

    /// <summary>Visões aparecem com ★ (trocam os filtros; as outras abas só somam um filtro à busca).</summary>
    public string TextoExibido => EhVisao ? "★ " + Texto : Texto;

    [ObservableProperty] private bool _selecionado;

    /// <summary>Quantos há nesta aba (nulo = ainda não contado ou a visão não pôde ser contada).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoQuantidade), nameof(TemQuantidade), nameof(Descricao))]
    private int? _quantidade;

    public string TextoQuantidade => Quantidade?.ToString("N0", TextoTela.Brasil) ?? string.Empty;
    public bool TemQuantidade => Quantidade is not null;

    /// <summary>Nome da aba para o leitor de tela ("Clientes, 5").</summary>
    public string Descricao => Quantidade is { } q ? $"{Texto}, {q}" : Texto;

    /// <summary>Contagem da aba (natureza, papel ou "Todos") vinda da API na página 1. Papel sem ninguém = 0.</summary>
    public int QuantidadeEm(ContagensAtalhosPessoas c) =>
        Natureza is { } n ? c.Naturezas.GetValueOrDefault(n.ToString())
        : PapelId is { } p ? c.Papeis.GetValueOrDefault(p)
        : c.Todos;

    public const string Todos = AbasPessoas.Todos;
    public static readonly string Fisicas = AbasPessoas.Natureza(NaturezaPessoa.Fisica);
    public static readonly string Juridicas = AbasPessoas.Natureza(NaturezaPessoa.Juridica);
    public static readonly string Estrangeiros = AbasPessoas.Natureza(NaturezaPessoa.Estrangeiro);
    public static readonly string Clientes = AbasPessoas.Papel(PapeisSistema.Id(TipoPapel.Cliente));
    public static readonly string Fornecedores = AbasPessoas.Papel(PapeisSistema.Id(TipoPapel.Fornecedor));

    /// <summary>Abas de quem ainda não escolheu (além de "Todos"). Ativos/inativos ficam no filtro "Situação do cadastro".</summary>
    public static IReadOnlyList<string> Padrao { get; } = [Fisicas, Juridicas, Clientes, Fornecedores];

    public static FiltroRapido CriarTodos() => new(Todos, "Todos", "Cadastros em uso (ativos e em análise)") { Selecionado = true };
}

/// <summary>Uma aba que o usuário pode pôr na lista (natureza, papel ou visão), com o nome já no plural.</summary>
public sealed record OpcaoAba(string Id, string Grupo, string Nome, string Dica);

/// <summary>Abas possíveis: naturezas, papéis ativos do cadastro (sem cadastro, os de sistema) e as visões salvas.</summary>
public static class CatalogoAbas
{
    public const string GrupoTipo = "Tipo de pessoa";
    public const string GrupoPapeis = "Papéis";
    public const string GrupoVisoes = "Visões salvas";

    public static List<OpcaoAba> Montar(IReadOnlyList<PapelCadastroDto> papeis, IReadOnlyList<FiltroSalvoDto> visoes)
    {
        var lista = new List<OpcaoAba>
        {
            new(FiltroRapido.Fisicas, GrupoTipo, "Pessoas físicas", "Só pessoas físicas"),
            new(FiltroRapido.Juridicas, GrupoTipo, "Pessoas jurídicas", "Só pessoas jurídicas"),
            new(FiltroRapido.Estrangeiros, GrupoTipo, "Estrangeiros", "Só estrangeiros")
        };
        var fonte = papeis.Count > 0
            ? papeis.Where(p => p.Ativo).OrderBy(p => p.Ordem).ThenBy(p => p.Nome).Select(p => (p.Id, p.Nome))
            : PapeisSistema.Todos.OrderBy(p => p.Ordem).Select(p => (p.Id, p.Nome));
        foreach (var (id, nome) in fonte)
            lista.Add(new OpcaoAba(AbasPessoas.Papel(id), GrupoPapeis, Plural(nome), $"Com o papel {nome} em vigor"));
        foreach (var v in visoes.OrderBy(v => v.Nome, StringComparer.Create(TextoTela.Brasil, ignoreCase: true)))
            lista.Add(new OpcaoAba(AbasPessoas.Visao(v.Id), GrupoVisoes, v.Nome,
                v.Proprio ? "Visão salva: tocar aplica os filtros dela" : $"Visão compartilhada por {v.Autor}: tocar aplica os filtros dela"));
        return lista;
    }

    /// <summary>
    /// Plural da primeira palavra ("Transportadora" → "Transportadoras", "Prestador de serviço" → "Prestadores de
    /// serviço", "Empresa do grupo" → "Empresas do grupo"). Regras simples do português; papel com nome que não se
    /// encaixa fica como está (melhor singular certo que plural errado).
    /// </summary>
    public static string Plural(string nome)
    {
        nome = (nome ?? string.Empty).Trim();
        if (nome.Length == 0) return nome;
        var espaco = nome.IndexOf(' ');
        var primeira = espaco < 0 ? nome : nome[..espaco];
        var resto = espaco < 0 ? string.Empty : nome[espaco..];
        // Duas palavras ou mais: só quando a segunda é preposição ("Prestador de serviço"); "Cliente especial" fica como está.
        var segunda = resto.TrimStart().Split(' ')[0].ToLower(TextoTela.Brasil);
        if (resto.Length > 0 && segunda is not ("de" or "do" or "da" or "dos" or "das" or "em" or "para" or "com")) return nome;
        var baixa = primeira.ToLower(TextoTela.Brasil);
        string plural;
        if (baixa.EndsWith("ão", StringComparison.Ordinal)) return nome; // cidadão/alemão/capitão: sem regra segura
        else if (baixa.EndsWith('s') || baixa.EndsWith('x')) plural = primeira;
        else if (baixa.EndsWith('r') || baixa.EndsWith('z')) plural = primeira + "es";
        else if (baixa.EndsWith('m')) plural = primeira[..^1] + "ns";
        else if (baixa.EndsWith("al") || baixa.EndsWith("ul")) plural = primeira[..^1] + "is";
        else if ("aeiouáéíóúâêôãõ".Contains(baixa[^1])) plural = primeira + "s";
        else plural = primeira;
        if (char.IsUpper(primeira[^1]) && primeira.All(c => !char.IsLetter(c) || char.IsUpper(c)))
            plural = plural.ToUpper(TextoTela.Brasil); // "CLIENTE" → "CLIENTES"
        return plural + resto;
    }
}

/// <summary>Uma aba no editor: caixa de marcar (grupos) ou, em "Nas abas", subir/descer/tirar.</summary>
public sealed partial class ItemAba : ObservableObject
{
    private readonly Action<string, bool> _alternar;
    private bool _definindo;

    public ItemAba(OpcaoAba opcao, bool marcado, Action<string, bool> alternar)
    {
        Opcao = opcao;
        _alternar = alternar;
        _marcado = marcado;
    }

    public OpcaoAba Opcao { get; }
    public string Id => Opcao.Id;
    public string Nome => Opcao.Nome;

    [ObservableProperty] private bool _marcado;
    [ObservableProperty] private bool _habilitado = true;

    partial void OnMarcadoChanged(bool value)
    {
        if (!_definindo) _alternar(Id, value);
    }

    /// <summary>Marca/desmarca sem avisar (a lista já mudou).</summary>
    public void Definir(bool marcado)
    {
        _definindo = true;
        Marcado = marcado;
        _definindo = false;
    }
}

public sealed record GrupoAbas(string Nome, IReadOnlyList<ItemAba> Itens);

/// <summary>
/// Editor das abas ("＋" no fim das abas, ou "Editar abas…" no ⋯): marcar/desmarcar por grupo, ordem com ↑↓, tirar e
/// restaurar o padrão. "Todos" é fixo (primeira). Até <see cref="AbasPessoas.Maximo"/> abas.
/// </summary>
public sealed partial class EditorAbas : ObservableObject
{
    private readonly List<OpcaoAba> _opcoes = [];
    private readonly List<string> _ids = [];

    /// <summary>As abas escolhidas mudaram (a tela remonta as abas e guarda a preferência).</summary>
    public Action? Mudou { get; set; }

    [ObservableProperty] private bool _aberto;
    [ObservableProperty] private string _aviso = string.Empty;

    public ObservableCollection<GrupoAbas> Grupos { get; } = new();
    public ObservableCollection<ItemAba> NaLista { get; } = new();

    /// <summary>Abas escolhidas, na ordem (só as que existem no catálogo agora).</summary>
    public IReadOnlyList<string> Ids => _ids;

    public bool TemAviso => Aviso.Length > 0;
    partial void OnAvisoChanged(string value) => OnPropertyChanged(nameof(TemAviso));

    public string TextoContagem => $"{_ids.Count} de {AbasPessoas.Maximo} abas (além de \"Todos\")";

    /// <summary>Monta a partir das abas possíveis e das escolhidas (nulo = padrão). Id que não existe mais é ignorado.</summary>
    public void Carregar(IEnumerable<OpcaoAba> opcoes, IReadOnlyList<string>? escolhidas)
    {
        _opcoes.Clear();
        _opcoes.AddRange(opcoes);
        _ids.Clear();
        _ids.AddRange((escolhidas ?? FiltroRapido.Padrao).Where(id => _opcoes.Any(o => o.Id == id)).Distinct().Take(AbasPessoas.Maximo));
        Grupos.Clear();
        foreach (var g in _opcoes.GroupBy(o => o.Grupo))
            Grupos.Add(new GrupoAbas(g.Key, [.. g.Select(o => new ItemAba(o, _ids.Contains(o.Id), Alternar))]));
        Reconstruir();
    }

    public OpcaoAba? Opcao(string id) => _opcoes.FirstOrDefault(o => o.Id == id);

    public void Abrir() => Aberto = true;

    [RelayCommand]
    private void Fechar()
    {
        Aberto = false;
        Aviso = string.Empty;
    }

    [RelayCommand]
    private void RestaurarPadrao()
    {
        var padrao = FiltroRapido.Padrao.Where(id => _opcoes.Any(o => o.Id == id)).ToList();
        if (padrao.SequenceEqual(_ids)) return;
        _ids.Clear();
        _ids.AddRange(padrao);
        Aviso = string.Empty;
        Reconstruir();
        Mudou?.Invoke();
    }

    [RelayCommand] private void Subir(ItemAba? item) => Mover(item, -1);
    [RelayCommand] private void Descer(ItemAba? item) => Mover(item, +1);

    [RelayCommand]
    private void Tirar(ItemAba? item)
    {
        if (item is not null) Alternar(item.Id, false);
    }

    private void Mover(ItemAba? item, int passo)
    {
        if (item is null) return;
        var de = _ids.IndexOf(item.Id);
        var para = de + passo;
        if (de < 0 || para < 0 || para >= _ids.Count) return;
        (_ids[de], _ids[para]) = (_ids[para], _ids[de]);
        Reconstruir();
        Mudou?.Invoke();
    }

    private void Alternar(string id, bool mostrar)
    {
        if (mostrar == _ids.Contains(id)) return;
        if (mostrar && _ids.Count >= AbasPessoas.Maximo)
        {
            Aviso = $"Máximo de {AbasPessoas.Maximo} abas. Tire uma antes de pôr outra.";
            Reconstruir(); // desmarca a caixa que o usuário acabou de marcar
            return;
        }
        if (mostrar) _ids.Add(id);
        else _ids.Remove(id);
        Aviso = string.Empty;
        Reconstruir();
        Mudou?.Invoke();
    }

    private void Reconstruir()
    {
        NaLista.Clear();
        foreach (var id in _ids)
            if (Opcao(id) is { } o) NaLista.Add(new ItemAba(o, true, Alternar));
        foreach (var i in Grupos.SelectMany(g => g.Itens)) i.Definir(_ids.Contains(i.Id));
        OnPropertyChanged(nameof(TextoContagem));
    }
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

    /// <summary>
    /// Rodapé da listagem (padrão em docs/UX-ARQUITETURA.md): diz o que está sendo contado e separa o total da página.
    /// "1.248 pessoas · página 1 de 25"; com uma página só, só "51 pessoas"; "1 pessoa"; vazio: "Nenhum registro".
    /// </summary>
    public static string Resumo(int pagina, int tamanho, int total, int naPagina, string singular = "registro", string plural = "registros")
    {
        if (total == 0 || naPagina == 0) return "Nenhum registro";
        var quantos = $"{total.ToString("N0", TextoTela.Brasil)} {(total == 1 ? singular : plural)}";
        var paginas = Paginas(total, tamanho);
        return paginas <= 1 ? quantos
            : $"{quantos} · página {pagina.ToString("N0", TextoTela.Brasil)} de {paginas.ToString("N0", TextoTela.Brasil)}";
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
