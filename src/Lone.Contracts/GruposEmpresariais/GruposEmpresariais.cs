using Lone.Domain.Enums;

namespace Lone.Contracts.GruposEmpresariais;

/// <summary>
/// Grupo empresarial (conjunto de pessoas jurídicas independentes), como trafega entre o aplicativo e a API.
/// Não é o grupo econômico.
/// </summary>
public sealed class GrupoEmpresarialDto
{
    public Guid Id { get; set; }
    public byte[]? Versao { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public bool Ativo { get; set; } = true;

    /// <summary>Somente leitura: quantas pessoas jurídicas fazem parte do grupo.</summary>
    public int QuantidadeEmpresas { get; set; }
}

/// <summary>Uma empresa (pessoa jurídica) do grupo, com a sua estrutura própria de estabelecimentos. Somente leitura.</summary>
public sealed class EmpresaDoGrupoEmpresarialDto
{
    public Guid Id { get; set; }
    public int Codigo { get; set; }

    /// <summary>Nome para exibir (exibição → nome fantasia → razão social).</summary>
    public string Nome { get; set; } = string.Empty;
    public string RazaoSocial { get; set; } = string.Empty;
    public string? CnpjPrincipal { get; set; }
    public int QuantidadeEstabelecimentos { get; set; }
    public int EstabelecimentosAtivos { get; set; }
    public SituacaoPessoa Situacao { get; set; }
}
