namespace Lone.Core.Validacao;

/// <summary>
/// Normalização, validação e formatação de CPF e CNPJ.
/// Suporta o CNPJ alfanumérico da Receita Federal (a partir de julho/2026):
/// as 12 primeiras posições podem ter letras A–Z e os 2 dígitos verificadores são numéricos.
/// </summary>
public static class Documento
{
    /// <summary>Mantém só letras e números, em maiúsculas.</summary>
    public static string Normalizar(string? valor) =>
        string.IsNullOrWhiteSpace(valor)
            ? string.Empty
            : new string(valor.ToUpperInvariant().Where(char.IsAsciiLetterOrDigit).ToArray());

    public static string SomenteDigitos(string? valor) =>
        valor is null ? string.Empty : new string(valor.Where(char.IsAsciiDigit).ToArray());

    public static bool CpfValido(string? valor)
    {
        var d = Normalizar(valor);
        if (d.Length != 11 || !d.All(char.IsAsciiDigit) || d.Distinct().Count() == 1)
            return false;

        int Dv(int tamanho)
        {
            var soma = 0;
            for (var i = 0; i < tamanho; i++)
                soma += (d[i] - '0') * (tamanho + 1 - i);
            var resto = soma % 11;
            return resto < 2 ? 0 : 11 - resto;
        }

        return Dv(9) == d[9] - '0' && Dv(10) == d[10] - '0';
    }

    public static bool CnpjValido(string? valor)
    {
        var c = Normalizar(valor);
        if (c.Length != 14 || !char.IsAsciiDigit(c[12]) || !char.IsAsciiDigit(c[13]) || c.Distinct().Count() == 1)
            return false;

        // Valor de cada caractere = código ASCII - 48 (dígitos 0–9, letras A=17 ... Z=42).
        int Dv(int tamanho)
        {
            int soma = 0, peso = 2;
            for (var i = tamanho - 1; i >= 0; i--)
            {
                soma += (c[i] - '0') * peso;
                peso = peso == 9 ? 2 : peso + 1;
            }
            var resto = soma % 11;
            return resto < 2 ? 0 : 11 - resto;
        }

        return Dv(12) == c[12] - '0' && Dv(13) == c[13] - '0';
    }

    public static string Formatar(string? valor)
    {
        var v = Normalizar(valor);
        return v.Length switch
        {
            11 => $"{v[..3]}.{v[3..6]}.{v[6..9]}-{v[9..]}",
            14 => $"{v[..2]}.{v[2..5]}.{v[5..8]}/{v[8..12]}-{v[12..]}",
            _ => v
        };
    }
}
