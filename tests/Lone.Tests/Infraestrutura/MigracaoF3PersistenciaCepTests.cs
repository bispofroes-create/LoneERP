using Lone.Domain.Enums;
using Lone.Infrastructure.Persistencia;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Lone.Tests.Infraestrutura;

/// <summary>
/// F3: a migration do motor de CEP aplicada num banco TEMPORÁRIO (nunca o do sistema). Só estrutura: endereço já gravado
/// fica "não conferido" (0), sem fonte e sem data (nada presumido), e nenhum dado antigo muda; CacheCep e ConsultasCep
/// criadas vazias; o Down volta ao esquema do P0 sem perder o endereço; o Up de novo funciona. Pulado sem
/// LONE_TESTES_SQLSERVER.
/// </summary>
public class MigracaoF3PersistenciaCepTests
{
    private const string Anterior = "20261005083229_P0ExclusaoAuditoria";

    [FatoSqlServer]
    public async Task Migration_F3_so_acrescenta_e_endereco_antigo_fica_nao_conferido()
    {
        var conexao = new SqlConnectionStringBuilder(FatoSqlServerAttribute.Conexao!)
        {
            InitialCatalog = "Lone_Teste_" + Guid.NewGuid().ToString("N")
        }.ConnectionString;
        await using var db = new LoneDbContext(new DbContextOptionsBuilder<LoneDbContext>().UseSqlServer(conexao).Options);
        var migrador = db.GetService<IMigrator>();
        try
        {
            // 1. Esquema anterior (P0) com uma pessoa e um endereço.
            await migrador.MigrateAsync(Anterior);
            await using var sql = new SqlConnection(conexao);
            await sql.OpenAsync();
            var pessoa = Guid.NewGuid();
            var endereco = Guid.NewGuid();
            await MigracaoCompletaTests.InserirAsync(sql, "Pessoas", new()
            {
                ["Id"] = pessoa, ["Nome"] = "F3 " + pessoa.ToString("N")[..6], ["Natureza"] = (byte)NaturezaPessoa.Juridica
            });
            await MigracaoCompletaTests.InserirAsync(sql, "PessoaEnderecos", new()
            {
                ["Id"] = endereco, ["PessoaId"] = pessoa, ["Cep"] = "35790000", ["Logradouro"] = "Rua Barão", ["Numero"] = "150",
                ["Bairro"] = "Centro", ["Cidade"] = "Curvelo", ["Uf"] = "MG", ["CodigoPais"] = "1058", ["Pais"] = "Brasil"
            });
            var antes = await TextoAsync(sql, Soma(endereco));

            // 2. Up: só acrescenta; o endereço antigo fica não conferido e com os mesmos dados.
            await migrador.MigrateAsync();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.False(db.Database.HasPendingModelChanges()); // a migration corresponde ao modelo atual
            Assert.Equal(1, await ContarAsync(sql,
                $"SELECT COUNT(*) FROM PessoaEnderecos WHERE Id = '{endereco}' AND CepSituacao = 0 AND CepFonte IS NULL AND CepConferidoEm IS NULL"));
            Assert.Equal(antes, await TextoAsync(sql, Soma(endereco)));
            Assert.Equal(0, await ContarAsync(sql, "SELECT COUNT(*) FROM CacheCep"));
            Assert.Equal(0, await ContarAsync(sql, "SELECT COUNT(*) FROM ConsultasCep"));
            Assert.Equal(1, await ContarAsync(sql, "SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID('ConsultasCep') AND name = 'IX_ConsultasCep_OcorridoEm'"));
            // Nenhuma coluna de pessoa nas tabelas técnicas.
            Assert.Equal(0, await ContarAsync(sql, """
                SELECT COUNT(*) FROM sys.columns WHERE object_id IN (OBJECT_ID('CacheCep'), OBJECT_ID('ConsultasCep'))
                  AND name IN ('PessoaId', 'Nome', 'Numero', 'Bairro', 'Complemento', 'Documento')
                """));

            // 3. Down: volta ao P0 sem as colunas e tabelas novas; o endereço continua igual. Up de novo.
            await migrador.MigrateAsync(Anterior);
            Assert.Equal(0, await ContarAsync(sql,
                "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('PessoaEnderecos') AND name IN ('CepSituacao', 'CepFonte', 'CepConferidoEm')"));
            Assert.Equal(0, await ContarAsync(sql, "SELECT COUNT(*) FROM sys.tables WHERE name IN ('CacheCep', 'ConsultasCep')"));
            Assert.Equal(antes, await TextoAsync(sql, Soma(endereco)));
            await migrador.MigrateAsync();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await db.Database.EnsureDeletedAsync(); // só o banco temporário deste teste
        }
    }

    /// <summary>Os dados antigos do endereço (nenhum pode mudar).</summary>
    private static string Soma(Guid endereco) =>
        $"SELECT CONCAT(Cep, '|', Logradouro, '|', Numero, '|', Bairro, '|', Cidade, '|', Uf, '|', CodigoPais, '|', Ativo) FROM PessoaEnderecos WHERE Id = '{endereco}'";

    private static async Task<int> ContarAsync(SqlConnection sql, string consulta)
    {
        await using var c = new SqlCommand(consulta, sql);
        return (int)(await c.ExecuteScalarAsync())!;
    }

    private static async Task<string> TextoAsync(SqlConnection sql, string consulta)
    {
        await using var c = new SqlCommand(consulta, sql);
        return (string)(await c.ExecuteScalarAsync())!;
    }
}
