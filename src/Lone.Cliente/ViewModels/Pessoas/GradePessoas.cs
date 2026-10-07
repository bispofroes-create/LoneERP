using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.Grade;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Pessoas;
using Lone.Domain.Comum;
using Lone.Domain.Enums;

using GradeCelula = Lone.Cliente.Grade.CelulaGrade;

namespace Lone.Cliente.ViewModels.Pessoas;

/// <summary>O que mudou nas colunas (decide se a tela relê a lista).</summary>
public enum MudancaGrade
{
    /// <summary>Ordem das colunas, coluna tirada, linha de filtro: só remonta as linhas.</summary>
    Aparencia,

    /// <summary>Coluna nova cujo valor a API ainda não mandou: relê a página.</summary>
    Colunas,

    /// <summary>Ordenação pelo cabeçalho: relê a partir da página 1.</summary>
    Ordenacao
}

/// <summary>Selo de um papel na linha (Cliente, Fornecedor...): o texto sempre aparece; a cor só ajuda.</summary>
public sealed record SeloPapel(string Texto, string Tom)
{
    /// <summary>Tom de cada papel de sistema pelo nome; papéis criados pelo usuário ficam neutros.</summary>
    public static SeloPapel De(string nome)
    {
        var tom = nome == NomesPessoa.Papel(TipoPapel.Cliente) ? "Informacao"
            : nome == NomesPessoa.Papel(TipoPapel.Fornecedor) ? "Aviso"
            : nome == NomesPessoa.Papel(TipoPapel.EmpresaDoGrupo) ? "Grupo"
            : "Neutro";
        return new SeloPapel(nome, tom);
    }
}

/// <summary>Uma célula da lista: texto já formatado, selo (situação, tipo) com o tom da cor, ou os selos dos papéis.</summary>
public sealed class CelulaGrade
{
    public const string SemValor = "—";

    public CelulaGrade(string texto, double largura, string? tomSelo = null, IReadOnlyList<SeloPapel>? papeis = null)
    {
        Texto = string.IsNullOrWhiteSpace(texto) ? SemValor : texto;
        Largura = largura;
        TomSelo = tomSelo;
        Papeis = papeis ?? [];
        EhPapeis = papeis is not null;
    }

    public string Texto { get; }
    public double Largura { get; }

    /// <summary>"Sucesso", "Aviso" ou "Neutro" (selo); nulo = texto simples.</summary>
    public string? TomSelo { get; }

    public IReadOnlyList<SeloPapel> Papeis { get; }
    public bool EhPapeis { get; }

    public bool EhSelo => TomSelo is not null && !EhPapeis;
    public bool EhTexto => TomSelo is null && !EhPapeis;
    public bool Vazia => Texto == SemValor;
}

/// <summary>Uma linha da lista: a pessoa, o texto embaixo do nome e as células das colunas escolhidas.</summary>
public sealed partial class LinhaPessoa : ObservableObject, ILinhaGrade
{
    /// <summary>Altura da linha: confortável (padrão) ou compacta. Fixa: a parte presa e a que rola ficam alinhadas.</summary>
    public const double AlturaConfortavel = 56;
    public const double AlturaCompacta = 44;

    /// <summary>Cartão do celular: nome, documento e cidade, papéis e o botão de ligar.</summary>
    public const double AlturaCartao = 92;

    public LinhaPessoa(PessoaResumo pessoa, string subtitulo, IReadOnlyList<CelulaGrade> celulas, bool compacta = false, bool cartao = false,
        IReadOnlyList<GradeCelula>? celulasGrade = null)
    {
        Pessoa = pessoa;
        Subtitulo = subtitulo;
        Celulas = celulas;
        CelulasGrade = celulasGrade ?? [];
        Cartao = cartao;
        Altura = cartao ? AlturaCartao : compacta ? AlturaCompacta : AlturaConfortavel;
        Papeis = [.. pessoa.Papeis.Select(SeloPapel.De)];
    }

    /// <summary>Tela estreita: a linha vira cartão (papéis em selos e o botão de ligar sempre à vista).</summary>
    public bool Cartao { get; }

    public PessoaResumo Pessoa { get; }
    public string Nome => Pessoa.Nome;
    public string Subtitulo { get; }
    public IReadOnlyList<CelulaGrade> Celulas { get; }
    public double Altura { get; }

    // ---- GradeLista (P2-B2, Etapa 3): a mesma linha vista pela grade nova. A tela atual ainda usa Celulas/Altura. ----

    /// <summary>As células no formato da GradeLista (sem largura; a largura é da coluna).</summary>
    public IReadOnlyList<GradeCelula> CelulasGrade { get; }

    Guid ILinhaGrade.Chave => Pessoa.Id;
    IReadOnlyList<GradeCelula> ILinhaGrade.Celulas => CelulasGrade;

    /// <summary>Marcada na grade = está na prévia.</summary>
    public bool Selecionada => NaPrevia;

