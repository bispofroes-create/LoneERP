using Lone.Application.Colaboradores;
using Lone.Application.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Validacao;
using Lone.Infrastructure.Persistencia.Servicos;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Repositorios;

/// <summary>Centro de custo (SQL Server via EF Core). Nunca apaga.</summary>
public class CentroCustoRepositorio : ServicoDadosBase, ICentroCustoRepositorio
{
    public CentroCustoRepositorio(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario) : base(fabrica, usuario) { }

    public async Task<List<CentroCusto>> ListarAsync(CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.CentrosCusto.AsNoTracking().ToListAsync(ct);
    }

    public async Task<CentroCusto?> ObterAsync(Guid id, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.CentrosCusto.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<Dictionary<Guid, CentroCusto>> ObterVariosAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return new();
        await using var db = await AbrirAsync(ct);
        var lista = ids.Distinct().ToList();
        return await db.CentrosCusto.AsNoTracking().Where(x => lista.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
    }

    public async Task<Dictionary<Guid, int>> ContarUsosAsync(Guid? somenteId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var lotacoes = db.LotacoesColaborador.AsNoTracking().Where(l => l.FimEm == null && l.CentroCustoId != null);
        if (somenteId is { } id) lotacoes = lotacoes.Where(l => l.CentroCustoId == id);
        return await lotacoes.GroupBy(l => l.CentroCustoId!.Value)
            .Select(g => new { g.Key, Quantidade = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Quantidade, ct);
    }

    public async Task SalvarAsync(CentroCusto item, bool novo, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        if (novo)
        {
            if (item.Id == Guid.Empty) item.Id = IdSequencial.Novo();
            db.CentrosCusto.Add(item);
        }
        else
        {
            var atual = await db.CentrosCusto.FirstOrDefaultAsync(x => x.Id == item.Id, ct) ?? throw new ConflitoDeEdicaoException();
            var versaoAberta = item.Versao;
            item.Versao = atual.Versao;
            item.CriadoEm = atual.CriadoEm;
            item.AtualizadoEm = atual.AtualizadoEm;

            var entrada = db.Entry(atual);
            entrada.CurrentValues.SetValues(item);
            entrada.Property(x => x.Versao).OriginalValue = versaoAberta;
            atual.ReceberEventosDe(item);
            entrada.Property(x => x.AtualizadoEm).IsModified = true;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflitoDeEdicaoException(ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new ValidacaoException(["Já existe um centro de custo com este nome (ou código)."]);
        }
    }
}
