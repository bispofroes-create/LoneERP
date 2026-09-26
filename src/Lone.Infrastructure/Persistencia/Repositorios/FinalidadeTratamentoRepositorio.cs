using Lone.Application.Privacidade;
using Lone.Application.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Validacao;
using Lone.Infrastructure.Persistencia.Servicos;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Repositorios;

/// <summary>Finalidades de tratamento (SQL Server via EF Core). Nunca apaga finalidades.</summary>
public class FinalidadeTratamentoRepositorio : ServicoDadosBase, IFinalidadeTratamentoRepositorio
{
    public FinalidadeTratamentoRepositorio(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario)
        : base(fabrica, usuario) { }

    public async Task<List<FinalidadeTratamento>> ListarAsync(bool incluirInativas, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var consulta = db.FinalidadesTratamento.AsNoTracking();
        if (!incluirInativas) consulta = consulta.Where(f => f.Ativo);
        return await consulta.OrderBy(f => f.Ordem).ThenBy(f => f.Nome).ToListAsync(ct);
    }

    public async Task<FinalidadeTratamento?> ObterAsync(Guid id, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.FinalidadesTratamento.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, ct);
    }

    public async Task<bool> NomeEmUsoAsync(string nome, Guid ignorarId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.FinalidadesTratamento.AnyAsync(f => f.Nome == nome && f.Id != ignorarId, ct);
    }

    public async Task<bool> CodigoEmUsoAsync(string codigo, Guid ignorarId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.FinalidadesTratamento.AnyAsync(f => f.Codigo == codigo && f.Id != ignorarId, ct);
    }

    public async Task<Dictionary<Guid, int>> ContarUsosAsync(Guid? somenteId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var consentimentos = db.PessoaConsentimentos.AsNoTracking();
        if (somenteId is { } id) consentimentos = consentimentos.Where(c => c.FinalidadeId == id);
        return await consentimentos.GroupBy(c => c.FinalidadeId)
            .Select(g => new { g.Key, Quantidade = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Quantidade, ct);
    }

    public async Task<int> ProximaOrdemAsync(CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        // "Registro anterior" fica no fim (99): as criadas pelo usuário entram antes dela.
        return (await db.FinalidadesTratamento.Where(f => !f.SomenteHistorico).MaxAsync(f => (int?)f.Ordem, ct) ?? 0) + 1;
    }

    public async Task SalvarAsync(FinalidadeTratamento finalidade, bool nova, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);

        if (nova)
        {
            if (finalidade.Id == Guid.Empty) finalidade.Id = IdSequencial.Novo();
            db.FinalidadesTratamento.Add(finalidade);
        }
        else
        {
            var atual = await db.FinalidadesTratamento.FirstOrDefaultAsync(f => f.Id == finalidade.Id, ct) ?? throw new ConflitoDeEdicaoException();
            var versaoAberta = finalidade.Versao;
            finalidade.Versao = atual.Versao;
            finalidade.CriadoEm = atual.CriadoEm;
            finalidade.AtualizadoEm = atual.AtualizadoEm;

            var entrada = db.Entry(atual);
            entrada.CurrentValues.SetValues(finalidade);
            entrada.Property(f => f.Versao).OriginalValue = versaoAberta;
            atual.ReceberEventosDe(finalidade);
            entrada.Property(f => f.AtualizadoEm).IsModified = true;
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
            throw new ValidacaoException(["Já existe uma finalidade com este nome ou código."]);
        }
    }
}
