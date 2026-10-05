namespace Lone.Domain.Auditoria;

/// <summary>
/// Texto curto que identifica o registro no histórico (ex.: "Maria Souza (Compradora)"), usado na inativação e na
/// reativação. É só apresentação: a ação (inativação/reativação) é decidida apenas pela mudança de <c>Ativo</c>, e uma
/// entidade sem esta interface (ou com resumo vazio ou com falha) registra a ação do mesmo jeito.
/// </summary>
public interface IResumoAuditoria
{
    string? ResumoAuditoria { get; }
}
