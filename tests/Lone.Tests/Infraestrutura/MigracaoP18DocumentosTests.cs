using Lone.Domain.Documentos;
using Lone.Domain.Enums;
using Lone.Infrastructure.Persistencia;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Lone.Tests.Infraestrutura;

/// <summary>
/// P1-8A: a migration REAL dos documentos aplicada num banco TEMPORÁRIO (nunca o do sistema), com dados gravados no esquema
/// anterior: Up (metadados com paridade, tipos do usuário neutros, nome de sistema renomeado intacto, número comparável
/// igual ao do domínio, nenhuma chave de unicidade, índices), Down (volta ao esquema anterior sem perder documento) e Up de
/// novo. Pulado sem LONE_TESTES_SQLSERVER.
/// </summary>
public class MigracaoP18DocumentosTests
{
    private const string Anterior = "20261006022548_F6ReconferenciaCep";
    private static readonly Guid Rg = TiposDocumentoSistema.Id(TipoDocumento.Rg);
    private static readonly Guid Cnh = TiposDocumentoSistema.Id(TipoDocumento.Cnh);
    private static readonly Guid Outro = TiposDocumentoSistema.Id(TipoDocumento.Outro);

    private static readonly string[] Numeros = ["12.345.678-9", "ab-12", "Ção 0012", "#-#", "", "MG 12/345", "ЖД-77", "0001"];

    /// <summary>As colunas antigas dos documentos e dos tipos: nenhuma pode mudar em Up nem em Down.</summary>
    private const string Soma = """
        SELECT CONCAT(
            (SELECT COUNT(*) FROM PessoaDocumentos), '|',
            (SELECT CHECKSUM_AGG(CHECKSUM(Id, PessoaId, TipoDocumentoId, Tipo, Ativo, Numero, Uf, OrgaoEmissor, ValidoAte)) FROM PessoaDocumentos), '|',
            (SELECT COUNT(*) FROM TiposDocumento), '|',
            (SELECT CHECKSUM_AGG(CHECKSUM(Id, Nome, Ordem, Ativo, TipoSistema, ExigeValidade, DiasAvisoVencimento)) FROM TiposDocumento))
        """;

    [FatoSqlServer]
    public async Task Migration_P18_preenche_o_numero_comparavel_semeia_com_paridade_e_volta_no_Down()
    {
        var conexao = new SqlConnectionStringBuilder(FatoSqlServerAttribute.Conexao!)
        {
            InitialCatalog = "Lone_Teste_" + Guid.NewGuid().ToString("N")
        }.ConnectionString;
        await using var db = new LoneDbContext(new DbContextOptionsBuilder<LoneDbContext>().UseSqlServer(conexao).Options);
        var migrador = db.GetService<IMigrator>();
        try
        {
            // 1. Esquema anterior + dados: um tipo do usuário, a CNH renomeada pelo cliente e documentos variados.
            await migrador.MigrateAsync(Anterior);
            await using var sql = new SqlConnection(conexao);
            await sql.OpenAsync();
            var alvara = Guid.NewGuid();
            await MigracaoCompletaTests.InserirAsync(sql, "TiposDocumento", new()
            {
                ["Id"] = alvara, ["Nome"] = "Alvará P18", ["Ordem"] = 20, ["Ativo"] = true, ["ExigeValidade"] = true, ["DiasAvisoVencimento"] = 60
            });
            await ExecutarAsync(sql, $"UPDATE TiposDocumento SET Nome = N'Carteira de motorista' WHERE Id = '{Cnh}'");
            var pessoa = Guid.NewGuid();
            await MigracaoCompletaTests.InserirAsync(sql, "Pessoas", new() { ["Id"] = pessoa, ["Nome"] = "P18 migração", ["Natureza"] = (byte)NaturezaPessoa.Fisica });
            Guid[] tipos = [Rg, Cnh, alvara, Outro];
            for (var i = 0; i < Numeros.Length; i++)
                await MigracaoCompletaTests.InserirAsync(sql, "PessoaDocumentos", new()
                {
                    ["Id"] = Guid.NewGuid(), ["PessoaId"] = pessoa, ["TipoDocumentoId"] = tipos[i % tipos.Length], ["Tipo"] = (byte)9,
                    ["Numero"] = Numeros[i], ["Ativo"] = i % 3 != 2, ["Uf"] = i % 2 == 0 ? "SP" : (object)DBNull.Value
                });
            var antes = await TextoAsync(sql, Soma);

            // 2. Up.
            await migrador.MigrateAsync();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.False(db.Database.HasPendingModelChanges()); // a migration corresponde ao modelo atual
            Assert.Equal(antes, await TextoAsync(sql, Soma));   // nada antigo mudou (nome renomeado inclusive)
            Assert.Equal("Carteira de motorista", await TextoAsync(sql, $"SELECT Nome FROM TiposDocumento WHERE Id = '{Cnh}'"));
            await ConferirDepoisDoUpAsync(sql, alvara);

            // 3. Down: volta ao esquema anterior sem perder documento nem tipo.
            await migrador.MigrateAsync(Anterior);
            Assert.Equal(antes, await TextoAsync(sql, Soma));
            Assert.Equal(0, await ContarAsync(sql, "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('PessoaDocumentos') AND name IN ('NumeroNormalizado', 'ChaveUnicidade')"));
            Assert.Equal(0, await ContarAsync(sql, "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('TiposDocumento') AND name LIKE 'Aplica%'"));

            // 4. Up de novo.
            await migrador.MigrateAsync();
            Assert.Equal(antes, await TextoAsync(sql, Soma));
            await ConferirDepoisDoUpAsync(sql, alvara);
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await db.Database.EnsureDeletedAsync(); // só o banco temporário deste teste
        }
    }

