namespace Lone.Infrastructure.Persistencia;

/// <summary>
/// Proteções dos territórios no banco. Migração Fase2b1ResponsaveisSemSobreposicao: <see cref="CriarProtecao"/> /
/// <see cref="RemoverProtecao"/>. Migração Fase2b1TravaArvore (D1 = B, 29/09/2026): no fim do Up, depois do CreateTable da
/// trava, <see cref="PreencherTravasDaArvore"/>, <see cref="CriarProtecaoArvore"/> e <see cref="CriarProtecaoPosicoes"/>;
/// no começo do Down, <see cref="RemoverProtecoesArvore"/>. Migração Fase2b1ResponsaveisConcorrencia (auditoria final,
/// 29/09/2026): <see cref="ReforcarProtecaoConcorrencia"/> / <see cref="DesfazerReforcoProtecaoConcorrencia"/>. As
/// constantes de migrações já aplicadas nunca mudam de texto (mudar uma delas mudaria o que a migração antiga executa).
///
/// Fase 2b-1a (aprovado pelo usuário em 29/09/2026): a regra de sobreposição dos responsáveis pelo território também no
/// banco, no mesmo padrão do gatilho da carteira (<see cref="SqlMigracaoCarteira"/>). É a mesma de
/// RegrasResponsavelTerritorio: dois responsáveis ativos do mesmo território, na mesma função (papel comercial) e da
/// mesma pessoa — ou da mesma equipe — não podem ter períodos que se cruzam. Fim nulo = em aberto; o dia do fim conta
/// como vigência (encostar o fim de um no início do outro no mesmo dia é sobreposição). Vale também para gravações feitas
/// direto no banco, fora da API.
///
/// Sem falso positivo: a pessoa só se compara com a mesma pessoa e a equipe só com a mesma equipe (as duas colunas nunca
/// são comparadas com nulo), e território, função e "ativo" entram na junção.
///
/// Uso na migração (gerada pelo usuário com Add-Migration; o snapshot só ganha a declaração do gatilho, que o EF precisa
/// para não usar OUTPUT na tabela): no fim do Up, <c>migrationBuilder.Sql(SqlMigracaoTerritorios.CriarProtecao);</c>; no
/// começo do Down, <c>migrationBuilder.Sql(SqlMigracaoTerritorios.RemoverProtecao);</c>. Não mexe em dados. O gatilho
/// confere cada comando; o TerritorioRepositorio grava os responsáveis alterados em dois passos (interseção antes × depois,
/// depois o final), como a carteira, para que nenhum passo intermediário pareça sobreposto.
/// </summary>
public static class SqlMigracaoTerritorios
{
    public const string Gatilho = "TR_TerritorioResponsaveis_SemSobreposicao";
    public const int ErroSobreposicao = 50070;

    /// <summary>
    /// CREATE TRIGGER num EXEC (texto fixo, sem valor de fora), como nos outros gatilhos: a migração fica num lote e numa
    /// transação só.
    /// </summary>
    public const string CriarProtecao = """
        SET XACT_ABORT ON;
        EXEC (N'CREATE TRIGGER TR_TerritorioResponsaveis_SemSobreposicao ON TerritorioResponsaveis
        AFTER INSERT, UPDATE AS
        BEGIN
            SET NOCOUNT ON;
            DECLARE @sempre date = DATEFROMPARTS(9999, 12, 31);
            IF EXISTS (
                SELECT 1
                FROM inserted i
                JOIN TerritorioResponsaveis r
                  ON r.TerritorioId = i.TerritorioId
                 AND r.Id <> i.Id
                 AND r.TipoCarteiraId = i.TipoCarteiraId
                 AND r.Ativo = 1
                 AND ((i.PessoaId IS NOT NULL AND r.PessoaId = i.PessoaId)
                      OR (i.EquipeId IS NOT NULL AND r.EquipeId = i.EquipeId))
                 AND r.InicioEm <= ISNULL(i.FimEm, @sempre)
                 AND i.InicioEm <= ISNULL(r.FimEm, @sempre)
                WHERE i.Ativo = 1)
                THROW 50070, N''Território: a mesma pessoa (ou equipe) aparece duas vezes na mesma função do mesmo território no mesmo período.'', 1;
        END');
        """;

    /// <summary>Down: só retira o gatilho (não mexe em dados).</summary>
    public const string RemoverProtecao = """
        DROP TRIGGER IF EXISTS TR_TerritorioResponsaveis_SemSobreposicao;
        """;

    // ------------------------------------------------------------------ Árvore e posições (migração Fase2b1TravaArvore)

