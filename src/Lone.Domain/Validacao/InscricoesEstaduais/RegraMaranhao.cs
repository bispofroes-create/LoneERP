// Portado do Caelum Stella (IEMaranhaoValidator), Apache License 2.0, Copyright Caelum. Veja THIRD-PARTY-NOTICES.md.

using System.Text.RegularExpressions;

namespace Lone.Domain.Validacao.InscricoesEstaduais;

/// <summary>
/// Inscrição Estadual do Maranhão (MA): 9 dígitos iniciados por 12; dígito verificador módulo 11 (pesos 2 a 9).
/// </summary>
internal sealed class RegraMaranhao : IRegraInscricaoEstadual
{
    private static readonly Regex Formato = new(@"^12[0-9]{7}\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <inheritdoc />
    public bool Valida(string digitos) =>
        Formato.IsMatch(digitos) && CalculoDigito.ConfereUltimoDigitoModulo11(digitos, CalculoDigito.Pesos2a9);
}
