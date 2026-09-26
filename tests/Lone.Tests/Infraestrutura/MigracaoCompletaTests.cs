using Lone.Domain.Enderecos;
using Lone.Domain.Enums;
using Lone.Infrastructure.Persistencia;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Lone.Tests.Infraestrutura;

/// <summary>
/// A migration REAL (a gerada pelo EF, com o SQL inserido pela ferramenta) aplicada num banco TEMPORÁRIO criado e apagado
/// pelo próprio teste (Lone_Teste_...), nunca no banco do sistema: dados antigos gravados no esquema anterior
/// (CadastroGeral), migração até o fim, conferência do resultado, das proteções e da volta (Down). Pulado sem
/// LONE_TESTES_SQLSERVER.
/// </summary>
public class MigracaoCompletaTests
{
    private const string Anterior = "20260925202711_CadastroGeral";
    private static readonly Guid Entrega = FinalidadesEnderecoIniciais.Id(FinalidadesEnderecoIniciais.Entrega);
    private static readonly Guid Fiscal = FinalidadesEnderecoIniciais.Id(FinalidadesEnderecoIniciais.Fiscal);

    private static readonly string[] Gatilhos =
    [
        SqlMigracaoFinalidadesEndereco.GatilhoPrincipalEnderecoAtivo,
        SqlMigracaoFinalidadesEndereco.GatilhoEnderecoInativo,
        SqlMigracaoFinalidadesEndereco.GatilhoFinalidadeSistema
    ];

