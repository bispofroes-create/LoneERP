using System.ComponentModel;
using Lone.Core.Enums;

namespace Lone.Core.Entidades;

/// <summary>
/// Bloqueio de uma pessoa (vários podem estar ativos ao mesmo tempo). Ativo enquanto FimEm for nulo.
/// Guarda quem bloqueou, quando, por quê — e quem liberou.
/// </summary>
[DisplayName("Bloqueio")]
public class Bloqueio : EntidadePessoaFilha
{
    /// <summary>Empresa do grupo em que vale. Nulo = todas.</summary>
    [DisplayName("Empresa")]
    public int? EmpresaId { get; set; }

    [DisplayName("Escopo")]
    public EscopoBloqueio Escopo { get; set; }

    [DisplayName("Origem")]
    public OrigemBloqueio Origem { get; set; }

    [DisplayName("Motivo")]
    public string Motivo { get; set; } = string.Empty;

    [DisplayName("Bloqueado em")]
    public DateTime InicioEm { get; set; }

    [DisplayName("Bloqueado por")]
    public string InicioPor { get; set; } = string.Empty;

    [DisplayName("Liberado em")]
    public DateTime? FimEm { get; set; }

    [DisplayName("Liberado por")]
    public string? FimPor { get; set; }

    [DisplayName("Motivo da liberação")]
    public string? MotivoLiberacao { get; set; }

    public bool Ativo => FimEm is null;
}
