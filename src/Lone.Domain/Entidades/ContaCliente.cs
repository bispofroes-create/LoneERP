using System.ComponentModel;

namespace Lone.Domain.Entidades;

/// <summary>
/// Relação comercial de cliente com uma empresa do grupo. EmpresaId nulo = conta padrão, válida para
/// as empresas que não têm conta própria. Crédito utilizado/disponível é calculado pelo financeiro.
/// </summary>
[DisplayName("Conta de cliente")]
public class ContaCliente : EntidadePessoaFilha
{
    /// <summary>Empresa do grupo (Pessoa com papel EmpresaDoGrupo). Nulo = todas.</summary>
    [DisplayName("Empresa")]
    public Guid? EmpresaId { get; set; }

    [DisplayName("Limite de crédito")]
    public decimal? LimiteCredito { get; set; }

    [DisplayName("Dias máximos de atraso")]
    public int? DiasMaximoAtraso { get; set; }

    [DisplayName("Desconto máximo (%)")]
    public decimal? DescontoMaximo { get; set; }

    /// <summary>Perfil comercial (padrões de venda); nulo = sem perfil (valem os campos da conta).</summary>
    [DisplayName("Perfil comercial")]
    public Guid? PerfilComercialId { get; set; }

    /// <summary>Condição de pagamento do cadastro. O texto antigo (<see cref="CondicaoPagamento"/>) continua guardado.</summary>
    [DisplayName("Condição de pagamento (cadastro)")]
    public Guid? CondicaoPagamentoId { get; set; }

    /// <summary>Texto livre de antes do cadastro de condições (mantido; a ficha mostra enquanto não houver condição escolhida).</summary>
    [DisplayName("Condição de pagamento")]
    public string? CondicaoPagamento { get; set; }

    [DisplayName("Exige aprovação acima do limite")]
    public bool ExigeAprovacaoAcimaLimite { get; set; } = true;

    /// <summary>Cópia do vendedor principal vigente da carteira (D5), mantida para compatibilidade.</summary>
    [DisplayName("Vendedor padrão")]
    public Guid? VendedorPadraoId { get; set; }

    [DisplayName("Observações comerciais")]
    public string? Observacoes { get; set; }
}
