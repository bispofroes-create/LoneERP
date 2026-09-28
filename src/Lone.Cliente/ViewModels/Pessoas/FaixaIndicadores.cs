using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Pessoas;

namespace Lone.Cliente.ViewModels.Pessoas;

/// <summary>Um número da faixa: "Com bloqueio 12". Tocar põe (ou tira) a condição no painel de filtros.</summary>
public sealed partial class IndicadorLista : ObservableObject
{
    public IndicadorLista(IndicadorPessoasDto dto, Action<IndicadorLista> alternar)
    {
        Dto = dto;
        AlternarCommand = new RelayCommand(() => alternar(this));
    }

    public IndicadorPessoasDto Dto { get; }
    public string Id => Dto.Id;
    public string Nome => Dto.Nome;
    public string? Dica => Dto.Dica;
    public string TextoTotal => Dto.Total.ToString("N0", TextoTela.Brasil);

    /// <summary>Zero fica apagado mas à vista: mostra que está tudo em dia.</summary>
    public bool Zerado => Dto.Total == 0;
    public bool EhAlerta => Dto.Alerta && !Zerado;
    public bool EhAtencao => !Dto.Alerta && !Zerado;

    /// <summary>A condição dele está valendo no painel (o indicador fica destacado; tocar de novo tira).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Descricao))]
    private bool _marcado;

    public string Descricao => $"{Nome}: {TextoTotal}" + (Marcado ? ", filtrando a lista" : string.Empty);

    public IRelayCommand AlternarCommand { get; }
}

/// <summary>
/// Faixa de indicadores acima da lista de pessoas (Etapa 3): números da base toda (ativos e em análise), contados no
/// servidor pela mesma condição do catálogo que o toque aplica. Cada usuário pode tirar indicadores ou esconder a faixa.
/// </summary>
public sealed partial class FaixaIndicadores : ObservableObject
{
    private readonly Action<IndicadorLista> _alternar;
    private List<IndicadorPessoasDto> _todos = [];
    private HashSet<string> _ocultos = new(StringComparer.Ordinal);

    public FaixaIndicadores(Action<IndicadorLista> alternar) => _alternar = alternar;

    /// <summary>Os indicadores mostrados (sem os que o usuário tirou), na ordem do servidor.</summary>
    public ObservableCollection<IndicadorLista> Itens { get; } = new();

    /// <summary>O servidor mandou indicadores (há o que mostrar e o que atualizar depois).</summary>
    public bool Carregados => _todos.Count > 0;

    /// <summary>O usuário escondeu a faixa inteira.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Visivel))]
    private bool _escondida;

    /// <summary>À vista mesmo com todos os indicadores tirados: o "⋯" da faixa é o caminho de volta.</summary>
    public bool Visivel => !Escondida && Carregados;

    /// <summary>Ids dos indicadores tirados da faixa (para a preferência). Nulo = nenhum.</summary>
    public List<string>? Ocultos => _ocultos.Count == 0 ? null : [.. _ocultos.Order(StringComparer.Ordinal)];

    /// <summary>Preferência lida junto com o catálogo (antes dos números).</summary>
    public void DefinirPreferencia(bool escondida, IEnumerable<string>? ocultos)
    {
        Escondida = escondida;
        _ocultos = new HashSet<string>(ocultos ?? [], StringComparer.Ordinal);
        Montar();
    }

    /// <summary>Números novos (catálogo ou atualização). Nulo = não vieram: a faixa continua como estava.</summary>
    public void Carregar(IEnumerable<IndicadorPessoasDto>? indicadores)
    {
        if (indicadores is null) return;
        _todos = [.. indicadores];
        Montar();
    }

    /// <summary>Tira ou põe um indicador na faixa.</summary>
    public void AlternarOculto(string id)
    {
        if (!_ocultos.Remove(id)) _ocultos.Add(id);
        Montar();
    }

    public bool EstaOculto(string id) => _ocultos.Contains(id);

    /// <summary>Todos os indicadores que o servidor mandou (o menu da faixa mostra os tirados também).</summary>
    public IReadOnlyList<IndicadorPessoasDto> Todos => _todos;

    /// <summary>Destaca os indicadores cuja condição está valendo no painel (chamado quando os filtros mudam).</summary>
    public void AtualizarMarcados()
    {
        foreach (var item in Itens) item.Marcado = _valendo(item.Dto.Condicao);
    }

    private Func<CondicaoFiltro, bool> _valendo = _ => false;

    /// <summary>Como saber se a condição de um indicador está valendo (o painel de filtros fornece).</summary>
    public void DefinirConsulta(Func<CondicaoFiltro, bool> valendo) => _valendo = valendo;

    private void Montar()
    {
        Itens.Clear();
        foreach (var dto in _todos.Where(i => !_ocultos.Contains(i.Id)))
            Itens.Add(new IndicadorLista(dto, _alternar) { Marcado = _valendo(dto.Condicao) });
        OnPropertyChanged(nameof(Visivel));
        OnPropertyChanged(nameof(Carregados));
    }
}
