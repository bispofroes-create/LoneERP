using Lone.Application.Metas;
using Lone.Application.Seguranca;
using Lone.Contracts.Metas;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Validacao;
using Lone.Infrastructure.Persistencia.Servicos;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Repositorios;

/// <summary>Equipes (SQL Server via EF Core). Nunca apaga equipe nem membro (a saída encerra).</summary>
public class EquipeRepositorio : ServicoDadosBase, IEquipeRepositorio
{
    public EquipeRepositorio(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario) : base(fabrica, usuario) { }

    public async Task<List<Equipe>> ListarAsync(CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.Equipes.AsNoTracking().Include(e => e.Membros).AsSplitQuery().ToListAsync(ct);
    }

    public async Task<Equipe?> ObterAsync(Guid id, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.Equipes.AsNoTracking().Include(e => e.Membros).FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    public async Task SalvarAsync(Equipe equipe, bool novo, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        foreach (var m in equipe.Membros)
        {
            m.EquipeId = equipe.Id;
            if (m.Id == Guid.Empty) m.Id = IdSequencial.Novo();
        }
        if (novo)
            db.Equipes.Add(equipe);
        else
        {
            var atual = await db.Equipes.Include(e => e.Membros).FirstOrDefaultAsync(e => e.Id == equipe.Id, ct)
                        ?? throw new ConflitoDeEdicaoException();
            var versaoAberta = equipe.Versao;
            equipe.Versao = atual.Versao;
            equipe.CriadoEm = atual.CriadoEm;
            equipe.AtualizadoEm = atual.AtualizadoEm;
            var entrada = db.Entry(atual);
            entrada.CurrentValues.SetValues(equipe);
            entrada.Property(x => x.Versao).OriginalValue = versaoAberta;
            Filhos.Sincronizar(db, atual.Membros, equipe.Membros, apagarAusentes: false);
            atual.ReceberEventosDe(equipe);
            entrada.Property(x => x.AtualizadoEm).IsModified = true;
        }
        await Filhos.GravarAsync(db, "Já existe uma equipe com este nome.", ct);
    }
}

/// <summary>Indicadores (SQL Server via EF Core). Nunca apaga.</summary>
public class IndicadorRepositorio : ServicoDadosBase, IIndicadorRepositorio
{
    public IndicadorRepositorio(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario) : base(fabrica, usuario) { }

    public async Task<List<Indicador>> ListarAsync(CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.Indicadores.AsNoTracking().ToListAsync(ct);
    }

