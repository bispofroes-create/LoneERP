using Lone.Application.Infraestrutura;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Servicos;

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
                "Add-Migration Inicial -Project Lone.Infrastructure -StartupProject Lone.Api -OutputDir Persistencia/Migracoes");

        await db.Database.MigrateAsync(ct);
    }

    public async Task<bool> DisponivelAsync(CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);
        return await db.Database.CanConnectAsync(ct);
    }
}
