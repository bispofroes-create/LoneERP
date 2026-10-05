namespace Lone.Domain.Enderecos.ConferenciaCep;

/// <summary>
/// Normalização do logradouro só para comparar ("R. Barao" = "Rua Barão"). Nunca muda o que foi digitado: devolve outro
/// texto. Usa exatamente a normalização da duplicidade (<see cref="DuplicidadeEndereco.Logradouro"/>): maiúsculas, sem
/// acentos, pontuação e espaços repetidos não contam, e só as abreviações inequívocas no começo (R, AV, AVN, TV, TRAV, PC,
/// PCA, ROD). Nenhuma regra nova: a lista conservadora é a mesma da duplicidade, de propósito.
/// </summary>
public static class NormalizadorLogradouro
{
    /// <summary>O logradouro normalizado para comparação; vazio quando não há letra nem dígito.</summary>
    public static string Normalizar(string? logradouro) => DuplicidadeEndereco.Logradouro(logradouro);

    /// <summary>Os dois são o mesmo logradouro depois de normalizar. Vazio nunca é equivalente a nada (nem a outro vazio).</summary>
    public static bool Equivalentes(string? a, string? b)
    {
        var na = Normalizar(a);
        return na.Length > 0 && string.Equals(na, Normalizar(b), StringComparison.Ordinal);
    }
}
