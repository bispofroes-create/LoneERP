using System.Globalization;
using System.Text.Json;

namespace Lone.Integracoes;

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

    public static int? Inteiro(JsonElement raiz, string campo) =>
        int.TryParse(Texto(raiz, campo), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;
}
