using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Lone.Infrastructure.Persistencia;

/// <summary>
/// Usada só pelas ferramentas do EF (Add-Migration, Update-Database, Get-Migration, Script-Migration) no Visual Studio.
/// O aplicativo em si lê a conexão do appsettings.json.
/// Incidente P18: o banco alvo tem de ser EXPLÍCITO na variável <see cref="Variavel"/>. Sem ela, nada cai mais no LoneERP:
/// o que só gera código (Add-Migration, Script-Migration) continua funcionando; o que precisa do banco (Update-Database,
/// Get-Migration, Remove-Migration sem -Force) para com uma mensagem clara, antes de abrir qualquer conexão.
/// </summary>
public class LoneDbContextFactory : IDesignTimeDbContextFactory<LoneDbContext>
{
    public const string Variavel = "LONE_CONEXAO";

    /// <summary>Conexão de mentira, só para o EF montar o modelo; qualquer tentativa de abri-la é barrada antes.</summary>
    internal const string SemAlvo = "Server=LONE_CONEXAO-nao-definida;Database=LONE_CONEXAO-nao-definida;Trusted_Connection=True;TrustServerCertificate=True";

    public const string MensagemSemAlvo =
        "Banco alvo não informado: defina a variável LONE_CONEXAO com a conexão do banco que deve ser usado " +
        "(ex.: no Console do Gerenciador de Pacotes, $env:LONE_CONEXAO = 'Server=.\\SQLEXPRESS;Database=<banco>;Trusted_Connection=True;TrustServerCertificate=True'). " +
        "As ferramentas do EF não escolhem mais um banco por conta própria.";

    public LoneDbContext CreateDbContext(string[] args) => Criar(Environment.GetEnvironmentVariable(Variavel));

    internal static LoneDbContext Criar(string? conexao)
    {
        var opcoes = new DbContextOptionsBuilder<LoneDbContext>();
        if (string.IsNullOrWhiteSpace(conexao))
            opcoes.UseSqlServer(SemAlvo).AddInterceptors(new BarrarConexaoSemAlvo());
        else
            opcoes.UseSqlServer(conexao);
        return new LoneDbContext(opcoes.Options);
    }

    /// <summary>Barra a abertura da conexão quando o alvo não foi informado (fail closed).</summary>
    private sealed class BarrarConexaoSemAlvo : DbConnectionInterceptor
    {
        public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result) =>
            throw new InvalidOperationException(MensagemSemAlvo);

        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData,
                                                                           InterceptionResult result, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(MensagemSemAlvo);
    }
}
