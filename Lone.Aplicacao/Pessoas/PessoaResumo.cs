using Lone.Core.Enums;
using Lone.Core.Validacao;

namespace Lone.Aplicacao.Pessoas;

/// <summary>Linha da lista de pessoas (só o necessário para exibir e buscar).</summary>
public class PessoaResumo
{
    public int Id { get; set; }
    public int Codigo { get; set; }
    public string Nome { get; set; } = string.Empty;
    public NaturezaPessoa Natureza { get; set; }
    public string? DocumentoPrincipal { get; set; }
    public string? CnpjPrincipal { get; set; }
    public int QuantidadeEstabelecimentos { get; set; }
    public SituacaoPessoa Situacao { get; set; }
    public List<TipoPapel> Papeis { get; set; } = new();
    public string? Cidade { get; set; }
    public string? Uf { get; set; }

    public string CodigoFormatado => Codigo.ToString("000000");

    public string DocumentoFormatado => Natureza switch
    {
        NaturezaPessoa.Juridica => Documento.Formatar(CnpjPrincipal) +
                                   (QuantidadeEstabelecimentos > 1 ? $" (+{QuantidadeEstabelecimentos - 1})" : string.Empty),
        NaturezaPessoa.Fisica => Documento.Formatar(DocumentoPrincipal),
        _ => DocumentoPrincipal ?? string.Empty
    };

    public string PapeisTexto => string.Join(" · ", Papeis.OrderBy(p => p).Select(NomePapel));

    public string Local => Cidade is null ? string.Empty : Uf is null ? Cidade : $"{Cidade}/{Uf}";

    public string SituacaoTexto => Situacao switch
    {
        SituacaoPessoa.EmAnalise => "Em análise",
        SituacaoPessoa.Inativo => "Inativo",
        SituacaoPessoa.Arquivado => "Arquivado",
        _ => string.Empty
    };

    public string Detalhe => string.Join("  ·  ",
        new[] { CodigoFormatado, DocumentoFormatado, Local, PapeisTexto, SituacaoTexto }.Where(s => s.Length > 0));

    public static string NomePapel(TipoPapel papel) => papel switch
    {
        TipoPapel.Cliente => "Cliente",
        TipoPapel.Fornecedor => "Fornecedor",
        TipoPapel.EmpresaDoGrupo => "Empresa do grupo",
        TipoPapel.Vendedor => "Vendedor",
        TipoPapel.Funcionario => "Funcionário",
        TipoPapel.Transportadora => "Transportadora",
        TipoPapel.Representante => "Representante",
        TipoPapel.PrestadorServico => "Prestador de serviço",
        _ => papel.ToString()
    };
}
