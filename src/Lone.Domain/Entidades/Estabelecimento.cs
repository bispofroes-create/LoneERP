using System.ComponentModel;
using Lone.Domain.Enums;

namespace Lone.Domain.Entidades;

/// <summary>
/// Unidade fiscal da pessoa: na PJ, cada CNPJ (matriz e filiais); na PF e no estrangeiro, um único
/// estabelecimento oculto. Os dados fiscais moram aqui, e não na identidade da pessoa.
/// </summary>
[DisplayName("Estabelecimento")]
public class Estabelecimento : EntidadePessoaFilha
{
    /// <summary>CNPJ completo (14 posições, pode ser alfanumérico). Nulo na PF e no estrangeiro.</summary>
    [DisplayName("CNPJ")]
    public string? Cnpj { get; set; }

    /// <summary>Estabelecimento principal da pessoa (um por pessoa; normalmente a matriz).</summary>
    [DisplayName("Principal")]
    public bool Principal { get; set; }

    [DisplayName("Nome fantasia")]
    public string? NomeFantasia { get; set; }

    [DisplayName("Ativo")]
    public bool Ativo { get; set; } = true;

    [DisplayName("Situação na Receita")]
    public string? SituacaoReceita { get; set; }

    [DisplayName("Consulta à Receita em")]
    public DateTime? ConsultadoReceitaEm { get; set; }

    // ---- Fiscal ----
    [DisplayName("Contribuinte do ICMS")]
    public IndicadorIE IndicadorIE { get; set; } = IndicadorIE.NaoInformado;

    [DisplayName("Inscrição estadual")]
    public string? InscricaoEstadual { get; set; }

    [DisplayName("Inscrição municipal")]
    public string? InscricaoMunicipal { get; set; }

    [DisplayName("Inscrição SUFRAMA")]
    public string? InscricaoSuframa { get; set; }

    [DisplayName("Regime tributário")]
    public RegimeTributario RegimeTributario { get; set; } = RegimeTributario.NaoInformado;

    /// <summary>CNAE principal, 7 dígitos.</summary>
    [DisplayName("CNAE principal")]
    public string? CnaePrincipal { get; set; }

    [DisplayName("Natureza jurídica")]
    public string? NaturezaJuridica { get; set; }

    /// <summary>CNAEs secundários (só os códigos, separados por vírgula), como vêm da Receita.</summary>
    [DisplayName("CNAEs secundários")]
    public string? CnaesSecundarios { get; set; }

    /// <summary>Endereço usado nos documentos fiscais. Nulo = endereço fiscal/principal da pessoa.</summary>
    [DisplayName("Endereço fiscal")]
    public Guid? EnderecoFiscalId { get; set; }
    public PessoaEndereco? EnderecoFiscal { get; set; }

    public bool EhMatriz() => Cnpj is { Length: 14 } && Cnpj.Substring(8, 4) == "0001";
}
