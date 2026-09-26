using System.ComponentModel;
using Lone.Domain.Enums;

namespace Lone.Domain.Entidades;

/// <summary>
/// Um PERÍODO de consentimento da pessoa para uma finalidade (LGPD, art. 8º), opcionalmente limitado a um canal
/// (vazio = qualquer canal compatível). Conceder cria uma linha; revogar encerra a linha (nunca é apagada nem
/// reaproveitada): concedido → revogado → concedido são duas linhas. Gravado por ações próprias (conceder/revogar),
/// nunca pelo Salvar da ficha. Os registros de antes da Fase 3 (por canal, sem finalidade) ficam ligados à finalidade
/// "Registro anterior", só para histórico.
/// </summary>
[DisplayName("Consentimento")]
public class PessoaConsentimento : EntidadePessoaFilha
{
    public const int TamanhoMaximoMotivo = 250;
    public const int TamanhoMaximoTexto = 80;
    public const int TamanhoMaximoUsuario = 100;

    [DisplayName("Finalidade")]
    public Guid FinalidadeId { get; set; }

    /// <summary>Nulo = qualquer canal compatível com a finalidade.</summary>
    [DisplayName("Canal")]
    public CanalComunicacao? Canal { get; set; }

    /// <summary>Período em vigor (concedido e não revogado). Revogar põe falso; nunca volta a verdadeiro.</summary>
    [DisplayName("Em vigor")]
    public bool Concedido { get; set; }

    /// <summary>Data do servidor. Pode ser nula só nos registros anteriores que não a tinham (não se inventa data).</summary>
    [DisplayName("Concedido em")]
    public DateTime? ConcedidoEm { get; set; }

    [DisplayName("Concedido por")]
    public string? ConcedidoPor { get; set; }

    [DisplayName("Motivo da concessão")]
    public string? Motivo { get; set; }

    /// <summary>Texto livre (ex.: "v1.0", "Política de Privacidade 2026.09"); não há cadastro de termos.</summary>
    [DisplayName("Versão do termo")]
    public string? VersaoTermo { get; set; }

    /// <summary>Como a autorização foi obtida (ex.: "Balcão", "Site", "Contrato").</summary>
    [DisplayName("Origem")]
    public string? Origem { get; set; }

    [DisplayName("Revogado em")]
    public DateTime? RevogadoEm { get; set; }

    [DisplayName("Revogado por")]
    public string? RevogadoPor { get; set; }

    [DisplayName("Motivo da revogação")]
    public string? MotivoRevogacao { get; set; }
}
