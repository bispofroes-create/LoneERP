using System.ComponentModel;

namespace Lone.Domain.Entidades;

/// <summary>Relação de compras com uma empresa do grupo. EmpresaId nulo = conta padrão.</summary>
[DisplayName("Conta de fornecedor")]
public class ContaFornecedor : EntidadePessoaFilha
{
    [DisplayName("Empresa")]
    public Guid? EmpresaId { get; set; }

    /// <summary>
    /// Texto livre de antes do cadastro de condições de pagamento. Preservado (nunca apagado): a migração liga à condição
    /// de mesmo nome quando existe; o que não converteu continua aqui e a ficha mostra como "não convertida".
    /// </summary>
    [DisplayName("Condição de pagamento (texto anterior)")]
    public string? CondicaoPagamento { get; set; }

    /// <summary>Condição de pagamento do cadastro (fonte principal, como no cliente).</summary>
    [DisplayName("Condição de pagamento")]
    public Guid? CondicaoPagamentoId { get; set; }

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
