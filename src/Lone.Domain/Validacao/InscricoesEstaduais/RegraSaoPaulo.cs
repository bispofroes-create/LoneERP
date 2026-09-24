// Portado do Caelum Stella (IESaoPauloComercioIndustriaValidator e IESaoPauloProdutorRuralValidator), Apache License 2.0, Copyright Caelum. Veja THIRD-PARTY-NOTICES.md.

using System.Text.RegularExpressions;

namespace Lone.Domain.Validacao.InscricoesEstaduais;

/// <summary>
/// Inscrição Estadual de São Paulo (SP). Aceita:
/// <list type="bullet">
/// <item>comércio e indústria: 12 dígitos; o 9º e o 12º são verificadores;</item>
/// <item>produtor rural: 'P' seguido de 12 dígitos; o 9º dígito é verificador.</item>
/// </list>
/// Os verificadores usam o resto da divisão por 11 (sem complemento), com 10 virando 0.
/// </summary>
internal sealed class RegraSaoPaulo : IRegraInscricaoEstadual
{
    private static readonly Regex FormatoComercioIndustria = new(@"^[0-9]{12}\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex FormatoProdutorRural = new(@"^P[0-9]{12}\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Pesos a partir do último algarismo (equivale a 1, 3, 4, 5, 6, 7, 8, 10 da esquerda para a direita).</summary>
    private static readonly int[] PesosPrimeiroDigito = [10, 8, 7, 6, 5, 4, 3, 1];
    private static readonly int[] Pesos2a10 = CalculoDigito.Sequencia(2, 10);
    private static readonly IReadOnlyDictionary<int, int> Trocas = new Dictionary<int, int> { [10] = 0, [11] = 1 };

    /// <inheritdoc />
    public bool Valida(string digitos) => ValidaComercioIndustria(digitos) || ValidaProdutorRural(digitos);

    private static bool ValidaComercioIndustria(string digitos)
    {
        if (!FormatoComercioIndustria.IsMatch(digitos))
            return false;

        var parte1 = digitos[..8];
        var parte2 = digitos[9..11];

        var digito1 = PrimeiroDigito(parte1);
        var digito2 = CalculoDigito.Calcula(
            CalculoDigito.Anexa(parte1, digito1) + parte2, Pesos2a10, 11, complementar: false, Trocas);

        return CalculoDigito.Valor(digitos[8]) == digito1 && CalculoDigito.Valor(digitos[11]) == digito2;
    }

    private static bool ValidaProdutorRural(string digitos)
    {
        if (!FormatoProdutorRural.IsMatch(digitos))
            return false;

        var numeros = digitos[1..];
        return CalculoDigito.Valor(numeros[8]) == PrimeiroDigito(numeros[..8]);
    }

    private static int PrimeiroDigito(string oitoAlgarismos) =>
        CalculoDigito.Calcula(oitoAlgarismos, PesosPrimeiroDigito, 11, complementar: false, Trocas);
}
