namespace Lone.Domain.Validacao;

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

    /// <summary>Sigla da UF pelo código IBGE (os 2 primeiros dígitos do código de um município).</summary>
    public static readonly IReadOnlyDictionary<int, string> PorCodigoIbge = new Dictionary<int, string>
    {
        [11] = "RO", [12] = "AC", [13] = "AM", [14] = "RR", [15] = "PA", [16] = "AP", [17] = "TO",
        [21] = "MA", [22] = "PI", [23] = "CE", [24] = "RN", [25] = "PB", [26] = "PE", [27] = "AL", [28] = "SE", [29] = "BA",
        [31] = "MG", [32] = "ES", [33] = "RJ", [35] = "SP",
        [41] = "PR", [42] = "SC", [43] = "RS",
        [50] = "MS", [51] = "MT", [52] = "GO", [53] = "DF"
    };

    /// <summary>UF de um código de município do IBGE (7 dígitos), ou nulo se o código não for válido.</summary>
    public static string? DoMunicipio(int codigoIbge) =>
        codigoIbge is >= 1000000 and <= 9999999 && PorCodigoIbge.TryGetValue(codigoIbge / 100000, out var uf) ? uf : null;
}
