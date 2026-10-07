using System.Reflection;
using Lone.Domain.Auditoria;
using Lone.Domain.Entidades;
using Lone.Domain.Enderecos;
using Lone.Domain.Enums;
using Lone.Infrastructure.Persistencia;
using Lone.Infrastructure.Persistencia.Configuracoes;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Lone.Tests.Infraestrutura;

/// <summary>
/// V2-1 (M1): o catálogo de países no modelo do EF, as operações da migration (só a tabela Paises) e a migration REAL
/// aplicada num banco TEMPORÁRIO (nunca o do sistema): cria, semeia os 261 códigos, recusa código fora do padrão e
/// duplicado, não toca em Pessoas nem em PessoaEnderecos (nem no esquema de nenhuma outra tabela), volta no Down e sobe
/// de novo. Os testes de banco são pulados sem LONE_TESTES_SQLSERVER.
/// </summary>
public class MigracaoV21CatalogoPaisesTests
{
    private const string Anterior = "20261006220248_P18DocumentosMetadados";
    private const string SufixoMigracao = "_V21CatalogoPaises";
    private const string Referencia = "2026-10-07";

    private static IModel Modelo()
    {
        using var db = new LoneDbContext(new DbContextOptionsBuilder<LoneDbContext>()
            .UseSqlServer("Server=(sem-conexao);Database=Lone;Trusted_Connection=True").Options);
        return db.GetService<IDesignTimeModel>().Model;
    }

    private static string IdV21() => MigracaoV21().GetType().GetCustomAttribute<MigrationAttribute>()!.Id;

    private static Migration MigracaoV21()
    {
        var tipos = typeof(LoneDbContext).Assembly.GetTypes()
            .Where(t => typeof(Migration).IsAssignableFrom(t) && !t.IsAbstract &&
                        t.GetCustomAttribute<MigrationAttribute>()?.Id.EndsWith(SufixoMigracao, StringComparison.Ordinal) == true)
            .ToArray();
        var tipo = Assert.Single(tipos); // gerada pelo Add-Migration V21CatalogoPaises
        return (Migration)Activator.CreateInstance(tipo)!;
    }

    [Fact]
    public void Modelo_tem_a_tabela_Paises_com_chave_char4_checks_e_a_semente_oficial()
    {
        var entidade = Modelo().FindEntityType(typeof(Pais))!;
        Assert.Equal("Paises", entidade.GetTableName());

        var chave = Assert.Single(entidade.FindPrimaryKey()!.Properties);
        Assert.Equal(nameof(Pais.CodigoPaisNFe), chave.Name);
        Assert.Equal("char(4)", chave.GetColumnType());
        Assert.Equal("char(4)", entidade.FindProperty(nameof(Pais.CodigoSucessor))!.GetColumnType());
        Assert.Equal("char(64)", entidade.FindProperty(nameof(Pais.HashFonte))!.GetColumnType());
        Assert.False(entidade.FindProperty(nameof(Pais.NomeFiscal))!.IsNullable);
        Assert.True(entidade.FindProperty(nameof(Pais.VigenciaFim))!.IsNullable);
        Assert.True(entidade.FindProperty(nameof(Pais.CodigoSucessor))!.IsNullable);

        var checks = entidade.GetCheckConstraints().ToDictionary(c => c.ModelName, c => c.Sql);
        Assert.Equal(3, checks.Count);
        Assert.Equal("[CodigoPaisNFe] COLLATE Latin1_General_BIN2 LIKE '[0-9][0-9][0-9][0-9]'", checks[PaisConfiguration.CheckCodigo]);
        Assert.Contains(PaisConfiguration.CheckSucessor, checks.Keys);
        Assert.Contains(PaisConfiguration.CheckVigencia, checks.Keys);

        // Sem FK em nenhum sentido: o catálogo não amarra endereços (V2-1) e o sucessor é só informativo.
        Assert.Empty(entidade.GetForeignKeys());
        Assert.Empty(entidade.GetReferencingForeignKeys());

        var sementes = entidade.GetSeedData().ToList();
        Assert.Equal(PaisesNFeOficiais.Todos.Count, sementes.Count);
        Assert.Equal(PaisesNFeOficiais.Todos.Select(p => p.CodigoPaisNFe), sementes.Select(s => (string)s[nameof(Pais.CodigoPaisNFe)]!));
        Assert.All(sementes, s =>
        {
            Assert.Equal(PaisesNFeOficiais.Fonte, s[nameof(Pais.Fonte)]);
            Assert.Equal(PaisesNFeOficiais.VersaoFonte, s[nameof(Pais.VersaoFonte)]);
            Assert.Equal(PaisesNFeOficiais.HashFonte, s[nameof(Pais.HashFonte)]);
        });

        Assert.NotNull(typeof(Pais).GetCustomAttribute<NaoAuditarAttribute>()); // tabela de sistema, só a migration escreve
    }

