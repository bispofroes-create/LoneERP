using Lone.Application.Parametros;
using Lone.Application.Seguranca;
using Lone.Domain.Entidades;
using Lone.Infrastructure.Persistencia.Servicos;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Repositorios;

/// <summary>Prazos de período (SQL Server via EF Core). Nunca apaga.</summary>
public class PrazoPeriodoRepositorio : ServicoDadosBase, IPrazoPeriodoRepositorio
{
    public PrazoPeriodoRepositorio(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario) : base(fabrica, usuario) { }

    public async Task<List<PrazoPeriodo>> ListarAsync(CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.PrazosPeriodo.AsNoTracking().ToListAsync(ct);
    }

    public async Task<PrazoPeriodo?> ObterAsync(Guid id, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.PrazosPeriodo.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task SalvarAsync(PrazoPeriodo item, bool novo, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        await GravacaoSimples.SalvarAsync(db, db.PrazosPeriodo, item, novo, "Já existe este prazo.", ct);
    }
}
