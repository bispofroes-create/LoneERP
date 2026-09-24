// Portado do Caelum Stella (IEParanaValidator), Apache License 2.0, Copyright Caelum. Veja THIRD-PARTY-NOTICES.md.

using System.Text.RegularExpressions;

namespace Lone.Domain.Validacao.InscricoesEstaduais;

/// <summary>
/// Inscrição Estadual do Paraná (PR): 10 dígitos; dois dígitos verificadores módulo 11 (pesos 2 a 7).
/// </summary>
internal sealed class RegraParana : IRegraInscricaoEstadual
{
    private static readonly Regex Formato = new(@"^[0-9]{10}\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly int[] Pesos2a7 = CalculoDigito.Sequencia(2, 7);

    /// <inheritdoc />
    public bool Valida(string digitos) =>
        Formato.IsMatch(digitos) && CalculoDigito.ConfereDoisDigitosModulo11(digitos, Pesos2a7);
}