    partial void OnNaPreviaChanged(bool value) => OnPropertyChanged(nameof(Selecionada));

    /// <summary>Iniciais no avatar (ex.: "Mercearia Gregório" → "MG"; "S.A." e afins não contam).</summary>
    public string Iniciais => IniciaisDe(Pessoa.Nome);

    /// <summary>PF: avatar redondo; PJ e estrangeiro: quadrado de cantos arredondados.</summary>
    public bool EhPessoaFisica => Pessoa.Natureza == NaturezaPessoa.Fisica;
    public bool EhEmpresa => !EhPessoaFisica;

    /// <summary>Selos dos papéis (cartões do celular).</summary>
    public IReadOnlyList<SeloPapel> Papeis { get; }
    public bool TemPapeis => Papeis.Count > 0;

    /// <summary>Ações rápidas ao passar o mouse (e nos cartões do celular): só com o contato principal cadastrado.</summary>
    public bool TemTelefone => !string.IsNullOrWhiteSpace(Pessoa.TelefonePrincipal);
    public bool TemEmail => !string.IsNullOrWhiteSpace(Pessoa.EmailPrincipal);

    /// <summary>Botões rápidos visíveis: mouse em cima (no computador) e contato cadastrado.</summary>
    public bool MostrarLigar => (Destacada || Cartao) && TemTelefone;
    public bool MostrarEmail => Destacada && !Cartao && TemEmail;
    public bool MostrarPapeisNoCartao => Cartao && TemPapeis;

    partial void OnDestacadaChanged(bool value)
    {
        OnPropertyChanged(nameof(MostrarLigar));
        OnPropertyChanged(nameof(MostrarEmail));
    }

    private static readonly HashSet<string> SemIniciais = new(StringComparer.OrdinalIgnoreCase)
        { "S.A.", "SA", "S/A", "LTDA", "LTDA.", "ME", "EPP", "EIRELI", "DE", "DA", "DO", "DAS", "DOS", "E", "&" };

    public static string IniciaisDe(string nome)
    {
        var partes = (nome ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !SemIniciais.Contains(p) && char.IsLetterOrDigit(p[0])).ToList();
        if (partes.Count == 0) return "?";
        var primeira = char.ToUpper(partes[0][0], TextoTela.Brasil);
        return partes.Count == 1 ? primeira.ToString() : $"{primeira}{char.ToUpper(partes[^1][0], TextoTela.Brasil)}";
    }

    /// <summary>Mouse em cima (a parte presa e a que rola acendem juntas).</summary>
    [ObservableProperty] private bool _destacada;

    /// <summary>A pessoa desta linha está na prévia ao lado (a linha fica marcada).</summary>
    [ObservableProperty] private bool _naPrevia;

    [RelayCommand] private void Entrar() => Destacada = true;
    [RelayCommand] private void Sair() => Destacada = false;
}

/// <summary>Uma coluna mostrada: título que ordena (3 cliques) e, se tiver, o filtro rápido embaixo.</summary>
public sealed partial class ColunaGrade : ObservableObject
{
    public ColunaGrade(ColunaListaDto definicao, Action<string> ordenar, FiltroColuna? filtro)
    {
        Definicao = definicao;
        Def = GradePessoas.Definicao(definicao);
        Filtro = filtro;
        OrdenarCommand = new RelayCommand(() => ordenar(definicao.Id));
    }

    public ColunaListaDto Definicao { get; }

    /// <summary>A coluna como a GradeLista a vê (regra de largura aprovada na P2). Uma instância por coluna mostrada.</summary>
    public ColunaGradeDef Def { get; }
    public string Id => Definicao.Id;
    public string Nome => Definicao.Nome;
    public string Titulo => Definicao.Nome.ToUpper(TextoTela.Brasil);
    public double Largura => Definicao.Largura;
    public FiltroColuna? Filtro { get; }
    public bool TemFiltro => Filtro is not null;
    public bool FiltroDigitado => Filtro?.EhDigitado == true;
    public bool FiltroEscolha => Filtro?.EhEscolha == true;
    public IRelayCommand OrdenarCommand { get; }

    /// <summary>▲ crescente, ▼ decrescente, ↕ sem ordenação (mais apagada).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Seta), nameof(Ordenando), nameof(DescricaoOrdenacao))]
    private DirecaoOrdenacao? _direcao;

    public bool Ordenando => Direcao is not null;
    public string Seta => Direcao switch { DirecaoOrdenacao.Crescente => "▲", DirecaoOrdenacao.Decrescente => "▼", _ => "↕" };

    public string DescricaoOrdenacao => Direcao switch
    {
        DirecaoOrdenacao.Crescente => $"{Nome}: ordem crescente. Toque para decrescente.",
        DirecaoOrdenacao.Decrescente => $"{Nome}: ordem decrescente. Toque para voltar à ordem padrão.",
        _ => $"Ordenar por {Nome}"
    };
}

