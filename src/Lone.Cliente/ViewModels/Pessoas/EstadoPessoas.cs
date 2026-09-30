using Lone.Cliente.Navegacao;
using Lone.Contracts.Pessoas;

namespace Lone.Cliente.ViewModels.Pessoas;

/// <summary>
/// Estado de navegação de Pessoas (piloto completo da preservação de contexto): além da pesquisa, ficha e rolagens da
/// base, a aba de visão/filtro rápido, os filtros do painel, a visão marcada, a ordenação, a página, a prévia e a aba da
/// ficha. Só em memória; nada disto é dado de negócio.
/// </summary>
public sealed record EstadoPessoas : EstadoTela
{
    public string AbaRapida { get; init; } = FiltroRapido.Todos;

    /// <summary>Condições do painel (cópias: o painel pode mudar depois sem afetar o retrato).</summary>
    public IReadOnlyList<CondicaoFiltro> Condicoes { get; init; } = [];

    public Guid? VisaoId { get; init; }
    public bool VisaoAlterada { get; init; }
    public string? OrdenacaoColuna { get; init; }
    public DirecaoOrdenacao OrdenacaoDirecao { get; init; } = DirecaoOrdenacao.Crescente;
    public int Pagina { get; init; } = 1;

    /// <summary>Pessoa mostrada na prévia (só se estiver na página restaurada).</summary>
    public Guid? PreviaId { get; init; }

    /// <summary>Aba da ficha (se não existir mais naquela pessoa, fica a padrão).</summary>
    public SecaoPessoa? Secao { get; init; }
}
