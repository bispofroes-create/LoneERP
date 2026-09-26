namespace Lone.Infrastructure.Persistencia;

/// <summary>
/// Endereço × finalidade: SQL da migração (colocado pela ferramenta Ferramentas/inserir-sql-migracao.py) e nomes das
/// proteções do banco usados também pelo modelo do EF e pela tradução de erros da gravação.
///
/// <see cref="MigrarFinalidades"/> (dados, depois de criar FinalidadesEndereco com os dados iniciais, a tabela
/// PessoaEnderecoFinalidades e as colunas novas; antes dos índices únicos). Nada é apagado; a coluna antiga
/// PessoaEnderecos.Finalidades fica (legada). Regras, por pessoa e finalidade:
///   1. o endereço ATIVO que era o único "principal" antigo (bit 1) e tem a finalidade vira o principal dela;
///   2. senão, se exatamente um endereço ativo tem a finalidade, ele vira o principal;
///   3. 2+ endereços ativos com a finalidade e nenhum era o antigo principal: relações sem principal + revisão;
///   4. antigo principal sem nenhuma finalidade: nenhuma é inventada + revisão (motivo no endereço);
///   5. dois ou mais antigos principais ativos: a regra 1 não se aplica (nada é escolhido) + revisão (motivo no endereço);
///   6. endereço inativo: relações criadas como histórico, nunca principal.
/// Nada é escolhido por menor Id, ordem ou data. As conferências no fim são INDEPENDENTES do INSERT (recalculam o
/// esperado pelos CÓDIGOS das finalidades, não pelos Ids usados para inserir) e desfazem tudo (THROW dentro da
/// transação da migração) se algo não conferir.
///
/// <see cref="CriarProtecoes"/> (fim do Up): gatilhos que o CHECK não consegue expressar. <see cref="RemoverProtecoes"/>
/// (início do Down): retira os gatilhos (não mexe em dados).
/// </summary>
public static class SqlMigracaoFinalidadesEndereco
{
    // Nomes fixos (o modelo do EF declara os mesmos; a gravação traduz a violação de cada um).
    public const string IndiceFinalidadeAtiva = "IX_PessoaEnderecoFinalidades_PessoaEnderecoId_FinalidadeId";
    public const string IndicePrincipal = "IX_PessoaEnderecoFinalidades_PessoaId_FinalidadeId";
    public const string CheckConsolidado = "CK_PessoaEnderecos_Consolidado";
    public const string CheckSistemaAtiva = "CK_FinalidadesEndereco_SistemaAtiva";
    public const string GatilhoPrincipalEnderecoAtivo = "TR_PessoaEnderecoFinalidades_PrincipalEnderecoAtivo";
    public const string GatilhoEnderecoInativo = "TR_PessoaEnderecos_InativoSemPrincipal";
    public const string GatilhoFinalidadeSistema = "TR_FinalidadesEndereco_ProtegerSistema";

    // Números dos erros lançados pelos gatilhos (THROW).
    public const int ErroPrincipalEmEnderecoInativo = 50040;
    public const int ErroEnderecoInativoComPrincipal = 50041;
    public const int ErroFinalidadeSistema = 50042;
    public const int ErroCodigoFinalidade = 50043;

