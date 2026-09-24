// Portado do Caelum Stella (IERioGrandeDoNorteValidator), Apache License 2.0, Copyright Caelum. Veja THIRD-PARTY-NOTICES.md.

using System.Text.RegularExpressions;

namespace Lone.Domain.Validacao.InscricoesEstaduais;

/// <summary>
/// Inscrição Estadual do Rio Grande do Norte (RN): 9 ou 10 dígitos iniciados por 20; dígito verificador módulo 11 (pesos 2 a 10).
/// </summary>
internal sealed class RegraRioGrandeDoNorte : IRegraInscricaoEstadual
{
    private static readonly Regex Formato = new(@"^20[0-9]{7,8}\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly int[] Pesos2a10 = CalculoDigito.Sequencia(2, 10);

    /// <inheritdoc />
    public bool Valida(string digitos) =>
        Formato.IsMatch(digitos) && CalculoDigito.ConfereUltimoDigitoModulo11(digitos, Pesos2a10);
}
