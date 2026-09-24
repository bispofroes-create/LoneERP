using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Lone.Data;

/// <summary>
/// Usada só pelas ferramentas do EF (Add-Migration, Update-Database) no Visual Studio.
/// O aplicativo em si lê a conexão do appsettings.json.
/// </summary>
public class LoneDbContextFactory : IDesignTimeDbContextFactory<LoneDbContext>
{
    public LoneDbContext CreateDbContext(string[] args)
    {
        var conexao = Environment.GetEnvironmentVariable("LONE_CONEXAO")
            ?? @"Server=.\SQLEXPRESS;Database=LoneERP;Trusted_Connection=True;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<LoneDbContext>()
            .UseSqlServer(conexao)
            .Options;

        return new LoneDbContext(options);
    }
}
