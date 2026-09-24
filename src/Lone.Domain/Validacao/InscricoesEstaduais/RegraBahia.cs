// Portado do Caelum Stella (IEBahiaValidator), Apache License 2.0, Copyright Caelum. Veja THIRD-PARTY-NOTICES.md.

using System.Text.RegularExpressions;

namespace Lone.Domain.Validacao.InscricoesEstaduais;

/// <summary>
/// Inscrição Estadual da Bahia (BA): 8 ou 9 dígitos, sendo os dois últimos verificadores.
/// O módulo é 11 quando o algarismo de controle (1º algarismo para 8 dígitos, 2º para 9 dígitos)
/// é 6, 7 ou 9; nos demais casos é 10. O último dígito é calculado primeiro, sobre o corpo;
/// o penúltimo, sobre o corpo acrescido do último.
/// </summary>
internal sealed class RegraBahia : IRegraInscricaoEstadual
{
    private static readonly Regex Formato = new(@"^[0-9]{8,9}\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly IReadOnlyDictionary<int, int> DezOuOnzeViramZero =
        new Dictionary<int, int> { [10] = 0, [11] = 0 };

    /// <inheritdoc />
    public bool Valida(string digitos)
    {
        if (!Formato.IsMatch(digitos))
            return false;

        var corpo = digitos[..^2];
        var controle = corpo.Length == 6 ? corpo[0] : corpo[1];
        var modulo = controle is '6' or '7' or '9' ? 11 : 10;

        var digito2 = CalculoDigito.Calcula(corpo, CalculoDigito.Pesos2a9, modulo, complementar: true, DezOuOnzeViramZero);
        var digito1 = CalculoDigito.Calcula(
            CalculoDigito.Anexa(corpo, digito2), CalculoDigito.Pesos2a9, modulo, complementar: true, DezOuOnzeViramZero);

        return CalculoDigito.Valor(digitos[^2]) == digito1 && CalculoDigito.Valor(digitos[^1]) == digito2;
    }
}
