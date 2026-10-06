using Lone.Domain.Enums;
using Lone.Infrastructure.Persistencia;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Lone.Tests.Infraestrutura;

/// <summary>
/// F6: a migration da reconferência aplicada num banco TEMPORÁRIO (nunca o do sistema). Só cria o índice
/// (CepSituacao, CepConferidoEm) usado pela seleção; nenhum dado muda; o Down só remove o índice; o Up de novo funciona.
/// Pulado sem LONE_TESTES_SQLSERVER.
/// </summary>
public class MigracaoF6ReconferenciaCepTests
{
    private const string Anterior = "20261005233438_F3PersistenciaCep";
    private const string Indice = "IX_PessoaEnderecos_CepSituacao_CepConferidoEm";

    [FatoSqlServer]
    public async Task Migration_F6_so_cria_o_indice_e_nao_muda_dados()
    {
        var conexao = new SqlConnectionStringBuilder(FatoSqlServerAttribute.Conexao!)
        {
            InitialCatalog = "Lone_Teste_" + Guid.NewGuid().ToString("N")
        }.ConnectionString;
        await using var db = new LoneDbContext(new DbContextOptionsBuilder<LoneDbContext>().UseSqlServer(conexao).Options);
        var migrador = db.GetService<IMigrator>();
        try
        {
            await migrador.MigrateAsync(Anterior);
            await using var sql = new SqlConnection(conexao);
            await sql.OpenAsync();
            var pessoa = Guid.NewGuid();
            var endereco = Guid.NewGuid();
            await MigracaoCompletaTests.InserirAsync(sql, "Pessoas", new()
            {
                ["Id"] = pessoa, ["Nome"] = "F6 " + pessoa.ToString("N")[..6], ["Natureza"] = (byte)NaturezaPessoa.Juridica
            });
            await MigracaoCompletaTests.InserirAsync(sql, "PessoaEnderecos", new()
            {
                ["Id"] = endereco, ["PessoaId"] = pessoa, ["Cep"] = "35790000", ["Logradouro"] = "Rua Barão", ["Numero"] = "150",
                ["Bairro"] = "Centro", ["Cidade"] = "Curvelo", ["Uf"] = "MG", ["CodigoPais"] = "1058", ["Pais"] = "Brasil",
                ["CepSituacao"] = (byte)2
            });
            var antes = await TextoAsync(sql, Soma(endereco));
            Assert.Equal(0, await ContarAsync(sql, ContarIndice));

            await migrador.MigrateAsync();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.False(db.Database.HasPendingModelChanges()); // a migration corresponde ao modelo atual
            Assert.Equal(1, await ContarAsync(sql, ContarIndice));
            Assert.Equal(antes, await TextoAsync(sql, Soma(endereco)));

            await migrador.MigrateAsync(Anterior);
            Assert.Equal(0, await ContarAsync(sql, ContarIndice));
            Assert.Equal(antes, await TextoAsync(sql, Soma(endereco)));
            await migrador.MigrateAsync();
            Assert.Equal(1, await ContarAsync(sql, ContarIndice));
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await db.Database.EnsureDeletedAsync(); // só o banco temporário deste teste
        }
    }

    private static readonly string ContarIndice =
        $"SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID('PessoaEnderecos') AND name = '{Indice}'";

    /// <summary>Os dados do endereço, inclusive a conferência (nenhum pode mudar).</summary>
    private static string Soma(Guid endereco) =>
        $"SELECT CONCAT(Cep, '|', Logradouro, '|', Numero, '|', Bairro, '|', Cidade, '|', Uf, '|', CodigoPais, '|', Ativo, '|', CepSituacao, '|', CepFonte, '|', CepConferidoEm) FROM PessoaEnderecos WHERE Id = '{endereco}'";

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
