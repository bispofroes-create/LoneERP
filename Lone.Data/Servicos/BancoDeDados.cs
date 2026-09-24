using Lone.Aplicacao.Infraestrutura;
using Microsoft.EntityFrameworkCore;

namespace Lone.Data.Servicos;

public class BancoDeDados : IBancoDeDados
{
    private readonly IDbContextFactory<LoneDbContext> _fabrica;

    public BancoDeDados(IDbContextFactory<LoneDbContext> fabrica)
    {
        _fabrica = fabrica;
    }

    public async Task PrepararAsync(CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);

        if (!db.Database.GetMigrations().Any())
            throw new InvalidOperationException(
                "Nenhuma migração encontrada. No Console do Gerenciador de Pacotes, rode:\n" +
                "Add-Migration Inicial -Project Lone.Data -StartupProject Lone.Data");

        await db.Database.MigrateAsync(ct);
    }
}
