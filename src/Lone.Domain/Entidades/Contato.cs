using System.ComponentModel;
using Lone.Domain.Auditoria;

namespace Lone.Domain.Entidades;

/// <summary>
/// Pessoa de contato de uma empresa (ex.: "Maria — Financeiro"). Removido depois de gravado, fica inativo (histórico);
/// nunca é apagado pela gravação da ficha.
/// </summary>
[DisplayName("Pessoa de contato")]
public class Contato : EntidadePessoaFilha, IResumoAuditoria
{
    [DisplayName("Nome")]
    public string Nome { get; set; } = string.Empty;

    [DisplayName("Cargo")]
    public string? Cargo { get; set; }

    [DisplayName("Departamento")]
    public string? Departamento { get; set; }

    [DisplayName("Telefone")]
    public string? Telefone { get; set; }

    [DisplayName("Celular")]
    public string? Celular { get; set; }

    [DisplayName("Celular é WhatsApp")]
    public bool CelularWhatsApp { get; set; }

    [DisplayName("E-mail")]
    public string? Email { get; set; }

    [DisplayName("Observações")]
    public string? Observacoes { get; set; }

    [DisplayName("Principal")]
    public bool Principal { get; set; }

    /// <summary>Quando o contato também é uma pessoa cadastrada.</summary>
    [DisplayName("Pessoa vinculada")]
    public Guid? PessoaVinculadaId { get; set; }

    /// <summary>Inativo: removido da ficha, mas guardado (não é principal, não é validado nem encontrado nas buscas).</summary>
    [DisplayName("Ativo")]
    public bool Ativo { get; set; } = true;

    public string? ResumoAuditoria => string.IsNullOrWhiteSpace(Cargo) ? Nome : $"{Nome} ({Cargo})";
}
