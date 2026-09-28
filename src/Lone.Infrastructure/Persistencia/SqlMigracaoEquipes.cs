namespace Lone.Infrastructure.Persistencia;

/// <summary>
/// Motor Comercial, Fase 2a (decisão F3): a liderança da equipe passa a ser um papel do membro, com vigência. O líder que
/// estava gravado em <c>Equipes.LiderId</c> vira membro com papel Líder; o <c>LiderId</c> fica como cópia do líder de hoje.
///
/// Uso na migração (gerada pelo usuário com Add-Migration): no Up, logo DEPOIS de criar a coluna
/// <c>MembrosEquipe.Papel</c>, <c>migrationBuilder.Sql(SqlMigracaoEquipes.LiderComoMembro);</c>. O Down não precisa de
/// nada (a coluna sai e o <c>LiderId</c> continua como estava). Não apaga nada e pode ser lido de novo sem efeito: só
/// inclui a participação de líder de quem ainda não tem uma em aberto.
/// </summary>
public static class SqlMigracaoEquipes
{
    /// <summary>
    /// 1) O líder que já é membro em aberto passa a ter o papel Líder nessa participação.
    /// 2) O líder que não é membro em aberto ganha uma participação de líder desde a criação da equipe (ou desde o dia
    ///    seguinte à última saída dele, para não sobrepor o histórico).
    /// </summary>
    public const string LiderComoMembro = """
        UPDATE m SET Papel = 1
        FROM MembrosEquipe m
        INNER JOIN Equipes e ON e.Id = m.EquipeId
        WHERE e.LiderId IS NOT NULL AND m.PessoaId = e.LiderId AND m.FimEm IS NULL;

        INSERT INTO MembrosEquipe (Id, EquipeId, PessoaId, InicioEm, FimEm, Papel, CriadoEm)
        SELECT NEWID(), e.Id, e.LiderId,
               CASE WHEN ult.UltimaSaida IS NOT NULL AND DATEADD(day, 1, ult.UltimaSaida) > CAST(e.CriadoEm AS date)
                    THEN DATEADD(day, 1, ult.UltimaSaida)
                    ELSE CAST(e.CriadoEm AS date) END,
               NULL, 1, SYSUTCDATETIME()
        FROM Equipes e
        OUTER APPLY (SELECT MAX(x.FimEm) AS UltimaSaida FROM MembrosEquipe x WHERE x.EquipeId = e.Id AND x.PessoaId = e.LiderId) ult
        WHERE e.LiderId IS NOT NULL
          AND NOT EXISTS (SELECT 1 FROM MembrosEquipe x WHERE x.EquipeId = e.Id AND x.PessoaId = e.LiderId AND x.FimEm IS NULL);
        """;
}
