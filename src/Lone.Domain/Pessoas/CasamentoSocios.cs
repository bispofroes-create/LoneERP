using System.Globalization;
using System.Text;

namespace Lone.Domain.Pessoas;

/// <summary>O que identifica um sócio para o casamento: documento (como a Receita divulga), nome e qualificação.</summary>
public sealed record DadosSocio(string? Documento, string? Nome, string? Qualificacao);

/// <summary>Resultado do casamento dos sócios recebidos da Receita com os já gravados (por posição nas listas).</summary>
/// <param name="GravadoDoRecebido">Para cada recebido, a posição do gravado com que casou; nulo = entra como novo.</param>
/// <param name="Ambiguos">Posições dos recebidos que tinham mais de um candidato (ou disputavam o mesmo gravado): entram como novos.</param>
/// <param name="GravadosPreservados">Gravados envolvidos numa ambiguidade: ficam exatamente como estão (nem casam, nem saem).</param>
public sealed record ResultadoCasamentoSocios(
    IReadOnlyList<int?> GravadoDoRecebido,
    IReadOnlySet<int> Ambiguos,
    IReadOnlySet<int> GravadosPreservados)
{
    /// <summary>Gravados que não casaram com ninguém e não estão preservados: saíram do quadro.</summary>
    public IEnumerable<int> GravadosQueSairam(int totalGravados) =>
        Enumerable.Range(0, totalGravados).Where(g => !GravadoDoRecebido.Contains(g) && !GravadosPreservados.Contains(g));
}

/// <summary>
/// Casamento do quadro de sócios da Receita com os sócios gravados (P0, D7). Em vez de trocar a lista inteira:
/// 1. por documento, quando os dois lados têm documento (texto exato, inclusive parcial como "***123456**");
/// 2. sem documento em um dos lados: por nome normalizado (sem acento, maiúsculas e espaços) e qualificação.
/// Só casa com exatamente um candidato, nos dois sentidos. Na dúvida, não casa: o recebido entra como novo, os gravados
/// envolvidos ficam como estão e a situação é registrada para conferência. Nunca escolhe arbitrariamente.
/// </summary>
public static class CasamentoSocios
{
    public const string TextoConferencia =
        "Sócios: {0} sócio(s) da Receita não puderam ser identificados com segurança entre os já gravados e foram incluídos " +
        "como novos. Confira o quadro.";

    public static ResultadoCasamentoSocios Casar(IReadOnlyList<DadosSocio> gravados, IReadOnlyList<DadosSocio> recebidos)
    {
        var candidatos = recebidos.Select(r => Enumerable.Range(0, gravados.Count).Where(g => Compativeis(gravados[g], r)).ToList()).ToList();
        var disputa = Enumerable.Range(0, gravados.Count)
            .ToDictionary(g => g, g => candidatos.Count(c => c.Contains(g)));

        var casados = new int?[recebidos.Count];
        var ambiguos = new HashSet<int>();
        var preservados = new HashSet<int>();
        for (var r = 0; r < recebidos.Count; r++)
        {
            var c = candidatos[r];
            if (c.Count == 0) continue; // novo
            if (c.Count == 1 && disputa[c[0]] == 1)
            {
                casados[r] = c[0];
                continue;
            }
            ambiguos.Add(r);
            preservados.UnionWith(c);
        }
        return new ResultadoCasamentoSocios(casados, ambiguos, preservados);
    }

    /// <summary>
    /// O mesmo sócio pelos critérios do casamento: documento quando os dois têm; senão nome normalizado e qualificação.
    /// Documentos diferentes nunca são o mesmo sócio, mesmo com o nome igual.
    /// </summary>
    public static bool Compativeis(DadosSocio a, DadosSocio b)
    {
        var docA = Texto(a.Documento);
        var docB = Texto(b.Documento);
        if (docA.Length > 0 && docB.Length > 0)
            return string.Equals(docA, docB, StringComparison.Ordinal);
        var nomeA = Normalizar(a.Nome);
        return nomeA.Length > 0 && nomeA == Normalizar(b.Nome) && Normalizar(a.Qualificacao) == Normalizar(b.Qualificacao);
    }

    private static string Texto(string? s) => (s ?? string.Empty).Trim();

    /// <summary>Sem acento, maiúsculas, espaços únicos ("João  da Silva" = "JOAO DA SILVA").</summary>
    public static string Normalizar(string? texto)
    {
        var decomposto = Texto(texto).Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposto.Length);
        foreach (var ch in decomposto)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        return string.Join(' ', sb.ToString().Normalize(NormalizationForm.FormC)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
    }
}
