using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Etiquetas;
using Lone.Domain.Comum;

namespace Lone.Cliente.ViewModels.Pessoas;

/// <summary>Uma etiqueta do cadastro na ficha da pessoa (associação simples: marcada ou não, sem período).</summary>
public sealed partial class EtiquetaMarcavel : ObservableObject
{
    public EtiquetaMarcavel(EtiquetaDto etiqueta, bool marcada)
    {
        Id = etiqueta.Id;
        Nome = etiqueta.Nome;
        Ativa = etiqueta.Ativo;
        _marcada = marcada;
    }

    public Guid Id { get; }
    public string Nome { get; }
    public bool Ativa { get; }
    public string Texto => Ativa ? Nome : Nome + " (desativada)";

    [ObservableProperty] private bool _marcada;

    /// <summary>O "×" do chip aparece para quem pode editar a pessoa.</summary>
    [ObservableProperty] private bool _removivel = true;

    public string DescricaoRemover => $"Remover a etiqueta {Nome}";
}

/// <summary>
/// Etiquetas da ficha. A ficha mostra só as atribuídas (chips); as outras ficam em "Adicionar etiqueta" (painel com
/// pesquisa). Etiqueta é classificação livre, sem período: o "×" tira a associação ao salvar, como sempre, e a
/// auditoria registra quem tirou e quando. Uma desativada no cadastro não é oferecida, mas continua em quem já a tem.
/// </summary>
public sealed partial class EtiquetasFormulario : ObservableObject
{
    private readonly List<EtiquetaMarcavel> _todas = [];

    /// <summary>Marcadas na pessoa mas fora do cadastro lido (ex.: o cadastro não pôde ser lido): voltam intactas.</summary>
    private readonly List<Guid> _desconhecidas = [];

    private static readonly StringComparer OrdemAlfabetica = StringComparer.Create(TextoTela.Brasil, ignoreCase: true);

    /// <summary>Chips da ficha: as atribuídas, em ordem alfabética.</summary>
    public ObservableCollection<EtiquetaMarcavel> Atribuidas { get; } = new();
    public bool SemAtribuidas => Atribuidas.Count == 0;

    /// <summary>Opções do painel: ativas ainda não atribuídas, filtradas pela pesquisa.</summary>
    public ObservableCollection<EtiquetaMarcavel> Disponiveis { get; } = new();
    public bool SemDisponiveis => Disponiveis.Count == 0;
    public string TextoSemDisponiveis => Vazio
        ? "Nenhuma etiqueta cadastrada ainda."
        : Busca.Trim().Length > 0 ? "Nenhuma etiqueta encontrada." : "Todas as etiquetas já estão atribuídas a esta pessoa.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoSemDisponiveis))]
    private string _busca = string.Empty;

    /// <summary>Painel "Adicionar etiqueta" aberto.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoBotaoAdicionar))]
    private bool _escolhendo;

    public string TextoBotaoAdicionar => Escolhendo ? "Fechar" : "+ Adicionar etiqueta";

    /// <summary>Quem só consulta vê as etiquetas, mas não adiciona nem remove (a gravação exige editar a pessoa).</summary>
    public bool PodeEditar { get; private set; } = true;

    /// <summary>O cadastro de etiquetas está vazio.</summary>
    public bool Vazio => _todas.Count == 0;
    public string Resumo => Marcadas.Count == 0 ? "Nenhuma etiqueta marcada" : string.Join(" · ", Atribuidas.Select(e => e.Nome));

    public IReadOnlyList<Guid> Marcadas => [.. _todas.Where(e => e.Marcada).Select(e => e.Id), .. _desconhecidas];

    public static EtiquetasFormulario Criar(IReadOnlyList<EtiquetaDto>? cadastro, IReadOnlyCollection<Guid> marcadas)
    {
        var f = new EtiquetasFormulario();
        var conhecidas = (cadastro ?? []).ToDictionary(e => e.Id);
        foreach (var etiqueta in conhecidas.Values
                     .Where(e => e.Ativo || marcadas.Contains(e.Id))
                     .OrderByDescending(e => marcadas.Contains(e.Id))
                     .ThenBy(e => e.Nome, StringComparer.CurrentCultureIgnoreCase))
            f.Incluir(new EtiquetaMarcavel(etiqueta, marcadas.Contains(etiqueta.Id)));
        f._desconhecidas.AddRange(marcadas.Where(id => !conhecidas.ContainsKey(id)));
        f.Atualizar();
        return f;
    }

    public void DefinirPermissao(bool podeEditar)
    {
        PodeEditar = podeEditar;
        foreach (var etiqueta in _todas) etiqueta.Removivel = podeEditar;
        if (!podeEditar) Escolhendo = false;
        OnPropertyChanged(nameof(PodeEditar));
    }

    [RelayCommand]
    private void AlternarEscolha()
    {
        if (!PodeEditar) return;
        Busca = string.Empty;
        Escolhendo = !Escolhendo;
    }

    /// <summary>Atribui a etiqueta escolhida no painel (gravada ao salvar a ficha). O painel fica aberto para escolher outras.</summary>
    [RelayCommand]
    private void Adicionar(EtiquetaMarcavel? etiqueta)
    {
        if (etiqueta is null || !PodeEditar) return;
        etiqueta.Marcada = true;
        Busca = string.Empty;
    }

    /// <summary>Tira a associação (ao salvar). A auditoria da pessoa registra a remoção.</summary>
    [RelayCommand]
    private void Remover(EtiquetaMarcavel? etiqueta)
    {
        if (etiqueta is null || !PodeEditar) return;
        etiqueta.Marcada = false;
    }

    /// <summary>Etiqueta criada agora (atalho da ficha): entra no cadastro da ficha, já atribuída se pedido.</summary>
    public void Incluir(EtiquetaDto etiqueta, bool marcar)
    {
        var existente = _todas.FirstOrDefault(e => e.Id == etiqueta.Id);
        if (existente is not null)
            existente.Marcada |= marcar;
        else
            Incluir(new EtiquetaMarcavel(etiqueta, marcar) { Removivel = PodeEditar });
        Busca = string.Empty;
        Atualizar();
    }

    private void Incluir(EtiquetaMarcavel etiqueta)
    {
        etiqueta.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(EtiquetaMarcavel.Marcada)) Atualizar();
        };
        _todas.Add(etiqueta);
        OnPropertyChanged(nameof(Vazio));
    }

    partial void OnBuscaChanged(string value) => AtualizarDisponiveis();

    private void Atualizar()
    {
        ColecaoSincronizada.Sincronizar(Atribuidas,
            _todas.Where(e => e.Marcada).OrderBy(e => e.Nome, OrdemAlfabetica).ToList());
        OnPropertyChanged(nameof(SemAtribuidas));
        OnPropertyChanged(nameof(Resumo));
        AtualizarDisponiveis();
    }

    private void AtualizarDisponiveis()
    {
        var termo = TextoBusca.Normalizar(Busca);
        ColecaoSincronizada.Sincronizar(Disponiveis, _todas
            .Where(e => !e.Marcada && e.Ativa)
            .Where(e => termo.Length == 0 || TextoBusca.Normalizar(e.Nome).Contains(termo, StringComparison.Ordinal))
            .OrderBy(e => e.Nome, OrdemAlfabetica)
            .ToList());
        OnPropertyChanged(nameof(SemDisponiveis));
        OnPropertyChanged(nameof(TextoSemDisponiveis));
    }
}