    [Fact]
    public void Migration_V21_vem_logo_depois_da_P18_e_so_mexe_na_tabela_Paises()
    {
        using (var db = new LoneDbContext(new DbContextOptionsBuilder<LoneDbContext>()
                   .UseSqlServer("Server=(sem-conexao);Database=Lone;Trusted_Connection=True").Options))
        {
            var todas = db.Database.GetMigrations().ToList();
            Assert.Equal(todas.IndexOf(Anterior) + 1, todas.IndexOf(IdV21()));
            Assert.False(db.Database.HasPendingModelChanges()); // migrations e snapshot correspondem ao modelo (sem conectar)
        }

        var migracao = MigracaoV21();
        Assert.NotEmpty(migracao.UpOperations);
        Assert.All(migracao.UpOperations, op =>
        {
            switch (op)
            {
                case CreateTableOperation t:
                    Assert.Equal("Paises", t.Name);
                    Assert.Equal(3, t.CheckConstraints.Count);
                    Assert.Empty(t.ForeignKeys);
                    break;
                case InsertDataOperation i:
                    Assert.Equal("Paises", i.Table);
                    break;
                case CreateIndexOperation x:
                    Assert.Equal("Paises", x.Table);
                    break;
                default:
                    Assert.Fail($"Operação fora do escopo do V2-1 na migration: {op.GetType().Name}");
                    break;
            }
        });
        Assert.Single(migracao.UpOperations.OfType<CreateTableOperation>());
        Assert.Equal(PaisesNFeOficiais.Todos.Count, migracao.UpOperations.OfType<InsertDataOperation>().Sum(i => i.Values.GetLength(0)));

        var down = Assert.Single(migracao.DownOperations);
        Assert.Equal("Paises", Assert.IsType<DropTableOperation>(down).Name);
    }

    /// <summary>Linhas e conteúdo de Pessoas e PessoaEnderecos (nenhum pode mudar).</summary>
    private const string SomaDados = """
        SELECT CONCAT(
            (SELECT COUNT(*) FROM Pessoas), '|', (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM Pessoas), '|',
            (SELECT COUNT(*) FROM PessoaEnderecos), '|', (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM PessoaEnderecos), '|',
            (SELECT STRING_AGG(CONCAT(CodigoPais, ':', Pais), ',') WITHIN GROUP (ORDER BY CodigoPais) FROM PessoaEnderecos))
        """;

    /// <summary>Esquema de todas as tabelas, menos Paises e o histórico do EF: colunas, índices e restrições.</summary>
    private const string SomaEsquema = """
        SELECT CONCAT(
            (SELECT COUNT(*) FROM sys.tables WHERE name NOT IN ('Paises', '__EFMigrationsHistory')), '|',
            (SELECT CHECKSUM_AGG(CHECKSUM(t.name, c.name, c.system_type_id, c.max_length, c.is_nullable, c.column_id))
               FROM sys.columns c JOIN sys.tables t ON t.object_id = c.object_id
              WHERE t.name NOT IN ('Paises', '__EFMigrationsHistory')), '|',
            (SELECT CHECKSUM_AGG(CHECKSUM(t.name, i.name, i.is_unique, i.has_filter))
               FROM sys.indexes i JOIN sys.tables t ON t.object_id = i.object_id
              WHERE t.name NOT IN ('Paises', '__EFMigrationsHistory')), '|',
            (SELECT COUNT(*) FROM sys.objects o JOIN sys.tables t ON t.object_id = o.parent_object_id
              WHERE o.type IN ('C', 'F', 'D', 'TR', 'UQ', 'PK') AND t.name NOT IN ('Paises', '__EFMigrationsHistory')))
        """;

