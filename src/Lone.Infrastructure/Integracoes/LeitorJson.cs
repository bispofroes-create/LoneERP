using System.Globalization;
using System.Text.Json;

namespace Lone.Infrastructure.Integracoes;

/// <summary>Lê campos de JSON de forma tolerante: aceita texto ou número e trata vazio como nulo.</summary>
internal static class LeitorJson
{
    public static string? Texto(JsonElement raiz, string campo)
    {
        if (!raiz.TryGetProperty(campo, out var valor))
            return null;

        var texto = valor.ValueKind switch
        {
            JsonValueKind.String => valor.GetString(),
            JsonValueKind.Number => valor.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };

        return string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
    }

    public static decimal? Decimal(JsonElement raiz, string campo) =>
        decimal.TryParse(Texto(raiz, campo), NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : null;

    /// <summary>true/false (ou "S"/"N"); ausente ou nulo = não informado.</summary>
    public static bool? Logico(JsonElement raiz, string campo) => Texto(raiz, campo)?.ToUpperInvariant() switch
    {
        "TRUE" or "S" or "SIM" => true,
        "FALSE" or "N" or "NAO" or "NÃO" => false,
        _ => null
    };

    /// <summary>Data no formato aaaa-mm-dd (o que vier depois, como horário, é ignorado).</summary>
    public static DateOnly? Data(JsonElement raiz, string campo)
    {
        var texto = Texto(raiz, campo);
        return texto is { Length: >= 10 } &&
               DateOnly.TryParseExact(texto[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data)
            ? data
            : null;
    }

    public static int? Inteiro(JsonElement raiz, string campo) =>
        int.TryParse(Texto(raiz, campo), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;
}
