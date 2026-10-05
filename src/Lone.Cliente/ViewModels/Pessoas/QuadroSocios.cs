using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Pessoas;

namespace Lone.Cliente.ViewModels.Pessoas;

/// <summary>
/// Sócios e administradores da Receita na Identificação (03/10/2026): empresas grandes trazem dezenas de nomes e
/// empurravam papéis e etiquetas para baixo. Como nos ERPs maduros com listas longas: o título traz o total, aparecem os
/// primeiros <see cref="Previa"/> e "Ver todos os N ▾" / "Recolher ▴"; com mais de <see cref="LimiteBusca"/>, uma busca
/// por nome ou qualificação. A tela mostra em duas ou três colunas na tela larga.
/// P0 (D7): o quadro mostra os sócios ativos; os que saíram (inativos, com a data de saída) ficam em "Ver ex-sócios".
/// </summary>
public sealed partial class QuadroSocios : ObservableObject
{
    public const int Previa = 6;
    public const int LimiteBusca = 20;

    private readonly ObservableCollection<SocioDto> _socios;

    public QuadroSocios(ObservableCollection<SocioDto> socios)
    {
        _socios = socios;
        _socios.CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Reset) { Expandido = false; Busca = string.Empty; }
            Atualizar();
        };
        Atualizar();
    }

    [ObservableProperty] private bool _expandido;
    [ObservableProperty] private string _busca = string.Empty;

    /// <summary>Os nomes mostrados agora (prévia, todos ou os que a busca encontrou).</summary>
    [ObservableProperty] private IReadOnlyList<SocioDto> _visiveis = [];

    /// <summary>Ex-sócios aparecendo (inativos, que saíram do quadro da Receita).</summary>
    [ObservableProperty] private bool _mostrarExSocios;

    [ObservableProperty] private IReadOnlyList<SocioDto> _exSocios = [];

    private List<SocioDto> Ativos => _socios.Where(s => s.Ativo).ToList();

    public bool TemSocios => _socios.Count > 0;
    public bool TemExSocios => _socios.Any(s => !s.Ativo);
    public string TextoExSocios => MostrarExSocios ? "Ocultar ex-sócios ▴" : $"Ver ex-sócios ({_socios.Count(s => !s.Ativo)}) ▾";

    public string Titulo => Ativos.Count > 1
        ? $"Sócios e administradores (Receita Federal) · {Ativos.Count}"
        : "Sócios e administradores (Receita Federal)";

    /// <summary>"Ver todos" só quando há mais que a prévia e não há busca (a busca já mostra tudo o que encontra).</summary>
    public bool MostrarBotao => Ativos.Count > Previa && Busca.Trim().Length == 0;
    public string TextoBotao => Expandido ? "Recolher ▴" : $"Ver todos os {Ativos.Count} ▾";
    public bool MostrarBusca => Ativos.Count > LimiteBusca;
    public bool SemResultado => Busca.Trim().Length > 0 && Visiveis.Count == 0;

    partial void OnExpandidoChanged(bool value) => Atualizar();
    partial void OnBuscaChanged(string value) => Atualizar();

    [RelayCommand]
    private void Alternar() => Expandido = !Expandido;

    [RelayCommand]
    private void AlternarExSocios() => MostrarExSocios = !MostrarExSocios;

    partial void OnMostrarExSociosChanged(bool value) => Atualizar();

    /// <summary>Sem diferenciar maiúsculas nem acentos ("joao" acha "João").</summary>
    private static bool Contem(string texto, string termo) =>
        TextoTela.Brasil.CompareInfo.IndexOf(texto, termo,
            System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0;

    private void Atualizar()
    {
        var termo = Busca.Trim();
        var ativos = Ativos;
        Visiveis = termo.Length > 0
            ? ativos.Where(s => Contem($"{s.Nome} {s.Qualificacao}", termo)).ToList()
            : Expandido ? ativos : ativos.Take(Previa).ToList();
        ExSocios = MostrarExSocios ? _socios.Where(s => !s.Ativo).ToList() : [];
        if (!TemExSocios && MostrarExSocios) MostrarExSocios = false;
        OnPropertyChanged(nameof(TemExSocios));
        OnPropertyChanged(nameof(TextoExSocios));
        OnPropertyChanged(nameof(TemSocios));
        OnPropertyChanged(nameof(Titulo));
        OnPropertyChanged(nameof(MostrarBotao));
        OnPropertyChanged(nameof(TextoBotao));
        OnPropertyChanged(nameof(MostrarBusca));
        OnPropertyChanged(nameof(SemResultado));
    }
}
