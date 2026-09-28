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

    /// <summary>Motor Comercial, Fase 1a: mais vínculos simultâneos do mesmo papel do que o "Quantos ao mesmo tempo" dele.</summary>
    public const int ErroAcimaDoLimite = 50061;

    public const string CheckPolitica = "CK_TiposCarteira_Politica";

    /// <summary>A regra do check <see cref="CheckPolitica"/> (a mesma de RegrasComercial.ValidarPapel).</summary>
    public const string RegraPolitica =
        "([LimitePorVez] IS NULL OR [LimitePorVez] BETWEEN 1 AND 99) " +
        "AND ([PercentualPadrao] IS NULL OR [PercentualPadrao] BETWEEN 0 AND 100) " +
        "AND ([ResponsavelDaConta] = 0 OR [LimitePorVez] = 1)";

    /// <summary>
    /// Versão da Etapa 4 (migração CarteiraSemSobreposicao). Congelada: é o texto daquela migração (usa a coluna
    /// "Principal", que a Fase 1a renomeia) e também o que o Down da Fase 1a recria.
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

    /// <summary>
    /// Motor Comercial, Fase 1a (migração PapeisComerciais): o mesmo gatilho, agora pela política do papel.
    /// Confere só o que cada gravação acrescenta a um vínculo (a tabela @faixas, como RegrasComercial.Crescimentos): o
    /// período todo se o vínculo é novo, foi reativado, mudou de papel ou empresa ou passou a exclusivo; senão só o trecho
    /// antecipado no início e o adiado no fim. Encurtar ou editar a observação não confere nada: uma política mais nova do
    /// papel não trava vínculos antigos, e os passos intermediários do PessoaRepositorio (só encurtam) passam.
    /// 1. Conflito par a par (erro 50060): papel de um por vez (LimitePorVez = 1, no lugar do antigo "Principal") ou
    ///    algum dos dois exclusivo, com sobreposição no trecho acrescentado.
    /// 2. Limite acima de 1 (erro 50061): em cada trecho acrescentado, o máximo de vínculos ativos simultâneos do papel
    ///    (que está no começo do trecho ou no início de algum vínculo dentro dele) não passa do limite.
    /// A mesma regra está em RegrasComercial (Validar e ValidarCarteira), que a ficha confere antes.
    /// </summary>
    public const string CriarProtecaoPorLimite = """
        SET XACT_ABORT ON;
        EXEC (N'CREATE TRIGGER TR_CarteiraClientes_SemSobreposicao ON CarteiraClientes
        AFTER INSERT, UPDATE AS
        BEGIN
            SET NOCOUNT ON;
            DECLARE @sempre date = DATEFROMPARTS(9999, 12, 31);
            DECLARE @vazio uniqueidentifier = CAST(0x0 AS uniqueidentifier);
            DECLARE @faixas TABLE (Id uniqueidentifier NOT NULL, De date NOT NULL, Ate date NOT NULL);

            -- Período todo: novo, reativado, outro papel ou empresa, ou passou a exclusivo.
            INSERT INTO @faixas (Id, De, Ate)
            SELECT i.Id, i.InicioEm, ISNULL(i.FimEm, @sempre)
            FROM inserted i
            LEFT JOIN deleted d ON d.Id = i.Id
            WHERE i.Ativo = 1
              AND (d.Id IS NULL OR d.Ativo = 0 OR d.TipoCarteiraId <> i.TipoCarteiraId
                   OR ISNULL(d.EmpresaId, @vazio) <> ISNULL(i.EmpresaId, @vazio)
                   OR (i.Exclusivo = 1 AND d.Exclusivo = 0));

            -- Mesmas regras: início antecipado.
            INSERT INTO @faixas (Id, De, Ate)
            SELECT i.Id, i.InicioEm,
                   CASE WHEN ISNULL(i.FimEm, @sempre) < DATEADD(DAY, -1, d.InicioEm) THEN ISNULL(i.FimEm, @sempre)
                        ELSE DATEADD(DAY, -1, d.InicioEm) END
            FROM inserted i
            JOIN deleted d ON d.Id = i.Id
            WHERE i.Ativo = 1 AND d.Ativo = 1 AND d.TipoCarteiraId = i.TipoCarteiraId
              AND ISNULL(d.EmpresaId, @vazio) = ISNULL(i.EmpresaId, @vazio)
              AND NOT (i.Exclusivo = 1 AND d.Exclusivo = 0)
              AND i.InicioEm < d.InicioEm;

            -- Mesmas regras: fim adiado.
            INSERT INTO @faixas (Id, De, Ate)
            SELECT i.Id,
                   CASE WHEN i.InicioEm > DATEADD(DAY, 1, d.FimEm) THEN i.InicioEm ELSE DATEADD(DAY, 1, d.FimEm) END,
                   ISNULL(i.FimEm, @sempre)
            FROM inserted i
            JOIN deleted d ON d.Id = i.Id
            WHERE i.Ativo = 1 AND d.Ativo = 1 AND d.TipoCarteiraId = i.TipoCarteiraId
              AND ISNULL(d.EmpresaId, @vazio) = ISNULL(i.EmpresaId, @vazio)
              AND NOT (i.Exclusivo = 1 AND d.Exclusivo = 0)
              AND d.FimEm IS NOT NULL AND d.FimEm < @sempre
              AND (i.FimEm IS NULL OR i.FimEm > d.FimEm);

            DELETE FROM @faixas WHERE Ate < De;
            IF NOT EXISTS (SELECT 1 FROM @faixas) RETURN;

            IF EXISTS (
                SELECT 1
                FROM @faixas f
                JOIN inserted i ON i.Id = f.Id
                JOIN TiposCarteira t ON t.Id = i.TipoCarteiraId
                JOIN CarteiraClientes c
                  ON c.PessoaId = i.PessoaId
                 AND c.Id <> i.Id
                 AND c.TipoCarteiraId = i.TipoCarteiraId
                 AND ISNULL(c.EmpresaId, @vazio) = ISNULL(i.EmpresaId, @vazio)
                 AND c.Ativo = 1
                 AND c.InicioEm <= f.Ate
                 AND f.De <= ISNULL(c.FimEm, @sempre)
                WHERE t.LimitePorVez = 1 OR c.Exclusivo = 1 OR i.Exclusivo = 1)
                THROW 50060, N''Carteira: dois vínculos ativos do mesmo papel de um por vez (ou exclusivo) no mesmo período.'', 1;

            IF EXISTS (
                SELECT 1
                FROM @faixas f
                JOIN inserted i ON i.Id = f.Id
                JOIN TiposCarteira t ON t.Id = i.TipoCarteiraId AND t.LimitePorVez > 1
                CROSS APPLY (
                    SELECT f.De AS Dia
                    UNION
                    SELECT p.InicioEm
                    FROM CarteiraClientes p
                    WHERE p.PessoaId = i.PessoaId
                      AND p.TipoCarteiraId = i.TipoCarteiraId
                      AND ISNULL(p.EmpresaId, @vazio) = ISNULL(i.EmpresaId, @vazio)
                      AND p.Ativo = 1
                      AND p.InicioEm > f.De AND p.InicioEm <= f.Ate) pontos
                WHERE t.LimitePorVez < (
                    SELECT COUNT(*)
                    FROM CarteiraClientes c
                    WHERE c.PessoaId = i.PessoaId
                      AND c.TipoCarteiraId = i.TipoCarteiraId
                      AND ISNULL(c.EmpresaId, @vazio) = ISNULL(i.EmpresaId, @vazio)
                      AND c.Ativo = 1
                      AND c.InicioEm <= pontos.Dia
                      AND ISNULL(c.FimEm, @sempre) >= pontos.Dia))
                THROW 50061, N''Carteira: mais vínculos ativos do mesmo papel ao mesmo tempo do que o limite do papel.'', 1;
        END');
        """;

    /// <summary>
    /// Motor Comercial, Fase 1a: a política de todos os papéis que já existem (os iniciais e os criados pelo usuário), a
    /// partir do que já valia. Roda logo depois de a coluna "Principal" virar "ResponsavelDaConta" e de as colunas novas
    /// entrarem (sem limite, sem crédito, não conta para metas) e antes do check CK_TiposCarteira_Politica, que exige o
    /// responsável da conta com limite 1. Como antes do Motor Comercial: o responsável da conta (o antigo principal) é um
    /// por vez e fica com o crédito; todos os papéis contam para metas (a empresa desmarca os que não devem contar).
    /// Os vínculos existentes ficam com origem "Manual" (padrão da coluna) e sem percentual (vale o do papel).
    /// </summary>
    public const string ConverterPolitica = """
        UPDATE TiposCarteira SET ContaParaMetas = 1;
        UPDATE TiposCarteira SET LimitePorVez = 1, TipoCredito = 1 WHERE ResponsavelDaConta = 1;
        """;

    /// <summary>Down: só retira o gatilho (não mexe em dados).</summary>
    public const string RemoverProtecao = """
        DROP TRIGGER IF EXISTS TR_CarteiraClientes_SemSobreposicao;
        """;
}