/// <summary>Uma coluna no seletor "Colunas": caixa de marcar (grupos) ou, na parte "Na lista", subir/descer/tirar.</summary>
public sealed partial class ItemSeletorColuna : ObservableObject
{
    private readonly Action<string, bool> _alternar;
    private bool _definindo;

    public ItemSeletorColuna(ColunaListaDto coluna, bool marcado, Action<string, bool> alternar)
    {
        Coluna = coluna;
        _marcado = marcado;
        _alternar = alternar;
    }

    public ColunaListaDto Coluna { get; }
    public string Id => Coluna.Id;
    public string Nome => Coluna.Nome;

    [ObservableProperty] private bool _marcado;
    [ObservableProperty] private bool _visivel = true;

    partial void OnMarcadoChanged(bool value)
    {
        if (!_definindo) _alternar(Id, value);
    }

    /// <summary>Atualiza a caixa sem avisar (a mudança veio da própria grade).</summary>
    public void Definir(bool marcado)
    {
        _definindo = true;
        try { Marcado = marcado; }
        finally { _definindo = false; }
    }
}

public sealed partial class GrupoSeletorColunas : ObservableObject
{
    public GrupoSeletorColunas(string nome, IReadOnlyList<ItemSeletorColuna> itens)
    {
        Nome = nome;
        Itens = itens;
    }

    public string Nome { get; }
    public string Titulo => Nome.ToUpper(TextoTela.Brasil);
    public IReadOnlyList<ItemSeletorColuna> Itens { get; }

    [ObservableProperty] private bool _visivel = true;
}

/// <summary>
/// Colunas da lista de pessoas: quais aparecem e em que ordem (o nome fica sempre, preso à esquerda), a ordenação pelo
/// cabeçalho (crescente → decrescente → padrão) e a linha de filtro das colunas (liga/desliga). Monta as células de cada
/// linha já formatadas. Quem guarda a escolha (preferência do usuário e visões) é a tela, pelo <see cref="Layout"/>.
/// </summary>
public sealed partial class GradePessoas : ObservableObject
{
    /// <summary>Largura da coluna do nome (presa à esquerda).</summary>
    public const double LarguraNome = 340;

    /// <summary>Até onde o nome encolhe em tela estreita, antes de a tabela rolar para o lado (P2).</summary>
    public const double LarguraMinimaNome = 260;

    private readonly List<ColunaListaDto> _catalogo = [];
    private readonly List<string> _ids = [];

    /// <summary>
    /// Colunas guardadas que o usuário não pode ver agora (ex.: perdeu a permissão): não aparecem, mas continuam na
    /// preferência para voltar quando a permissão voltar (o servidor também as mantém).
    /// </summary>
    private readonly List<string> _guardadas = [];
    private OrdenacaoLista? _ordenacao;
    private PainelFiltrosPessoas? _painel;

    /// <summary>Avisa a tela que as colunas mudaram (e se precisa ler a lista de novo).</summary>
    public Action<MudancaGrade>? Mudou { get; set; }

    public ObservableCollection<ColunaGrade> Visiveis { get; } = new();
    public ObservableCollection<GrupoSeletorColunas> Grupos { get; } = new();
    public ObservableCollection<ItemSeletorColuna> NaLista { get; } = new();

    /// <summary>Coluna do nome: ordena e filtra como as outras, mas não sai da lista.</summary>
    public ColunaGrade Nome { get; private set; }

    public GradePessoas() => Nome = CriarNome();

    public bool Carregada { get; private set; }

    [ObservableProperty] private bool _filtroNasColunas;

    /// <summary>Linhas mais baixas (cabe mais na tela). Fica guardado com as colunas.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoDensidade))]
    private bool _compacta;

    public string TextoDensidade => Compacta ? "Densidade: compacta" : "Densidade: confortável";

    partial void OnCompactaChanged(bool value)
    {
        OnPropertyChanged(nameof(AlturaLinha));
        Mudou?.Invoke(MudancaGrade.Aparencia);
    }

    [RelayCommand] private void AlternarDensidade() => Compacta = !Compacta;

    /// <summary>Tela larga o bastante para as colunas (no celular, só o nome com o documento embaixo).</summary>
    [ObservableProperty] private bool _mostrarColunas = true;

    [ObservableProperty] private bool _seletorAberto;

    [ObservableProperty] private string _buscaColuna = string.Empty;

    public double LarguraColunas => Visiveis.Sum(c => c.Largura);
    public string TextoBotaoColunas => $"Colunas ({Visiveis.Count + 1})";
    public OrdenacaoLista? Ordenacao => _ordenacao;

    public string TextoOrdenacao => _ordenacao is { } o && Titulo(o.Coluna) is { } nome
        ? $"Ordenado por {nome} ({(o.Direcao == DirecaoOrdenacao.Crescente ? "crescente" : "decrescente")})"
        : string.Empty;

