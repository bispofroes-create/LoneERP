using Lone.Infrastructure.Persistencia;
using Lone.Infrastructure.Persistencia.Servicos;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lone.Tests.Infraestrutura;

/// <summary>
/// Incidente P18 (06/10/2026): migração em banco persistente nunca acontece só porque a API iniciou, e as ferramentas do
/// EF não escolhem mais o LoneERP sozinhas. Os testes com SQL Server usam só banco TEMPORÁRIO (nunca o LoneERP).
/// </summary>
public class ProtecaoMigracaoTests
{
    private static IConfiguration Configuracao(string? aplicarAoIniciar) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [MigracaoAoIniciar.ChaveConfiguracao] = aplicarAoIniciar
        }).Build();

    [Theory]
    [InlineData(null, null, DecisaoMigracaoAoIniciar.NaoPedida)]           // padrão: desligada
    [InlineData("false", "true", DecisaoMigracaoAoIniciar.NaoPedida)]      // a variável sozinha não basta
    [InlineData("talvez", "true", DecisaoMigracaoAoIniciar.NaoPedida)]     // configuração inválida não pede
    [InlineData("true", null, DecisaoMigracaoAoIniciar.Bloqueada)]         // variável ausente
    [InlineData("true", "", DecisaoMigracaoAoIniciar.Bloqueada)]
    [InlineData("true", "false", DecisaoMigracaoAoIniciar.Bloqueada)]
    [InlineData("true", "1", DecisaoMigracaoAoIniciar.Bloqueada)]          // valor inválido
    [InlineData("true", "sim", DecisaoMigracaoAoIniciar.Bloqueada)]
    [InlineData("true", " true", DecisaoMigracaoAoIniciar.Bloqueada)]
    [InlineData("true", "true", DecisaoMigracaoAoIniciar.Permitida)]       // só o opt-in explícito
    [InlineData("True", "TRUE", DecisaoMigracaoAoIniciar.Permitida)]
    public void So_configuracao_mais_opt_in_explicito_permitem_migrar_ao_iniciar(string? configuracao, string? variavel, DecisaoMigracaoAoIniciar esperada) =>
        Assert.Equal(esperada, MigracaoAoIniciar.Decidir(Configuracao(configuracao), variavel));

    [Fact]
    public void Configuracao_de_Development_do_repositorio_nao_pede_migracao_ao_iniciar()
    {
        var api = PastaDaApi();
        var configuracao = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(api, "appsettings.json"))
            .AddJsonFile(Path.Combine(api, "appsettings.Development.json"))
            .Build();

        Assert.Equal(DecisaoMigracaoAoIniciar.NaoPedida, MigracaoAoIniciar.Decidir(configuracao, null));
        Assert.Equal(DecisaoMigracaoAoIniciar.NaoPedida, MigracaoAoIniciar.Decidir(configuracao, "true")); // nem com a variável
    }

    /// <summary>src/Lone.Api a partir da pasta dos binários dos testes.</summary>
    private static string PastaDaApi()
    {
        for (var pasta = new DirectoryInfo(AppContext.BaseDirectory); pasta is not null; pasta = pasta.Parent)
        {
            var api = Path.Combine(pasta.FullName, "src", "Lone.Api");
            if (File.Exists(Path.Combine(api, "appsettings.Development.json"))) return api;
        }
        throw new InvalidOperationException("Pasta src/Lone.Api não encontrada a partir dos testes.");
    }

    [Fact]
    public void Ferramentas_do_EF_sem_LONE_CONEXAO_nao_caem_no_LoneERP_e_param_antes_de_conectar()
    {
        using var db = LoneDbContextFactory.Criar(null);

        Assert.DoesNotContain("LoneERP", db.Database.GetConnectionString(), StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(db.Model.GetEntityTypes()); // o modelo continua disponível (Add-Migration e Script-Migration não conectam)
        var erro = Assert.Throws<InvalidOperationException>(() => db.Database.OpenConnection());
        Assert.Equal(LoneDbContextFactory.MensagemSemAlvo, erro.Message);
        Assert.Contains("LONE_CONEXAO", erro.Message);

        using var embranco = LoneDbContextFactory.Criar("   ");
        Assert.Throws<InvalidOperationException>(() => embranco.Database.OpenConnection());
    }

    [Fact]
    public void Ferramentas_do_EF_com_LONE_CONEXAO_usam_exatamente_o_alvo_informado()
    {
        const string alvo = "Server=.\\SQLEXPRESS;Database=Lone_Alvo_Explicito;Trusted_Connection=True;TrustServerCertificate=True";
        using var db = LoneDbContextFactory.Criar(alvo);
        Assert.Contains("Lone_Alvo_Explicito", db.Database.GetConnectionString());
    }

    [FatoSqlServer]
    public async Task Banco_com_migracao_pendente_nao_e_alterado_sem_opt_in_e_so_migra_com_ele()
    {
        var conexao = new SqlConnectionStringBuilder(FatoSqlServerAttribute.Conexao!)
        {
            InitialCatalog = "Lone_Teste_" + Guid.NewGuid().ToString("N")
        }.ConnectionString;
        await using var db = new LoneDbContext(new DbContextOptionsBuilder<LoneDbContext>().UseSqlServer(conexao).Options);
        try
        {
            await db.GetService<IMigrator>().MigrateAsync("20261006022548_F6ReconferenciaCep"); // a P18 fica pendente
            var banco = new BancoDeDados(new FabricaFixa(conexao));

            foreach (var (configuracao, variavel) in new (string?, string?)[] { (null, null), ("true", null), ("true", "false"), ("true", "1"), ("false", "true") })
            {
                var resultado = await MigracaoAoIniciar.ExecutarAsync(Configuracao(configuracao), variavel, banco, NullLogger.Instance);
                Assert.False(resultado.Aplicou);
                Assert.Contains("20261006220248_P18DocumentosMetadados", resultado.Pendentes);
                Assert.Equal("20261006022548_F6ReconferenciaCep", (await db.Database.GetAppliedMigrationsAsync()).Last()); // nada mudou
            }

            var permitida = await MigracaoAoIniciar.ExecutarAsync(Configuracao("true"), "true", banco, NullLogger.Instance);
            Assert.True(permitida.Aplicou);
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await db.Database.EnsureDeletedAsync(); // só o banco temporário deste teste
        }
    }

    private sealed class FabricaFixa(string conexao) : IDbContextFactory<LoneDbContext>
    {
        public LoneDbContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<LoneDbContext>().UseSqlServer(conexao).Options);
    }
}
