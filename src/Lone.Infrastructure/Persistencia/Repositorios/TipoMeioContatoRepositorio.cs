using Lone.Application.Contatos;
using Lone.Application.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Validacao;
using Lone.Infrastructure.Persistencia.Servicos;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Repositorios;

/// <summary>Tipos de telefone/e-mail (SQL Server via EF Core). Nunca apaga tipos.</summary>
public class TipoMeioContatoRepositorio : ServicoDadosBase, ITipoMeioContatoRepositorio
{
    public TipoMeioContatoRepositorio(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario)
        : base(fabrica, usuario) { }

    public async Task<List<TipoMeioContato>> ListarAsync(bool incluirInativos, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var consulta = db.TiposMeioContato.AsNoTracking();
        if (!incluirInativos) consulta = consulta.Where(t => t.Ativo);
        return await consulta.OrderBy(t => t.Categoria).ThenBy(t => t.Ordem).ThenBy(t => t.Nome).ToListAsync(ct);
    }

    public async Task<TipoMeioContato?> ObterAsync(Guid id, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.TiposMeioContato.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<Dictionary<Guid, TipoMeioContato>> ObterVariosAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return new();
        await using var db = await AbrirAsync(ct);
        var lista = ids.Distinct().ToList();
        return await db.TiposMeioContato.AsNoTracking().Where(t => lista.Contains(t.Id)).ToDictionaryAsync(t => t.Id, ct);
    }

    public async Task<bool> NomeEmUsoAsync(CategoriaMeioContato categoria, string nome, Guid ignorarId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.TiposMeioContato.AnyAsync(t => t.Categoria == categoria && t.Nome == nome && t.Id != ignorarId, ct);
    }

    public async Task<Dictionary<Guid, int>> ContarUsosAsync(Guid? somenteTipoId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var meios = db.MeiosContato.AsNoTracking().Where(m => m.Ativo && m.TipoMeioContatoId != null);
        if (somenteTipoId is { } id) meios = meios.Where(m => m.TipoMeioContatoId == id);
        return await meios.GroupBy(m => m.TipoMeioContatoId!.Value)
            .Select(g => new { g.Key, Quantidade = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Quantidade, ct);
    }

    public async Task<int> ProximaOrdemAsync(CategoriaMeioContato categoria, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return (await db.TiposMeioContato.Where(t => t.Categoria == categoria).MaxAsync(t => (int?)t.Ordem, ct) ?? 0) + 1;
    }

    public async Task SalvarAsync(TipoMeioContato tipo, bool novo, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);

        if (novo)
        {
            if (tipo.Id == Guid.Empty) tipo.Id = IdSequencial.Novo();
            db.TiposMeioContato.Add(tipo);
        }
        else
        {
            var atual = await db.TiposMeioContato.FirstOrDefaultAsync(t => t.Id == tipo.Id, ct) ?? throw new ConflitoDeEdicaoException();
            var versaoAberta = tipo.Versao;
            tipo.Versao = atual.Versao;
            tipo.CriadoEm = atual.CriadoEm;
            tipo.AtualizadoEm = atual.AtualizadoEm;

            var entrada = db.Entry(atual);
            entrada.CurrentValues.SetValues(tipo);
            entrada.Property(t => t.Versao).OriginalValue = versaoAberta;
            atual.ReceberEventosDe(tipo);
            entrada.Property(t => t.AtualizadoEm).IsModified = true;
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
            throw new ValidacaoException(["Já existe um tipo com este nome nesta categoria."]);
        }
    }
}