    public const string GatilhoArvore = "TR_Territorios_Arvore";
    public const string GatilhoPosicoes = "TR_TerritorioPosicoes_SemSobreposicao";
    public const int ErroArvore = 50071;
    public const int ErroPosicoes = 50072;

    /// <summary>
    /// Uma trava da árvore para cada mapa que já existia (os novos nascem com a sua). Só inclui o que falta: rodar de novo
    /// não duplica nem altera nada.
    /// </summary>
    public const string PreencherTravasDaArvore = """
        INSERT INTO MapaTerritorialArvores (MapaId, AtualizadoEm)
        SELECT m.Id, SYSUTCDATETIME()
        FROM MapasTerritoriais m
        WHERE NOT EXISTS (SELECT 1 FROM MapaTerritorialArvores a WHERE a.MapaId = m.Id);
        """;

    /// <summary>
    /// A árvore também no banco (a mesma regra de RegrasArvoreTerritorial.ValidarPai): nenhum território fica abaixo dele
    /// mesmo nem de um de baixo (ciclo) e a árvore não passa de 12 níveis (raiz = 1; conta ativos e encerrados, como o
    /// domínio). Só roda quando o pai muda ou há inclusão; confere apenas os territórios gravados no comando (subindo até
    /// a raiz e descendo até as folhas deles), para qualquer quantidade de linhas. Os dois percursos têm limite de passos,
    /// então nem uma árvore já corrompida prende o gatilho. As leituras usam READCOMMITTEDLOCK: mesmo num banco com
    /// READ_COMMITTED_SNAPSHOT, duas gravações simultâneas feitas por fora (A→B e B→A) se enxergam e uma é barrada. Os
    /// percursos levam o MapaId para usar o índice (MapaId, PaiId).
    /// </summary>
    public const string CriarProtecaoArvore = """
        SET XACT_ABORT ON;
        EXEC (N'CREATE TRIGGER TR_Territorios_Arvore ON Territorios
        AFTER INSERT, UPDATE AS
        BEGIN
            SET NOCOUNT ON;
            IF NOT UPDATE(PaiId) RETURN;

            DECLARE @acima TABLE (Origem uniqueidentifier PRIMARY KEY, Acima int NOT NULL, Ciclo int NOT NULL);
            WITH Subida AS (
                SELECT i.Id AS Origem, i.PaiId AS Atual, 1 AS Passos
                FROM inserted i
                WHERE i.PaiId IS NOT NULL
                UNION ALL
                SELECT s.Origem, t.PaiId, s.Passos + 1
                FROM Subida s
                JOIN Territorios t WITH (READCOMMITTEDLOCK) ON t.Id = s.Atual
                WHERE t.PaiId IS NOT NULL AND s.Atual <> s.Origem AND s.Passos <= 12
            )
            INSERT INTO @acima (Origem, Acima, Ciclo)
            SELECT Origem, MAX(Passos), MAX(CASE WHEN Atual = Origem THEN 1 ELSE 0 END)
            FROM Subida
            GROUP BY Origem;

            IF EXISTS (SELECT 1 FROM @acima WHERE Ciclo = 1)
                THROW 50071, N''Território: a árvore formaria um ciclo (um território abaixo dele mesmo ou de um território que está abaixo dele).'', 1;

            DECLARE @abaixo TABLE (Origem uniqueidentifier PRIMARY KEY, Abaixo int NOT NULL);
            WITH Descida AS (
                SELECT i.Id AS Origem, i.MapaId, t.Id AS Atual, 1 AS Passos
                FROM inserted i
                JOIN Territorios t WITH (READCOMMITTEDLOCK) ON t.MapaId = i.MapaId AND t.PaiId = i.Id
                UNION ALL
                SELECT d.Origem, d.MapaId, t.Id, d.Passos + 1
                FROM Descida d
                JOIN Territorios t WITH (READCOMMITTEDLOCK) ON t.MapaId = d.MapaId AND t.PaiId = d.Atual
                WHERE t.Id <> d.Origem AND d.Passos <= 12
            )
            INSERT INTO @abaixo (Origem, Abaixo)
            SELECT Origem, MAX(Passos)
            FROM Descida
            GROUP BY Origem;

            IF EXISTS (
                SELECT 1
                FROM inserted i
                LEFT JOIN @acima a ON a.Origem = i.Id
                LEFT JOIN @abaixo b ON b.Origem = i.Id
                WHERE ISNULL(a.Acima, 0) + 1 + ISNULL(b.Abaixo, 0) > 12)
                THROW 50071, N''Território: a árvore passaria de 12 níveis.'', 1;
        END');
        """;