    [FatoSqlServer]
    public async Task Migration_real_converte_os_bits_cria_as_protecoes_e_volta_no_Down()
    {
        var conexao = new SqlConnectionStringBuilder(FatoSqlServerAttribute.Conexao!)
        {
            InitialCatalog = "Lone_Teste_" + Guid.NewGuid().ToString("N")
        }.ConnectionString;
        await using var db = new LoneDbContext(new DbContextOptionsBuilder<LoneDbContext>().UseSqlServer(conexao).Options);
        var migrador = db.GetService<IMigrator>();
        try
        {
            // 1. Esquema anterior + dados no formato antigo (bits em PessoaEnderecos.Finalidades).
            await migrador.MigrateAsync(Anterior);
            await using var sql = new SqlConnection(conexao);
            await sql.OpenAsync();
            var antigoPrincipal = await PessoaAsync(sql, (FinalidadeEndereco.Principal | FinalidadeEndereco.Entrega | FinalidadeEndereco.Fiscal, true),
                                                        (FinalidadeEndereco.Entrega, true));
            var ambigua = await PessoaAsync(sql, (FinalidadeEndereco.Entrega, true), (FinalidadeEndereco.Entrega, true));
            var semFinalidade = await PessoaAsync(sql, (FinalidadeEndereco.Principal, true), (FinalidadeEndereco.Fiscal, true));
            var repetidos = await PessoaAsync(sql, (FinalidadeEndereco.Principal | FinalidadeEndereco.Entrega, true),
                                                   (FinalidadeEndereco.Principal | FinalidadeEndereco.Entrega | FinalidadeEndereco.Fiscal, true));
            var inativo = await PessoaAsync(sql, (FinalidadeEndereco.Principal | FinalidadeEndereco.Entrega, false), (FinalidadeEndereco.Entrega, true));

            // 2. A migration até o fim (a de endereço × finalidade inclusive).
            await migrador.MigrateAsync();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.False(db.Database.HasPendingModelChanges()); // a migration corresponde ao modelo atual

            // 3. Resultado das regras aprovadas.
            var usos = await db.PessoaEnderecoFinalidades.AsNoTracking().ToListAsync();
            var pessoas = await db.Pessoas.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.RevisarFinalidadesEndereco);
            var enderecos = await db.PessoaEnderecos.AsNoTracking().ToDictionaryAsync(e => e.Id);

            Assert.All(usos.Where(u => u.PessoaEnderecoId == antigoPrincipal.Enderecos[0]), u => Assert.True(u.Principal));
            Assert.False(usos.Single(u => u.PessoaEnderecoId == antigoPrincipal.Enderecos[1]).Principal);
            Assert.False(pessoas[antigoPrincipal.Pessoa]);

            Assert.All(usos.Where(u => u.PessoaId == ambigua.Pessoa), u => Assert.False(u.Principal));
            Assert.True(pessoas[ambigua.Pessoa]);

            Assert.DoesNotContain(usos, u => u.PessoaEnderecoId == semFinalidade.Enderecos[0]);
            Assert.Equal(MotivoRevisaoEndereco.AntigoPrincipalSemFinalidade, enderecos[semFinalidade.Enderecos[0]].RevisaoMigracao);
            Assert.True(pessoas[semFinalidade.Pessoa]);

            Assert.All(usos.Where(u => u.PessoaId == repetidos.Pessoa && u.FinalidadeId == Entrega), u => Assert.False(u.Principal));
            Assert.True(usos.Single(u => u.PessoaId == repetidos.Pessoa && u.FinalidadeId == Fiscal).Principal);
            Assert.True(pessoas[repetidos.Pessoa]);

            Assert.False(usos.Single(u => u.PessoaEnderecoId == inativo.Enderecos[0]).Principal);
            Assert.True(usos.Single(u => u.PessoaEnderecoId == inativo.Enderecos[1]).Principal);
            Assert.False(pessoas[inativo.Pessoa]);

            // 4. Proteções criadas pela migration (gatilhos, CHECKs e a FK composta de MescladoEmId).
            Assert.Equal(Gatilhos.Order(), (await NomesAsync(sql, "SELECT name FROM sys.triggers WHERE parent_class = 1")).Where(Gatilhos.Contains).Order());
            var checks = await NomesAsync(sql, "SELECT name FROM sys.check_constraints");
            Assert.Contains(SqlMigracaoFinalidadesEndereco.CheckConsolidado, checks);
            Assert.Contains(SqlMigracaoFinalidadesEndereco.CheckSistemaAtiva, checks);
            Assert.Contains("CK_PessoaEnderecoFinalidades_PrincipalAtivo", checks);
            Assert.Equal(new[] { "MescladoEmId", "PessoaId" }, await NomesAsync(sql, """
                SELECT c.name FROM sys.foreign_key_columns k
                JOIN sys.foreign_keys f ON f.object_id = k.constraint_object_id
                JOIN sys.columns c ON c.object_id = k.parent_object_id AND c.column_id = k.parent_column_id
                WHERE f.name = 'FK_PessoaEnderecos_PessoaEnderecos_MescladoEmId_PessoaId' ORDER BY k.constraint_column_id
                """));

            // 5. Down: volta ao esquema anterior sem deixar gatilho órfão; e sobe de novo.
            await migrador.MigrateAsync(Anterior);
            Assert.Empty((await NomesAsync(sql, "SELECT name FROM sys.triggers")).Where(Gatilhos.Contains));
            await migrador.MigrateAsync();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await db.Database.EnsureDeletedAsync(); // só o banco temporário deste teste
        }
    }

    private static async Task<(Guid Pessoa, Guid[] Enderecos)> PessoaAsync(SqlConnection sql, params (FinalidadeEndereco Bits, bool Ativo)[] enderecos)
    {
        var pessoa = Guid.NewGuid();
        await InserirAsync(sql, "Pessoas", new() { ["Id"] = pessoa, ["Nome"] = "Teste " + pessoa.ToString("N")[..6], ["Natureza"] = (byte)NaturezaPessoa.Fisica });
        var ids = new List<Guid>();
        for (var i = 0; i < enderecos.Length; i++)
        {
            var id = Guid.NewGuid();
            await InserirAsync(sql, "PessoaEnderecos", new()
            {
                ["Id"] = id, ["PessoaId"] = pessoa, ["Logradouro"] = $"Rua {i}", ["Numero"] = $"{i}", ["Cidade"] = "Curvelo",
                ["Ordem"] = i, ["Finalidades"] = (short)enderecos[i].Bits, ["Ativo"] = enderecos[i].Ativo, ["CodigoPais"] = "1058", ["Pais"] = "Brasil"
            });
            ids.Add(id);
        }
        return (pessoa, ids.ToArray());
    }

    /// <summary>
    /// INSERT no esquema ANTIGO (o do modelo atual não serve ali): as colunas informadas e, nas obrigatórias sem valor
    /// padrão, um valor neutro do tipo (lido do próprio banco).
    /// </summary>
    private static async Task InserirAsync(SqlConnection sql, string tabela, Dictionary<string, object> valores)
    {
        await using (var colunas = new SqlCommand("""
            SELECT c.name, t.name FROM sys.columns c JOIN sys.types t ON t.user_type_id = c.user_type_id
            WHERE c.object_id = OBJECT_ID(@tabela) AND c.is_nullable = 0 AND c.default_object_id = 0
              AND c.is_identity = 0 AND c.is_computed = 0 AND t.name <> 'timestamp'
            """, sql))
        {
            colunas.Parameters.AddWithValue("@tabela", tabela);
            await using var leitor = await colunas.ExecuteReaderAsync();
            while (await leitor.ReadAsync())
            {
                var (nome, tipo) = (leitor.GetString(0), leitor.GetString(1));
                if (valores.ContainsKey(nome)) continue;
                valores[nome] = tipo switch
                {
                    "bit" => false,
                    "tinyint" => (byte)0,
                    "smallint" => (short)0,
                    "int" => 0,
                    "bigint" => 0L,
                    "decimal" or "numeric" or "money" => 0m,
                    "float" or "real" => 0d,
                    "uniqueidentifier" => Guid.NewGuid(),
                    "date" or "datetime" or "datetime2" or "smalldatetime" => new DateTime(2000, 1, 1),
                    "datetimeoffset" => new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    "time" => TimeSpan.Zero,
                    "varbinary" or "binary" => Array.Empty<byte>(),
                    _ => string.Empty
                };
            }
        }

        var nomes = valores.Keys.ToList();
        await using var insert = new SqlCommand(
            $"INSERT INTO [{tabela}] ({string.Join(", ", nomes.Select(n => $"[{n}]"))}) VALUES ({string.Join(", ", nomes.Select((_, i) => $"@p{i}"))})", sql);
        for (var i = 0; i < nomes.Count; i++) insert.Parameters.AddWithValue($"@p{i}", valores[nomes[i]]);
        await insert.ExecuteNonQueryAsync();
    }

    private static async Task<List<string>> NomesAsync(SqlConnection sql, string consulta)
    {
        var nomes = new List<string>();
        await using var comando = new SqlCommand(consulta, sql);
        await using var leitor = await comando.ExecuteReaderAsync();
        while (await leitor.ReadAsync()) nomes.Add(leitor.GetString(0));
        return nomes;
    }
}
