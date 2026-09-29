namespace Lone.Domain.Enums;

/// <summary>
/// Situação de um território (Fase 2b). Encerrado nunca é apagado: continua na árvore histórica (posições) e nos documentos
/// que o citam. Gravado no banco: valores novos entram no fim.
/// </summary>
public enum SituacaoTerritorio : byte
{
    /// <summary>Faz parte da árvore de hoje.</summary>
    Ativo = 0,

    /// <summary>Saiu da árvore: <c>Territorio.FimEm</c> é o último dia em que existiu.</summary>
    Encerrado = 1
}
