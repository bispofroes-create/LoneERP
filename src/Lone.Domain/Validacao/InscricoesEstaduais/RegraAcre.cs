// Portado do Caelum Stella (IEAcreValidator), Apache License 2.0, Copyright Caelum. Veja THIRD-PARTY-NOTICES.md.

using System.Text.RegularExpressions;

namespace Lone.Domain.Validacao.InscricoesEstaduais;

/// <summary>
/// Inscrição Estadual do Acre (AC): 13 dígitos iniciados por 01; dois dígitos verificadores módulo 11 (pesos 2 a 9).
/// </summary>
internal sealed class RegraAcre : IRegraInscricaoEstadual
{
    private static readonly Regex Formato = new(@"^01[0-9]{11}\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <inheritdoc />
    public bool Valida(string digitos) =>
        Formato.IsMatch(digitos) && CalculoDigito.ConfereDoisDigitosModulo11(digitos, CalculoDigito.Pesos2a9);
}