    public const string MigrarFinalidades = """
        SET NOCOUNT ON;

        -- Pré-condições: nada migrado antes; as seis finalidades de sistema existem.
        IF EXISTS (SELECT 1 FROM PessoaEnderecoFinalidades)
            THROW 50020, N'Migração das finalidades de endereço: a tabela de relações já tem dados. Nada foi alterado.', 1;
        IF (SELECT COUNT(*) FROM FinalidadesEndereco WHERE DoSistema = 1 AND Codigo IN
               ('COMERCIAL', 'RESIDENCIAL', 'FISCAL', 'ENTREGA', 'COBRANCA', 'CORRESPONDENCIA')) <> 6
            THROW 50021, N'Migração das finalidades de endereço: finalidades de sistema ausentes. Nada foi alterado.', 1;

        -- Bit legado -> Id estável (usado só para INSERIR; as conferências usam o Código).
        DECLARE @Bits TABLE (Bit smallint NOT NULL, FinalidadeId uniqueidentifier NOT NULL);
        INSERT INTO @Bits (Bit, FinalidadeId) VALUES
            (64, '7a9e1c07-0000-0000-0000-000000000001'),  -- Comercial
            (32, '7a9e1c07-0000-0000-0000-000000000002'),  -- Residencial
            (2,  '7a9e1c07-0000-0000-0000-000000000003'),  -- Fiscal
            (8,  '7a9e1c07-0000-0000-0000-000000000004'),  -- Entrega
            (4,  '7a9e1c07-0000-0000-0000-000000000005'),  -- Cobrança
            (16, '7a9e1c07-0000-0000-0000-000000000006');  -- Correspondência

        -- Uma relação por bit de finalidade ligado (o bit 1, "Principal", não vira finalidade), ativa, sem principal.
        INSERT INTO PessoaEnderecoFinalidades (Id, PessoaId, PessoaEnderecoId, FinalidadeId, Principal, Ativo, CriadoEm)
        SELECT NEWID(), e.PessoaId, e.Id, b.FinalidadeId, 0, 1, SYSUTCDATETIME()
        FROM PessoaEnderecos e JOIN @Bits b ON (e.Finalidades & b.Bit) <> 0;

        -- Pessoas com mais de um antigo principal ATIVO (inconsistência: não se escolhe nenhum).
        DECLARE @PrincipalRepetido TABLE (PessoaId uniqueidentifier PRIMARY KEY);
        INSERT INTO @PrincipalRepetido (PessoaId)
        SELECT PessoaId FROM PessoaEnderecos WHERE Ativo = 1 AND (Finalidades & 1) <> 0 GROUP BY PessoaId HAVING COUNT(*) > 1;

        -- Regra 1: o antigo principal (único e ativo) é o principal de cada finalidade que ele tem.
        UPDATE u SET Principal = 1
        FROM PessoaEnderecoFinalidades u
        JOIN PessoaEnderecos e ON e.Id = u.PessoaEnderecoId AND e.PessoaId = u.PessoaId
        WHERE e.Ativo = 1 AND (e.Finalidades & 1) <> 0
          AND NOT EXISTS (SELECT 1 FROM @PrincipalRepetido r WHERE r.PessoaId = u.PessoaId);

        -- Regra 2: finalidade em exatamente um endereço ativo, ainda sem principal.
        UPDATE u SET Principal = 1
        FROM PessoaEnderecoFinalidades u
        JOIN PessoaEnderecos e ON e.Id = u.PessoaEnderecoId AND e.PessoaId = u.PessoaId
        WHERE e.Ativo = 1 AND u.Principal = 0
          AND NOT EXISTS (
              SELECT 1 FROM PessoaEnderecoFinalidades u2
              JOIN PessoaEnderecos e2 ON e2.Id = u2.PessoaEnderecoId AND e2.PessoaId = u2.PessoaId
              WHERE u2.PessoaId = u.PessoaId AND u2.FinalidadeId = u.FinalidadeId AND u2.Id <> u.Id AND e2.Ativo = 1);

        -- Regras 4 e 5: motivo gravado no endereço (explica a revisão).
        UPDATE PessoaEnderecos SET RevisaoMigracao = RevisaoMigracao | 1
        WHERE Ativo = 1 AND (Finalidades & 1) <> 0 AND (Finalidades & 126) = 0;
        UPDATE e SET RevisaoMigracao = e.RevisaoMigracao | 2
        FROM PessoaEnderecos e JOIN @PrincipalRepetido r ON r.PessoaId = e.PessoaId
        WHERE e.Ativo = 1 AND (e.Finalidades & 1) <> 0;

        -- Regras 3, 4 e 5: marca geral da pessoa.
        UPDATE Pessoas SET RevisarFinalidadesEndereco = 1
        WHERE Id IN (
            SELECT u.PessoaId
            FROM PessoaEnderecoFinalidades u
            JOIN PessoaEnderecos e ON e.Id = u.PessoaEnderecoId AND e.PessoaId = u.PessoaId
            WHERE e.Ativo = 1
            GROUP BY u.PessoaId, u.FinalidadeId
            HAVING COUNT(*) > 1 AND SUM(CAST(u.Principal AS int)) = 0
            UNION
            SELECT PessoaId FROM PessoaEnderecos WHERE RevisaoMigracao <> 0);

        -- ======================= Conferências independentes (qualquer falha desfaz tudo) =======================

        -- Esperado, recalculado pelos CÓDIGOS (outro caminho que o do INSERT, que usou os Ids).
        DECLARE @Esperado TABLE (
            PessoaId uniqueidentifier NOT NULL, EnderecoId uniqueidentifier NOT NULL, EnderecoAtivo bit NOT NULL,
            AntigoPrincipal bit NOT NULL, FinalidadeId uniqueidentifier NOT NULL, PRIMARY KEY (EnderecoId, FinalidadeId));
        INSERT INTO @Esperado (PessoaId, EnderecoId, EnderecoAtivo, AntigoPrincipal, FinalidadeId)
        SELECT e.PessoaId, e.Id, e.Ativo, CASE WHEN (e.Finalidades & 1) <> 0 THEN 1 ELSE 0 END, f.Id
        FROM PessoaEnderecos e
        CROSS APPLY (VALUES ('COMERCIAL', 64), ('RESIDENCIAL', 32), ('FISCAL', 2), ('ENTREGA', 8), ('COBRANCA', 4),
                            ('CORRESPONDENCIA', 16)) AS m (Codigo, Bit)
        JOIN FinalidadesEndereco f ON f.Codigo = m.Codigo
        WHERE (e.Finalidades & m.Bit) <> 0;

        -- Total: soma aritmética dos bits de finalidade (sem o bit 1) = relações criadas.
        DECLARE @TotalBits int = (SELECT ISNULL(SUM(
              CASE WHEN (Finalidades & 2) <> 0 THEN 1 ELSE 0 END + CASE WHEN (Finalidades & 4) <> 0 THEN 1 ELSE 0 END
            + CASE WHEN (Finalidades & 8) <> 0 THEN 1 ELSE 0 END + CASE WHEN (Finalidades & 16) <> 0 THEN 1 ELSE 0 END
            + CASE WHEN (Finalidades & 32) <> 0 THEN 1 ELSE 0 END + CASE WHEN (Finalidades & 64) <> 0 THEN 1 ELSE 0 END), 0)
            FROM PessoaEnderecos);
        IF @TotalBits <> (SELECT COUNT(*) FROM PessoaEnderecoFinalidades)
            THROW 50022, N'Migração das finalidades de endereço: o total de relações não confere com os bits antigos. Nada foi alterado.', 1;

        -- Cada relação criada é exatamente uma finalidade que estava nos bits daquele endereço (nos dois sentidos).
        IF EXISTS (SELECT PessoaEnderecoId, FinalidadeId FROM PessoaEnderecoFinalidades
                   EXCEPT SELECT EnderecoId, FinalidadeId FROM @Esperado)
           OR EXISTS (SELECT EnderecoId, FinalidadeId FROM @Esperado
                      EXCEPT SELECT PessoaEnderecoId, FinalidadeId FROM PessoaEnderecoFinalidades)
            THROW 50023, N'Migração das finalidades de endereço: relações diferentes dos bits antigos. Nada foi alterado.', 1;

        -- Relação e endereço da mesma pessoa.
        IF EXISTS (SELECT 1 FROM PessoaEnderecoFinalidades u JOIN PessoaEnderecos e ON e.Id = u.PessoaEnderecoId WHERE e.PessoaId <> u.PessoaId)
            THROW 50024, N'Migração das finalidades de endereço: relação com pessoa diferente do endereço. Nada foi alterado.', 1;

        -- Nenhuma finalidade repetida ativa no mesmo endereço.
        IF EXISTS (SELECT 1 FROM PessoaEnderecoFinalidades WHERE Ativo = 1 GROUP BY PessoaEnderecoId, FinalidadeId HAVING COUNT(*) > 1)
            THROW 50025, N'Migração das finalidades de endereço: finalidade repetida no mesmo endereço. Nada foi alterado.', 1;

        -- No máximo um principal por pessoa + finalidade.
        IF EXISTS (SELECT 1 FROM PessoaEnderecoFinalidades WHERE Principal = 1 GROUP BY PessoaId, FinalidadeId HAVING COUNT(*) > 1)
            THROW 50026, N'Migração das finalidades de endereço: dois principais na mesma finalidade. Nada foi alterado.', 1;

        -- Endereço inativo (ou relação inativa) nunca é principal.
        IF EXISTS (SELECT 1 FROM PessoaEnderecoFinalidades u JOIN PessoaEnderecos e ON e.Id = u.PessoaEnderecoId
                   WHERE u.Principal = 1 AND (e.Ativo = 0 OR u.Ativo = 0))
            THROW 50027, N'Migração das finalidades de endereço: endereço inativo marcado como principal. Nada foi alterado.', 1;

        -- Toda finalidade usada existe no cadastro.
        IF EXISTS (SELECT 1 FROM PessoaEnderecoFinalidades u LEFT JOIN FinalidadesEndereco f ON f.Id = u.FinalidadeId WHERE f.Id IS NULL)
            THROW 50028, N'Migração das finalidades de endereço: finalidade inexistente. Nada foi alterado.', 1;

        -- Principal de cada pessoa + finalidade = o que as regras mandam (recalculado de forma declarativa sobre o
        -- esperado): antigo principal único com a finalidade; senão o único endereço ativo; senão NENHUM (ambiguidade).
        DECLARE @Grupos TABLE (PessoaId uniqueidentifier NOT NULL, FinalidadeId uniqueidentifier NOT NULL,
                               Ativos int NOT NULL, Esperado char(36) NULL, Real char(36) NULL);
        INSERT INTO @Grupos (PessoaId, FinalidadeId, Ativos, Esperado, Real)
        SELECT g.PessoaId, g.FinalidadeId, g.Ativos,
               COALESCE(g.DoAntigoPrincipal, CASE WHEN g.Ativos = 1 THEN g.UnicoAtivo END),
               (SELECT CAST(u.PessoaEnderecoId AS char(36)) FROM PessoaEnderecoFinalidades u
                WHERE u.PessoaId = g.PessoaId AND u.FinalidadeId = g.FinalidadeId AND u.Principal = 1)
        FROM (
            SELECT x.PessoaId, x.FinalidadeId,
                   SUM(CASE WHEN x.EnderecoAtivo = 1 THEN 1 ELSE 0 END) AS Ativos,
                   MAX(CASE WHEN x.EnderecoAtivo = 1 THEN CAST(x.EnderecoId AS char(36)) END) AS UnicoAtivo,
                   MAX(CASE WHEN x.EnderecoAtivo = 1 AND x.AntigoPrincipal = 1 AND a.Quantos = 1
                            THEN CAST(x.EnderecoId AS char(36)) END) AS DoAntigoPrincipal
            FROM @Esperado x
            CROSS APPLY (SELECT COUNT(*) AS Quantos FROM PessoaEnderecos p
                         WHERE p.PessoaId = x.PessoaId AND p.Ativo = 1 AND (p.Finalidades & 1) <> 0) a
            GROUP BY x.PessoaId, x.FinalidadeId) g;
        IF EXISTS (SELECT 1 FROM @Grupos WHERE ISNULL(Esperado, '') <> ISNULL(Real, ''))
            THROW 50029, N'Migração das finalidades de endereço: principal diferente do que as regras mandam (nenhuma ambiguidade pode ser resolvida por escolha). Nada foi alterado.', 1;

        -- Motivos por endereço: exatamente os casos 4 e 5.
        IF EXISTS (SELECT 1 FROM PessoaEnderecos e
                   WHERE e.RevisaoMigracao <>
                         CASE WHEN e.Ativo = 1 AND (e.Finalidades & 1) <> 0 AND (e.Finalidades & 126) = 0 THEN 1 ELSE 0 END
                       + CASE WHEN e.Ativo = 1 AND (e.Finalidades & 1) <> 0
                                   AND (SELECT COUNT(*) FROM PessoaEnderecos p WHERE p.PessoaId = e.PessoaId AND p.Ativo = 1 AND (p.Finalidades & 1) <> 0) > 1
                              THEN 2 ELSE 0 END)
            THROW 50030, N'Migração das finalidades de endereço: motivos de revisão dos endereços não conferem. Nada foi alterado.', 1;

        -- Marca de revisão: ligada SÓ quando necessária (ambiguidade sem principal ou motivo em algum endereço).
        IF EXISTS (SELECT 1 FROM Pessoas p
                   WHERE p.RevisarFinalidadesEndereco <>
                         CASE WHEN EXISTS (SELECT 1 FROM @Grupos g WHERE g.PessoaId = p.Id AND g.Ativos > 1 AND g.Esperado IS NULL)
                                OR EXISTS (SELECT 1 FROM PessoaEnderecos e WHERE e.PessoaId = p.Id AND e.RevisaoMigracao <> 0)
                              THEN 1 ELSE 0 END)
            THROW 50031, N'Migração das finalidades de endereço: marca de revisão das pessoas não confere. Nada foi alterado.', 1;
        """;

