// Portado do Caelum Stella (IERoraimaValidator), Apache License 2.0, Copyright Caelum. Veja THIRD-PARTY-NOTICES.md.

using System.Text.RegularExpressions;

namespace Lone.Domain.Validacao.InscricoesEstaduais;

/// <summary>
/// Inscrição Estadual de Roraima (RR): 9 dígitos iniciados por 24. O dígito verificador é o resto
/// da divisão por 9 (sem complemento) da soma com pesos 1 a 8 da esquerda para a direita.
/// </summary>
internal sealed class RegraRoraima : IRegraInscricaoEstadual
{
    private static readonly Regex Formato = new(@"^24[0-9]{7}\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Pesos a partir do último algarismo (equivale a 1..8 da esquerda para a direita).</summary>
    private static readonly int[] Pesos = [8, 7, 6, 5, 4, 3, 2, 1];

    private static readonly IReadOnlyDictionary<int, int> SemTrocas = new Dictionary<int, int>();

    /// <inheritdoc />
    public bool Valida(string digitos)
    {
        if (!Formato.IsMatch(digitos))
            return false;

        var corpo = digitos[..^1];
        var digito = CalculoDigito.Calcula(corpo, Pesos, 9, complementar: false, SemTrocas);
        return CalculoDigito.Valor(digitos[^1]) == digito;
    }
}
