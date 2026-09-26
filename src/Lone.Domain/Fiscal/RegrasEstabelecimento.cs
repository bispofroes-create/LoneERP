using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Validacao;

namespace Lone.Domain.Fiscal;

/// <summary>
/// Estabelecimentos (matriz e filiais) de uma MESMA pessoa jurídica: mesma raiz de CNPJ (PessoaValidador). Empresa com
/// outra raiz é outra pessoa jurídica — nunca filial. Estabelecimento gravado nunca é apagado: sai de operação pelo
/// "Ativo" (histórico, documentos, auditoria e referências fiscais continuam).
/// </summary>
public static class RegrasEstabelecimento
{
    /// <summary>
    /// A ficha manda todos os estabelecimentos gravados (os desativados também). Faltou algum = recusa, em vez de apagar.
    /// </summary>
    public static List<string> ValidarCompleto(Pessoa dados, Pessoa? anterior)
    {
        if (anterior is null) return [];
        var enviados = dados.Estabelecimentos.Select(e => e.Id).ToHashSet();
        var faltando = anterior.Estabelecimentos.Where(e => !enviados.Contains(e.Id)).ToList();
        if (faltando.Count == 0) return [];

        // Pessoa jurídica que virou outra natureza: recusada (com o que seria perdido) por RegrasNaturezaPessoa.
        if (anterior.Natureza == NaturezaPessoa.Juridica && dados.Natureza != NaturezaPessoa.Juridica)
            return [];

        return faltando.Select(e =>
            $"O estabelecimento {Descrever(e)} não veio na gravação. Estabelecimento gravado não é excluído, é desativado. Reabra o cadastro e tente de novo.")
            .ToList();
    }

    /// <summary>Inclusão, desativação, reativação e troca do principal viram frases no histórico (só pessoa jurídica).</summary>
    public static IEnumerable<string> Mudancas(Pessoa? anterior, Pessoa dados)
    {
        if (anterior is null || dados.Natureza != NaturezaPessoa.Juridica) yield break;
        var antes = anterior.Estabelecimentos.ToDictionary(e => e.Id);

        foreach (var e in dados.Estabelecimentos)
        {
            if (!antes.TryGetValue(e.Id, out var gravado))
            {
                yield return $"Estabelecimento {Descrever(e)} incluído" + (e.Ativo ? "." : " (inativo).");
                continue;
            }
            if (gravado.Ativo && !e.Ativo) yield return $"Estabelecimento {Descrever(e)} desativado.";
            else if (!gravado.Ativo && e.Ativo) yield return $"Estabelecimento {Descrever(e)} reativado.";
        }

        var principalAntes = anterior.EstabelecimentoPrincipal();
        var principalAgora = dados.EstabelecimentoPrincipal();
        if (anterior.Natureza == NaturezaPessoa.Juridica && principalAntes is not null && principalAgora is not null &&
            principalAntes.Id != principalAgora.Id)
            yield return $"Estabelecimento principal alterado de {Descrever(principalAntes)} para {Descrever(principalAgora)}.";
    }

    private static string Descrever(Estabelecimento e) =>
        e.Cnpj is { Length: > 0 } cnpj ? Documento.Formatar(cnpj) : "sem CNPJ";
}
