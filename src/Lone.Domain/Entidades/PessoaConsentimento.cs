using System.ComponentModel;
using Lone.Domain.Enums;

namespace Lone.Domain.Entidades;

/// <summary>
/// Consentimento da pessoa para receber comunicações por um canal (LGPD, art. 8º). Um registro por canal;
/// quem mudou e quando fica também no histórico (auditoria).
/// </summary>
[DisplayName("Consentimento")]
public class PessoaConsentimento : EntidadePessoaFilha
{
    [DisplayName("Canal")]
    public CanalComunicacao Canal { get; set; }

    [DisplayName("Autorizado")]
    public bool Concedido { get; set; }

    [DisplayName("Autorizado em")]
    public DateTime? ConcedidoEm { get; set; }

    [DisplayName("Revogado em")]
    public DateTime? RevogadoEm { get; set; }

    /// <summary>Como a autorização foi obtida (ex.: "Cadastro no balcão", "Site", "Contrato").</summary>
    [DisplayName("Origem")]
    public string? Origem { get; set; }
}
