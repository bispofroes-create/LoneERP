using System.ComponentModel;
using Lone.Core.Enums;

namespace Lone.Core.Entidades;

/// <summary>Papel que a pessoa exerce. Desligar um papel só o inativa, para não perder o histórico.</summary>
[DisplayName("Papel")]
public class PessoaPapel : EntidadePessoaFilha
{
    [DisplayName("Papel")]
    public TipoPapel Papel { get; set; }

    [DisplayName("Ativo")]
    public bool Ativo { get; set; } = true;

    [DisplayName("Início")]
    public DateOnly InicioEm { get; set; }

    [DisplayName("Fim")]
    public DateOnly? FimEm { get; set; }

    [DisplayName("Observações")]
    public string? Observacoes { get; set; }
}