    [FatoSqlServer]
    public async Task Migration_V21_cria_e_semeia_Paises_sem_tocar_em_enderecos_e_volta_no_Down()
    {
        var construtor = new SqlConnectionStringBuilder(FatoSqlServerAttribute.Conexao!)
        {
            InitialCatalog = "Lone_Teste_" + Guid.NewGuid().ToString("N")
        };
        // Só banco temporário: nunca o do sistema nem o da homologação.
        Assert.StartsWith("Lone_Teste_", construtor.InitialCatalog, StringComparison.Ordinal);
        var conexao = construtor.ConnectionString;
        await using var db = new LoneDbContext(new DbContextOptionsBuilder<LoneDbContext>().UseSqlServer(conexao).Options);
        var migrador = db.GetService<IMigrator>();
        var v21 = IdV21(); // até a V21, não até a última: migrations futuras não mudam o que este teste prova
        try
        {
            // 1. Esquema anterior + uma pessoa com endereço no Brasil e um no exterior com o código RFB antigo (249).
            await migrador.MigrateAsync(Anterior);
            await using var sql = new SqlConnection(conexao);
            await sql.OpenAsync();
            var pessoa = Guid.NewGuid();
            await MigracaoCompletaTests.InserirAsync(sql, "Pessoas", new() { ["Id"] = pessoa, ["Nome"] = "V2-1 migração", ["Natureza"] = (byte)NaturezaPessoa.Fisica });
            await MigracaoCompletaTests.InserirAsync(sql, "PessoaEnderecos", new()
            {
                ["Id"] = Guid.NewGuid(), ["PessoaId"] = pessoa, ["Cep"] = "35790000", ["Logradouro"] = "Rua Barão", ["Numero"] = "150",
                ["Bairro"] = "Centro", ["Cidade"] = "Curvelo", ["Uf"] = "MG", ["CodigoPais"] = "1058", ["Pais"] = "Brasil"
            });
            await MigracaoCompletaTests.InserirAsync(sql, "PessoaEnderecos", new()
            {
                ["Id"] = Guid.NewGuid(), ["PessoaId"] = pessoa, ["Cep"] = "", ["Logradouro"] = "Main Street", ["Numero"] = "1",
                ["Cidade"] = "Boston", ["Uf"] = "EX", ["CodigoPais"] = "249", ["Pais"] = "Estados Unidos"
            });
            var dados = await TextoAsync(sql, SomaDados);
            var esquema = await TextoAsync(sql, SomaEsquema);
            Assert.Equal(0, await ContarAsync(sql, "SELECT COUNT(*) FROM sys.tables WHERE name = 'Paises'"));

            // 2. Up.
            await migrador.MigrateAsync(v21);
            Assert.Equal(v21, (await db.Database.GetAppliedMigrationsAsync()).Last());
            Assert.Equal(dados, await TextoAsync(sql, SomaDados));     // endereços e pessoas intactos (o 249 não virou 2496)
            Assert.Equal(esquema, await TextoAsync(sql, SomaEsquema)); // nenhuma outra tabela mudou
            await ConferirCatalogoAsync(sql);
            await ConferirRestricoesAsync(sql);

            // 3. Down: a tabela some, o resto fica como estava.
            await migrador.MigrateAsync(Anterior);
            Assert.Equal(0, await ContarAsync(sql, "SELECT COUNT(*) FROM sys.tables WHERE name = 'Paises'"));
            Assert.Equal(dados, await TextoAsync(sql, SomaDados));
            Assert.Equal(esquema, await TextoAsync(sql, SomaEsquema));

            // 4. Up de novo.
            await migrador.MigrateAsync(v21);
            Assert.Equal(dados, await TextoAsync(sql, SomaDados));
            Assert.Equal(esquema, await TextoAsync(sql, SomaEsquema));
            await ConferirCatalogoAsync(sql);
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await db.Database.EnsureDeletedAsync(); // só o banco temporário deste teste
        }
    }

