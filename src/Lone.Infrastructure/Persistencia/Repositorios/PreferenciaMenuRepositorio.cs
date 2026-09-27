using Lone.Application.Menu;
using Lone.Application.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Infrastructure.Persistencia.Servicos;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Repositorios;

/// <summary>Favoritos e recentes do menu. Uma linha por usuário + rota; nunca apaga.</summary>
public class PreferenciaMenuRepositorio : ServicoDadosBase, IPreferenciaMenuRepositorio
{
    public PreferenciaMenuRepositorio(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario) : base(fabrica, usuario) { }

    public async Task<List<PreferenciaMenu>> ListarAsync(Guid usuarioId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.PreferenciasMenu.AsNoTracking().Where(p => p.UsuarioId == usuarioId).ToListAsync(ct);
    }

    public async Task AtualizarAsync(Guid usuarioId, string rota, Action<PreferenciaMenu> alterar, CancellationToken ct)
    {
        try
        {
            await GravarAsync(usuarioId, rota, alterar, ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // Outro aparelho do mesmo usuário criou a linha ao mesmo tempo: agora ela existe, então é só alterar.
            await GravarAsync(usuarioId, rota, alterar, ct);
        }
    }

    private async Task GravarAsync(Guid usuarioId, string rota, Action<PreferenciaMenu> alterar, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var linha = await db.PreferenciasMenu.FirstOrDefaultAsync(p => p.UsuarioId == usuarioId && p.Rota == rota, ct);
        if (linha is null)
        {
            linha = new PreferenciaMenu { Id = IdSequencial.Novo(), UsuarioId = usuarioId, Rota = rota };
            db.PreferenciasMenu.Add(linha);
        }
        alterar(linha);
        await db.SaveChangesAsync(ct);
    }
}
