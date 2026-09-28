using Lone.Application.Comercial;
using Lone.Application.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Validacao;
using Lone.Infrastructure.Persistencia.Servicos;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Repositorios;

/// <summary>Tipo de carteira (SQL Server via EF Core). Nunca apaga.</summary>
public class TipoCarteiraRepositorio : ServicoDadosBase, ITipoCarteiraRepositorio
{
    public TipoCarteiraRepositorio(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario) : base(fabrica, usuario) { }

    public async Task<List<TipoCarteira>> ListarAsync(CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.TiposCarteira.AsNoTracking().ToListAsync(ct);
    }

    public async Task<TipoCarteira?> ObterAsync(Guid id, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.TiposCarteira.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<Dictionary<Guid, TipoCarteira>> ObterVariosAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return new();
        await using var db = await AbrirAsync(ct);
        var lista = ids.Distinct().ToList();
        return await db.TiposCarteira.AsNoTracking().Where(x => lista.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
    }

    public async Task<Dictionary<Guid, int>> ContarUsosAsync(Guid? somenteId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var carteira = db.CarteiraClientes.AsNoTracking().Where(c => c.Ativo && c.FimEm == null);
        if (somenteId is { } id) carteira = carteira.Where(c => c.TipoCarteiraId == id);
        return await carteira.GroupBy(c => c.TipoCarteiraId)
            .Select(g => new { g.Key, Quantidade = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Quantidade, ct);
    }

    public async Task<List<CarteiraCliente>> VinculosAtivosAsync(Guid tipoId, DateOnly desde, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        // Só as colunas que a conferência usa (sem acompanhar alterações).
        return await db.CarteiraClientes.AsNoTracking()
            .Where(c => c.TipoCarteiraId == tipoId && c.Ativo && (c.FimEm == null || c.FimEm >= desde))
            .Select(c => new CarteiraCliente
            {
                Id = c.Id, PessoaId = c.PessoaId, EmpresaId = c.EmpresaId, TipoCarteiraId = c.TipoCarteiraId,
                InicioEm = c.InicioEm, FimEm = c.FimEm, Ativo = c.Ativo
            })
            .ToListAsync(ct);
    }

    public async Task SalvarAsync(TipoCarteira item, bool novo, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        if (novo)
        {
            if (item.Id == Guid.Empty) item.Id = IdSequencial.Novo();
            db.TiposCarteira.Add(item);
        }
        else
        {
            var atual = await db.TiposCarteira.FirstOrDefaultAsync(x => x.Id == item.Id, ct) ?? throw new ConflitoDeEdicaoException();
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
