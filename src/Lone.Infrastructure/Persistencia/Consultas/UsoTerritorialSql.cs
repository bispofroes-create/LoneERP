using Lone.Application.Seguranca;
using Lone.Application.Territorios;
using Lone.Infrastructure.Persistencia.Servicos;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Consultas;

/// <summary>
/// Uso operacional dos territórios (decisões T14/T18): território com versão de regra publicada ou com atribuição de
/// cliente, vigente ou histórica. Linhas anuladas (Ativo = 0: começavam no mesmo dia ou vinham de operação desfeita) não
/// contam: nunca valeram. Na 2b-1a esta classe respondia "nenhum" (as tabelas ainda não existiam); na 2b-1b consulta as
/// tabelas do motor, e nenhuma regra, serviço ou tela precisou mudar por isso.
/// </summary>
public sealed class UsoTerritorialSql : ServicoDadosBase, IUsoTerritorial
{
    public UsoTerritorialSql(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario) : base(fabrica, usuario) { }

    public async Task<IReadOnlySet<Guid>> TerritoriosComUsoAsync(Guid mapaId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await TerritoriosComUsoAsync(db, mapaId, ct);
    }

    public async Task<IReadOnlySet<Guid>> MapasEmUsoAsync(CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return (await db.MapasTerritoriais.AsNoTracking()
            .Where(m => db.RegrasTerritorio.Any(r => r.MapaId == m.Id && r.Ativo) || db.AtribuicoesTerritorio.Any(a => a.MapaId == m.Id && a.Ativo))
            .Select(m => m.Id).ToListAsync(ct)).ToHashSet();
    }

    /// <summary>
    /// A mesma leitura num contexto já aberto: os repositórios a repetem dentro da transação, depois das travas, para decidir
    /// com o uso confirmado naquele instante (L3), não com o que a tela viu.
    /// </summary>
    internal static async Task<IReadOnlySet<Guid>> TerritoriosComUsoAsync(LoneDbContext db, Guid mapaId, CancellationToken ct) =>
        (await db.Territorios.AsNoTracking()
            .Where(t => t.MapaId == mapaId &&
                        (db.RegrasTerritorio.Any(r => r.MapaId == mapaId && r.TerritorioId == t.Id && r.Ativo) ||
                         db.AtribuicoesTerritorio.Any(a => a.MapaId == mapaId && a.TerritorioId == t.Id && a.Ativo)))
            .Select(t => t.Id).ToListAsync(ct)).ToHashSet();

    internal static async Task<bool> MapaEmUsoAsync(LoneDbContext db, Guid mapaId, CancellationToken ct) =>
        await db.RegrasTerritorio.AnyAsync(r => r.MapaId == mapaId && r.Ativo, ct) ||
        await db.AtribuicoesTerritorio.AnyAsync(a => a.MapaId == mapaId && a.Ativo, ct);
}
