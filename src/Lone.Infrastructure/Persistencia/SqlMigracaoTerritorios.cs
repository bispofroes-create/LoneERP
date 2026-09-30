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

    // ------------------------------------------------------------------ Motor territorial (migração Fase2b1bMotor, Fase 2b-1b)
    //
    // Uso na migração Fase2b1bMotor: no fim do Up, nesta ordem, PreencherMotores, CriarChavesExclusivo, CriarProtecaoRegras,
    // CriarProtecaoExcecoes, CriarProtecaoAtribuicoes e CriarProtecaoItens; no começo do Down, RemoverProtecoesMotor (retira
    // só o que o Up criou por SQL; as tabelas, colunas e índices são retirados pelo EF). Todo gatilho novo lê com
    // READCOMMITTEDLOCK (o LoneERP está com READ_COMMITTED_SNAPSHOT: sem a dica, duas gravações simultâneas feitas por fora
    // não se enxergariam), com a mesma forma de comparar períodos da 2b-1a: fim nulo = em aberto, o dia do fim conta,
    // anuladas (Ativo = 0) não contam.

    public const string GatilhoRegras = "TR_RegrasTerritorio_Protecao";
    public const string GatilhoExcecoes = "TR_ExcecoesTerritorio_SemSobreposicao";
    public const string GatilhoAtribuicoes = "TR_AtribuicoesTerritorio_SemSobreposicao";
    public const string GatilhoItens = "TR_OperacaoTerritorialItens_Imutavel";
    public const int ErroRegras = 50073;
    public const int ErroExcecoes = 50074;
    public const int ErroAtribuicoes = 50075;
    public const int ErroItens = 50076;

    public const string ChaveExcecoesExclusivo = "FK_ExcecoesTerritorio_MapasTerritoriais_MapaId_Exclusivo";
    public const string ChaveAtribuicoesExclusivo = "FK_AtribuicoesTerritorio_MapasTerritoriais_MapaId_Exclusivo";

    /// <summary>
    /// Uma linha do motor (DN-02) para cada mapa que já existia (os novos nascem com a sua). Só inclui o que falta: rodar de
    /// novo não duplica nem altera nada.
    /// </summary>
    public const string PreencherMotores = """
        INSERT INTO MapaTerritorialMotor (MapaId, UltimaOperacaoId, UltimoEfeitoEm, AtualizadoEm)
        SELECT m.Id, NULL, NULL, SYSUTCDATETIME()
        FROM MapasTerritoriais m
        WHERE NOT EXISTS (SELECT 1 FROM MapaTerritorialMotor x WHERE x.MapaId = m.Id);
        """;

    /// <summary>
    /// A cópia de Exclusivo em exceções e atribuições amarrada ao mapa: FK (MapaId, Exclusivo) → MapasTerritoriais(Id,
    /// Exclusivo), sobre o índice único UX_MapasTerritoriais_Id_Exclusivo (criado pelo EF). Consequência de propósito: a
    /// exclusividade de um mapa com exceção ou atribuição gravada não muda mais (o UPDATE do mapa é recusado). Fica fora do
    /// modelo do EF porque, no EF, uma chave alternativa (Id, Exclusivo) tornaria Exclusivo imutável também nos mapas sem uso
    /// — e trocar a exclusividade de um mapa ainda sem uso é permitido desde a 2b-1a.
    /// </summary>
    public const string CriarChavesExclusivo = """
        ALTER TABLE ExcecoesTerritorio WITH CHECK ADD CONSTRAINT FK_ExcecoesTerritorio_MapasTerritoriais_MapaId_Exclusivo
            FOREIGN KEY (MapaId, Exclusivo) REFERENCES MapasTerritoriais (Id, Exclusivo);
        ALTER TABLE AtribuicoesTerritorio WITH CHECK ADD CONSTRAINT FK_AtribuicoesTerritorio_MapasTerritoriais_MapaId_Exclusivo
            FOREIGN KEY (MapaId, Exclusivo) REFERENCES MapasTerritoriais (Id, Exclusivo);
        """;

    /// <summary>
    /// Regras (50073): (1) duas versões válidas da regra do mesmo território não têm períodos que se cruzam; (2) versão
    /// publicada é imutável: só fim, anulação (Ativo), operação de encerramento e de anulação, data de alteração e rowversion
    /// podem mudar. A comparação por EXCEPT trata nulo como valor (Prioridade e mudança podem ser nulas).
    /// </summary>
    public const string CriarProtecaoRegras = """
        SET XACT_ABORT ON;
        EXEC (N'CREATE TRIGGER TR_RegrasTerritorio_Protecao ON RegrasTerritorio
        AFTER INSERT, UPDATE AS
        BEGIN
            SET NOCOUNT ON;
            IF EXISTS (SELECT 1 FROM deleted) AND EXISTS (
                SELECT i.Id, i.MapaId, i.TerritorioId, i.Numero, i.Grupos, i.Criterios, i.Prioridade, i.InicioEm, i.OperacaoId, i.OperacaoMudancaId, i.CriadoEm
                FROM inserted i
                EXCEPT
                SELECT d.Id, d.MapaId, d.TerritorioId, d.Numero, d.Grupos, d.Criterios, d.Prioridade, d.InicioEm, d.OperacaoId, d.OperacaoMudancaId, d.CriadoEm
                FROM deleted d)
                THROW 50073, N''Território: uma versão publicada da regra não pode ser alterada (só encerrada ou anulada por operação).'', 1;

            DECLARE @sempre date = DATEFROMPARTS(9999, 12, 31);
            IF EXISTS (
                SELECT 1
                FROM inserted i
                JOIN RegrasTerritorio r WITH (READCOMMITTEDLOCK)
                  ON r.TerritorioId = i.TerritorioId
                 AND r.Id <> i.Id
                 AND r.Ativo = 1
                 AND r.InicioEm <= ISNULL(i.FimEm, @sempre)
                 AND i.InicioEm <= ISNULL(r.FimEm, @sempre)
                WHERE i.Ativo = 1)
                THROW 50073, N''Território: duas versões da regra do mesmo território no mesmo período.'', 1;
        END');
        """;

    /// <summary>
    /// Exceções (50074): no mesmo mapa e para o mesmo cliente, com períodos que se cruzam, não podem existir (1) duas
    /// fixações num mapa exclusivo (o índice único só pega as abertas); (2) Fixar e Retirar do mesmo território; (3) duas
    /// exceções do mesmo tipo no mesmo território (repetição).
    /// </summary>
    public const string CriarProtecaoExcecoes = """
        SET XACT_ABORT ON;
        EXEC (N'CREATE TRIGGER TR_ExcecoesTerritorio_SemSobreposicao ON ExcecoesTerritorio
        AFTER INSERT, UPDATE AS
        BEGIN
            SET NOCOUNT ON;
            DECLARE @sempre date = DATEFROMPARTS(9999, 12, 31);
            DECLARE @cruzadas TABLE (MesmoTerritorio bit NOT NULL, MesmoTipo bit NOT NULL, Fixar bit NOT NULL, Exclusivo bit NOT NULL);
            INSERT INTO @cruzadas (MesmoTerritorio, MesmoTipo, Fixar, Exclusivo)
            SELECT CASE WHEN x.TerritorioId = i.TerritorioId THEN 1 ELSE 0 END,
                   CASE WHEN x.Tipo = i.Tipo THEN 1 ELSE 0 END,
                   CASE WHEN x.Tipo = 1 AND i.Tipo = 1 THEN 1 ELSE 0 END,
                   i.Exclusivo
            FROM inserted i
            JOIN ExcecoesTerritorio x WITH (READCOMMITTEDLOCK)
              ON x.PessoaId = i.PessoaId
             AND x.MapaId = i.MapaId
             AND x.Id <> i.Id
             AND x.Ativo = 1
             AND x.InicioEm <= ISNULL(i.FimEm, @sempre)
             AND i.InicioEm <= ISNULL(x.FimEm, @sempre)
            WHERE i.Ativo = 1;

            IF EXISTS (SELECT 1 FROM @cruzadas WHERE Fixar = 1 AND Exclusivo = 1)
                THROW 50074, N''Território: o mesmo cliente fixado em dois territórios de um mapa exclusivo no mesmo período.'', 1;
            IF EXISTS (SELECT 1 FROM @cruzadas WHERE MesmoTerritorio = 1 AND MesmoTipo = 0)
                THROW 50074, N''Território: o mesmo cliente fixado e retirado do mesmo território no mesmo período.'', 1;
            IF EXISTS (SELECT 1 FROM @cruzadas WHERE MesmoTerritorio = 1 AND MesmoTipo = 1)
                THROW 50074, N''Território: a mesma exceção repetida para o mesmo cliente e território no mesmo período.'', 1;
        END');
        """;

    /// <summary>
    /// Atribuições (50075): num mapa exclusivo, um cliente tem no máximo um território por dia; num não exclusivo, o mesmo
    /// território não aparece duas vezes para o mesmo cliente no mesmo período.
    /// </summary>
    public const string CriarProtecaoAtribuicoes = """
        SET XACT_ABORT ON;
        EXEC (N'CREATE TRIGGER TR_AtribuicoesTerritorio_SemSobreposicao ON AtribuicoesTerritorio
        AFTER INSERT, UPDATE AS
        BEGIN
            SET NOCOUNT ON;
            DECLARE @sempre date = DATEFROMPARTS(9999, 12, 31);
            IF EXISTS (
                SELECT 1
                FROM inserted i
                JOIN AtribuicoesTerritorio a WITH (READCOMMITTEDLOCK)
                  ON a.PessoaId = i.PessoaId
                 AND a.MapaId = i.MapaId
                 AND a.Id <> i.Id
                 AND a.Ativo = 1
                 AND (i.Exclusivo = 1 OR a.TerritorioId = i.TerritorioId)
                 AND a.InicioEm <= ISNULL(i.FimEm, @sempre)
                 AND i.InicioEm <= ISNULL(a.FimEm, @sempre)
                WHERE i.Ativo = 1)
                THROW 50075, N''Território: o mesmo cliente atribuído duas vezes no mesmo período (dois territórios no mapa exclusivo, ou o mesmo território repetido).'', 1;
        END');
        """;

    /// <summary>Itens aplicados (50076): gravados uma vez; nenhum UPDATE nem DELETE, venha de onde vier.</summary>
    public const string CriarProtecaoItens = """
        SET XACT_ABORT ON;
        EXEC (N'CREATE TRIGGER TR_OperacaoTerritorialItens_Imutavel ON OperacaoTerritorialItens
        AFTER UPDATE, DELETE AS
        BEGIN
            SET NOCOUNT ON;
            IF EXISTS (SELECT 1 FROM deleted)
                THROW 50076, N''Território: o resultado aplicado de uma operação não pode ser alterado nem apagado.'', 1;
        END');
        """;

    /// <summary>Down da Fase2b1bMotor: retira só o que o Up criou por SQL (gatilhos e as duas FKs de Exclusivo), antes das tabelas.</summary>
    public const string RemoverProtecoesMotor = """
        DROP TRIGGER IF EXISTS TR_OperacaoTerritorialItens_Imutavel;
        DROP TRIGGER IF EXISTS TR_AtribuicoesTerritorio_SemSobreposicao;
        DROP TRIGGER IF EXISTS TR_ExcecoesTerritorio_SemSobreposicao;
        DROP TRIGGER IF EXISTS TR_RegrasTerritorio_Protecao;
        ALTER TABLE AtribuicoesTerritorio DROP CONSTRAINT IF EXISTS FK_AtribuicoesTerritorio_MapasTerritoriais_MapaId_Exclusivo;
        ALTER TABLE ExcecoesTerritorio DROP CONSTRAINT IF EXISTS FK_ExcecoesTerritorio_MapasTerritoriais_MapaId_Exclusivo;
        """;
}
