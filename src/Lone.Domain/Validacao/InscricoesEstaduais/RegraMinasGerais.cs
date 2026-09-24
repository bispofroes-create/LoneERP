// Portado do Caelum Stella (IEMinasGeraisValidator), Apache License 2.0, Copyright Caelum. Veja THIRD-PARTY-NOTICES.md.

using System.Text.RegularExpressions;

namespace Lone.Domain.Validacao.InscricoesEstaduais;

/// <summary>
/// Inscrição Estadual de Minas Gerais (MG): 13 dígitos, sendo os dois últimos verificadores.
/// 1º dígito: insere-se um 0 após o 3º algarismo, pesos 2 e 1 alternados, soma dos algarismos
/// de cada produto, complemento do módulo 10 (10 vira 0).
/// 2º dígito: corpo + 1º dígito, módulo 11 com pesos 2 a 11 (10 e 11 viram 0).
/// </summary>
internal sealed class RegraMinasGerais : IRegraInscricaoEstadual
{
    private static readonly Regex Formato = new(@"^[0-9]{13}\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly int[] Pesos2e1 = [2, 1];
    private static readonly int[] Pesos2a11 = CalculoDigito.Sequencia(2, 11);
    private static readonly IReadOnlyDictionary<int, int> DezViraZero = new Dictionary<int, int> { [10] = 0 };

    /// <inheritdoc />
    public bool Valida(string digitos)
    {
        if (!Formato.IsMatch(digitos))
            return false;

        var corpo = digitos[..^2];
        var corpoComZero = corpo[..3] + "0" + corpo[3..];

        var digito1 = CalculoDigito.Calcula(
            corpoComZero, Pesos2e1, 10, complementar: true, DezViraZero, somarIndividualmente: true);
        var digito2 = CalculoDigito.Modulo11(CalculoDigito.Anexa(corpo, digito1), Pesos2a11);

        return CalculoDigito.Valor(digitos[^2]) == digito1 && CalculoDigito.Valor(digitos[^1]) == digito2;
    }
}
