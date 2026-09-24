namespace Lone.Core.Validacao;

public static class Ufs
{
    public static readonly IReadOnlySet<string> Todas = new HashSet<string>
    {
        "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA", "MT", "MS", "MG", "PA",
        "PB", "PR", "PE", "PI", "RJ", "RN", "RS", "RO", "RR", "SC", "SP", "SE", "TO"
    };

    /// <summary>UF usada na NF-e para endereços no exterior.</summary>
    public const string Exterior = "EX";

    public static bool Valida(string? uf) => uf is not null && Todas.Contains(uf);
}
