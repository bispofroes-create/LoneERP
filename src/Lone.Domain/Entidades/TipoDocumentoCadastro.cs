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
