using Lone.Domain.Enums;
using Lone.Domain.Privacidade;
using Lone.Infrastructure.Persistencia;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Lone.Tests.Infraestrutura;

/// <summary>
/// Fase 3: a migration REAL (PrivacidadeConsentimentos, com o SQL inserido por Ferramentas/inserir-sql-privacidade.py)
/// num banco TEMPORÁRIO. Consentimentos antigos (por canal) viram "Registro anterior" com tudo preservado; nada é
/// inventado (Marketing do e-mail e "Aceita comunicações" não viram consentimento). Pulado sem LONE_TESTES_SQLSERVER.
/// </summary>
public class MigracaoPrivacidadeTests
{
    private const string Anterior = "20260926095739_EstruturaEmpresarial";

    [FatoSqlServer]
    public async Task Consentimentos_antigos_viram_registro_anterior_sem_inventar_nada()
    {
        var conexao = new SqlConnectionStringBuilder(FatoSqlServerAttribute.Conexao!)
        {
            InitialCatalog = "Lone_Teste_" + Guid.NewGuid().ToString("N")
        }.ConnectionString;
        await using var db = new LoneDbContext(new DbContextOptionsBuilder<LoneDbContext>().UseSqlServer(conexao).Options);
        var migrador = db.GetService<IMigrator>();
        try
        {
            // 1. Esquema anterior + dados no formato antigo (um consentimento por canal, sem finalidade).
            await migrador.MigrateAsync(Anterior);
            await using var sql = new SqlConnection(conexao);
            await sql.OpenAsync();
            var pessoa = Guid.NewGuid();
            await MigracaoCompletaTests.InserirAsync(sql, "Pessoas", new() { ["Id"] = pessoa, ["Nome"] = "Ana", ["Natureza"] = (byte)NaturezaPessoa.Fisica });
            var concedido = Guid.NewGuid();
            var revogado = Guid.NewGuid();
            var concedidoEm = new DateTime(2026, 5, 1, 10, 0, 0);
            await MigracaoCompletaTests.InserirAsync(sql, "PessoaConsentimentos", new()
            {
                ["Id"] = concedido, ["PessoaId"] = pessoa, ["Canal"] = (byte)CanalComunicacao.Email, ["Concedido"] = true,
                ["ConcedidoEm"] = concedidoEm, ["Origem"] = "Balcão"
            });
            await MigracaoCompletaTests.InserirAsync(sql, "PessoaConsentimentos", new()
            {
                ["Id"] = revogado, ["PessoaId"] = pessoa, ["Canal"] = (byte)CanalComunicacao.WhatsApp, ["Concedido"] = false,
                ["ConcedidoEm"] = concedidoEm, ["RevogadoEm"] = concedidoEm.AddDays(10)
            });
            // E-mail com "Uso para marketing" e "Aceita comunicações": não pode virar consentimento.
            await MigracaoCompletaTests.InserirAsync(sql, "PessoaMeiosContato", new()
            {
                ["Id"] = Guid.NewGuid(), ["PessoaId"] = pessoa, ["Tipo"] = (byte)TipoContato.Email, ["Valor"] = "ana@exemplo.com.br",
                ["Finalidades"] = (short)FinalidadeEmail.Marketing, ["PermiteComunicacao"] = true, ["Ativo"] = true
            });

            // 2. Migração até o fim.
            await migrador.MigrateAsync();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.False(db.Database.HasPendingModelChanges()); // a migration corresponde ao modelo atual

            // 3. Resultado: os dois viraram "Registro anterior", com canal, datas, situação e origem preservados.
            var anterior = FinalidadesTratamentoIniciais.Id(FinalidadesTratamentoIniciais.RegistroAnterior);
            var consentimentos = await db.PessoaConsentimentos.AsNoTracking().ToListAsync();
            Assert.Equal(2, consentimentos.Count); // nenhum criado
            Assert.All(consentimentos, c => Assert.Equal(anterior, c.FinalidadeId));
            var email = consentimentos.Single(c => c.Id == concedido);
            Assert.Equal(CanalComunicacao.Email, email.Canal);
            Assert.True(email.Concedido);
            Assert.Equal(concedidoEm, email.ConcedidoEm);
            Assert.Equal("Balcão", email.Origem);
            Assert.Null(email.Motivo);
            Assert.Null(email.VersaoTermo);
            Assert.Null(email.ConcedidoPor);
            var whatsapp = consentimentos.Single(c => c.Id == revogado);
            Assert.False(whatsapp.Concedido);
            Assert.Equal(concedidoEm.AddDays(10), whatsapp.RevogadoEm);

            // 4. Finalidades iniciais e proteções.
            var finalidades = await db.FinalidadesTratamento.AsNoTracking().ToListAsync();
            Assert.Equal(FinalidadesTratamentoIniciais.Todas.Select(t => t.Id).Order(), finalidades.Select(f => f.Id).Order());
            var checks = new List<string>();
            await using (var cmd = new SqlCommand("SELECT name FROM sys.check_constraints", sql))
            await using (var leitor = await cmd.ExecuteReaderAsync())
                while (await leitor.ReadAsync()) checks.Add(leitor.GetString(0));
            Assert.Contains(SqlMigracaoPrivacidade.CheckSistemaAtiva, checks);
            Assert.Contains(SqlMigracaoPrivacidade.CheckRevogadoForaDeVigor, checks);

            // 5. Índice de vigência: dois períodos em vigor para a mesma finalidade e canal são recusados pelo banco.
            var marketing = FinalidadesTratamentoIniciais.Id(FinalidadesTratamentoIniciais.Marketing);
            await using (var i1 = new SqlCommand(
                "INSERT INTO PessoaConsentimentos (Id, PessoaId, FinalidadeId, Canal, Concedido, CriadoEm) VALUES (NEWID(), @p, @f, NULL, 1, SYSUTCDATETIME())", sql))
            {
                i1.Parameters.AddWithValue("@p", pessoa);
                i1.Parameters.AddWithValue("@f", marketing);
                await i1.ExecuteNonQueryAsync();
            }
            var duplicado = await Assert.ThrowsAsync<SqlException>(async () =>
            {
                await using var i2 = new SqlCommand(
                    "INSERT INTO PessoaConsentimentos (Id, PessoaId, FinalidadeId, Canal, Concedido, CriadoEm) VALUES (NEWID(), @p, @f, NULL, 1, SYSUTCDATETIME())", sql);
                i2.Parameters.AddWithValue("@p", pessoa);
                i2.Parameters.AddWithValue("@f", marketing);
                await i2.ExecuteNonQueryAsync();
            });
            Assert.Contains(SqlMigracaoPrivacidade.IndiceEmVigor, duplicado.Message);
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await db.Database.EnsureDeletedAsync(); // só o banco temporário deste teste
        }
    }
}
