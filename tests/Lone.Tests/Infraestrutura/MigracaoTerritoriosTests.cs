using Lone.Domain.Enderecos;
using Lone.Infrastructure.Persistencia;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Lone.Tests.Infraestrutura;

/// <summary>
/// As migrations REAIS da trava da árvore (Fase2b1TravaArvore) e do reforço de concorrência dos responsáveis
/// (Fase2b1ResponsaveisConcorrencia), aplicadas num banco TEMPORÁRIO (Lone_Teste_...) — nunca no banco do sistema: um mapa
/// que já existia ganha a sua trava, os três gatilhos existem com o texto esperado, o modelo confere com as migrations, e o
/// Down volta exatamente ao estado da Fase2b1ResponsaveisSemSobreposicao (sem apagar o gatilho dela). Os testes de banco dos
/// territórios criam o banco pelo modelo; este é o que prova que as MIGRATIONS criam as mesmas proteções.
/// </summary>
public class MigracaoTerritoriosTests
{
    private const string Anterior = "20260929122721_Fase2b1ResponsaveisSemSobreposicao";

    [Fact]
    public void O_reforco_so_muda_a_forma_de_ler_e_o_Down_devolve_o_texto_da_migration_2()
    {
        Assert.Equal(SqlMigracaoTerritorios.DesfazerReforcoProtecaoConcorrencia,
            SqlMigracaoTerritorios.ReforcarProtecaoConcorrencia.Replace(" WITH (READCOMMITTEDLOCK)", "", StringComparison.Ordinal));
        Assert.Equal(SqlMigracaoTerritorios.CriarProtecao,
            SqlMigracaoTerritorios.DesfazerReforcoProtecaoConcorrencia.Replace("CREATE OR ALTER TRIGGER", "CREATE TRIGGER", StringComparison.Ordinal));
        Assert.DoesNotContain("READCOMMITTEDLOCK", SqlMigracaoTerritorios.CriarProtecao); // a migration 2 não mudou
    }

    [FatoSqlServer]
    public async Task Migrations_da_trava_e_do_reforco_sobem_preenchem_a_trava_e_voltam_no_Down()
    {
        var conexao = new SqlConnectionStringBuilder(FatoSqlServerAttribute.Conexao!)
        {
            InitialCatalog = "Lone_Teste_" + Guid.NewGuid().ToString("N")
        }.ConnectionString;
        await using var db = new LoneDbContext(new DbContextOptionsBuilder<LoneDbContext>().UseSqlServer(conexao).Options);
        var migrador = db.GetService<IMigrator>();
        try
        {
            // 1. Banco como está hoje no LoneERP (até a segunda migration da 2b-1a) com um mapa já cadastrado.
            await migrador.MigrateAsync(Anterior);
            await using var sql = new SqlConnection(conexao);
            await sql.OpenAsync();
            var mapa = Guid.NewGuid();
            await MigracaoCompletaTests.InserirAsync(sql, "MapasTerritoriais", new()
            {
                ["Id"] = mapa, ["Codigo"] = "ANTIGO", ["Nome"] = "Mapa antigo", ["Exclusivo"] = true, ["Ativo"] = true,
                ["FinalidadeEnderecoReferenciaId"] = FinalidadesEnderecoIniciais.Id(FinalidadesEnderecoIniciais.Comercial)
            });
            Assert.DoesNotContain("READCOMMITTEDLOCK", await DefinicaoAsync(sql, SqlMigracaoTerritorios.Gatilho));

            // 2. As migrations novas até o fim.
            await migrador.MigrateAsync();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.False(db.Database.HasPendingModelChanges()); // as migrations correspondem ao modelo atual
            Assert.Equal(1, await EscalarAsync(sql, $"SELECT COUNT(*) FROM MapaTerritorialArvores WHERE MapaId = '{mapa}'"));
            var gatilhos = await GatilhosAsync(sql);
            Assert.Contains(SqlMigracaoTerritorios.Gatilho, gatilhos);
            Assert.Contains(SqlMigracaoTerritorios.GatilhoArvore, gatilhos);
            Assert.Contains(SqlMigracaoTerritorios.GatilhoPosicoes, gatilhos);
            Assert.Contains("JOIN TerritorioResponsaveis r WITH (READCOMMITTEDLOCK)", await DefinicaoAsync(sql, SqlMigracaoTerritorios.Gatilho));
            Assert.Contains("READCOMMITTEDLOCK", await DefinicaoAsync(sql, SqlMigracaoTerritorios.GatilhoArvore));
            Assert.Contains("READCOMMITTEDLOCK", await DefinicaoAsync(sql, SqlMigracaoTerritorios.GatilhoPosicoes));

            // 3. Down até a segunda migration: sem a trava e sem os dois gatilhos novos; o 50070 continua, como era.
            await migrador.MigrateAsync(Anterior);
            gatilhos = await GatilhosAsync(sql);
            Assert.Contains(SqlMigracaoTerritorios.Gatilho, gatilhos);
            Assert.DoesNotContain(SqlMigracaoTerritorios.GatilhoArvore, gatilhos);
            Assert.DoesNotContain(SqlMigracaoTerritorios.GatilhoPosicoes, gatilhos);
            Assert.DoesNotContain("READCOMMITTEDLOCK", await DefinicaoAsync(sql, SqlMigracaoTerritorios.Gatilho));
            Assert.Equal(0, await EscalarAsync(sql, "SELECT COUNT(*) FROM sys.tables WHERE name = 'MapaTerritorialArvores'"));

            // 4. Sobe de novo: a trava do mapa antigo volta uma vez só.
            await migrador.MigrateAsync();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.Equal(1, await EscalarAsync(sql, $"SELECT COUNT(*) FROM MapaTerritorialArvores WHERE MapaId = '{mapa}'"));
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await db.Database.EnsureDeletedAsync(); // só o banco temporário deste teste
        }
    }

    private static async Task<string> DefinicaoAsync(SqlConnection sql, string gatilho)
    {
        await using var comando = new SqlCommand("SELECT OBJECT_DEFINITION(OBJECT_ID(@nome))", sql);
        comando.Parameters.AddWithValue("@nome", gatilho);
        return await comando.ExecuteScalarAsync() as string ?? throw new Xunit.Sdk.XunitException($"Gatilho {gatilho} não existe.");
    }

    private static async Task<int> EscalarAsync(SqlConnection sql, string consulta)
    {
        await using var comando = new SqlCommand(consulta, sql);
        return Convert.ToInt32(await comando.ExecuteScalarAsync());
    }

    private static async Task<List<string>> GatilhosAsync(SqlConnection sql)
    {
        var nomes = new List<string>();
        await using var comando = new SqlCommand("SELECT name FROM sys.triggers WHERE parent_class = 1", sql);
        await using var leitor = await comando.ExecuteReaderAsync();
        while (await leitor.ReadAsync()) nomes.Add(leitor.GetString(0));
        return nomes;
    }
}
