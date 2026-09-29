using System.Globalization;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;

namespace Lone.Domain.Territorios;

/// <summary>
/// Regras comuns dos cadastros territoriais (Fase 2b): código estável e texto limpo. O código identifica o registro em
/// relatórios, integrações e nos documentos que copiam o território (princípio P-T1), por isso só tem letras sem acento,
/// números, "_", "-" e ".", em maiúsculas.
/// </summary>
public static class RegrasCadastroTerritorial
{
    /// <summary>"mg norte" → "MG_NORTE"; "Curvelo-01" → "CURVELO-01". Acentos caem ("São" → "SAO").</summary>
    public static string NormalizarCodigo(string? texto)
    {
        var semAcento = RemoverAcentos((texto ?? string.Empty).Trim().ToUpperInvariant());
        var resultado = new System.Text.StringBuilder(semAcento.Length);
        var separadorPendente = false;
        foreach (var c in semAcento)
        {
            if (char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.')
            {
                if (separadorPendente && resultado.Length > 0) resultado.Append('_');
                separadorPendente = false;
                resultado.Append(c);
            }
            else if (char.IsWhiteSpace(c))
                separadorPendente = true;
        }
        return resultado.ToString();
    }

    private static string RemoverAcentos(string texto)
    {
        var decomposto = texto.Normalize(System.Text.NormalizationForm.FormD);
        return new string(decomposto.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray())
            .Normalize(System.Text.NormalizationForm.FormC);
    }

    /// <summary>Espaços do começo e do fim tirados e os repetidos reduzidos a um; vazio vira vazio.</summary>
    public static string Texto(string? s) =>
        string.IsNullOrWhiteSpace(s) ? string.Empty : string.Join(' ', s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Como <see cref="Texto"/>, mas vazio vira nulo (campos opcionais).</summary>
    public static string? TextoOpcional(string? s) => Texto(s) is { Length: > 0 } t ? t : null;

    public static string Data(DateOnly d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    /// <summary>Conferências de código e nome comuns a tipos, mapas e territórios.</summary>
    public static IEnumerable<string> ValidarCodigoENome(string codigo, int maximoCodigo, string nome, int maximoNome, string exemploCodigo)
    {
        if (codigo.Length == 0) yield return $"Informe o código (ex.: {exemploCodigo}).";
        else if (codigo.Length > maximoCodigo) yield return $"O código pode ter no máximo {maximoCodigo} caracteres.";
        if (nome.Length == 0) yield return "Informe o nome.";
        else if (nome.Length > maximoNome) yield return $"O nome pode ter no máximo {maximoNome} caracteres.";
    }

    /// <summary>Tipo de território: código e nome únicos (entre ativos e desativados; nome sem acento/maiúscula).</summary>
    public static List<string> ValidarTipo(TipoTerritorio dados, IReadOnlyCollection<TipoTerritorio> todos, TipoTerritorio? anterior)
    {
        var erros = ValidarCodigoENome(dados.Codigo, TipoTerritorio.TamanhoMaximoCodigo, dados.Nome, TipoTerritorio.TamanhoMaximoNome, "GEOGRAFICO").ToList();
        if (dados.Descricao?.Length > TipoTerritorio.TamanhoMaximoDescricao)
            erros.Add($"A descrição pode ter no máximo {TipoTerritorio.TamanhoMaximoDescricao} caracteres.");
        if (dados.Codigo.Length > 0 && todos.Any(t => t.Id != dados.Id && t.Codigo == dados.Codigo))
            erros.Add($"Já existe o tipo de território de código {dados.Codigo}.");
        if (dados.Nome.Length > 0 && todos.Any(t => t.Id != dados.Id && TextoBusca.Normalizar(t.Nome) == TextoBusca.Normalizar(dados.Nome)))
            erros.Add($"Já existe o tipo de território \"{dados.Nome}\" (ativo ou desativado; maiúsculas e acentos não contam).");
        if (anterior is not null && anterior.Codigo != dados.Codigo)
            erros.Add("O código do tipo não muda depois de criado (relatórios e integrações dependem dele).");
        return erros;
    }
}
