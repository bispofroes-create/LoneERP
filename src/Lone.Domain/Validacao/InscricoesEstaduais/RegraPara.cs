// Portado do Caelum Stella (IEParaValidator), Apache License 2.0, Copyright Caelum. Veja THIRD-PARTY-NOTICES.md.

using System.Text.RegularExpressions;

namespace Lone.Domain.Validacao.InscricoesEstaduais;

/// <summary>
/// Inscrição Estadual do Pará (PA): 9 dígitos iniciados por 15 ou 75 a 79; dígito verificador módulo 11 (pesos 2 a 9).
/// </summary>
internal sealed class RegraPara : IRegraInscricaoEstadual
{
    private static readonly Regex Formato = new(@"^(?:15|7[5-9])[0-9]{7}\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <inheritdoc />
    public bool Valida(string digitos) =>
        Formato.IsMatch(digitos) && CalculoDigito.ConfereUltimoDigitoModulo11(digitos, CalculoDigito.Pesos2a9);
}
