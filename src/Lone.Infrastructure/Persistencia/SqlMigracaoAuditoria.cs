namespace Lone.Infrastructure.Persistencia;

/// <summary>
/// P0 da auditoria do cadastro (D4, aprovada em 04/10/2026, condicionada à segunda busca global): a tabela Auditoria só
/// aceita inclusão. O gatilho barra UPDATE e DELETE de linhas do histórico, venham de onde vierem (SQL à mão, rotina
/// futura). Vale só para a tabela Auditoria: não é uma proibição geral de DELETE no banco.
///
/// Uso na migração do P0 (gerada pelo usuário com Add-Migration; o snapshot ganha a declaração do gatilho, que o EF
/// precisa para não usar OUTPUT na tabela): no Up, <c>migrationBuilder.Sql(SqlMigracaoAuditoria.CriarProtecao);</c>; no
/// Down, <c>migrationBuilder.Sql(SqlMigracaoAuditoria.RemoverProtecao);</c>. Não mexe em dados (nem no Up, nem no Down).
/// TRUNCATE não dispara gatilho no SQL Server; ele exige permissão de ALTER na tabela, que a aplicação não usa.
/// </summary>
public static class SqlMigracaoAuditoria
{
    public const string Gatilho = "TR_Auditoria_SomenteInclusao";
    public const int ErroSomenteInclusao = 50080;

    /// <summary>
    /// CREATE TRIGGER num EXEC (texto fixo, sem valor de fora), como nas outras proteções: a migração fica num lote só.
    /// Um UPDATE ou DELETE que não atinge nenhuma linha passa (não há o que proteger).
    /// </summary>
    public const string CriarProtecao = """
        SET XACT_ABORT ON;
        EXEC (N'CREATE TRIGGER TR_Auditoria_SomenteInclusao ON Auditoria
        AFTER UPDATE, DELETE AS
        BEGIN
            SET NOCOUNT ON;
            IF EXISTS (SELECT 1 FROM deleted)
                THROW 50080, N''Auditoria: o histórico só aceita inclusão (UPDATE e DELETE não são permitidos).'', 1;
        END');
        """;

    /// <summary>Down: só retira o gatilho (não mexe em dados).</summary>
    public const string RemoverProtecao = """
        DROP TRIGGER IF EXISTS TR_Auditoria_SomenteInclusao;
        """;
}
