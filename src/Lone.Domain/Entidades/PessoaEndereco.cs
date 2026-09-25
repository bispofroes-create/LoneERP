using System.ComponentModel;
using Lone.Domain.Enums;

namespace Lone.Domain.Entidades;

/// <summary>
/// Endereço da pessoa. Um endereço serve para vários usos (Finalidades), então não precisa ser
/// cadastrado de novo para cobrança ou entrega. O padrão de cada uso é o de menor Ordem que o tem.
/// </summary>
[DisplayName("Endereço")]
public class PessoaEndereco : EntidadePessoaFilha
{
    public const string CodigoPaisBrasil = "1058";

    /// <summary>Identificação livre (ex.: "Loja centro", "Depósito").</summary>
    [DisplayName("Descrição")]
    public string? Descricao { get; set; }

    /// <summary>Classificação do cadastro de tipos de endereço (Sede, Depósito...); opcional.</summary>
    [DisplayName("Tipo")]
    public Guid? TipoEnderecoId { get; set; }

    [DisplayName("Observações")]
    public string? Observacoes { get; set; }

    /// <summary>Falso = removido na ficha: fica gravado (histórico), não é principal nem endereço fiscal.</summary>
    [DisplayName("Ativo")]
    public bool Ativo { get; set; } = true;

    [DisplayName("Finalidades")]
    public FinalidadeEndereco Finalidades { get; set; } = FinalidadeEndereco.Comercial;

    /// <summary>Ordem de exibição e de preferência (o primeiro com a finalidade é o padrão dela).</summary>
    [DisplayName("Ordem")]
    public int Ordem { get; set; }

    [DisplayName("CEP")]
    public string? Cep { get; set; }

    [DisplayName("Logradouro")]
    public string Logradouro { get; set; } = string.Empty;

    [DisplayName("Número")]
    public string? Numero { get; set; }

    [DisplayName("Complemento")]
    public string? Complemento { get; set; }

    [DisplayName("Bairro")]
    public string? Bairro { get; set; }

    /// <summary>
    /// Município (código IBGE, na tabela Municipios). Obrigatório no Brasil; nulo no exterior.
    /// Cidade, UF e código IBGE abaixo são cópias gravadas a partir dele pela API (o texto exato da época).
    /// </summary>
    [DisplayName("Município")]
    public int? MunicipioId { get; set; }

    /// <summary>Nome do município (cópia do IBGE) ou, no exterior, a cidade digitada.</summary>
    [DisplayName("Cidade")]
    public string Cidade { get; set; } = string.Empty;

    [DisplayName("UF")]
    public string? Uf { get; set; }

    /// <summary>Código IBGE do município (7 dígitos), obrigatório na NF-e. Cópia de MunicipioId.</summary>
    [DisplayName("Código IBGE")]
    public string? CodigoMunicipioIbge { get; set; }

    /// <summary>Código do país na tabela do BACEN (Brasil = 1058).</summary>
    [DisplayName("Código do país")]
    public string CodigoPais { get; set; } = CodigoPaisBrasil;

    [DisplayName("País")]
    public string Pais { get; set; } = "Brasil";

    public bool EhBrasil => CodigoPais == CodigoPaisBrasil;
    public bool Tem(FinalidadeEndereco finalidade) => (Finalidades & finalidade) == finalidade;
}
