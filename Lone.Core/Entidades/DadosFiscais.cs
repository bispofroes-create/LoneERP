using System.ComponentModel;
using Lone.Core.Enums;

namespace Lone.Core.Entidades;

/// <summary>Dados fiscais da pessoa. Gravados na própria tabela Pessoas (vão para o Estabelecimento na etapa 3).</summary>
[DisplayName("Fiscal")]
public class DadosFiscais
{
    [DisplayName("Contribuinte do ICMS")]
    public IndicadorIE IndicadorIE { get; set; } = IndicadorIE.NaoInformado;

    [DisplayName("Inscrição estadual")]
    public string? InscricaoEstadual { get; set; }

    [DisplayName("Inscrição municipal")]
    public string? InscricaoMunicipal { get; set; }

    [DisplayName("Inscrição SUFRAMA")]
    public string? InscricaoSuframa { get; set; }
}
