using System.ComponentModel;

namespace Lone.Domain.Entidades;

/// <summary>
/// Finalidade (uso) de endereço: Comercial, Residencial, Fiscal, Entrega, Cobrança, Correspondência... É o cadastro
/// que define as finalidades (o sistema trabalha com o Id; regras que precisam de uma específica usam o Código, que
/// não muda). "Principal" não é finalidade: é marcado em cada relação endereço × finalidade. A Ordem define a
/// exibição e o endereço de referência da listagem. Nunca é excluída: desativada, some das escolhas novas e continua
/// nos endereços que já a têm. As de sistema (as iniciais) não mudam de código nem são desativadas.
/// </summary>
[DisplayName("Finalidade de endereço")]
public class FinalidadeEnderecoCadastro : AgregadoRaiz
{
    public const int TamanhoMaximoCodigo = 30;
    public const int TamanhoMaximoNome = 40;

    [DisplayName("Código")]
    public string Codigo { get; set; } = string.Empty;

    [DisplayName("Nome")]
    public string Nome { get; set; } = string.Empty;

    [DisplayName("Ordem")]
    public int Ordem { get; set; }

    [DisplayName("De sistema")]
    public bool DoSistema { get; set; }

    [DisplayName("Ativa")]
    public bool Ativo { get; set; } = true;
}

/// <summary>
/// Uma finalidade de um endereço da pessoa (endereço físico × uso). Regras (na API e no banco): no máximo uma relação
/// ativa por endereço + finalidade; no máximo um endereço principal por pessoa + finalidade; relação inativa nunca é
/// principal. Retirar a finalidade desativa a linha; adicioná-la de novo reativa a mesma linha (histórico sem duplicata).
/// </summary>
[DisplayName("Finalidade do endereço")]
public class PessoaEnderecoFinalidade : EntidadePessoaFilha
{
    [DisplayName("Endereço")]
    public Guid PessoaEnderecoId { get; set; }

    [DisplayName("Finalidade")]
    public Guid FinalidadeId { get; set; }

    /// <summary>Este é o endereço principal da pessoa para esta finalidade (explícito; nunca pela ordem).</summary>
    [DisplayName("Principal")]
    public bool Principal { get; set; }

    [DisplayName("Ativa")]
    public bool Ativo { get; set; } = true;
}
