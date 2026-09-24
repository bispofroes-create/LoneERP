using Lone.Domain.Auditoria;

namespace Lone.Domain.Entidades;

/// <summary>
/// Token de renovação da sessão (o aplicativo troca por um novo token de acesso sem pedir a senha de novo).
/// Só o hash (SHA-256) é gravado. A cada renovação o token usado é revogado e substituído por outro;
/// se um token já revogado for reapresentado, toda a cadeia do usuário é revogada (possível roubo).
/// </summary>
[NaoAuditar]
public class TokenRenovacao
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }

    /// <summary>SHA-256 do token, em Base64.</summary>
    public string Hash { get; set; } = string.Empty;

    /// <summary>Estabelecimento ativo na sessão (nulo enquanto nenhuma empresa foi escolhida).</summary>
    public Guid? EstabelecimentoId { get; set; }

    public DateTime CriadoEm { get; set; }
    public DateTime ExpiraEm { get; set; }
    public DateTime? RevogadoEm { get; set; }
    public Guid? SubstituidoPorId { get; set; }

    /// <summary>Identificação do aparelho (ex.: "Windows - PC-CAIXA-01"), para o usuário ver as sessões abertas.</summary>
    public string? Dispositivo { get; set; }

    public bool Valido(DateTime agoraUtc) => RevogadoEm is null && ExpiraEm > agoraUtc;
}
