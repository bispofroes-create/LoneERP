namespace Lone.Infrastructure.Persistencia;

/// <summary>
/// SQL da migração que liga os papéis das pessoas (enum em PessoaPapeis.Papel) ao cadastro de papéis.
/// Os oito papéis de sistema nascem na mesma migração (dados iniciais, Ids fixos); cada período existente recebe o
/// PapelId correspondente. A coluna Papel continua (cópia do papel de sistema, usada pelas regras). Nada é apagado.
/// Se sobrar período sem papel, a migração inteira é desfeita.
/// </summary>
public static class SqlMigracaoPapeis
{
    /// <summary>Depois de criar Papeis (com os dados iniciais) e a coluna PessoaPapeis.PapelId (ainda nula).</summary>
    public const string LigarPeriodosAosPapeis = """
        SET NOCOUNT ON;

        UPDATE pp SET PapelId = p.Id
        FROM PessoaPapeis pp
        JOIN Papeis p ON p.PapelSistema = pp.Papel;

        IF EXISTS (SELECT 1 FROM PessoaPapeis WHERE PapelId IS NULL)
            THROW 50004, N'Migração de papéis: há papel de pessoa sem correspondente no cadastro de papéis. Nada foi alterado.', 1;
        """;
}
