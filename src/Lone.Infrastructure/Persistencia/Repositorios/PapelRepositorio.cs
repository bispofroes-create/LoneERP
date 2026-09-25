using Lone.Application.Papeis;
using Lone.Application.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Validacao;
using Lone.Infrastructure.Persistencia.Servicos;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Repositorios;

/// <summary>Cadastro de papéis (SQL Server via EF Core). Nunca apaga papéis.</summary>
public class PapelRepositorio : ServicoDadosBase, IPapelRepositorio
{
    public PapelRepositorio(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario)
        : base(fabrica, usuario) { }

    public async Task<List<Papel>> ListarAsync(bool incluirInativos, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var consulta = db.Papeis.AsNoTracking();
        if (!incluirInativos) consulta = consulta.Where(p => p.Ativo);
        return await consulta.OrderBy(p => p.Ordem).ThenBy(p => p.Nome).ToListAsync(ct);
    }

    public async Task<Papel?> ObterAsync(Guid id, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.Papeis.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<Dictionary<Guid, Papel>> ObterVariosAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return new();
        await using var db = await AbrirAsync(ct);
        var lista = ids.Distinct().ToList();
        return await db.Papeis.AsNoTracking().Where(p => lista.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
    }

    public async Task<(bool Nome, bool Codigo)> EmUsoAsync(string nome, string codigo, Guid ignorarId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var nomeEmUso = await db.Papeis.AnyAsync(p => p.Nome == nome && p.Id != ignorarId, ct);
        var codigoEmUso = await db.Papeis.AnyAsync(p => p.Codigo == codigo && p.Id != ignorarId, ct);
        return (nomeEmUso, codigoEmUso);
    }

    public async Task<Dictionary<Guid, int>> ContarPessoasAsync(Guid? somentePapelId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var ativos = db.PessoaPapeis.AsNoTracking().Where(p => p.Ativo);
        if (somentePapelId is { } id) ativos = ativos.Where(p => p.PapelId == id);
        // Índice (PapelId, Ativo): conta sem ler o cadastro de pessoas.
        return await ativos.GroupBy(p => p.PapelId)
            .Select(g => new { g.Key, Quantidade = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Quantidade, ct);
    }

    public async Task<int> ProximaOrdemAsync(CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return (await db.Papeis.MaxAsync(p => (int?)p.Ordem, ct) ?? 0) + 1;
    }

    public async Task SalvarAsync(Papel papel, bool novo, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);

        if (novo)
        {
            if (papel.Id == Guid.Empty) papel.Id = IdSequencial.Novo();
            db.Papeis.Add(papel);
        }
        else
        {
            var atual = await db.Papeis.FirstOrDefaultAsync(p => p.Id == papel.Id, ct) ?? throw new ConflitoDeEdicaoException();
            var versaoAberta = papel.Versao;
            papel.Versao = atual.Versao;
            papel.CriadoEm = atual.CriadoEm;
            papel.AtualizadoEm = atual.AtualizadoEm;

            var entrada = db.Entry(atual);
            entrada.CurrentValues.SetValues(papel);
            entrada.Property(p => p.Versao).OriginalValue = versaoAberta;
            atual.ReceberEventosDe(papel);
            entrada.Property(p => p.AtualizadoEm).IsModified = true; // confere e troca a versão mesmo sem outra mudança
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
            throw new ValidacaoException(["Já existe um papel com este nome ou código."]);
        }
    }
}
