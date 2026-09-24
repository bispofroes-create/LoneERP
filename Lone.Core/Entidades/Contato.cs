using System.ComponentModel;

namespace Lone.Core.Entidades;

/// <summary>Pessoa de contato de uma empresa (ex.: "Maria — Financeiro").</summary>
[DisplayName("Pessoa de contato")]
public class Contato : EntidadePessoaFilha
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
    public int? PessoaVinculadaId { get; set; }
}
