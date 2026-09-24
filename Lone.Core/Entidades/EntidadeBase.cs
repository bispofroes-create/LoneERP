namespace Lone.Core.Entidades;

/// <summary>
/// Base de todas as entidades persistidas: chave e datas de auditoria.
/// As datas são preenchidas automaticamente pelo LoneDbContext.
/// </summary>
public abstract class EntidadeBase
{
    public int Id { get; set; }
    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }
}
