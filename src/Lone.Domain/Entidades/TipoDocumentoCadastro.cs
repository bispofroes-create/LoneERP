using System.ComponentModel;
using Lone.Domain.Enums;

namespace Lone.Domain.Entidades;

/// <summary>
/// Tipo de documento da pessoa (RG, CNH, Passaporte, Alvará, Certificado digital...). Os cinco de sistema
/// (<see cref="TipoSistema"/> preenchido) nascem com a base e ficam ligados ao enum <see cref="TipoDocumento"/>
/// (a coluna antiga dos documentos continua existindo e recebe a cópia); os demais são criados pelo usuário.
/// Cada tipo diz se a validade é obrigatória e com quantos dias de antecedência avisar o vencimento.
/// Nunca é excluído: desativado, some das escolhas novas mas continua nos documentos que já o têm.
/// </summary>
/// <remarks>
/// Chama-se "TipoDocumentoCadastro" porque o nome "TipoDocumento" já é do enum (usado em regras e em integrações).
/// </remarks>
[DisplayName("Tipo de documento")]
public class TipoDocumentoCadastro : AgregadoRaiz
{
    public const int TamanhoMaximoNome = 40;
    public const int MaximoDiasAviso = 3650;
    public const int DiasAvisoPadrao = 30;

    [DisplayName("Nome")]
    public string Nome { get; set; } = string.Empty;

    [DisplayName("Ordem")]
    public int Ordem { get; set; }

    [DisplayName("Ativo")]
    public bool Ativo { get; set; } = true;

    /// <summary>Tipo de sistema ligado ao enum (nulo = criado pelo usuário). Não muda depois de criado.</summary>
    [DisplayName("Tipo de sistema")]
    public TipoDocumento? TipoSistema { get; set; }

    /// <summary>Documento ativo deste tipo precisa ter "válido até" (ex.: CNH, passaporte, alvará).</summary>
    [DisplayName("Exige validade")]
    public bool ExigeValidade { get; set; }

    /// <summary>Quantos dias antes do vencimento o documento aparece como "vence em breve" (0 = só avisa quando vencer).</summary>
    [DisplayName("Dias de aviso do vencimento")]
    public int DiasAvisoVencimento { get; set; } = DiasAvisoPadrao;

    // ---- P1-8: regras do tipo como dados (os cinco de sistema nascem com o comportamento de antes) ----

    [DisplayName("Pode ser usado para pessoa física")]
    public bool AplicaPessoaFisica { get; set; } = true;

    [DisplayName("Pode ser usado para pessoa jurídica")]
    public bool AplicaPessoaJuridica { get; set; } = true;

    [DisplayName("Pode ser usado para estrangeiro")]
    public bool AplicaEstrangeiro { get; set; } = true;

    [DisplayName("Órgão emissor")]
    public UsoCampoDocumento UsoOrgaoEmissor { get; set; } = UsoCampoDocumento.Oculto;

    [DisplayName("UF")]
    public UsoCampoDocumento UsoUf { get; set; } = UsoCampoDocumento.Oculto;

    [DisplayName("Data de emissão")]
    public UsoCampoDocumento UsoEmissao { get; set; } = UsoCampoDocumento.Opcional;

    [DisplayName("Formato do número")]
    public FormatoNumeroDocumento FormatoNumero { get; set; } = FormatoNumeroDocumento.Livre;

    /// <summary>Mínimo de caracteres do número comparável (sem pontos, hífens...). Nulo = sem mínimo.</summary>
    [DisplayName("Tamanho mínimo do número")]
    public int? TamanhoMinimoNumero { get; set; }

    /// <summary>Máximo de caracteres do número comparável (até o tamanho da coluna). Nulo = só o limite da coluna.</summary>
    [DisplayName("Tamanho máximo do número")]
    public int? TamanhoMaximoNumero { get; set; }

    /// <summary>O mesmo número em outra pessoa: não verifica, avisa ou bloqueia. Na mesma pessoa é sempre erro.</summary>
    [DisplayName("Número repetido em outra pessoa")]
    public UnicidadeDocumento Unicidade { get; set; } = UnicidadeDocumento.Nenhuma;

    /// <summary>O tipo pode ser usado por uma pessoa desta natureza (só vale para documento novo ou troca de tipo).</summary>
    public bool AplicaA(NaturezaPessoa natureza) => natureza switch
    {
        NaturezaPessoa.Fisica => AplicaPessoaFisica,
        NaturezaPessoa.Juridica => AplicaPessoaJuridica,
        _ => AplicaEstrangeiro
    };

    public void Desativar()
    {
        if (!Ativo) return;
        Ativo = false;
        RegistrarEvento($"Tipo de documento '{Nome}' desativado.");
    }

    public void Reativar()
    {
        if (Ativo) return;
        Ativo = true;
        RegistrarEvento($"Tipo de documento '{Nome}' reativado.");
    }
}
