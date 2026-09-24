// Portado do Caelum Stella (IEGoiasValidator), Apache License 2.0, Copyright Caelum. Veja THIRD-PARTY-NOTICES.md.

using System.Globalization;
using System.Text.RegularExpressions;

namespace Lone.Domain.Validacao.InscricoesEstaduais;

/// <summary>
/// Inscrição Estadual de Goiás (GO): 9 dígitos iniciados por 10, 11, 19, 20, 21 ou 29.
/// Dígito verificador módulo 11 (pesos 2 a 9); o resultado 11 vira 0 e o resultado 10 vira 1
/// na faixa 10103105–10119997 (0 fora dela). A inscrição 11094402 aceita dígito 0 ou 1.
/// </summary>
internal sealed class RegraGoias : IRegraInscricaoEstadual
{
    private static readonly Regex Formato = new(@"^[12][019][0-9]{7}\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <inheritdoc />
    public bool Valida(string digitos)
    {
        if (!Formato.IsMatch(digitos))
            return false;

        var corpo = digitos[..^1];
        var digito = CalculoDigito.Valor(digitos[^1]);
        return ExcecaoAceita(corpo, digito) || digito == CalculaDigito(corpo);
    }

    /// <summary>Exceção documentada pela SEFAZ-GO: 11094402 com dígito 0 ou 1.</summary>
    private static bool ExcecaoAceita(string corpo, int digito) =>
        corpo == "11094402" && (digito == 0 || digito == 1);

    private static int CalculaDigito(string corpo)
    {
        var numero = int.Parse(corpo, NumberStyles.None, CultureInfo.InvariantCulture);
        var d = numero >= 10103105 && numero <= 10119997 ? 1 : 0;

        var trocas = new Dictionary<int, int> { [10] = d, [11] = 0 };
        return CalculoDigito.Calcula(corpo, CalculoDigito.Pesos2a9, 11, complementar: true, trocas);
    }
}
