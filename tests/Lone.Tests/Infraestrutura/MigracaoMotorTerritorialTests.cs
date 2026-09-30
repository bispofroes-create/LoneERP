using Lone.Domain.Enderecos;
using Lone.Domain.Entidades;
using Lone.Infrastructure.Persistencia;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Lone.Tests.Infraestrutura;

/// <summary>
/// As duas migrations REAIS da 2b-1b (Fase2b1bNumeracao e Fase2b1bMotor) num banco TEMPORÁRIO, a partir do estado da
/// 2b-1a aplicado no LoneERP, com um mapa já cadastrado: Up cria a linha do motor do mapa antigo, os parâmetros com 30 dias,
/// "registrar nos documentos" falso, os quatro gatilhos (os três que leem a tabela com READCOMMITTEDLOCK) e as FKs de
/// Exclusivo; o modelo confere com as migrations; Down volta exatamente à 2b-1a (os três gatilhos dela intactos) e, até a
/// Numeracao, deixa só a numeração; Up de novo refaz tudo sem duplicar.
/// </summary>
public class MigracaoMotorTerritorialTests
{
    private const string UltimaDa2b1a = "20260929200500_Fase2b1ResponsaveisConcorrencia";
    private const string Numeracao = "20260930004153_Fase2b1bNumeracao";

    [FatoSqlServer]
    public async Task Migrations_da_2b1b_sobem_voltam_no_Down_e_sobem_de_novo()
    {
        var conexao = new SqlConnectionStringBuilder(FatoSqlServerAttribute.Conexao!) { InitialCatalog = "Lone_Teste_" + Guid.NewGuid().ToString("N") }.ConnectionString;
        await using var db = new LoneDbContext(new DbContextOptionsBuilder<LoneDbContext>().UseSqlServer(conexao).Options);
        var migrador = db.GetService<IMigrator>();
        try
        {
            await migrador.MigrateAsync(UltimaDa2b1a);
            await using var sql = new SqlConnection(conexao);
            await sql.OpenAsync();
            var mapa = Guid.NewGuid();
            await MigracaoCompletaTests.InserirAsync(sql, "MapasTerritoriais", new()
            {
                ["Id"] = mapa, ["Codigo"] = "ANTIGO", ["Nome"] = "Mapa antigo", ["Exclusivo"] = true, ["Ativo"] = true,
                ["FinalidadeEnderecoReferenciaId"] = FinalidadesEnderecoIniciais.Id(FinalidadesEnderecoIniciais.Comercial)
            });

            // Up até o fim.
            await migrador.MigrateAsync();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.False(db.Database.HasPendingModelChanges());
            Assert.Equal(1, await EscalarAsync(sql, $"SELECT COUNT(*) FROM MapaTerritorialMotor WHERE MapaId = '{mapa}' AND UltimaOperacaoId IS NULL"));
            Assert.Equal(30, await EscalarAsync(sql, $"SELECT DiasRetroativosMaximo FROM ParametrosTerritoriais WHERE Id = '{ParametrosTerritoriais.IdUnico}'"));
            Assert.Equal(0, await EscalarAsync(sql, $"SELECT CAST(RegistrarNosDocumentos AS int) FROM MapasTerritoriais WHERE Id = '{mapa}'"));
            var gatilhos = await NomesAsync(sql, "SELECT name FROM sys.triggers WHERE parent_class = 1");
            foreach (var g in new[] { SqlMigracaoTerritorios.GatilhoRegras, SqlMigracaoTerritorios.GatilhoExcecoes, SqlMigracaoTerritorios.GatilhoAtribuicoes,
                                      SqlMigracaoTerritorios.GatilhoItens, SqlMigracaoTerritorios.Gatilho, SqlMigracaoTerritorios.GatilhoArvore, SqlMigracaoTerritorios.GatilhoPosicoes })
                Assert.Contains(g, gatilhos);
            foreach (var g in new[] { SqlMigracaoTerritorios.GatilhoRegras, SqlMigracaoTerritorios.GatilhoExcecoes, SqlMigracaoTerritorios.GatilhoAtribuicoes })
                Assert.Contains("READCOMMITTEDLOCK", await DefinicaoAsync(sql, g));
            var chaves = await NomesAsync(sql, "SELECT name FROM sys.foreign_keys");
            Assert.Contains(SqlMigracaoTerritorios.ChaveExcecoesExclusivo, chaves);
            Assert.Contains(SqlMigracaoTerritorios.ChaveAtribuicoesExclusivo, chaves);

            // Down até a Numeracao: só a numeração fica.
            await migrador.MigrateAsync(Numeracao);
            Assert.Equal(1, await EscalarAsync(sql, "SELECT COUNT(*) FROM sys.tables WHERE name = 'NumeracoesDocumento'"));
            Assert.Equal(0, await EscalarAsync(sql, "SELECT COUNT(*) FROM sys.tables WHERE name IN ('MapaTerritorialMotor', 'RegrasTerritorio', 'OperacoesTerritoriais', 'ParametrosTerritoriais')"));

            // Down até a 2b-1a: o banco volta a ser o de antes; os gatilhos da 2b-1a continuam, com o reforço.
            await migrador.MigrateAsync(UltimaDa2b1a);
            Assert.Equal(0, await EscalarAsync(sql, "SELECT COUNT(*) FROM sys.tables WHERE name = 'NumeracoesDocumento'"));
            Assert.Equal(0, await EscalarAsync(sql, "SELECT COUNT(*) FROM sys.columns WHERE name = 'RegistrarNosDocumentos'"));
            Assert.Equal(0, await EscalarAsync(sql, "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('TerritorioPosicoes') AND name LIKE 'Operacao%'"));
            gatilhos = await NomesAsync(sql, "SELECT name FROM sys.triggers WHERE parent_class = 1");
            Assert.Contains(SqlMigracaoTerritorios.Gatilho, gatilhos);
            Assert.Contains(SqlMigracaoTerritorios.GatilhoArvore, gatilhos);
            Assert.Contains(SqlMigracaoTerritorios.GatilhoPosicoes, gatilhos);
            Assert.DoesNotContain(SqlMigracaoTerritorios.GatilhoRegras, gatilhos);
            Assert.Contains("READCOMMITTEDLOCK", await DefinicaoAsync(sql, SqlMigracaoTerritorios.Gatilho));

            // Up de novo: a linha do motor do mapa antigo volta uma vez só.
            await migrador.MigrateAsync();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.Equal(1, await EscalarAsync(sql, $"SELECT COUNT(*) FROM MapaTerritorialMotor WHERE MapaId = '{mapa}'"));
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await db.Database.EnsureDeletedAsync(); // só o banco temporário deste teste
        }
    }

    private static async Task<int> EscalarAsync(SqlConnection sql, string consulta)
    {
        await using var comando = new SqlCommand(consulta, sql);
        return Convert.ToInt32(await comando.ExecuteScalarAsync());
    }

    private static async Task<string> DefinicaoAsync(SqlConnection sql, string gatilho)
    {
        await using var comando = new SqlCommand("SELECT OBJECT_DEFINITION(OBJECT_ID(@nome))", sql);
        comando.Parameters.AddWithValue("@nome", gatilho);
        return await comando.ExecuteScalarAsync() as string ?? throw new Xunit.Sdk.XunitException($"Gatilho {gatilho} não existe.");
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