    public async Task<Indicador?> ObterAsync(Guid id, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.Indicadores.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task SalvarAsync(Indicador item, bool novo, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        if (novo)
        {
            if (item.Id == Guid.Empty) item.Id = IdSequencial.Novo();
            db.Indicadores.Add(item);
        }
        else
        {
            var atual = await db.Indicadores.FirstOrDefaultAsync(x => x.Id == item.Id, ct) ?? throw new ConflitoDeEdicaoException();
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
        await Filhos.GravarAsync(db, "Já existe um indicador com este código ou nome.", ct);
    }
}

/// <summary>
/// Metas (SQL Server via EF Core). Itens, faixas, participantes e alvos só são removidos enquanto a meta é rascunho
/// (o serviço garante); depois disso a estrutura não muda e o realizado/resultado são alterações auditadas.
/// </summary>
public class MetaRepositorio : ServicoDadosBase, IMetaRepositorio
{
    public MetaRepositorio(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario) : base(fabrica, usuario) { }

    public async Task<List<MetaResumoDto>> ListarAsync(bool incluirInativas, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.Metas.AsNoTracking()
            .Where(m => incluirInativas || m.Ativo)
            .OrderByDescending(m => m.InicioEm).ThenBy(m => m.Nome)
            .Select(m => new MetaResumoDto
            {
                Id = m.Id, Nome = m.Nome, InicioEm = m.InicioEm, FimEm = m.FimEm, Situacao = m.Situacao,
                Participantes = m.Participantes.Count, Ativo = m.Ativo
            })
            .ToListAsync(ct);
    }

    public async Task<Meta?> ObterAsync(Guid id, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await Completa(db).AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct);
    }

    public async Task<Dictionary<Guid, List<(NivelParticipante Nivel, Guid ReferenciaId)>>> ParticipantesAsync(IReadOnlyCollection<Guid> metas,
                                                                                                              CancellationToken ct)
    {
        if (metas.Count == 0) return new();
        await using var db = await AbrirAsync(ct);
        var lista = metas.Distinct().ToList();
        var linhas = await db.Metas.AsNoTracking().Where(m => lista.Contains(m.Id))
            .SelectMany(m => m.Participantes.Select(p => new { p.MetaId, p.Nivel, p.ReferenciaId }))
            .ToListAsync(ct);
        return linhas.GroupBy(l => l.MetaId).ToDictionary(g => g.Key, g => g.Select(l => (l.Nivel, l.ReferenciaId)).ToList());
    }

    private static IQueryable<Meta> Completa(LoneDbContext db) =>
        db.Metas.Include(m => m.Itens).Include(m => m.Faixas).Include(m => m.Participantes).Include(m => m.Alvos).AsSplitQuery();

    public async Task SalvarAsync(Meta meta, bool novo, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        foreach (var f in meta.Itens.Cast<object>().Concat(meta.Faixas).Concat(meta.Participantes).Concat(meta.Alvos).Cast<EntidadeBase>())
            if (f.Id == Guid.Empty) f.Id = IdSequencial.Novo();
        meta.Itens.ForEach(x => x.MetaId = meta.Id);
        meta.Faixas.ForEach(x => x.MetaId = meta.Id);
        meta.Participantes.ForEach(x => x.MetaId = meta.Id);
        meta.Alvos.ForEach(x => x.MetaId = meta.Id);

        if (novo)
            db.Metas.Add(meta);
        else
        {
            var atual = await Completa(db).FirstOrDefaultAsync(m => m.Id == meta.Id, ct) ?? throw new ConflitoDeEdicaoException();
            var versaoAberta = meta.Versao;
            meta.Versao = atual.Versao;
            meta.CriadoEm = atual.CriadoEm;
            meta.AtualizadoEm = atual.AtualizadoEm;
            var entrada = db.Entry(atual);
            entrada.CurrentValues.SetValues(meta);
            entrada.Property(x => x.Versao).OriginalValue = versaoAberta;

            // Alvos primeiro (apontam para itens e participantes).
            Filhos.Sincronizar(db, atual.Alvos, meta.Alvos, apagarAusentes: true);
            Filhos.Sincronizar(db, atual.Itens, meta.Itens, apagarAusentes: true);
            Filhos.Sincronizar(db, atual.Faixas, meta.Faixas, apagarAusentes: true);
            Filhos.Sincronizar(db, atual.Participantes, meta.Participantes, apagarAusentes: true);
            atual.ReceberEventosDe(meta);
            entrada.Property(x => x.AtualizadoEm).IsModified = true;
        }
        await Filhos.GravarAsync(db, "O mesmo indicador ou participante aparece duas vezes na meta.", ct);
    }
}

/// <summary>Sincronização de filhos por Id e gravação com tradução de conflito/duplicidade (compartilhado pelas metas).</summary>
internal static class Filhos
{
    public static void Sincronizar<T>(LoneDbContext db, List<T> atuais, List<T> novos, bool apagarAusentes) where T : EntidadeBase
    {
        if (apagarAusentes)
            foreach (var removido in atuais.Where(a => novos.All(n => n.Id != a.Id)).ToList())
            {
                atuais.Remove(removido);
                db.Remove(removido);
            }

        foreach (var novo in novos)
        {
            var existente = atuais.FirstOrDefault(a => a.Id == novo.Id);
            if (existente is null)
            {
                atuais.Add(novo);
                db.Add(novo); // explícito: Id preenchido não pode ser confundido com registro existente
            }
            else
            {
                novo.CriadoEm = existente.CriadoEm;
                novo.AtualizadoEm = existente.AtualizadoEm;
                db.Entry(existente).CurrentValues.SetValues(novo);
            }
        }
    }

    public static async Task GravarAsync(LoneDbContext db, string duplicado, CancellationToken ct)
    {
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
            throw new ValidacaoException([duplicado]);
        }
    }
}
