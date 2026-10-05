namespace Lone.Domain.Enums;

public enum AcaoAuditoria : byte
{
    Inclusao = 0,
    Alteracao = 1,
    Exclusao = 2,

    /// <summary>Fato de negócio descrito em texto (ex.: "Cadastro desativado. Motivo: ..."), em Descricao.</summary>
    Evento = 3,

    /// <summary>
    /// O registro continua no banco e passou de ativo para inativo (Ativo: Sim → Não). Não é exclusão: o conteúdo segue
    /// gravado. Descricao traz o resumo do registro, quando a entidade o oferece (<see cref="Auditoria.IResumoAuditoria"/>).
    /// </summary>
    Inativacao = 4,

    /// <summary>O registro inativo voltou a ser ativo (Ativo: Não → Sim), o mesmo registro (mesmo Id).</summary>
    Reativacao = 5
}
