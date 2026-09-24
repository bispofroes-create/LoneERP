// Portado do Caelum Stella (IERondoniaValidator), Apache License 2.0, Copyright Caelum. Veja THIRD-PARTY-NOTICES.md.

using System.Text.RegularExpressions;

namespace Lone.Domain.Validacao.InscricoesEstaduais;

/// <summary>
/// Inscrição Estadual de Rondônia (RO): 14 dígitos; dígito verificador módulo 11 (pesos 2 a 9),
/// com resultado 10 virando 0 e 11 virando 1.
/// </summary>
internal sealed class RegraRondonia : IRegraInscricaoEstadual
{
    private static readonly Regex Formato = new(@"^[0-9]{14}\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly IReadOnlyDictionary<int, int> Trocas = new Dictionary<int, int> { [10] = 0, [11] = 1 };

    /// <inheritdoc />
    public bool Valida(string digitos)
    {
        if (!Formato.IsMatch(digitos))
            return false;

        var corpo = digitos[..^1];
        var digito = CalculoDigito.Calcula(corpo, CalculoDigito.Pesos2a9, 11, complementar: true, Trocas);
        return CalculoDigito.Valor(digitos[^1]) == digito;
    }
}
