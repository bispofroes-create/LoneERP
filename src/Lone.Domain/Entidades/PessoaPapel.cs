using System.ComponentModel;
using Lone.Domain.Enums;

namespace Lone.Domain.Entidades;

/// <summary>
/// Um período de um papel na pessoa (ex.: cliente de 01/2024 a 06/2025). Não é apagado: ao desmarcar, o período é
/// encerrado (Ativo = falso, Fim preenchido); ao marcar de novo, começa outro período. Só um ativo por papel.
/// </summary>
[DisplayName("Papel")]
public class PessoaPapel : EntidadePessoaFilha
{
    [DisplayName("Papel")]
    public Guid PapelId { get; set; }

    /// <summary>Cópia do papel de sistema (enum) do cadastro de papéis, usada pelas regras e consultas; nula nos papéis do usuário.</summary>
    [DisplayName("Papel de sistema")]
    public TipoPapel? Papel { get; set; }

    [DisplayName("Ativo")]
    public bool Ativo { get; set; } = true;

    [DisplayName("Início")]
    public DateOnly InicioEm { get; set; }

    [DisplayName("Fim")]
    public DateOnly? FimEm { get; set; }

    [DisplayName("Observações")]
    public string? Observacoes { get; set; }
}
