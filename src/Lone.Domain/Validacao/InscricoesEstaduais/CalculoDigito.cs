// Portado do Caelum Stella (br.com.caelum.stella.DigitoPara), Apache License 2.0, Copyright Caelum. Veja THIRD-PARTY-NOTICES.md.

namespace Lone.Domain.Validacao.InscricoesEstaduais;

/// <summary>
/// Cálculo de dígito verificador por soma ponderada e módulo (equivalente ao DigitoPara do Stella).
/// Os pesos são aplicados da direita para a esquerda: o primeiro peso multiplica o último algarismo
/// do trecho e a lista de pesos recomeça quando termina.
/// </summary>
internal static class CalculoDigito
{
    /// <summary>Pesos 2, 3, ..., 9 (padrão do módulo 11).</summary>
    public static readonly IReadOnlyList<int> Pesos2a9 = Sequencia(2, 9);

    private static readonly IReadOnlyDictionary<int, int> DezOuOnzeViramZero =
        new Dictionary<int, int> { [10] = 0, [11] = 0 };

    /// <summary>Pesos consecutivos de <paramref name="inicio"/> até <paramref name="fim"/>.</summary>
    public static int[] Sequencia(int inicio, int fim)
    {
        var pesos = new int[fim - inicio + 1];
        for (var i = 0; i < pesos.Length; i++)
            pesos[i] = inicio + i;
        return pesos;
    }

    /// <summary>
    /// Calcula o dígito: soma ponderada, resto por <paramref name="modulo"/>, complemento opcional
    /// (<c>modulo - resto</c>) e troca de resultados especiais (ex.: 10 e 11 viram 0).
    /// </summary>
    /// <param name="trecho">Somente algarismos.</param>
    /// <param name="pesos">Pesos aplicados a partir do último algarismo.</param>
    /// <param name="modulo">Divisor do resto.</param>
    /// <param name="complementar">Se verdadeiro, o resultado é <c>modulo - resto</c>.</param>
    /// <param name="trocas">Resultado calculado → dígito que o substitui.</param>
    /// <param name="somarIndividualmente">Se verdadeiro, soma os algarismos de cada produto (18 conta 1 + 8).</param>
    public static int Calcula(
        string trecho,
        IReadOnlyList<int> pesos,
        int modulo,
        bool complementar,
        IReadOnlyDictionary<int, int> trocas,
        bool somarIndividualmente = false)
    {
        var soma = 0;
        var indicePeso = 0;
        for (var i = trecho.Length - 1; i >= 0; i--)
        {
            var produto = Valor(trecho[i]) * pesos[indicePeso];
            soma += somarIndividualmente ? produto / 10 + produto % 10 : produto;
            indicePeso = (indicePeso + 1) % pesos.Count;
        }

        var resultado = soma % modulo;
        if (complementar)
            resultado = modulo - resultado;

        return trocas.TryGetValue(resultado, out var substituto) ? substituto : resultado;
    }

    /// <summary>Módulo 11 complementar com pesos 2 a 9; resultados 10 e 11 viram 0.</summary>
    public static int Modulo11(string trecho) => Modulo11(trecho, Pesos2a9);

    /// <summary>Módulo 11 complementar com os pesos informados; resultados 10 e 11 viram 0.</summary>
    public static int Modulo11(string trecho, IReadOnlyList<int> pesos) =>
        Calcula(trecho, pesos, 11, complementar: true, DezOuOnzeViramZero);

    /// <summary>Valor numérico de um algarismo ('0' → 0).</summary>
    public static int Valor(char algarismo) => algarismo - '0';

    /// <summary>Acrescenta um dígito calculado ao fim do trecho.</summary>
    public static string Anexa(string trecho, int digito) => trecho + (char)('0' + digito);

    /// <summary>Confere o último algarismo contra o módulo 11 padrão dos anteriores.</summary>
    public static bool ConfereUltimoDigitoModulo11(string digitos, IReadOnlyList<int> pesos)
    {
        var corpo = digitos[..^1];
        return Valor(digitos[^1]) == Modulo11(corpo, pesos);
    }

    /// <summary>
    /// Confere os dois últimos algarismos: o primeiro é o módulo 11 do corpo e o segundo,
    /// o módulo 11 do corpo acrescido do primeiro.
    /// </summary>
    public static bool ConfereDoisDigitosModulo11(string digitos, IReadOnlyList<int> pesos)
    {
        var corpo = digitos[..^2];
        var digito1 = Modulo11(corpo, pesos);
        var digito2 = Modulo11(Anexa(corpo, digito1), pesos);
        return Valor(digitos[^2]) == digito1 && Valor(digitos[^1]) == digito2;
    }
}
