using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Pessoas;

namespace Lone.Cliente.ViewModels.Pessoas;

/// <summary>O que a tela faz com os relacionamentos (gravados na hora, à parte do "Salvar" da ficha).</summary>
public sealed class AcoesRelacionamento
{
    public Func<Task>? BuscarPessoa { get; set; }
    public Func<Task>? Incluir { get; set; }
    public Func<RelacionamentoItem, Task>? Encerrar { get; set; }
    public Func<RelacionamentoItem, Task>? Desativar { get; set; }
}

/// <summary>Tipo escolhido na ficha: o tipo e o sentido ("Sócio de" = a ficha é a origem; "Tem como sócio" = é o destino).</summary>
public sealed record TipoRelacionamentoEscolhido(Guid TipoId, bool Inverso, bool Societario);

/// <summary>Um relacionamento visto da ficha aberta (ex.: "Sócio de · ABC Comércio").</summary>
public sealed partial class RelacionamentoItem : ObservableObject
{
    private readonly AcoesRelacionamento _acoes;

    public RelacionamentoItem(PessoaRelacionamentoDto dados, AcoesRelacionamento acoes)
    {
        _acoes = acoes;
        Dados = dados;
    }

    public PessoaRelacionamentoDto Dados { get; private set; }
    public Guid Id => Dados.Id;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Visivel))]
    private bool _mostrarEncerrados;

    /// <summary>Vale hoje, ou está em aberto (começa numa data futura). Encerrados e desativados: só em "Mostrar encerrados".</summary>
    public bool Aberto => Dados.Vigente || (Dados.Ativo && Dados.FimEm is null);

    public bool Visivel => Aberto || MostrarEncerrados;

    public string Titulo => $"{Dados.Tipo} · {Dados.OutraPessoaNome}";

    public string Detalhe => string.Join("  ·  ", new[]
    {
        Dados.InicioEm is { } inicio ? "desde " + TextoTela.Data(inicio) : string.Empty,
        Dados.FimEm is { } fim ? "até " + TextoTela.Data(fim) : string.Empty,
        !Dados.Ativo ? "desativado (lançado por engano)" : Aberto ? string.Empty : "encerrado",
        Dados.Observacoes ?? string.Empty
    }.Where(t => t.Length > 0));

    public bool PodeEncerrar => Dados.Ativo && Dados.FimEm is null;
    public bool PodeDesativar => Dados.Ativo;

    public void Atualizar(PessoaRelacionamentoDto dados)
    {
        Dados = dados;
        OnPropertyChanged(string.Empty);
    }

    [RelayCommand]
    private Task EncerrarAsync() => _acoes.Encerrar?.Invoke(this) ?? Task.CompletedTask;

    [RelayCommand]
    private Task DesativarAsync() => _acoes.Desativar?.Invoke(this) ?? Task.CompletedTask;
}

/// <summary>
/// Relacionamentos da pessoa com outros cadastros (sócio de, administrador de, contato de, responsável por...). Separados
/// do cadastro e da LGPD: cada um é gravado na hora pela API, nunca apagado (encerrar preenche o fim; desativar marca o
/// lançado por engano). Opcional: nenhuma pessoa precisa de relacionamento para ser usada.
/// </summary>
public sealed partial class RelacionamentosFormulario : ObservableObject
{
    public static readonly Opcao<TipoRelacionamentoEscolhido?> SemTipo = new(null, "— escolha —");

    public AcoesRelacionamento Acoes { get; } = new();

    public ObservableCollection<RelacionamentoItem> Itens { get; } = new();

    /// <summary>Lidos para esta ficha (a aba lê na primeira vez que abre).</summary>
    public bool Carregados { get; private set; }

    [ObservableProperty] private bool _mostrarEncerrados;

    public bool TemEncerrados => Itens.Any(i => !i.Aberto);
    public bool Vazio => Carregados && !Itens.Any(i => i.Visivel);

    /// <summary>Cada tipo nos dois sentidos ("Sócio de" / "Tem como sócio"). Array: o Picker precisa de IList.</summary>
    [ObservableProperty] private Opcao<TipoRelacionamentoEscolhido?>[] _tipos = [SemTipo];
    [ObservableProperty] private Opcao<TipoRelacionamentoEscolhido?> _novoTipo = SemTipo;

