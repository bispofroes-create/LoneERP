// Portado do Caelum Stella (IETocantinsValidator), Apache License 2.0, Copyright Caelum. Veja THIRD-PARTY-NOTICES.md.

using System.Text.RegularExpressions;

namespace Lone.Domain.Validacao.InscricoesEstaduais;

/// <summary>
/// Inscrição Estadual do Tocantins (TO): 9 dígitos, ou 11 dígitos no formato antigo
/// (o 3º e o 4º algarismos indicam o tipo de empresa e são ignorados no cálculo).
/// Dígito verificador módulo 11 (pesos 2 a 9).
/// </summary>
internal sealed class RegraTocantins : IRegraInscricaoEstadual
{
    private static readonly Regex Formato = new(@"^(?:[0-9]{9}|[0-9]{11})\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <inheritdoc />
    public bool Valida(string digitos)
    {
        if (!Formato.IsMatch(digitos))
            return false;

        var corpo = digitos[..^1];
        if (corpo.Length == 10)
            corpo = corpo[..2] + corpo[4..];

        return CalculoDigito.Valor(digitos[^1]) == CalculoDigito.Modulo11(corpo);
    }
}
