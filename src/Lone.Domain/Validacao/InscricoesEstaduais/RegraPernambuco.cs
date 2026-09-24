// Portado do Caelum Stella (IEPernambucoAntigaValidator e IEPernambucoNovaValidator), Apache License 2.0, Copyright Caelum. Veja THIRD-PARTY-NOTICES.md.

using System.Text.RegularExpressions;

namespace Lone.Domain.Validacao.InscricoesEstaduais;

/// <summary>
/// Inscrição Estadual de Pernambuco (PE). Aceita os dois formatos em uso:
/// <list type="bullet">
/// <item>eFisco (novo): 9 dígitos, dois dígitos verificadores módulo 11 (pesos 2 a 9);</item>
/// <item>CACEPE (antigo): 14 dígitos iniciados por 18 e 3º algarismo de 1 a 9; um dígito verificador
/// módulo 11 com pesos 1 a 9 sobre o corpo acrescido de 0 (10 vira 0; 11 vira 1).</item>
/// </list>
/// </summary>
internal sealed class RegraPernambuco : IRegraInscricaoEstadual
{
    private static readonly Regex FormatoNovo = new(@"^[0-9]{9}\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex FormatoAntigo = new(@"^18[1-9][0-9]{11}\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly int[] Pesos1a9 = CalculoDigito.Sequencia(1, 9);
    private static readonly IReadOnlyDictionary<int, int> TrocasAntigo = new Dictionary<int, int> { [10] = 0, [11] = 1 };

    /// <inheritdoc />
    public bool Valida(string digitos) => ValidaNovo(digitos) || ValidaAntigo(digitos);

    private static bool ValidaNovo(string digitos) =>
        FormatoNovo.IsMatch(digitos) && CalculoDigito.ConfereDoisDigitosModulo11(digitos, CalculoDigito.Pesos2a9);

    private static bool ValidaAntigo(string digitos)
    {
        if (!FormatoAntigo.IsMatch(digitos))
            return false;

        var corpo = digitos[..^1];
        var digito = CalculoDigito.Calcula(corpo + "0", Pesos1a9, 11, complementar: true, TrocasAntigo);
        return CalculoDigito.Valor(digitos[^1]) == digito;
    }
}
