namespace Lone.Domain.Enums;

/// <summary>
/// Indicador de contribuinte do ICMS. Os valores numéricos são os do campo indIEDest da NF-e.
/// A camada fiscal (futura) é quem valida o uso final numa nota.
/// </summary>
public enum IndicadorIE : byte
{
    NaoInformado = 0,
    Contribuinte = 1,
    Isento = 2,
    NaoContribuinte = 9
}
