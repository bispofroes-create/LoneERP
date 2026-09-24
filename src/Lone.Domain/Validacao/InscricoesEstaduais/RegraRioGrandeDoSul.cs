// Portado do Caelum Stella (IERioGrandeDoSulValidator), Apache License 2.0, Copyright Caelum. Veja THIRD-PARTY-NOTICES.md.

using System.Text.RegularExpressions;

namespace Lone.Domain.Validacao.InscricoesEstaduais;

/// <summary>
/// Inscrição Estadual do Rio Grande do Sul (RS): 10 dígitos (município 000–499, 800 ou 900); dígito verificador módulo 11 (pesos 2 a 9).
/// </summary>
internal sealed class RegraRioGrandeDoSul : IRegraInscricaoEstadual
{
    private static readonly Regex Formato = new(@"^(?:[0-4][0-9]{2}|800|900)[0-9]{7}\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <inheritdoc />
    public bool Valida(string digitos) =>
        Formato.IsMatch(digitos) && CalculoDigito.ConfereUltimoDigitoModulo11(digitos, CalculoDigito.Pesos2a9);
}
