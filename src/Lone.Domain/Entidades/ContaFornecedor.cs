using System.ComponentModel;

namespace Lone.Domain.Entidades;

/// <summary>Relação de compras com uma empresa do grupo. EmpresaId nulo = conta padrão.</summary>
[DisplayName("Conta de fornecedor")]
public class ContaFornecedor : EntidadePessoaFilha
{
    [DisplayName("Empresa")]
    public Guid? EmpresaId { get; set; }

    [DisplayName("Condição de pagamento")]
    public string? CondicaoPagamento { get; set; }

    [DisplayName("Prazo médio (dias)")]
    public int? PrazoMedioDias { get; set; }

    [DisplayName("Lead time (dias)")]
    public int? LeadTimeDias { get; set; }

    [DisplayName("Transportadora padrão")]
    public Guid? TransportadoraPadraoId { get; set; }

    /// <summary>Nota de 1 a 5.</summary>
    [DisplayName("Avaliação")]
    public byte? Avaliacao { get; set; }

    [DisplayName("Observações de compras")]
    public string? Observacoes { get; set; }
}