    private static async Task ConferirDepoisDoUpAsync(SqlConnection sql, Guid alvara)
    {
        // Número comparável igual ao do domínio, em todos (ativos e inativos); nenhuma chave de unicidade.
        await using (var c = new SqlCommand("SELECT Numero, NumeroNormalizado, ChaveUnicidade FROM PessoaDocumentos", sql))
        await using (var leitor = await c.ExecuteReaderAsync())
        {
            var n = 0;
            while (await leitor.ReadAsync())
            {
                n++;
                Assert.Equal(NumeroDocumento.Normalizar(leitor.GetString(0)), leitor.GetString(1));
                Assert.True(leitor.IsDBNull(2));
            }
            Assert.Equal(Numeros.Length, n);
        }

        // Semente dos cinco de sistema = TiposDocumentoSistema.Semente; tipo do usuário com o comportamento de sempre.
        await using (var c = new SqlCommand("""
            SELECT Id, TipoSistema, AplicaPessoaFisica, AplicaPessoaJuridica, AplicaEstrangeiro, UsoOrgaoEmissor, UsoUf, UsoEmissao,
                   FormatoNumero, Unicidade, TamanhoMinimoNumero, TamanhoMaximoNumero
            FROM TiposDocumento
            """, sql))
        await using (var leitor = await c.ExecuteReaderAsync())
        {
            var vistos = 0;
            while (await leitor.ReadAsync())
            {
                var lidos = (leitor.GetBoolean(2), leitor.GetBoolean(3), leitor.GetBoolean(4), (UsoCampoDocumento)leitor.GetByte(5),
                             (UsoCampoDocumento)leitor.GetByte(6), (UsoCampoDocumento)leitor.GetByte(7), (FormatoNumeroDocumento)leitor.GetByte(8),
                             (UnicidadeDocumento)leitor.GetByte(9));
                Assert.True(leitor.IsDBNull(10) && leitor.IsDBNull(11));
                if (!leitor.IsDBNull(1))
                {
                    var s = TiposDocumentoSistema.Semente((TipoDocumento)leitor.GetByte(1));
                    Assert.Equal((s.Fisica, s.Juridica, s.Estrangeiro, s.Orgao, s.Uf, s.Emissao, s.Formato, s.Unicidade), lidos);
                    vistos++;
                }
                else if (leitor.GetGuid(0) == alvara)
                {
                    Assert.Equal((true, true, true, UsoCampoDocumento.Oculto, UsoCampoDocumento.Oculto, UsoCampoDocumento.Opcional,
                                  FormatoNumeroDocumento.Livre, UnicidadeDocumento.Nenhuma), lidos);
                    vistos++;
                }
            }
            Assert.Equal(6, vistos);
        }

        Assert.Equal(1, await ContarAsync(sql, """
            SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID('PessoaDocumentos') AND name = 'IX_PessoaDocumentos_ChaveUnicidade'
              AND is_unique = 1 AND has_filter = 1
            """));
        Assert.Equal(1, await ContarAsync(sql,
            "SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID('PessoaDocumentos') AND name = 'IX_PessoaDocumentos_NumeroNormalizado_TipoDocumentoId'"));
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

    private static async Task<string> TextoAsync(SqlConnection sql, string consulta)
    {
        await using var c = new SqlCommand(consulta, sql);
        return (string)(await c.ExecuteScalarAsync())!;
    }
}
