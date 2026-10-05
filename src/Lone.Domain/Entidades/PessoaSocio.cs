using System.ComponentModel;
using Lone.Domain.Auditoria;

namespace Lone.Domain.Entidades;

/// <summary>
/// Sócio ou administrador que consta na Receita Federal (quadro societário). Informativo: vem da consulta
/// de CNPJ. Vínculos com pessoas cadastradas ficam em Relacionamentos.
/// </summary>
[DisplayName("Sócio")]
public class PessoaSocio : EntidadePessoaFilha, IResumoAuditoria
{
    [DisplayName("Nome")]
    public string Nome { get; set; } = string.Empty;

    /// <summary>Ex.: "Sócio-Administrador".</summary>
    [DisplayName("Qualificação")]
    public string? Qualificacao { get; set; }

    /// <summary>A Receita divulga o CPF do sócio só em parte (***123456**) ou o CNPJ completo, se for empresa.</summary>
    [DisplayName("Documento"), DadoSensivel]
    public string? Documento { get; set; }

    [DisplayName("Entrada na sociedade")]
    public DateOnly? EntradaEm { get; set; }

    /// <summary>Inativo: não consta mais no quadro da Receita (ex-sócio). Nunca é apagado; volta a ativo se reaparecer.</summary>
    [DisplayName("Ativo")]
    public bool Ativo { get; set; } = true;

    /// <summary>
    /// Data em que deixou de constar no quadro (estado atual): preenchida só enquanto inativo. Na volta, fica vazia; as
    /// saídas e voltas anteriores ficam no histórico (auditoria).
    /// </summary>
    [DisplayName("Saída da sociedade")]
    public DateOnly? SaiuEm { get; set; }

    public string? ResumoAuditoria => string.IsNullOrWhiteSpace(Qualificacao) ? Nome : $"{Nome} ({Qualificacao})";
}
