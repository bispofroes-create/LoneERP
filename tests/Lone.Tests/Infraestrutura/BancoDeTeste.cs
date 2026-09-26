using System.Text.RegularExpressions;
using Lone.Infrastructure.Persistencia;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lone.Tests.Infraestrutura;

/// <summary>
/// Teste que precisa de um SQL Server de verdade (constraints, gatilhos e o SQL da migração). Sem a variável de ambiente
/// <see cref="Variavel"/> ele é PULADO (e aparece como pulado, nunca como aprovado). Exemplo:
/// LONE_TESTES_SQLSERVER = Server=.\SQLEXPRESS;Trusted_Connection=True;TrustServerCertificate=True
/// Cada classe cria um banco temporário próprio (Lone_Teste_...) e o remove no fim — nunca toca no banco do sistema.
/// </summary>
public sealed class FatoSqlServerAttribute : FactAttribute
{
    public const string Variavel = "LONE_TESTES_SQLSERVER";

    public static string? Conexao => Environment.GetEnvironmentVariable(Variavel) is { Length: > 0 } c ? c : null;

    public FatoSqlServerAttribute()
    {
        if (Conexao is null)
            Skip = $"Defina {Variavel} (ex.: Server=.\\SQLEXPRESS;Trusted_Connection=True;TrustServerCertificate=True) para rodar no SQL Server.";
    }
}

/// <summary>Banco temporário criado pelo modelo atual (EnsureCreated) e, se pedido, com os gatilhos da migração.</summary>
public sealed partial class BancoDeTeste : IAsyncDisposable
{
    private BancoDeTeste(string conexao) => Conexao = conexao;

    public string Conexao { get; }

    public static async Task<BancoDeTeste> CriarAsync(bool comProtecoes)
    {
        var construtor = new SqlConnectionStringBuilder(FatoSqlServerAttribute.Conexao!)
        {
            InitialCatalog = "Lone_Teste_" + Guid.NewGuid().ToString("N")
        };
        var banco = new BancoDeTeste(construtor.ConnectionString);
        await using var db = banco.Contexto();
        await db.Database.EnsureCreatedAsync();
        if (comProtecoes)
            foreach (var lote in Lotes(SqlMigracaoFinalidadesEndereco.CriarProtecoes))
                await db.Database.ExecuteSqlRawAsync(lote);
        return banco;
    }

    public LoneDbContext Contexto() =>
        new(new DbContextOptionsBuilder<LoneDbContext>().UseSqlServer(Conexao).Options);

    /// <summary>Separa o SQL nas linhas "GO" (como a migração do EF faz).</summary>
    public static IEnumerable<string> Lotes(string sql) =>
        LinhaGo().Split(sql).Select(l => l.Trim()).Where(l => l.Length > 0);

    [GeneratedRegex(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex LinhaGo();

    public async ValueTask DisposeAsync()
    {
        await using var db = Contexto();
        await db.Database.EnsureDeletedAsync(); // só o banco temporário deste teste
    }
}
