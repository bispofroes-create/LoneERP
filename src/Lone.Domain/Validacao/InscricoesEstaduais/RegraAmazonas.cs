// Portado do Caelum Stella (IEAmazonasValidator), Apache License 2.0, Copyright Caelum. Veja THIRD-PARTY-NOTICES.md.

using System.Text.RegularExpressions;

namespace Lone.Domain.Validacao.InscricoesEstaduais;

/// <summary>
/// Inscrição Estadual do Amazonas (AM): 9 dígitos; dígito verificador módulo 11 (pesos 2 a 9).
/// </summary>
internal sealed class RegraAmazonas : IRegraInscricaoEstadual
{
    private static readonly Regex Formato = new(@"^[0-9]{9}\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <inheritdoc />
    public bool Valida(string digitos) =>
        Formato.IsMatch(digitos) && CalculoDigito.ConfereUltimoDigitoModulo11(digitos, CalculoDigito.Pesos2a9);
}
