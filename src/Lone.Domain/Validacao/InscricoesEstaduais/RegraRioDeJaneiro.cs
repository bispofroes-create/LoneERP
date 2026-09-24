// Portado do Caelum Stella (IERioDeJaneiroValidator), Apache License 2.0, Copyright Caelum. Veja THIRD-PARTY-NOTICES.md.

using System.Text.RegularExpressions;

namespace Lone.Domain.Validacao.InscricoesEstaduais;

/// <summary>
/// Inscrição Estadual do Rio de Janeiro (RJ): 8 dígitos; dígito verificador módulo 11 (pesos 2 a 7).
/// </summary>
internal sealed class RegraRioDeJaneiro : IRegraInscricaoEstadual
{
    private static readonly Regex Formato = new(@"^[0-9]{8}\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly int[] Pesos2a7 = CalculoDigito.Sequencia(2, 7);

    /// <inheritdoc />
    public bool Valida(string digitos) =>
        Formato.IsMatch(digitos) && CalculoDigito.ConfereUltimoDigitoModulo11(digitos, Pesos2a7);
}