    /// <summary>Linha de filtro visível: ligada e com colunas (no celular o botão não aparece, então ela também não).</summary>
    public bool MostrarLinhaFiltro => FiltroNasColunas && MostrarColunas;

    partial void OnFiltroNasColunasChanged(bool value)
    {
        OnPropertyChanged(nameof(MostrarLinhaFiltro));
        Mudou?.Invoke(MudancaGrade.Aparencia);
    }

    partial void OnMostrarColunasChanged(bool value)
    {
        OnPropertyChanged(nameof(MostrarLinhaFiltro));
        OnPropertyChanged(nameof(SemColunas));
        OnPropertyChanged(nameof(AlturaLinha));
    }

    /// <summary>Celular: sem colunas nem cabeçalho (ordenar pelo botão "Ordenar").</summary>
    public bool SemColunas => !MostrarColunas;

    partial void OnBuscaColunaChanged(string value)
    {
        var termo = TextoBusca.Normalizar(value);
        foreach (var g in Grupos)
        {
            foreach (var i in g.Itens)
                i.Visivel = termo.Length == 0 || TextoBusca.Normalizar(i.Nome + " " + g.Nome).Contains(termo, StringComparison.Ordinal);
            g.Visivel = g.Itens.Any(i => i.Visivel);
        }
    }

    /// <summary>Monta a partir do catálogo (colunas permitidas) e do que o usuário deixou da última vez.</summary>
    public void Carregar(IEnumerable<ColunaListaDto> colunas, LayoutListaPessoas? layout, PainelFiltrosPessoas painel)
    {
        _painel = painel;
        _catalogo.Clear();
        _catalogo.AddRange(colunas);
        Grupos.Clear();
        foreach (var g in _catalogo.GroupBy(c => c.Grupo))
            Grupos.Add(new GrupoSeletorColunas(g.Key, [.. g.Select(c => new ItemSeletorColuna(c, false, Alternar))]));
        Nome = CriarNome();
        OnPropertyChanged(nameof(Nome));
        OnPropertyChanged(nameof(ColunaFixa));
        Visiveis.Clear(); // o painel foi remontado: os filtros das colunas ligam nos campos novos
        Carregada = true;
        AplicarLayout(layout ?? new LayoutListaPessoas { Colunas = Padrao() });
    }

    /// <summary>Colunas, ordenação e linha de filtro (para guardar e para as visões).</summary>
    public LayoutListaPessoas Layout() => new()
    {
        Colunas = [.. _ids, .. _guardadas.Where(id => !_ids.Contains(id))],
        Ordenacao = _ordenacao is null ? null : new OrdenacaoLista { Coluna = _ordenacao.Coluna, Direcao = _ordenacao.Direcao },
        FiltroNasColunas = FiltroNasColunas,
        Compacta = Compacta
    };

