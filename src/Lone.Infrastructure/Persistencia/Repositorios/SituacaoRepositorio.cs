using Lone.Application.Seguranca;
using Lone.Application.Situacoes;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Validacao;
using Lone.Infrastructure.Persistencia.Servicos;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Repositorios;

/// <summary>Bloqueios, interações e parâmetros de relacionamento. Nada é apagado.</summary>
public class SituacaoRepositorio : ServicoDadosBase, ISituacaoRepositorio
{
    public SituacaoRepositorio(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario) : base(fabrica, usuario) { }

    public async Task<SituacaoPessoa?> SituacaoDaPessoaAsync(Guid pessoaId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.Pessoas.AsNoTracking().Where(p => p.Id == pessoaId).Select(p => (SituacaoPessoa?)p.Situacao).FirstOrDefaultAsync(ct);
    }

    public async Task IncluirBloqueioAsync(Bloqueio bloqueio, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        db.Set<Bloqueio>().Add(bloqueio);
        await db.SaveChangesAsync(ct);
    }

    public async Task<Bloqueio?> ObterBloqueioAsync(Guid bloqueioId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.Set<Bloqueio>().AsNoTracking().FirstOrDefaultAsync(b => b.Id == bloqueioId, ct);
    }

    public async Task LiberarBloqueioAsync(Guid bloqueioId, DateTime fimEm, string fimPor, string motivo, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var bloqueio = await db.Set<Bloqueio>().FirstOrDefaultAsync(b => b.Id == bloqueioId, ct) ?? throw new ConflitoDeEdicaoException();
        if (bloqueio.FimEm is not null) return;
        bloqueio.FimEm = fimEm;
        bloqueio.FimPor = fimPor;
        bloqueio.MotivoLiberacao = motivo;
        await db.SaveChangesAsync(ct);
    }

    public async Task IncluirInteracaoAsync(Interacao interacao, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        db.Interacoes.Add(interacao);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<Interacao>> ListarInteracoesAsync(Guid pessoaId, int limite, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.Interacoes.AsNoTracking().Where(i => i.PessoaId == pessoaId)
            .OrderByDescending(i => i.DataHora).Take(limite).ToListAsync(ct);
    }

    public async Task<DateTime?> UltimaInteracaoAsync(Guid pessoaId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.Interacoes.AsNoTracking().Where(i => i.PessoaId == pessoaId).MaxAsync(i => (DateTime?)i.DataHora, ct);
    }

    public async Task<ParametrosRelacionamento> ObterParametrosAsync(CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.ParametrosRelacionamento.AsNoTracking().FirstOrDefaultAsync(p => p.Id == ParametrosRelacionamento.IdUnico, ct)
               ?? new ParametrosRelacionamento { Id = ParametrosRelacionamento.IdUnico };
    }

    public async Task SalvarParametrosAsync(ParametrosRelacionamento parametros, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var atual = await db.ParametrosRelacionamento.FirstOrDefaultAsync(p => p.Id == ParametrosRelacionamento.IdUnico, ct);
        if (atual is null)
        {
            db.ParametrosRelacionamento.Add(parametros);
        }
        else
        {
            var versaoAberta = parametros.Versao;
            var entrada = db.Entry(atual);
            atual.DiasEmRisco = parametros.DiasEmRisco;
            atual.DiasInativo = parametros.DiasInativo;
            entrada.Property(p => p.Versao).OriginalValue = versaoAberta;
            atual.ReceberEventosDe(parametros);
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflitoDeEdicaoException(ex);
        }
    }
}
