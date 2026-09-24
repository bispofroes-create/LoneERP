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

    /// <summary>Texto livre até existir o cadastro de condições de pagamento.</summary>
    [DisplayName("Condição de pagamento")]
    public string? CondicaoPagamento { get; set; }

    [DisplayName("Exige aprovação acima do limite")]
    public bool ExigeAprovacaoAcimaLimite { get; set; } = true;

    [DisplayName("Vendedor padrão")]
    public Guid? VendedorPadraoId { get; set; }

    [DisplayName("Observações comerciais")]
    public string? Observacoes { get; set; }
}