    /// <summary>Aplica colunas e ordenação (de uma preferência ou visão). Coluna que o usuário não pode ver é ignorada.</summary>
    public void AplicarLayout(LayoutListaPessoas layout)
    {
        _ids.Clear();
        _guardadas.Clear();
        foreach (var id in (layout.Colunas ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct())
            (_catalogo.Any(c => c.Id == id) ? _ids : _guardadas).Add(id);
        _ordenacao = layout.Ordenacao is { } o && (o.Coluna == ColunasPessoas.Nome || _catalogo.Any(c => c.Id == o.Coluna))
            ? new OrdenacaoLista { Coluna = o.Coluna, Direcao = o.Direcao }
            : null;
        // Direto nos campos, de propósito: as propriedades geradas chamariam OnFiltroNasColunasChanged/OnCompactaChanged, que
        // avisam a tela (Mudou) e fariam releitura e gravação da preferência só por aplicar um layout.
#pragma warning disable MVVMTK0034
        if (SetProperty(ref _filtroNasColunas, layout.FiltroNasColunas, nameof(FiltroNasColunas)))
            OnPropertyChanged(nameof(MostrarLinhaFiltro));
        if (SetProperty(ref _compacta, layout.Compacta, nameof(Compacta)))
        {
            OnPropertyChanged(nameof(TextoDensidade));
            OnPropertyChanged(nameof(AlturaLinha));
        }
#pragma warning restore MVVMTK0034
        Reconstruir();
    }

    /// <summary>Colunas cujo valor a API precisa mandar (as outras a linha já traz).</summary>
    public List<string> ColunasExtras() => [.. _ids.Where(id => Coluna(id) is { } c && !VemNaLinha(c.Tipo))];

    private static bool VemNaLinha(TipoColunaLista tipo) => tipo >= TipoColunaLista.Codigo;

    // ---- Ordenação: crescente → decrescente → padrão ----

    public void Ordenar(string id)
    {
        _ordenacao = _ordenacao?.Coluna != id ? new OrdenacaoLista { Coluna = id, Direcao = DirecaoOrdenacao.Crescente }
            : _ordenacao.Direcao == DirecaoOrdenacao.Crescente ? new OrdenacaoLista { Coluna = id, Direcao = DirecaoOrdenacao.Decrescente }
            : null;
        AtualizarSetas();
        Mudou?.Invoke(MudancaGrade.Ordenacao);
    }

    // ---- Seletor de colunas ----

    [RelayCommand] private void AlternarSeletor() => SeletorAberto = !SeletorAberto;

    [RelayCommand]
    private void FecharSeletor()
    {
        SeletorAberto = false;
        BuscaColuna = string.Empty;
    }

    [RelayCommand] private void AlternarFiltroNasColunas() => FiltroNasColunas = !FiltroNasColunas;

    [RelayCommand]
    private void MarcarTodas()
    {
        var novas = _catalogo.Select(c => c.Id).Where(id => !_ids.Contains(id)).ToList();
        if (novas.Count == 0) return;
        _ids.AddRange(novas);
        Reconstruir();
        Mudou?.Invoke(Mudanca(novas));
    }

    [RelayCommand]
    private void RestaurarPadrao()
    {
        var padrao = Padrao();
        if (padrao.SequenceEqual(_ids)) return;
        var novas = padrao.Except(_ids).ToList();
        var anteriores = _ids.ToList();
        _ids.Clear();
        _ids.AddRange(padrao);
        var semOrdenacao = SoltarOrdenacaoSeTirou(anteriores.Except(_ids));
        Reconstruir();
        Mudou?.Invoke(semOrdenacao ? MudancaGrade.Ordenacao : Mudanca(novas));
    }

    [RelayCommand]
    private void Subir(ItemSeletorColuna? item) => Mover(item, -1);

    [RelayCommand]
    private void Descer(ItemSeletorColuna? item) => Mover(item, +1);

    [RelayCommand]
    private void Tirar(ItemSeletorColuna? item)
    {
        if (item is not null) Alternar(item.Id, false);
    }

    private void Mover(ItemSeletorColuna? item, int passo)
    {
        if (item is null) return;
        var de = _ids.IndexOf(item.Id);
        var para = de + passo;
        if (de < 0 || para < 0 || para >= _ids.Count) return;
        (_ids[de], _ids[para]) = (_ids[para], _ids[de]);
        Reconstruir();
        Mudou?.Invoke(MudancaGrade.Aparencia);
    }

    private void Alternar(string id, bool mostrar)
    {
        if (mostrar == _ids.Contains(id)) return;
        if (mostrar) _ids.Add(id);
        else _ids.Remove(id);
        var semOrdenacao = !mostrar && SoltarOrdenacaoSeTirou([id]);
        Reconstruir();
        Mudou?.Invoke(mostrar ? Mudanca([id]) : semOrdenacao ? MudancaGrade.Ordenacao : MudancaGrade.Aparencia);
    }

    /// <summary>
    /// A coluna que ordenava saiu da lista: volta à ordem padrão (senão a lista ficaria ordenada por algo que não se vê e
    /// sem cabeçalho para desfazer). Verdadeiro se a ordenação mudou (a lista precisa ser relida).
    /// </summary>
    private bool SoltarOrdenacaoSeTirou(IEnumerable<string> tiradas)
    {
        if (_ordenacao is not { } o || !tiradas.Contains(o.Coluna)) return false;
        _ordenacao = null;
        return true;
    }

    // ---- "Ordenar por" (celular: não há cabeçalho para clicar) ----

    /// <summary>Colunas pelas quais dá para ordenar: o nome e todas as do catálogo (visíveis ou não).</summary>
    public IReadOnlyList<(string Id, string Nome)> ColunasOrdenaveis() =>
        [(ColunasPessoas.Nome, "Nome"), .. _catalogo.Select(c => (c.Id, c.Nome))];

    /// <summary>Escolha direta da ordenação (nula = ordem padrão).</summary>
    public void OrdenarPor(string? id, DirecaoOrdenacao direcao)
    {
        var nova = id is null ? null : new OrdenacaoLista { Coluna = id, Direcao = direcao };
        if (nova?.Coluna == _ordenacao?.Coluna && nova?.Direcao == _ordenacao?.Direcao) return;
        _ordenacao = nova;
        AtualizarSetas();
        Mudou?.Invoke(MudancaGrade.Ordenacao);
    }

    /// <summary>Texto do botão "Ordenar" do celular (ex.: "Nome ↑").</summary>
    public string TextoBotaoOrdenar => _ordenacao is { } o && Titulo(o.Coluna) is { } nome
        ? $"{nome} {(o.Direcao == DirecaoOrdenacao.Crescente ? "↑" : "↓")}"
        : "Ordenar";

    /// <summary>Coluna nova sem valor na linha: a API precisa mandar (relê); senão, só remonta.</summary>
    private MudancaGrade Mudanca(IEnumerable<string> novas) =>
        novas.Any(id => Coluna(id) is { } c && !VemNaLinha(c.Tipo)) ? MudancaGrade.Colunas : MudancaGrade.Aparencia;

    // ---- Células ----

    /// <summary>A linha da pessoa com as células das colunas escolhidas, já formatadas.</summary>
    /// <summary>
    /// Com colunas: "Cód. 000123" embaixo do nome. No celular (cartões): documento e cidade embaixo do nome, com os
    /// papéis em selos.
    /// </summary>
    public LinhaPessoa Linha(PessoaResumo p) =>
        new(p,
            MostrarColunas ? $"Cód. {p.CodigoFormatado}" : string.Join(" · ", new[] { p.DocumentoFormatado, p.Local }.Where(t => t.Length > 0)),
            MostrarColunas ? [.. Visiveis.Select(c => Celula(c.Definicao, p))] : [],
            compacta: Compacta && MostrarColunas,
            cartao: !MostrarColunas,
            celulasGrade: MostrarColunas ? [.. Visiveis.Select(c => CelulaGradeNova(c.Def, c.Definicao, p))] : []);

    // ---- GradeLista (P2-B2, Etapa 3): colunas, parte fixa, altura e conteúdo no formato da grade nova ----

    /// <summary>A coluna do nome (parte fixa da grade): a que mais cresce; encolhe até 260 antes de a tabela rolar para o lado.</summary>
    public ColunaGradeDef ColunaFixa => Nome.Def;

    /// <summary>Colunas de célula na ordem mostrada (no cartão, nenhuma). Mesmas instâncias enquanto a escolha não muda.</summary>
    public IReadOnlyList<ColunaGradeDef> ColunasGrade => MostrarColunas ? [.. Visiveis.Select(c => c.Def)] : [];

    /// <summary>Altura das linhas na grade: confortável, compacta ou cartão (a densidade não reconstrói as linhas).</summary>
    public double AlturaLinha => !MostrarColunas ? LinhaPessoa.AlturaCartao
        : Compacta ? LinhaPessoa.AlturaCompacta : LinhaPessoa.AlturaConfortavel;

    /// <summary>Colunas e linhas juntas, numa troca só. As linhas precisam ter sido montadas com as colunas atuais.</summary>
    public ConteudoGrade Conteudo(IReadOnlyList<LinhaPessoa> linhas) => new(ColunasGrade, linhas);

    /// <summary>Peso do nome no espaço livre: a coluna principal cresce mais que as de texto (peso 1).</summary>
    public const double PesoNome = 3;

    /// <summary>
    /// Regra de largura de cada coluna (revisão de 03/10/2026, substitui a P2; padrão em docs/UX-ARQUITETURA.md):
    /// <list type="bullet">
    /// <item><b>Nome / razão social</b> (parte fixa): cresce com o maior peso (3); encolhe até 260 quando falta espaço.</item>
    /// <item><b>Texto livre</b> (Papéis, Cidade, e-mail, bairro, nome fantasia...): cresce com peso 1, a partir da mínima
    /// (Papéis e Cidade 160; as outras, a largura do catálogo).</item>
    /// <item><b>Dado curto</b> (código, CPF/CNPJ, tipo, situação, datas, números, valores, telefone, CEP, opções):
    /// largura fixa; nunca cresce. A UF é dado curto (80).</item>
    /// </list>
    /// O tipo de célula segue a montagem de <see cref="CelulaGradeNova"/>.
    /// </summary>
    public static ColunaGradeDef Definicao(ColunaListaDto c)
    {
        var titulo = c.Nome.ToUpper(TextoTela.Brasil);
        if (c.Id == ColunasPessoas.Nome)
            return ColunaGradeDef.Proporcional(c.Id, titulo, TipoCelula.Texto, peso: PesoNome, minima: LarguraMinimaNome);
        var catalogo = c.Largura > 0 ? c.Largura : 120;
        return c.Tipo switch
        {
            TipoColunaLista.Documento => ColunaGradeDef.Fixa(c.Id, titulo, TipoCelula.Texto, 180),
            TipoColunaLista.Natureza => ColunaGradeDef.Fixa(c.Id, titulo, TipoCelula.Selo, 80),
            TipoColunaLista.Situacao => ColunaGradeDef.Fixa(c.Id, titulo, TipoCelula.Selo, 120),
            TipoColunaLista.Papeis => ColunaGradeDef.Proporcional(c.Id, titulo, TipoCelula.Pilulas, peso: 1, minima: 160),
            TipoColunaLista.Cidade => ColunaGradeDef.Proporcional(c.Id, titulo, TipoCelula.Texto, peso: 1, minima: 160),
            TipoColunaLista.Uf => ColunaGradeDef.Fixa(c.Id, titulo, TipoCelula.Texto, 80),
            TipoColunaLista.Texto => ColunaGradeDef.Proporcional(c.Id, titulo, TipoCelula.Texto, peso: 1, minima: catalogo),
            _ => ColunaGradeDef.Fixa(c.Id, titulo, TipoCelula.Texto, catalogo)
        };
    }

    /// <summary>A mesma formatação de <see cref="Celula"/>, no formato da GradeLista (avisos como texto com tom).</summary>
    public static GradeCelula CelulaGradeNova(ColunaGradeDef d, ColunaListaDto c, PessoaResumo p) => c.Tipo switch
    {
        TipoColunaLista.Codigo => GradeCelula.DeTexto(d, p.CodigoFormatado),
        TipoColunaLista.Documento when p.DocumentoFormatado.Length == 0 && p.Natureza != NaturezaPessoa.Estrangeiro =>
            GradeCelula.DeTexto(d, p.Natureza == NaturezaPessoa.Fisica ? "Sem CPF" : "Sem CNPJ", "Aviso"),
        TipoColunaLista.Documento => GradeCelula.DeTexto(d, p.DocumentoFormatado),
        TipoColunaLista.Natureza => GradeCelula.DeSelo(d, p.NaturezaSigla, "Neutro"),
        TipoColunaLista.Papeis => GradeCelula.DePilulas(d, [.. p.Papeis.Select(SeloPapel.De).Select(s => new SeloGrade(s.Texto, s.Tom))]),
        TipoColunaLista.Cidade when p.MunicipioACorrigir =>
            GradeCelula.DeTexto(d, Cidade(p).Length > 0 ? Cidade(p) + " (a corrigir)" : "Município a corrigir", "Aviso"),
        TipoColunaLista.Cidade => GradeCelula.DeTexto(d, Cidade(p)),
        TipoColunaLista.Uf => GradeCelula.DeTexto(d, p.Uf),
        TipoColunaLista.Situacao => GradeCelula.DeSelo(d, p.SituacaoSelo, p.Situacao switch
        {
            SituacaoPessoa.Ativo => "Sucesso",
            SituacaoPessoa.EmAnalise => "Aviso",
            _ => "Neutro"
        }),
        _ => GradeCelula.DeTexto(d, Formatar(c, p.Valores.GetValueOrDefault(c.Id)))
    };

    public static CelulaGrade Celula(ColunaListaDto c, PessoaResumo p) => c.Tipo switch
    {
        TipoColunaLista.Codigo => new(p.CodigoFormatado, c.Largura),
        // Sem documento: o aviso fica à vista na própria coluna (pendência de cadastro).
        TipoColunaLista.Documento when p.DocumentoFormatado.Length == 0 && p.Natureza != NaturezaPessoa.Estrangeiro =>
            new(p.Natureza == NaturezaPessoa.Fisica ? "Sem CPF" : "Sem CNPJ", c.Largura, "Aviso"),
        TipoColunaLista.Documento => new(p.DocumentoFormatado, c.Largura),
        TipoColunaLista.Natureza => new(p.NaturezaSigla, c.Largura, "Neutro"),
        TipoColunaLista.Papeis => new(p.PapeisTexto, c.Largura, papeis: [.. p.Papeis.Select(SeloPapel.De)]),
        TipoColunaLista.Cidade when p.MunicipioACorrigir => new(Cidade(p).Length > 0 ? Cidade(p) + " (a corrigir)" : "Município a corrigir", c.Largura, "Aviso"),
        TipoColunaLista.Cidade => new(Cidade(p), c.Largura),
        TipoColunaLista.Uf => new(p.Uf ?? string.Empty, c.Largura),
        TipoColunaLista.Situacao => new(p.SituacaoSelo, c.Largura, p.Situacao switch
        {
            SituacaoPessoa.Ativo => "Sucesso",
            SituacaoPessoa.EmAnalise => "Aviso",
            _ => "Neutro"
        }),
        _ => new(Formatar(c, p.Valores.GetValueOrDefault(c.Id)), c.Largura)
    };

    /// <summary>Só o nome da cidade (a UF tem coluna própria desde 03/10/2026).</summary>
    private static string Cidade(PessoaResumo p) => p.Cidade ?? string.Empty;

    /// <summary>Valor da API (texto invariável) no formato da tela.</summary>
    public static string Formatar(ColunaListaDto c, string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return string.Empty;
        return c.Tipo switch
        {
            TipoColunaLista.Numero when decimal.TryParse(valor, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) =>
                TextoTela.Numero(n),
            TipoColunaLista.Moeda when decimal.TryParse(valor, NumberStyles.Number, CultureInfo.InvariantCulture, out var m) =>
                m.ToString("C2", TextoTela.Brasil),
            TipoColunaLista.Data when DateOnly.TryParseExact(valor, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) =>
                TextoTela.Data(d),
            TipoColunaLista.Telefone => Mascara.Aplicar(TipoMascara.Telefone, valor),
            // Só o CEP do Brasil (8 algarismos) leva a máscara; o código postal do exterior ("SW1A 1AA", "1000-001") aparece como gravado.
            TipoColunaLista.Cep => valor.Length == 8 && valor.All(char.IsAsciiDigit) ? Mascara.Aplicar(TipoMascara.Cep, valor) : valor,
            TipoColunaLista.Opcao => c.Opcoes.FirstOrDefault(o => o.Valor == valor)?.Texto ?? valor,
            _ => valor
        };
    }

    // ---- Montagem ----

    /// <summary>Colunas de sempre, sem filtro rápido: quando o catálogo não chega (a lista continua útil).</summary>
    public static List<ColunaListaDto> ColunasBasicas() =>
    [
        new() { Id = CamposFiltroPessoas.Documento, Grupo = "Identificação", Nome = "CPF / CNPJ", Tipo = TipoColunaLista.Documento, Largura = 180, Padrao = true },
        new() { Id = CamposFiltroPessoas.Natureza, Grupo = "Identificação", Nome = "Tipo", Tipo = TipoColunaLista.Natureza, Largura = 80, Padrao = true },
        new() { Id = CamposFiltroPessoas.Papeis, Grupo = "Identificação", Nome = "Papéis", Tipo = TipoColunaLista.Papeis, Largura = 210, Padrao = true },
        new() { Id = CamposFiltroPessoas.Cidade, Grupo = "Endereços", Nome = "Cidade", Tipo = TipoColunaLista.Cidade, Largura = 200, Padrao = true },
        new() { Id = CamposFiltroPessoas.Uf, Grupo = "Endereços", Nome = "UF", Tipo = TipoColunaLista.Uf, Largura = 80, Padrao = true },
        new() { Id = CamposFiltroPessoas.Situacao, Grupo = "Situação", Nome = "Situação", Tipo = TipoColunaLista.Situacao, Largura = 120, Padrao = true }
    ];

    private ColunaListaDto? Coluna(string id) => _catalogo.FirstOrDefault(c => c.Id == id);

    private List<string> Padrao() => [.. _catalogo.Where(c => c.Padrao).Select(c => c.Id)];

    private string? Titulo(string id) => id == ColunasPessoas.Nome ? "Nome" : Coluna(id)?.Nome;

    private ColunaGrade CriarNome() =>
        new(new ColunaListaDto { Id = ColunasPessoas.Nome, Nome = "Nome / razão social", Largura = LarguraNome, Tipo = TipoColunaLista.Texto },
            Ordenar, Filtro(CamposFiltroPessoas.Nome));

    private FiltroColuna? Filtro(string? campo) =>
        campo is not null && _painel?.CampoPorId(campo) is { } item ? new FiltroColuna(item) : null;

    private void Reconstruir()
    {
        // Reaproveita a coluna já montada (e o filtro dela, com o que está digitado).
        var anteriores = Visiveis.ToDictionary(c => c.Id);
        Visiveis.Clear();
        foreach (var id in _ids)
            if (Coluna(id) is { } c)
                Visiveis.Add(anteriores.TryGetValue(id, out var existente) ? existente : new ColunaGrade(c, Ordenar, Filtro(c.CampoFiltro)));

        NaLista.Clear();
        foreach (var id in _ids)
            if (Coluna(id) is { } c)
                NaLista.Add(new ItemSeletorColuna(c, true, Alternar));
        foreach (var i in Grupos.SelectMany(g => g.Itens)) i.Definir(_ids.Contains(i.Id));

        AtualizarSetas();
        OnPropertyChanged(nameof(LarguraColunas));
        OnPropertyChanged(nameof(TextoBotaoColunas));
    }

    private void AtualizarSetas()
    {
        foreach (var c in Visiveis.Append(Nome))
            c.Direcao = _ordenacao?.Coluna == c.Id ? _ordenacao.Direcao : null;
        OnPropertyChanged(nameof(TextoOrdenacao));
        OnPropertyChanged(nameof(TextoBotaoOrdenar));
        OnPropertyChanged(nameof(Ordenacao));
        OnPropertyChanged(nameof(ColunaOrdenadaChave));
        OnPropertyChanged(nameof(OrdemDecrescente));
    }

    // ---- GradeLista: ordenação pelo título e destaque pelo ponteiro (a grade repassa; a regra continua aqui) ----

    /// <summary>Chave da coluna que ordena agora (nula = ordem padrão): a grade mostra ▲/▼ no título dela.</summary>
    public string? ColunaOrdenadaChave => _ordenacao?.Coluna;

    public bool OrdemDecrescente => _ordenacao?.Direcao == DirecaoOrdenacao.Decrescente;

    /// <summary>Toque no título da grade: a mesma regra do cabeçalho de antes (crescente → decrescente → padrão).</summary>
    [RelayCommand]
    private void OrdenarColuna(ColunaGradeDef? coluna)
    {
        if (coluna is not null) Ordenar(coluna.Chave);
    }

    [RelayCommand]
    private void EntrarNaLinha(LinhaPessoa? linha)
    {
        if (linha is not null) linha.Destacada = true;
    }

    [RelayCommand]
    private void SairDaLinha(LinhaPessoa? linha)
    {
        if (linha is not null) linha.Destacada = false;
    }
}