    private static async Task ConferirCatalogoAsync(SqlConnection sql)
    {
        Assert.Equal(261, await ContarAsync(sql, "SELECT COUNT(*) FROM Paises"));
        Assert.Equal(249, await ContarAsync(sql,
            $"SELECT COUNT(*) FROM Paises WHERE VigenciaInicio <= '{Referencia}' AND (VigenciaFim IS NULL OR VigenciaFim >= '{Referencia}')"));
        Assert.Equal(0, await ContarAsync(sql, "SELECT COUNT(*) FROM Paises WHERE DATALENGTH(CodigoPaisNFe) <> 4 OR CodigoPaisNFe NOT LIKE '[0-9][0-9][0-9][0-9]'"));
        Assert.Equal(0, await ContarAsync(sql,
            "SELECT COUNT(*) FROM Paises p WHERE CodigoSucessor IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Paises s WHERE s.CodigoPaisNFe = p.CodigoSucessor)"));
        Assert.Equal("char|4", await TextoAsync(sql,
            "SELECT CONCAT(TYPE_NAME(system_type_id), '|', max_length) FROM sys.columns WHERE object_id = OBJECT_ID('Paises') AND name = 'CodigoPaisNFe'"));

        // Linha a linha igual ao artefato gerado da fonte oficial.
        var esperado = PaisesNFeOficiais.Todos.ToDictionary(p => p.CodigoPaisNFe);
        await using var c = new SqlCommand(
            "SELECT CodigoPaisNFe, NomeFiscal, SituacaoFonte, VigenciaInicio, VigenciaFim, CodigoSucessor, Fonte, VersaoFonte, HashFonte FROM Paises", sql);
        await using var leitor = await c.ExecuteReaderAsync();
        var lidos = 0;
        while (await leitor.ReadAsync())
        {
            lidos++;
            var p = esperado[leitor.GetString(0)];
            Assert.Equal(p.NomeFiscal, leitor.GetString(1));
            Assert.Equal(p.SituacaoFonte, leitor.IsDBNull(2) ? null : leitor.GetString(2));
            Assert.Equal(p.VigenciaInicio, DateOnly.FromDateTime(leitor.GetDateTime(3)));
            Assert.Equal(p.VigenciaFim, leitor.IsDBNull(4) ? (DateOnly?)null : DateOnly.FromDateTime(leitor.GetDateTime(4)));
            Assert.Equal(p.CodigoSucessor, leitor.IsDBNull(5) ? null : leitor.GetString(5));
            Assert.Equal(PaisesNFeOficiais.Fonte, leitor.GetString(6));
            Assert.Equal(PaisesNFeOficiais.VersaoFonte, leitor.GetString(7));
            Assert.Equal(PaisesNFeOficiais.HashFonte, leitor.GetString(8));
        }
        Assert.Equal(261, lidos);
    }

    private static async Task ConferirRestricoesAsync(SqlConnection sql)
    {
        const string Colunas = "INSERT INTO Paises (CodigoPaisNFe, NomeFiscal, VigenciaInicio, VigenciaFim, CodigoSucessor, Fonte, VersaoFonte, HashFonte) VALUES ";
        const string Resto = ", 'X', '1.01', REPLICATE('0', 64))";
        // 547 = CHECK; 2627 = chave primária duplicada.
        Assert.Equal(547, await ErroAsync(sql, Colunas + "('639', 'TESTE', '2006-01-01', NULL, NULL" + Resto));
        Assert.Equal(547, await ErroAsync(sql, Colunas + "('ABCD', 'TESTE', '2006-01-01', NULL, NULL" + Resto));
        Assert.Equal(547, await ErroAsync(sql, Colunas + "('¹058', 'TESTE', '2006-01-01', NULL, NULL" + Resto)); // só dígito ASCII
        Assert.Equal(547, await ErroAsync(sql, Colunas + "('9001', 'TESTE', '2006-01-01', NULL, '9001'" + Resto));
        Assert.Equal(547, await ErroAsync(sql, Colunas + "('9002', 'TESTE', '2006-01-01', NULL, '99'" + Resto));
        Assert.Equal(547, await ErroAsync(sql, Colunas + "('9003', 'TESTE', '2018-05-31', '2006-01-01', NULL" + Resto));
        Assert.Equal(2627, await ErroAsync(sql, Colunas + "('1058', 'TESTE', '2006-01-01', NULL, NULL" + Resto));
        Assert.Equal(261, await ContarAsync(sql, "SELECT COUNT(*) FROM Paises"));
    }

    private static async Task<int> ErroAsync(SqlConnection sql, string comando)
    {
        await using var c = new SqlCommand(comando, sql);
        var erro = await Assert.ThrowsAsync<SqlException>(() => c.ExecuteNonQueryAsync());
        return erro.Number;
    }

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
