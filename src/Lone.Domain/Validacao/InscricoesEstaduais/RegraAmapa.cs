// Portado do Caelum Stella (IEAmapaValidator), Apache License 2.0, Copyright Caelum. Veja THIRD-PARTY-NOTICES.md.

using System.Globalization;
using System.Text.RegularExpressions;

namespace Lone.Domain.Validacao.InscricoesEstaduais;

/// <summary>
/// Inscrição Estadual do Amapá (AP): 9 dígitos iniciados por 03. O dígito verificador usa módulo 11
/// com pesos 1 a 9 sobre o corpo acrescido de um algarismo "p", e o resultado 11 vira "d";
/// p e d dependem da faixa numérica do corpo (Sintegra):
/// 03000001–03017000 → p = 5, d = 0; 03017001–03019022 → p = 9, d = 1; demais → p = 0, d = 0.
/// </summary>
internal sealed class RegraAmapa : IRegraInscricaoEstadual
{
    private static readonly Regex Formato = new(@"^03[0-9]{7}\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly int[] Pesos1a9 = CalculoDigito.Sequencia(1, 9);

    /// <inheritdoc />
    public bool Valida(string digitos)
    {
        if (!Formato.IsMatch(digitos))
            return false;

        var corpo = digitos[..^1];
        return CalculoDigito.Valor(digitos[^1]) == CalculaDigito(corpo);
    }

    private static int CalculaDigito(string corpo)
    {
        var numero = int.Parse(corpo, NumberStyles.None, CultureInfo.InvariantCulture);

        var p = 0;
        var d = 0;
        if (numero >= 3000001 && numero <= 3017000)
        {
            p = 5;
        }
        else if (numero >= 3017001 && numero <= 3019022)
        {
            p = 9;
            d = 1;
        }

        var trocas = new Dictionary<int, int> { [10] = 0, [11] = d };
        return CalculoDigito.Calcula(CalculoDigito.Anexa(corpo, p), Pesos1a9, 11, complementar: true, trocas);
    }
}
