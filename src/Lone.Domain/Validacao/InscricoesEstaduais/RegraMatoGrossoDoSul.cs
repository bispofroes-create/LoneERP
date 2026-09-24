// Portado do Caelum Stella (IEMatoGrossoDoSulValidator), Apache License 2.0, Copyright Caelum. Veja THIRD-PARTY-NOTICES.md.

using System.Text.RegularExpressions;

namespace Lone.Domain.Validacao.InscricoesEstaduais;

/// <summary>
/// Inscrição Estadual do Mato Grosso do Sul (MS): 9 dígitos iniciados por 28; dígito verificador módulo 11 (pesos 2 a 9).
/// </summary>
internal sealed class RegraMatoGrossoDoSul : IRegraInscricaoEstadual
{
    private static readonly Regex Formato = new(@"^28[0-9]{7}\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <inheritdoc />
    public bool Valida(string digitos) =>
        Formato.IsMatch(digitos) && CalculoDigito.ConfereUltimoDigitoModulo11(digitos, CalculoDigito.Pesos2a9);
}
