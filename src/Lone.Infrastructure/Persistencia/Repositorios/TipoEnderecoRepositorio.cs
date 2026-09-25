using Lone.Application.Enderecos;
using Lone.Application.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Validacao;
using Lone.Infrastructure.Persistencia.Servicos;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Repositorios;

/// <summary>Tipos de endereço (SQL Server via EF Core). Nunca apaga tipos.</summary>
public class TipoEnderecoRepositorio : ServicoDadosBase, ITipoEnderecoRepositorio
{
    public TipoEnderecoRepositorio(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario)
        : base(fabrica, usuario) { }

    public async Task<List<TipoEndereco>> ListarAsync(bool incluirInativos, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var consulta = db.TiposEndereco.AsNoTracking();
        if (!incluirInativos) consulta = consulta.Where(t => t.Ativo);
        return await consulta.OrderBy(t => t.Ordem).ThenBy(t => t.Nome).ToListAsync(ct);
    }

    public async Task<TipoEndereco?> ObterAsync(Guid id, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.TiposEndereco.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<Dictionary<Guid, TipoEndereco>> ObterVariosAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return new();
        await using var db = await AbrirAsync(ct);
        var lista = ids.Distinct().ToList();
        return await db.TiposEndereco.AsNoTracking().Where(t => lista.Contains(t.Id)).ToDictionaryAsync(t => t.Id, ct);
    }

    public async Task<bool> NomeEmUsoAsync(string nome, Guid ignorarId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.TiposEndereco.AnyAsync(t => t.Nome == nome && t.Id != ignorarId, ct);
    }

    public async Task<Dictionary<Guid, int>> ContarUsosAsync(Guid? somenteTipoId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var enderecos = db.PessoaEnderecos.AsNoTracking().Where(e => e.Ativo && e.TipoEnderecoId != null);
        if (somenteTipoId is { } id) enderecos = enderecos.Where(e => e.TipoEnderecoId == id);
        return await enderecos.GroupBy(e => e.TipoEnderecoId!.Value)
            .Select(g => new { g.Key, Quantidade = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Quantidade, ct);
    }

    public async Task<int> ProximaOrdemAsync(CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return (await db.TiposEndereco.MaxAsync(t => (int?)t.Ordem, ct) ?? 0) + 1;
    }

    public async Task SalvarAsync(TipoEndereco tipo, bool novo, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);

        if (novo)
        {
            if (tipo.Id == Guid.Empty) tipo.Id = IdSequencial.Novo();
            db.TiposEndereco.Add(tipo);
        }
        else
        {
            var atual = await db.TiposEndereco.FirstOrDefaultAsync(t => t.Id == tipo.Id, ct) ?? throw new ConflitoDeEdicaoException();
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
            throw new ValidacaoException(["Já existe um tipo de endereço com este nome."]);
        }
    }
}
