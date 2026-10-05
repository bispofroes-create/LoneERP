using Lone.Domain.Enums;
using Lone.Infrastructure.Persistencia;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Lone.Tests.Infraestrutura;

/// <summary>
/// P0 (revisão 2.2): a migration P0ExclusaoAuditoria aplicada num banco TEMPORÁRIO (Lone_Teste_..., criado e apagado
/// pelo teste, nunca o do sistema). Só estrutura: contatos e sócios já gravados ficam ativos e sem data de saída, nada
/// é apagado; o gatilho da Auditoria é criado no Up e removido no Down, e as linhas continuam lá. Pulado sem
/// LONE_TESTES_SQLSERVER.
/// </summary>
public class MigracaoP0ExclusaoAuditoriaTests
{
    private const string Anterior = "20261004090755_PrazosPeriodo";

    [FatoSqlServer]
    public async Task Migration_P0_so_muda_estrutura_cria_o_gatilho_e_volta_no_Down_sem_perder_linhas()
    {
        var conexao = new SqlConnectionStringBuilder(FatoSqlServerAttribute.Conexao!)
        {
            InitialCatalog = "Lone_Teste_" + Guid.NewGuid().ToString("N")
        }.ConnectionString;
        await using var db = new LoneDbContext(new DbContextOptionsBuilder<LoneDbContext>().UseSqlServer(conexao).Options);
        var migrador = db.GetService<IMigrator>();
        try
        {
            // 1. Esquema anterior com uma pessoa, um contato, um sócio e uma linha de auditoria.
            await migrador.MigrateAsync(Anterior);
            await using var sql = new SqlConnection(conexao);
            await sql.OpenAsync();
            var pessoa = Guid.NewGuid();
            var contato = Guid.NewGuid();
            var socio = Guid.NewGuid();
            await MigracaoCompletaTests.InserirAsync(sql, "Pessoas", new()
            {
                ["Id"] = pessoa, ["Nome"] = "Empresa P0 " + pessoa.ToString("N")[..6], ["Natureza"] = (byte)NaturezaPessoa.Juridica
            });
            await MigracaoCompletaTests.InserirAsync(sql, "PessoaContatos", new() { ["Id"] = contato, ["PessoaId"] = pessoa, ["Nome"] = "Maria" });
            await MigracaoCompletaTests.InserirAsync(sql, "PessoaSocios", new() { ["Id"] = socio, ["PessoaId"] = pessoa, ["Nome"] = "João" });
            await ExecutarAsync(sql, $"""
                INSERT INTO Auditoria (DataHora, Usuario, OperacaoId, Origem, Entidade, RegistroId, RaizEntidade, RaizId, Acao)
                VALUES (SYSUTCDATETIME(), 'teste', NEWID(), 0, 'Pessoa', '{pessoa}', 'Pessoa', '{pessoa}', 1)
                """);
            Assert.Equal(0, await ContarAsync(sql, "SELECT COUNT(*) FROM sys.triggers WHERE name = 'TR_Auditoria_SomenteInclusao'"));

            // 2. Up: colunas novas com o padrão (ativo, sem data de saída), nada apagado, gatilho criado.
            await migrador.MigrateAsync();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.False(db.Database.HasPendingModelChanges());
            Assert.Equal(1, await ContarAsync(sql, $"SELECT COUNT(*) FROM PessoaContatos WHERE Id = '{contato}' AND Ativo = 1"));
            Assert.Equal(1, await ContarAsync(sql, $"SELECT COUNT(*) FROM PessoaSocios WHERE Id = '{socio}' AND Ativo = 1 AND SaiuEm IS NULL"));
            Assert.Equal(1, await ContarAsync(sql, $"SELECT COUNT(*) FROM Auditoria WHERE RaizId = '{pessoa}'"));
            Assert.Equal(1, await ContarAsync(sql, "SELECT COUNT(*) FROM sys.triggers WHERE name = 'TR_Auditoria_SomenteInclusao'"));
            Assert.Equal(50080, (await Assert.ThrowsAsync<SqlException>(() => ExecutarAsync(sql, $"DELETE FROM Auditoria WHERE RaizId = '{pessoa}'"))).Number);

            // 3. Down: sem gatilho e sem as colunas novas; as linhas continuam (a limitação do Down sobre as inativações não é corrigida aqui).
            await migrador.MigrateAsync(Anterior);
            Assert.Equal(0, await ContarAsync(sql, "SELECT COUNT(*) FROM sys.triggers WHERE name = 'TR_Auditoria_SomenteInclusao'"));
            Assert.Equal(0, await ContarAsync(sql, "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('PessoaSocios') AND name IN ('Ativo', 'SaiuEm')"));
            Assert.Equal(0, await ContarAsync(sql, "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('PessoaContatos') AND name = 'Ativo'"));
            Assert.Equal(1, await ContarAsync(sql, $"SELECT COUNT(*) FROM PessoaContatos WHERE Id = '{contato}'"));
            Assert.Equal(1, await ContarAsync(sql, $"SELECT COUNT(*) FROM PessoaSocios WHERE Id = '{socio}'"));
            Assert.Equal(1, await ContarAsync(sql, $"SELECT COUNT(*) FROM Auditoria WHERE RaizId = '{pessoa}'"));

            await migrador.MigrateAsync();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await db.Database.EnsureDeletedAsync(); // só o banco temporário deste teste
        }
    }

    private static async Task ExecutarAsync(SqlConnection sql, string comando)
    {
        await using var c = new SqlCommand(comando, sql);
        await c.ExecuteNonQueryAsync();
    }

    private static async Task<int> ContarAsync(SqlConnection sql, string consulta)
    {
        await using var c = new SqlCommand(consulta, sql);
        return (int)(await c.ExecuteScalarAsync())!;
    }
}
