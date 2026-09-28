namespace Lone.Infrastructure.Persistencia;

/// <summary>
/// Etapa 4 (substituição de vendedor, decisão D3; aprovada pelo usuário em 28/09/2026): a regra da carteira também no
/// banco. É a mesma de RegrasComercial.Conflitam — dois vínculos ativos do mesmo tipo e da mesma empresa (nula = todas)
/// com períodos sobrepostos, quando o tipo é principal ou algum dos dois é exclusivo — para que nem uma gravação fora do
/// agregado da pessoa (SQL à mão, rotina futura) deixe dois responsáveis ao mesmo tempo.
///
/// Uso na migração (gerada pelo usuário com Add-Migration: sem operação de esquema; o snapshot só ganha a declaração do
/// gatilho, que o EF precisa para não usar OUTPUT na tabela): no Up,
/// <c>migrationBuilder.Sql(SqlMigracaoCarteira.CriarProtecao);</c>; no Down, <c>migrationBuilder.Sql(SqlMigracaoCarteira.RemoverProtecao);</c>.
/// Não mexe em dados. Não confere o que já está gravado: só barra INSERT/UPDATE daqui em diante (a validação da ficha já
/// recusa gravar uma pessoa com sobreposição). O gatilho confere cada comando; o PessoaRepositorio grava a carteira em
/// dois passos (interseção antes × depois, depois o final) para que nenhum passo intermediário pareça sobreposto.
/// </summary>
public static class SqlMigracaoCarteira
{
    public const string Gatilho = "TR_CarteiraClientes_SemSobreposicao";
    public const int ErroSobreposicao = 50060;

    /// <summary>
    /// CREATE TRIGGER num EXEC (texto fixo, sem valor de fora), como em SqlMigracaoFinalidadesEndereco: a migração fica num
    /// lote e numa transação só. O fim nulo vale como "sem fim" (31/12/9999); o dia do fim conta como vigência.
    /// </summary>
    public const string CriarProtecao = """
        SET XACT_ABORT ON;
        EXEC (N'CREATE TRIGGER TR_CarteiraClientes_SemSobreposicao ON CarteiraClientes
        AFTER INSERT, UPDATE AS
        BEGIN
            SET NOCOUNT ON;
            IF EXISTS (
                SELECT 1
                FROM inserted i
                JOIN CarteiraClientes c
                  ON c.PessoaId = i.PessoaId
                 AND c.Id <> i.Id
                 AND c.TipoCarteiraId = i.TipoCarteiraId
                 AND (c.EmpresaId = i.EmpresaId OR (c.EmpresaId IS NULL AND i.EmpresaId IS NULL))
                 AND c.Ativo = 1
                 AND c.InicioEm <= ISNULL(i.FimEm, DATEFROMPARTS(9999, 12, 31))
                 AND i.InicioEm <= ISNULL(c.FimEm, DATEFROMPARTS(9999, 12, 31))
                JOIN TiposCarteira t ON t.Id = i.TipoCarteiraId
                WHERE i.Ativo = 1 AND (t.Principal = 1 OR c.Exclusivo = 1 OR i.Exclusivo = 1))
                THROW 50060, N''Carteira: dois vínculos ativos do mesmo tipo principal ou exclusivo no mesmo período.'', 1;
        END');
        """;

    /// <summary>Down: só retira o gatilho (não mexe em dados).</summary>
    public const string RemoverProtecao = """
        DROP TRIGGER IF EXISTS TR_CarteiraClientes_SemSobreposicao;
        """;
}
