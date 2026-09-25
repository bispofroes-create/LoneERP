using Lone.Domain.Enderecos;
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
    /// <param name="referencia">Endereço de referência da listagem (RegrasFinalidadeEndereco.EnderecoReferencia).</param>
    public static IEnumerable<string> Avisos(Pessoa pessoa, PessoaEndereco? referencia = null)
    {
        foreach (var estabelecimento in pessoa.Estabelecimentos)
        {
            var ie = estabelecimento.InscricaoEstadual;
            if (string.IsNullOrWhiteSpace(ie) || InscricaoEstadual.EhIsento(ie)) continue;

            var uf = UfFiscal(pessoa, estabelecimento, referencia);
            if (uf is null || uf == Ufs.Exterior) continue;

            if (!InscricaoEstadual.Valida(uf, ie))
                yield return $"A inscrição estadual {ie}{Complemento(pessoa, estabelecimento)} não confere com as regras de {uf}. " +
                             "Confira o número e a UF do endereço fiscal.";
        }
    }

    /// <summary>
    /// UF do endereço fiscal do estabelecimento; sem ele, do principal da finalidade Fiscal; sem esse, do endereço de
    /// referência (quando informado). Sem nenhum deles, a UF não é conferida.
    /// </summary>
    private static string? UfFiscal(Pessoa pessoa, Estabelecimento estabelecimento, PessoaEndereco? referencia)
    {
        var endereco = estabelecimento.EnderecoFiscalId is { } id
            ? pessoa.Enderecos.FirstOrDefault(e => e.Id == id)
            : null;
        endereco ??= RegrasFinalidadeEndereco.EnderecoPrincipal(pessoa, FinalidadesEnderecoIniciais.Id(FinalidadesEnderecoIniciais.Fiscal)) ?? referencia;
        return endereco?.Uf is { Length: > 0 } uf ? uf.Trim().ToUpperInvariant() : null;
    }

    private static string Complemento(Pessoa pessoa, Estabelecimento estabelecimento) =>
        pessoa.Estabelecimentos.Count > 1 && estabelecimento.Cnpj is { } cnpj
            ? $" (CNPJ {Documento.Formatar(cnpj)})"
            : string.Empty;
}
