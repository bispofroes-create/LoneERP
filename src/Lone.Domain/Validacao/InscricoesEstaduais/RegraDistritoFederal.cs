// Portado do Caelum Stella (IEDistritoFederalValidator), Apache License 2.0, Copyright Caelum. Veja THIRD-PARTY-NOTICES.md.

using System.Text.RegularExpressions;

namespace Lone.Domain.Validacao.InscricoesEstaduais;

/// <summary>
/// Inscrição Estadual do Distrito Federal (DF): 13 dígitos iniciados por 07 ou 08; dois dígitos verificadores módulo 11 (pesos 2 a 9).
/// </summary>
internal sealed class RegraDistritoFederal : IRegraInscricaoEstadual
{
    private static readonly Regex Formato = new(@"^0[78][0-9]{11}\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <inheritdoc />
    public bool Valida(string digitos) =>
        Formato.IsMatch(digitos) && CalculoDigito.ConfereDoisDigitosModulo11(digitos, CalculoDigito.Pesos2a9);
}