    /// <summary>Gatilhos das regras que um CHECK não expressa (cruzam tabelas ou comparam antes/depois).</summary>
    public const string CriarProtecoes = """
        CREATE TRIGGER TR_PessoaEnderecoFinalidades_PrincipalEnderecoAtivo ON PessoaEnderecoFinalidades
        AFTER INSERT, UPDATE AS
        BEGIN
            SET NOCOUNT ON;
            IF EXISTS (SELECT 1 FROM inserted i JOIN PessoaEnderecos e ON e.Id = i.PessoaEnderecoId
                       WHERE i.Principal = 1 AND e.Ativo = 0)
                THROW 50040, N'Endereço inativo não pode ser principal de uma finalidade.', 1;
        END
        GO
        CREATE TRIGGER TR_PessoaEnderecos_InativoSemPrincipal ON PessoaEnderecos
        AFTER UPDATE AS
        BEGIN
            SET NOCOUNT ON;
            IF UPDATE(Ativo) AND EXISTS (SELECT 1 FROM inserted e JOIN PessoaEnderecoFinalidades u ON u.PessoaEnderecoId = e.Id
                                         WHERE e.Ativo = 0 AND u.Principal = 1)
                THROW 50041, N'Endereço inativo não pode continuar como principal de uma finalidade.', 1;
        END
        GO
        CREATE TRIGGER TR_FinalidadesEndereco_ProtegerSistema ON FinalidadesEndereco
        AFTER UPDATE, DELETE AS
        BEGIN
            SET NOCOUNT ON;
            IF EXISTS (SELECT 1 FROM deleted d WHERE d.DoSistema = 1 AND NOT EXISTS (
                           SELECT 1 FROM inserted i WHERE i.Id = d.Id AND i.DoSistema = 1
                             AND i.Codigo COLLATE Latin1_General_BIN2 = d.Codigo COLLATE Latin1_General_BIN2))
                THROW 50042, N'Finalidade de endereço de sistema: não muda de código, continua de sistema e não é excluída.', 1;
            IF EXISTS (SELECT 1 FROM deleted d JOIN inserted i ON i.Id = d.Id
                       WHERE i.Codigo COLLATE Latin1_General_BIN2 <> d.Codigo COLLATE Latin1_General_BIN2)
                THROW 50043, N'O código de uma finalidade de endereço não pode ser alterado.', 1;
        END
        """;

    /// <summary>Down: retira os gatilhos antes de o EF desfazer tabelas e colunas (não mexe em dados).</summary>
    public const string RemoverProtecoes = """
        DROP TRIGGER IF EXISTS TR_FinalidadesEndereco_ProtegerSistema;
        DROP TRIGGER IF EXISTS TR_PessoaEnderecos_InativoSemPrincipal;
        DROP TRIGGER IF EXISTS TR_PessoaEnderecoFinalidades_PrincipalEnderecoAtivo;
        """;
}