    [ObservableProperty] private string _buscaPessoa = string.Empty;
    public ObservableCollection<PessoaResumo> ResultadosBusca { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoPessoaEscolhida), nameof(TemPessoaEscolhida))]
    private PessoaResumo? _pessoaEscolhida;

    public bool TemPessoaEscolhida => PessoaEscolhida is not null;
    public string TextoPessoaEscolhida => PessoaEscolhida is { } p ? $"Escolhida: {p.Nome} ({p.Detalhe})" : string.Empty;

    [ObservableProperty] private string _novoInicio = string.Empty;
    [ObservableProperty] private string _novasObservacoes = string.Empty;

    public void DefinirTipos(IReadOnlyList<TipoRelacionamentoDto> tipos)
    {
        var lista = new List<Opcao<TipoRelacionamentoEscolhido?>> { SemTipo };
        foreach (var t in tipos)
        {
            lista.Add(new(new TipoRelacionamentoEscolhido(t.Id, false, t.Societario), t.Nome));
            if (!string.Equals(t.NomeInverso, t.Nome, StringComparison.CurrentCultureIgnoreCase))
                lista.Add(new(new TipoRelacionamentoEscolhido(t.Id, true, t.Societario), t.NomeInverso));
        }
        Tipos = [.. lista.Take(1), .. lista.Skip(1).OrderBy(o => o.Texto, StringComparer.CurrentCultureIgnoreCase)];
        NovoTipo = SemTipo;
    }

    public void Carregar(IEnumerable<PessoaRelacionamentoDto> vinculos)
    {
        Itens.Clear();
        foreach (var v in vinculos) Incluir(v);
        Carregados = true;
        Avisar();
    }

    public void Incluido(PessoaRelacionamentoDto dto)
    {
        Incluir(dto);
        LimparNovo();
        Avisar();
    }

    public void Atualizado(RelacionamentoItem item, PessoaRelacionamentoDto dto)
    {
        item.Atualizar(dto);
        item.MostrarEncerrados = MostrarEncerrados;
        Avisar();
    }

    public void DefinirResultados(IEnumerable<PessoaResumo> pessoas)
    {
        ResultadosBusca.Clear();
        foreach (var p in pessoas) ResultadosBusca.Add(p);
    }

    /// <summary>Confere o que dá para conferir antes de chamar a API (o resto, como duplicidade, é da API).</summary>
    public IReadOnlyList<string> ValidarNovo()
    {
        var erros = new List<string>();
        if (NovoTipo.Valor is null) erros.Add("Escolha o tipo de relacionamento.");
        if (PessoaEscolhida is null) erros.Add("Busque e escolha a outra pessoa.");
        if (!TextoTela.TentarData(NovoInicio, out _)) erros.Add("Início inválido (use dd/mm/aaaa).");
        return erros;
    }

    public IncluirRelacionamentoRequisicao ParaRequisicao()
    {
        TextoTela.TentarData(NovoInicio, out var inicio);
        return new IncluirRelacionamentoRequisicao
        {
            TipoRelacionamentoId = NovoTipo.Valor!.TipoId,
            Inverso = NovoTipo.Valor.Inverso,
            OutraPessoaId = PessoaEscolhida!.Id,
            InicioEm = inicio,
            Observacoes = TextoTela.Nulo(NovasObservacoes)
        };
    }

    partial void OnMostrarEncerradosChanged(bool value)
    {
        foreach (var i in Itens) i.MostrarEncerrados = value;
        Avisar();
    }

    [RelayCommand]
    private Task BuscarPessoaAsync() => Acoes.BuscarPessoa?.Invoke() ?? Task.CompletedTask;

    [RelayCommand]
    private Task IncluirAsync() => Acoes.Incluir?.Invoke() ?? Task.CompletedTask;

    private void Incluir(PessoaRelacionamentoDto dto) =>
        Itens.Add(new RelacionamentoItem(dto, Acoes) { MostrarEncerrados = MostrarEncerrados });

    private void LimparNovo()
    {
        NovoTipo = SemTipo;
        BuscaPessoa = string.Empty;
        ResultadosBusca.Clear();
        PessoaEscolhida = null;
        NovoInicio = string.Empty;
        NovasObservacoes = string.Empty;
    }

    private void Avisar()
    {
        OnPropertyChanged(nameof(TemEncerrados));
        OnPropertyChanged(nameof(Vazio));
    }
}
