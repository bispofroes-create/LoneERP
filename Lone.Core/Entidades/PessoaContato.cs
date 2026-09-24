using System.ComponentModel;
using Lone.Core.Enums;

namespace Lone.Core.Entidades;

[DisplayName("Contato")]
public class PessoaContato : EntidadePessoaFilha
{
    [DisplayName("Tipo")]
    public TipoContato Tipo { get; set; } = TipoContato.Celular;

    /// <summary>Número de telefone ou endereço de e-mail.</summary>
    [DisplayName("Número/e-mail")]
    public string Valor { get; set; } = string.Empty;

    /// <summary>Ex.: "Financeiro", "Compras", "Maria (recepção)".</summary>
    [DisplayName("Descrição")]
    public string? Descricao { get; set; }

    /// <summary>Contato padrão do seu tipo (um telefone principal, um e-mail principal...).</summary>
    [DisplayName("Principal")]
    public bool Principal { get; set; }
}
