using System.Globalization;

namespace Lone.Cliente.ViewModels.Comum;

/// <summary>
/// Datas e números digitados como texto (dd/mm/aaaa, 1.234,56): funciona igual no teclado do computador e do
/// celular, e campo vazio vira nulo. Quem valida o texto é a ficha, antes de enviar.
/// </summary>
public static class TextoTela
{
    public static readonly CultureInfo Brasil = new("pt-BR");
    private static readonly string[] FormatosData = ["dd/MM/yyyy", "d/M/yyyy", "ddMMyyyy", "dd/MM/yy", "d/M/yy"];

    public static string Data(DateOnly? data) => data?.ToString("dd/MM/yyyy", Brasil) ?? string.Empty;

    public static bool TentarData(string? texto, out DateOnly? data)
    {
        data = null;
        if (string.IsNullOrWhiteSpace(texto)) return true;
        if (!DateOnly.TryParseExact(texto.Trim(), FormatosData, Brasil, DateTimeStyles.None, out var lida)) return false;
        data = lida;
        return true;
    }

    /// <summary>Só a data completa "dd/mm/aaaa" (para cálculos ao vivo, como a idade); qualquer outra coisa = nulo.</summary>
    public static DateOnly? DataCompleta(string? texto) =>
        DateOnly.TryParseExact((texto ?? string.Empty).Trim(), "dd/MM/yyyy", Brasil, DateTimeStyles.None, out var d) ? d : null;

    public static string Decimal(decimal? valor) => valor?.ToString("#,0.##", Brasil) ?? string.Empty;

    public static bool TentarDecimal(string? texto, out decimal? valor)
    {
        valor = null;
        if (string.IsNullOrWhiteSpace(texto)) return true;
        if (!decimal.TryParse(texto.Trim(), NumberStyles.Number, Brasil, out var lido)) return false;
        valor = lido;
        return true;
    }

    public static string Inteiro(int? valor) => valor?.ToString(Brasil) ?? string.Empty;

    public static bool TentarInteiro(string? texto, out int? valor)
    {
        valor = null;
        if (string.IsNullOrWhiteSpace(texto)) return true;
        if (!int.TryParse(texto.Trim(), NumberStyles.Integer, Brasil, out var lido)) return false;
        valor = lido;
        return true;
    }

    /// <summary>Número com as casas que tiver (até 6), ex.: "1.234,5".</summary>
    public static string Numero(decimal? valor) => valor?.ToString("#,0.######", Brasil) ?? string.Empty;

    private static readonly string[] FormatosDataHora = ["dd/MM/yyyy HH:mm", "d/M/yyyy H:mm", "dd/MM/yyyy H:mm", "dd/MM/yyyy"];

    public static string DataHora(DateTime? valor) => valor?.ToString("dd/MM/yyyy HH:mm", Brasil) ?? string.Empty;

    /// <summary>"dd/mm/aaaa hh:mm" (hora opcional = 00:00). Vazio = nulo.</summary>
    public static bool TentarDataHora(string? texto, out DateTime? valor)
    {
        valor = null;
        if (string.IsNullOrWhiteSpace(texto)) return true;
        if (!DateTime.TryParseExact(texto.Trim(), FormatosDataHora, Brasil, DateTimeStyles.None, out var lido)) return false;
        valor = lido;
        return true;
    }

    public static string? Nulo(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto;
}
