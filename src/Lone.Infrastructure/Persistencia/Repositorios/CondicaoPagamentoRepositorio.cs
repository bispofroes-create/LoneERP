using Lone.Application.Comercial;
using Lone.Application.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Validacao;
using Lone.Infrastructure.Persistencia.Servicos;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Repositorios;

/// <summary>Condição de pagamento (SQL Server via EF Core). Nunca apaga.</summary>
public class CondicaoPagamentoRepositorio : ServicoDadosBase, ICondicaoPagamentoRepositorio
{
    public CondicaoPagamentoRepositorio(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario) : base(fabrica, usuario) { }

    public async Task<List<CondicaoPagamento>> ListarAsync(CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.CondicoesPagamento.AsNoTracking().ToListAsync(ct);
    }

    public async Task<CondicaoPagamento?> ObterAsync(Guid id, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.CondicoesPagamento.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<Dictionary<Guid, CondicaoPagamento>> ObterVariosAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return new();
        await using var db = await AbrirAsync(ct);
        var lista = ids.Distinct().ToList();
        return await db.CondicoesPagamento.AsNoTracking().Where(x => lista.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
    }

    public async Task<Dictionary<Guid, int>> ContarUsosAsync(Guid? somenteId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var contas = db.ContasCliente.AsNoTracking().Where(c => c.CondicaoPagamentoId != null);
        if (somenteId is { } id) contas = contas.Where(c => c.CondicaoPagamentoId == id);
        return await contas.GroupBy(c => c.CondicaoPagamentoId!.Value)
            .Select(g => new { g.Key, Quantidade = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Quantidade, ct);
    }

    public async Task SalvarAsync(CondicaoPagamento item, bool novo, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        if (novo)
        {
            if (item.Id == Guid.Empty) item.Id = IdSequencial.Novo();
            db.CondicoesPagamento.Add(item);
        }
        else
        {
            var atual = await db.CondicoesPagamento.FirstOrDefaultAsync(x => x.Id == item.Id, ct) ?? throw new ConflitoDeEdicaoException();
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
            throw new ValidacaoException(["Já existe cadastro com este nome."]);
        }
    }
}
