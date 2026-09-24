namespace Lone.Domain.Enums;

public enum AcaoAuditoria : byte
{
    Inclusao = 0,
    Alteracao = 1,
    Exclusao = 2,

    /// <summary>Fato de negócio descrito em texto (ex.: "Cadastro desativado. Motivo: ..."), em Descricao.</summary>
    Evento = 3
}
