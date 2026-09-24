using System.ComponentModel;
using Lone.Domain.Enums;

namespace Lone.Domain.Entidades;

/// <summary>Telefone ou e-mail da própria pessoa (os de pessoas de contato ficam no Contato).</summary>
[DisplayName("Telefone/e-mail")]
public class MeioContato : EntidadePessoaFilha
{
    [DisplayName("Tipo")]
    public TipoContato Tipo { get; set; } = TipoContato.Celular;

    /// <summary>Telefone só com dígitos (ou "+" e dígitos se internacional), ou e-mail em minúsculas.</summary>
    [DisplayName("Número/e-mail")]
    public string Valor { get; set; } = string.Empty;

    /// <summary>Ex.: "Financeiro", "NF-e", "Recepção".</summary>
    [DisplayName("Descrição")]
    public string? Descricao { get; set; }

    /// <summary>Principal do seu tipo (um telefone principal, um e-mail principal...).</summary>
    [DisplayName("Principal")]
    public bool Principal { get; set; }

    /// <summary>A pessoa autorizou receber comunicações por este meio (LGPD).</summary>
    [DisplayName("Permite comunicação")]
    public bool PermiteComunicacao { get; set; } = true;
}