    /// <summary>
    /// Posições válidas do mesmo território não se cruzam (início e fim contam como dias de vigência; fim nulo = em aberto;
    /// anuladas não contam) — o índice único já garante uma aberta só. Mesmo desenho do gatilho dos responsáveis.
    /// </summary>
    public const string CriarProtecaoPosicoes = """
        SET XACT_ABORT ON;
        EXEC (N'CREATE TRIGGER TR_TerritorioPosicoes_SemSobreposicao ON TerritorioPosicoes
        AFTER INSERT, UPDATE AS
        BEGIN
            SET NOCOUNT ON;
            DECLARE @sempre date = DATEFROMPARTS(9999, 12, 31);
            IF EXISTS (
                SELECT 1
                FROM inserted i
                JOIN TerritorioPosicoes p WITH (READCOMMITTEDLOCK)
                  ON p.MapaId = i.MapaId
                 AND p.TerritorioId = i.TerritorioId
                 AND p.Id <> i.Id
                 AND p.Ativo = 1
                 AND p.InicioEm <= ISNULL(i.FimEm, @sempre)
                 AND i.InicioEm <= ISNULL(p.FimEm, @sempre)
                WHERE i.Ativo = 1)
                THROW 50072, N''Território: duas posições na árvore no mesmo período.'', 1;
        END');
        """;

    /// <summary>Down da Fase2b1TravaArvore: só retira os dois gatilhos (a tabela da trava é retirada pelo EF).</summary>
    public const string RemoverProtecoesArvore = """
        DROP TRIGGER IF EXISTS TR_TerritorioPosicoes_SemSobreposicao;
        DROP TRIGGER IF EXISTS TR_Territorios_Arvore;
        """;

    // ------------------------------------------------------------------ Responsáveis sob concorrência (migração Fase2b1ResponsaveisConcorrencia)

    /// <summary>
    /// Auditoria final da 2b-1a (29/09/2026): o gatilho 50070 passa a ler a tabela com READCOMMITTEDLOCK, como os gatilhos
    /// da árvore e das posições. Sem a dica, num banco com READ_COMMITTED_SNAPSHOT ligado (ou numa transação SNAPSHOT) a
    /// leitura do gatilho enxerga só a última versão CONFIRMADA: duas gravações simultâneas feitas por fora da API, cada uma
    /// ainda não confirmada, não se veem, os dois gatilhos passam e as duas confirmam — sobreposição gravada (demonstrado em
    /// BancoTerritoriosRcsiTests). Com a dica, a leitura do gatilho respeita as travas: a segunda gravação espera a primeira
    /// terminar, passa a vê-la e é barrada. Mesma regra, mesma junção, mesma mensagem e mesmo número: só muda a forma de ler.
    /// CREATE OR ALTER (SQL Server 2016 SP1 ou mais novo) troca o gatilho sem janela em que a tabela fique sem proteção.
    /// </summary>
    public const string ReforcarProtecaoConcorrencia = """
        SET XACT_ABORT ON;
        EXEC (N'CREATE OR ALTER TRIGGER TR_TerritorioResponsaveis_SemSobreposicao ON TerritorioResponsaveis
        AFTER INSERT, UPDATE AS
        BEGIN
            SET NOCOUNT ON;
            DECLARE @sempre date = DATEFROMPARTS(9999, 12, 31);
            IF EXISTS (
                SELECT 1
                FROM inserted i
                JOIN TerritorioResponsaveis r WITH (READCOMMITTEDLOCK)
                  ON r.TerritorioId = i.TerritorioId
                 AND r.Id <> i.Id
                 AND r.TipoCarteiraId = i.TipoCarteiraId
                 AND r.Ativo = 1
                 AND ((i.PessoaId IS NOT NULL AND r.PessoaId = i.PessoaId)
                      OR (i.EquipeId IS NOT NULL AND r.EquipeId = i.EquipeId))
                 AND r.InicioEm <= ISNULL(i.FimEm, @sempre)
                 AND i.InicioEm <= ISNULL(r.FimEm, @sempre)
                WHERE i.Ativo = 1)
                THROW 50070, N''Território: a mesma pessoa (ou equipe) aparece duas vezes na mesma função do mesmo território no mesmo período.'', 1;
        END');
        """;

    /// <summary>
    /// Down da Fase2b1ResponsaveisConcorrencia: devolve o gatilho EXATAMENTE como a Fase2b1ResponsaveisSemSobreposicao o
    /// criou (o mesmo texto de <see cref="CriarProtecao"/>, trocando só CREATE por CREATE OR ALTER). Não apaga o gatilho:
    /// ele pertence à migração anterior.
    /// </summary>
    public static string DesfazerReforcoProtecaoConcorrencia =>
        CriarProtecao.Replace("EXEC (N'CREATE TRIGGER ", "EXEC (N'CREATE OR ALTER TRIGGER ", StringComparison.Ordinal);
}
