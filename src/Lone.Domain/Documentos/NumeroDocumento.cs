using System.Text;
using Lone.Domain.Enums;

namespace Lone.Domain.Documentos;

/// <summary>
/// O número do documento na forma de comparar (P1-8). Contrato único, determinístico e só com caracteres ASCII:
/// <list type="number">
/// <item>letra latina acentuada vira a letra base, pela tabela <see cref="Acentuadas"/> → <see cref="SemAcento"/>;</item>
/// <item>a..z vira A..Z;</item>
/// <item>fica só o que é A..Z ou 0..9 — pontos, hífens, barras, espaços e qualquer outro caractere saem.</item>
/// </list>
/// Zeros à esquerda ficam ("0012" ≠ "12"). Letras fora do alfabeto latino básico (ex.: cirílico) saem, como nos números
/// de passaporte do padrão internacional (só A–Z e 0–9). O número exibido continua o digitado: este valor é técnico.
/// A migração repete exatamente esta regra em SQL (mesma tabela, caractere a caractere); um teste confere a paridade.
/// </summary>
public static class NumeroDocumento
{
    /// <summary>Tamanho da coluna do número (o comparável nunca é maior que o digitado).</summary>
    public const int TamanhoMaximo = 30;

    /// <summary>Separadores aceitos nos formatos <see cref="FormatoNumeroDocumento.Alfanumerico"/> e <see cref="FormatoNumeroDocumento.SomenteDigitos"/>.</summary>
    public const string Separadores = " .-/";

    /// <summary>Letras acentuadas reconhecidas (maiúsculas e minúsculas). Mesma posição em <see cref="SemAcento"/>.</summary>
    public const string Acentuadas = "ÀÁÂÃÄÅÇÈÉÊËÌÍÎÏÑÒÓÔÕÖÙÚÛÜÝàáâãäåçèéêëìíîïñòóôõöùúûüýÿ";

    /// <summary>A letra base de cada uma de <see cref="Acentuadas"/>.</summary>
    public const string SemAcento = "AAAAAACEEEEIIIINOOOOOUUUUYAAAAAACEEEEIIIINOOOOOUUUUYY";

    public static string Normalizar(string? numero)
    {
        if (string.IsNullOrEmpty(numero)) return string.Empty;
        var resultado = new StringBuilder(numero.Length);
        foreach (var c in numero)
            if (Comparavel(c) is { } letra) resultado.Append(letra);
        return resultado.ToString();
    }

    /// <summary>O caractere na forma comparável, ou nulo se ele sai.</summary>
    private static char? Comparavel(char c)
    {
        var i = Acentuadas.IndexOf(c);
        var x = i >= 0 ? SemAcento[i] : c;
        if (x is >= 'a' and <= 'z') x = (char)(x - ('a' - 'A'));
        return x is >= 'A' and <= 'Z' or >= '0' and <= '9' ? x : null;
    }

    /// <summary>
    /// Confere o número digitado contra o formato e os tamanhos do tipo (tamanhos contados no número comparável).
    /// Nulo = está de acordo; senão, o problema em uma frase curta.
    /// </summary>
    public static string? ConferirFormato(string? numero, FormatoNumeroDocumento formato, int? tamanhoMinimo, int? tamanhoMaximo)
    {
        var texto = numero ?? string.Empty;
        var comparavel = Normalizar(texto);

        if (formato != FormatoNumeroDocumento.Livre)
        {
            if (comparavel.Length == 0)
                return formato == FormatoNumeroDocumento.SomenteDigitos ? "deve ter números" : "deve ter letras ou números";
            foreach (var c in texto)
            {
                if (Separadores.Contains(c)) continue;
                if (Comparavel(c) is not { } letra)
                    return formato == FormatoNumeroDocumento.SomenteDigitos
                        ? "deve ter só números (pontos, hífens, barras e espaços são aceitos)"
                        : "deve ter só letras e números (pontos, hífens, barras e espaços são aceitos)";
                if (formato == FormatoNumeroDocumento.SomenteDigitos && letra is < '0' or > '9')
                    return "deve ter só números (pontos, hífens, barras e espaços são aceitos)";
            }
        }

        if (tamanhoMinimo is { } minimo && tamanhoMaximo is { } maximo && minimo == maximo && comparavel.Length != minimo)
            return $"deve ter {minimo} caracteres (sem contar pontos, hífens e barras)";
        if (tamanhoMinimo is { } min && comparavel.Length < min)
            return $"deve ter pelo menos {min} caracteres (sem contar pontos, hífens e barras)";
        if (tamanhoMaximo is { } max && comparavel.Length > max)
            return $"pode ter no máximo {max} caracteres (sem contar pontos, hífens e barras)";
        return null;
    }

    /// <summary>
    /// Chave da unicidade entre pessoas, gravada no documento e protegida por índice único filtrado (ativos com chave).
    /// Nula quando o modo não bloqueia, quando não há número comparável ou quando falta a UF exigida pelo modo —
    /// nunca uma chave com parte ausente (que juntaria documentos diferentes).
    /// </summary>
    public static string? ChaveUnicidade(UnicidadeDocumento modo, Guid tipoDocumentoId, string? uf, string numeroNormalizado)
    {
        if (string.IsNullOrEmpty(numeroNormalizado) || tipoDocumentoId == Guid.Empty) return null;
        return modo switch
        {
            UnicidadeDocumento.PorTipo => $"{tipoDocumentoId:N}|{numeroNormalizado}",
            UnicidadeDocumento.PorTipoEUf when uf is { Length: 2 } => $"{tipoDocumentoId:N}|{uf.ToUpperInvariant()}|{numeroNormalizado}",
            _ => null
        };
    }

    /// <summary>Tamanho da coluna da chave: Guid (32) + "|" + UF (2) + "|" + número (30).</summary>
    public const int TamanhoChave = 70;
}
