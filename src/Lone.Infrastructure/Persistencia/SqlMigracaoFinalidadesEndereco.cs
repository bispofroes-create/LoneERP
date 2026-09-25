namespace Lone.Infrastructure.Persistencia;

/// <summary>
/// Endereço × finalidade: SQL de dados da migração (colocado à mão, pela ferramenta Ferramentas/inserir-sql-migracao.py,
/// depois de criar FinalidadesEndereco com os dados iniciais, a tabela PessoaEnderecoFinalidades e a coluna
/// Pessoas.RevisarFinalidadesEndereco). Nada é apagado; a coluna antiga PessoaEnderecos.Finalidades fica (legada).
/// Confere as contagens e desfaz tudo (THROW dentro da transação da migração) se não conferirem.
///
/// Regras do antigo bit Principal (valor 1), por pessoa e finalidade nos endereços ATIVOS:
///   1. o endereço que tinha o bit Principal e tem a finalidade vira o principal dela (se houver um só assim);
///   2. senão, se um só endereço ativo tem a finalidade, ele vira o principal;
///   3. 2+ endereços com a finalidade e nenhum era o antigo principal: relações sem principal + pessoa para revisão;
///   4. antigo principal sem nenhuma finalidade: nenhuma é inventada + pessoa para revisão;
///   5. dois bits Principal ativos na mesma pessoa (inconsistência): a regra 1 não se aplica + pessoa para revisão.
/// Endereços inativos: relações criadas como histórico, sem principal. "Principal" não vira finalidade.
/// </summary>
public static class SqlMigracaoFinalidadesEndereco
{
    public const string MigrarFinalidades = """
        SET NOCOUNT ON;
        DECLARE @Bits TABLE (Bit smallint NOT NULL, FinalidadeId uniqueidentifier NOT NULL);
        INSERT INTO @Bits (Bit, FinalidadeId) VALUES
            (64, '7a9e1c07-0000-0000-0000-000000000001'),  -- Comercial
            (32, '7a9e1c07-0000-0000-0000-000000000002'),  -- Residencial
            (2,  '7a9e1c07-0000-0000-0000-000000000003'),  -- Fiscal
            (8,  '7a9e1c07-0000-0000-0000-000000000004'),  -- Entrega
            (4,  '7a9e1c07-0000-0000-0000-000000000005'),  -- Cobrança
            (16, '7a9e1c07-0000-0000-0000-000000000006');  -- Correspondência

        -- Uma relação por bit ligado (o bit 1, "Principal", não vira finalidade).
        DECLARE @Esperadas int = (SELECT COUNT(*) FROM PessoaEnderecos e JOIN @Bits b ON (e.Finalidades & b.Bit) <> 0);
        INSERT INTO PessoaEnderecoFinalidades (Id, PessoaId, PessoaEnderecoId, FinalidadeId, Principal, Ativo, CriadoEm)
        SELECT NEWID(), e.PessoaId, e.Id, b.FinalidadeId, 0, 1, SYSUTCDATETIME()
        FROM PessoaEnderecos e JOIN @Bits b ON (e.Finalidades & b.Bit) <> 0;
        IF @@ROWCOUNT <> @Esperadas
            THROW 50020, N'Migração das finalidades de endereço: a contagem não conferiu. Nada foi alterado.', 1;

        -- Pessoas com mais de um antigo principal ativo (inconsistência: não decide).
        DECLARE @PrincipalDuplo TABLE (PessoaId uniqueidentifier PRIMARY KEY);
        INSERT INTO @PrincipalDuplo (PessoaId)
        SELECT PessoaId FROM PessoaEnderecos WHERE Ativo = 1 AND (Finalidades & 1) <> 0 GROUP BY PessoaId HAVING COUNT(*) > 1;

        -- Regra 1: o antigo principal (único e ativo) é o principal de cada finalidade que ele tem.
        UPDATE u SET Principal = 1
        FROM PessoaEnderecoFinalidades u
        JOIN PessoaEnderecos e ON e.Id = u.PessoaEnderecoId AND e.PessoaId = u.PessoaId
        WHERE e.Ativo = 1 AND (e.Finalidades & 1) <> 0
          AND NOT EXISTS (SELECT 1 FROM @PrincipalDuplo d WHERE d.PessoaId = u.PessoaId);

        -- Regra 2: finalidade num só endereço ativo, ainda sem principal.
        UPDATE u SET Principal = 1
        FROM PessoaEnderecoFinalidades u
        JOIN PessoaEnderecos e ON e.Id = u.PessoaEnderecoId AND e.PessoaId = u.PessoaId
        WHERE e.Ativo = 1 AND u.Principal = 0
          AND NOT EXISTS (
              SELECT 1 FROM PessoaEnderecoFinalidades u2
              JOIN PessoaEnderecos e2 ON e2.Id = u2.PessoaEnderecoId AND e2.PessoaId = u2.PessoaId
              WHERE u2.PessoaId = u.PessoaId AND u2.FinalidadeId = u.FinalidadeId AND u2.Id <> u.Id AND e2.Ativo = 1);

        -- Regras 3, 4 e 5: pessoas para revisão manual (a ficha mostra o motivo por finalidade).
        UPDATE Pessoas SET RevisarFinalidadesEndereco = 1
        WHERE Id IN (
            SELECT u.PessoaId
            FROM PessoaEnderecoFinalidades u
            JOIN PessoaEnderecos e ON e.Id = u.PessoaEnderecoId AND e.PessoaId = u.PessoaId
            WHERE e.Ativo = 1
            GROUP BY u.PessoaId, u.FinalidadeId
            HAVING COUNT(*) > 1 AND SUM(CAST(u.Principal AS int)) = 0
            UNION
            SELECT e.PessoaId FROM PessoaEnderecos e
            WHERE e.Ativo = 1 AND (e.Finalidades & 1) <> 0 AND (e.Finalidades & 126) = 0
            UNION
            SELECT PessoaId FROM @PrincipalDuplo);

        -- Conferências: um principal por pessoa + finalidade; relação e endereço da mesma pessoa; inativo sem principal.
        IF EXISTS (SELECT 1 FROM PessoaEnderecoFinalidades WHERE Principal = 1 GROUP BY PessoaId, FinalidadeId HAVING COUNT(*) > 1)
            THROW 50021, N'Migração das finalidades de endereço: dois principais na mesma finalidade. Nada foi alterado.', 1;
        IF EXISTS (SELECT 1 FROM PessoaEnderecoFinalidades u JOIN PessoaEnderecos e ON e.Id = u.PessoaEnderecoId WHERE e.PessoaId <> u.PessoaId)
            THROW 50022, N'Migração das finalidades de endereço: relação com pessoa diferente do endereço. Nada foi alterado.', 1;
        IF EXISTS (SELECT 1 FROM PessoaEnderecoFinalidades u JOIN PessoaEnderecos e ON e.Id = u.PessoaEnderecoId WHERE e.Ativo = 0 AND u.Principal = 1)
            THROW 50023, N'Migração das finalidades de endereço: endereço inativo marcado como principal. Nada foi alterado.', 1;
        """;
}
