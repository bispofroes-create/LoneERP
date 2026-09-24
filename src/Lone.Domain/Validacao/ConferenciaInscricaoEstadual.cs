using Lone.Domain.Entidades;
using Lone.Domain.Enums;

namespace Lone.Domain.Validacao;

/// <summary>
/// Confere o dígito da inscrição estadual de cada estabelecimento com a regra da UF do seu endereço fiscal.
/// Gera avisos, não erros: a regra de algum estado pode mudar antes de o sistema ser atualizado, e o usuário
/// não deve ficar impedido de gravar um número que a própria Sefaz aceita.
/// </summary>
public static class ConferenciaInscricaoEstadual
{
    public static IEnumerable<string> Avisos(Pessoa pessoa)
    {
        foreach (var estabelecimento in pessoa.Estabelecimentos)
        {
            var ie = estabelecimento.InscricaoEstadual;
            if (string.IsNullOrWhiteSpace(ie) || InscricaoEstadual.EhIsento(ie)) continue;

            var uf = UfFiscal(pessoa, estabelecimento);
            if (uf is null || uf == Ufs.Exterior) continue;

            if (!InscricaoEstadual.Valida(uf, ie))
                yield return $"A inscrição estadual {ie}{Complemento(pessoa, estabelecimento)} não confere com as regras de {uf}. " +
                             "Confira o número e a UF do endereço fiscal.";
        }
    }

    /// <summary>UF do endereço fiscal do estabelecimento ou, sem ele, do endereço principal da pessoa.</summary>
    private static string? UfFiscal(Pessoa pessoa, Estabelecimento estabelecimento)
    {
        var endereco = estabelecimento.EnderecoFiscalId is { } id
            ? pessoa.Enderecos.FirstOrDefault(e => e.Id == id)
            : null;
        endereco ??= pessoa.Enderecos.FirstOrDefault(e => e.Tem(FinalidadeEndereco.Principal)) ?? pessoa.Enderecos.FirstOrDefault();
        return endereco?.Uf is { Length: > 0 } uf ? uf.Trim().ToUpperInvariant() : null;
    }

    private static string Complemento(Pessoa pessoa, Estabelecimento estabelecimento) =>
        pessoa.Estabelecimentos.Count > 1 && estabelecimento.Cnpj is { } cnpj
            ? $" (CNPJ {Documento.Formatar(cnpj)})"
            : string.Empty;
}
