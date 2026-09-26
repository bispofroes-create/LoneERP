using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Validacao;

namespace Lone.Domain.Pessoas;

/// <summary>
/// Troca do tipo de pessoa (natureza) num cadastro gravado. Pessoa jurídica gravada NÃO vira pessoa física nem
/// estrangeiro: o normalizador apagaria CNPJ, nome fantasia e dados da empresa, e o grupo empresarial e os vínculos
/// societários ficariam incoerentes. Nada é removido em silêncio: a gravação é recusada, com a lista do que seria perdido.
/// Se o tipo estava errado, o caminho é cadastrar a pessoa correta e desativar este cadastro.
/// </summary>
public static class RegrasNaturezaPessoa
{
    /// <param name="vinculosSocietariosComoEmpresa">
    /// Vínculos "sócio de"/"administrador de" em aberto em que esta pessoa é a empresa (o destino).
    /// </param>
    public static string? ValidarTroca(Pessoa? anterior, Pessoa dados, int vinculosSocietariosComoEmpresa)
    {
        if (anterior is null || anterior.Natureza != NaturezaPessoa.Juridica || dados.Natureza == NaturezaPessoa.Juridica)
            return null;

        var perdas = new List<string>();
        var cnpjs = anterior.Estabelecimentos.Where(e => e.Cnpj is { Length: > 0 }).Select(e => Documento.Formatar(e.Cnpj)).ToList();
        if (cnpjs.Count == 1) perdas.Add($"o CNPJ {cnpjs[0]}");
        else if (cnpjs.Count > 1) perdas.Add($"{cnpjs.Count} estabelecimentos (matriz e filiais: {string.Join(", ", cnpjs)})");
        if (anterior.Estabelecimentos.Any(e => !string.IsNullOrWhiteSpace(e.NomeFantasia))) perdas.Add("o nome fantasia");
        if (anterior.GrupoEmpresarialId is not null) perdas.Add("a participação no grupo empresarial");
        if (vinculosSocietariosComoEmpresa > 0)
            perdas.Add($"{vinculosSocietariosComoEmpresa} vínculo(s) de sócio/administrador em que ela é a empresa");
        if (anterior.Socios.Count > 0) perdas.Add("o quadro de sócios da Receita");
        if (anterior.DataAbertura is not null || anterior.Porte is not null || anterior.CapitalSocial is not null)
            perdas.Add("os dados da empresa (abertura, porte, capital social)");

        var natureza = dados.Natureza == NaturezaPessoa.Fisica ? "pessoa física" : "estrangeiro";
        return $"Uma pessoa jurídica gravada não pode virar {natureza}" +
               (perdas.Count > 0 ? $": seriam perdidos {string.Join("; ", perdas)}" : string.Empty) +
               ". Nada foi alterado. Se o tipo de pessoa está errado, cadastre a pessoa correta e desative este cadastro.";
    }
}
