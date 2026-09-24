using System.ComponentModel;
using Lone.Core.Auditoria;
using Lone.Core.Enums;

namespace Lone.Core.Entidades;

/// <summary>
/// Identidade central do ERP (Business Partner): existe uma vez só, e os papéis dizem o que ela é
/// para a empresa. Pessoa jurídica = a empresa (raiz do CNPJ); cada CNPJ completo é um Estabelecimento.
/// Toda pessoa tem ao menos um estabelecimento (na PF ele fica oculto e guarda os dados fiscais).
/// </summary>
[DisplayName("Cadastro")]
public class Pessoa : AgregadoRaiz
{
    /// <summary>Código de exibição (000123), gerado pelo banco. Não é a chave.</summary>
    [DisplayName("Código")]
    public int Codigo { get; set; }

    [DisplayName("Natureza")]
    public NaturezaPessoa Natureza { get; set; } = NaturezaPessoa.Fisica;

    [DisplayName("Situação")]
    public SituacaoPessoa Situacao { get; set; } = SituacaoPessoa.Ativo;

    /// <summary>Nome civil completo (PF), razão social (PJ) ou nome (estrangeiro). É o que vai nos documentos.</summary>
    [DisplayName("Nome / razão social")]
    public string Nome { get; set; } = string.Empty;

    /// <summary>Nome social (PF), quando a pessoa informar.</summary>
    [DisplayName("Nome social")]
    public string? NomeSocial { get; set; }

    /// <summary>Nome curto usado nas telas.</summary>
    [DisplayName("Nome de exibição")]
    public string? NomeExibicao { get; set; }

    [DisplayName("Apelido")]
    public string? Apelido { get; set; }

    /// <summary>CPF (PF), raiz do CNPJ com 8 posições (PJ) ou identificação do estrangeiro. Único por natureza.</summary>
    [DisplayName("Documento"), DadoSensivel]
    public string? DocumentoPrincipal { get; set; }

    [DisplayName("Data de nascimento"), DadoSensivel]
    public DateOnly? DataNascimento { get; set; }

    [DisplayName("Grupo econômico")]
    public int? GrupoEconomicoId { get; set; }

    /// <summary>Quando um cadastro duplicado é arquivado, aponta para o que ficou valendo.</summary>
    [DisplayName("Mesclado em")]
    public int? MescladaEmId { get; set; }

    [DisplayName("Observações")]
    public string? Observacoes { get; set; }

    public List<Estabelecimento> Estabelecimentos { get; set; } = new();
    public List<PessoaDocumento> Documentos { get; set; } = new();
    public List<PessoaEndereco> Enderecos { get; set; } = new();
    public List<MeioContato> MeiosContato { get; set; } = new();
    public List<Contato> Contatos { get; set; } = new();
    public List<PessoaPapel> Papeis { get; set; } = new();
    public List<ContaCliente> ContasCliente { get; set; } = new();
    public List<ContaFornecedor> ContasFornecedor { get; set; } = new();

    /// <summary>Gravados só pelas operações de bloquear/liberar (não pelo formulário de cadastro).</summary>
    public List<Bloqueio> Bloqueios { get; set; } = new();

    /// <summary>Vínculos em que esta pessoa é a origem (ex.: "Sócio de" outra pessoa).</summary>
    public List<PessoaRelacionamento> Relacionamentos { get; set; } = new();

    public string NomeParaExibir() =>
        !string.IsNullOrWhiteSpace(NomeExibicao) ? NomeExibicao
        : !string.IsNullOrWhiteSpace(NomeSocial) ? NomeSocial
        : Nome;

    public bool TemPapel(TipoPapel papel) => Papeis.Any(p => p.Papel == papel && p.Ativo);

    public Estabelecimento? EstabelecimentoPrincipal() =>
        Estabelecimentos.FirstOrDefault(e => e.Principal) ?? Estabelecimentos.FirstOrDefault();
}
