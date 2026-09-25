using System.ComponentModel;
using Lone.Domain.Enums;

namespace Lone.Domain.Entidades;

/// <summary>
/// Telefone ou e-mail da própria pessoa. O número guarda o DDD junto (ex.: 31987654321). Não é apagado: removido na
/// ficha, fica inativo (histórico e busca por número antigo).
/// </summary>
[DisplayName("Telefone/e-mail")]
public class MeioContato : EntidadePessoaFilha
{
    /// <summary>Formato (telefone fixo, celular, e-mail, outro): define máscara e validação.</summary>
    [DisplayName("Tipo")]
    public TipoContato Tipo { get; set; } = TipoContato.Celular;

    [DisplayName("Número/e-mail")]
    public string Valor { get; set; } = string.Empty;

    /// <summary>Classificação do cadastro de tipos (Comercial, Residencial...); opcional.</summary>
    [DisplayName("Classificação")]
    public Guid? TipoMeioContatoId { get; set; }

    [DisplayName("Ramal")]
    public string? Ramal { get; set; }

    [DisplayName("WhatsApp")]
    public bool WhatsApp { get; set; }

    [DisplayName("SMS")]
    public bool Sms { get; set; }

    /// <summary>E-mail: financeiro, cobrança, NF-e, marketing.</summary>
    [DisplayName("Finalidades")]
    public FinalidadeEmail Finalidades { get; set; }

    [DisplayName("Observação")]
    public string? Descricao { get; set; }

    [DisplayName("Principal")]
    public bool Principal { get; set; }

    [DisplayName("Permite comunicação")]
    public bool PermiteComunicacao { get; set; } = true;

    [DisplayName("Ativo")]
    public bool Ativo { get; set; } = true;
}
