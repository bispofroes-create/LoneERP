using System.ComponentModel;

namespace Lone.Core.Entidades;

/// <summary>Papel de fornecedor: dados de compras.</summary>
[DisplayName("Fornecedor")]
public class Fornecedor : PapelBase
{
    [DisplayName("Condição de pagamento")]
    public string? CondicaoPagamento { get; set; }

    [DisplayName("Prazo de entrega (dias)")]
    public int? PrazoEntregaDias { get; set; }
}
