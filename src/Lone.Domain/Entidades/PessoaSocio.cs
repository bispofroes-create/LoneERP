using System.ComponentModel;
using Lone.Domain.Auditoria;

namespace Lone.Domain.Entidades;

/// <summary>
/// Sócio ou administrador que consta na Receita Federal (quadro societário). Informativo: vem da consulta
/// de CNPJ. Vínculos com pessoas cadastradas ficam em Relacionamentos.
/// </summary>
[DisplayName("Sócio")]
public class PessoaSocio : EntidadePessoaFilha
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
}
