using System.Globalization;
using System.Text;

namespace Lone.Domain.Comum;

/// <summary>
/// Forma de comparação de nomes: sem acentos, em maiúsculas, só letras e números separados por um espaço.
/// "São João del-Rei", "SAO JOAO DEL REI" e "sao  joão del rei" viram a mesma coisa.
/// </summary>
public static class TextoBusca
{
    public static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;

        var decomposto = texto.Normalize(NormalizationForm.FormD);
        var resultado = new StringBuilder(decomposto.Length);
        var espacoPendente = false;

        foreach (var c in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue; // acento separado da letra pela decomposição

            if (char.IsLetterOrDigit(c))
            {
                if (espacoPendente && resultado.Length > 0) resultado.Append(' ');
                espacoPendente = false;
                resultado.Append(char.ToUpperInvariant(c));
            }
            else
            {
                espacoPendente = true; // hífen, apóstrofo, barra, espaços repetidos...
            }
        }

        return resultado.ToString();
    }
}
