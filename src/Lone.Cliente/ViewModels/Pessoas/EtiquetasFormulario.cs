using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Lone.Contracts.Etiquetas;
using Lone.Domain.Comum;

namespace Lone.Cliente.ViewModels.Pessoas;

/// <summary>Uma etiqueta do cadastro na ficha da pessoa, para marcar ou desmarcar.</summary>
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
}

/// <summary>
/// Etiquetas da ficha: todas as ativas do cadastro, mais as desativadas que a pessoa já tinha (uma desativada não
/// é oferecida para marcações novas). A busca só filtra o que aparece; o que está marcado não muda.
/// </summary>
public sealed partial class EtiquetasFormulario : ObservableObject
{
    private readonly List<EtiquetaMarcavel> _todas = [];

    /// <summary>Marcadas na pessoa mas fora do cadastro lido (ex.: o cadastro não pôde ser lido): voltam intactas.</summary>
    private readonly List<Guid> _desconhecidas = [];

    /// <summary>O que aparece na tela (filtrado pela busca): marcadas primeiro, depois em ordem alfabética.</summary>
    public ObservableCollection<EtiquetaMarcavel> Visiveis { get; } = new();

    [ObservableProperty] private string _busca = string.Empty;

    public bool Vazio => _todas.Count == 0;
    public string Resumo => Marcadas.Count == 0 ? "Nenhuma etiqueta marcada" : string.Join(" · ", _todas.Where(e => e.Marcada).Select(e => e.Nome));

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
        f.Filtrar();
        return f;
    }

    /// <summary>Etiqueta criada agora (atalho da ficha): entra na lista, já marcada se pedido.</summary>
    public void Incluir(EtiquetaDto etiqueta, bool marcar)
    {
        var existente = _todas.FirstOrDefault(e => e.Id == etiqueta.Id);
        if (existente is not null)
        {
            existente.Marcada |= marcar;
        }
        else
        {
            Incluir(new EtiquetaMarcavel(etiqueta, marcar));
        }
        Busca = string.Empty;
        Filtrar();
    }

    private void Incluir(EtiquetaMarcavel etiqueta)
    {
        etiqueta.PropertyChanged += (_, _) => OnPropertyChanged(nameof(Resumo));
        _todas.Add(etiqueta);
        OnPropertyChanged(nameof(Vazio));
    }

    partial void OnBuscaChanged(string value) => Filtrar();

    private void Filtrar()
    {
        var termo = TextoBusca.Normalizar(Busca);
        Visiveis.Clear();
        foreach (var e in _todas.Where(e => termo.Length == 0 || TextoBusca.Normalizar(e.Nome).Contains(termo, StringComparison.Ordinal)))
            Visiveis.Add(e);
        OnPropertyChanged(nameof(Resumo));
    }
}
