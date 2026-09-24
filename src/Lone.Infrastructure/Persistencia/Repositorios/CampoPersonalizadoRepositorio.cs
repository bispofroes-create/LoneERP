using Lone.Application.CamposPersonalizados;
using Lone.Application.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Infrastructure.Persistencia.Servicos;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Repositorios;

/// <summary>Definições de campos personalizados (SQL Server via EF Core). Nunca apaga campos nem opções.</summary>
public class CampoPersonalizadoRepositorio : ServicoDadosBase, ICampoPersonalizadoRepositorio
{
    public CampoPersonalizadoRepositorio(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario)
        : base(fabrica, usuario) { }

    public async Task<List<CampoPersonalizado>> ListarAsync(EntidadePersonalizavel entidade, bool incluirInativos, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var consulta = db.CamposPersonalizados.AsNoTracking().Include(c => c.Opcoes).Where(c => c.Entidade == entidade);
        if (!incluirInativos) consulta = consulta.Where(c => c.Ativo);
        return await consulta.OrderBy(c => c.Ordem).ThenBy(c => c.Nome).AsSplitQuery().ToListAsync(ct);
    }

    public async Task<CampoPersonalizado?> ObterAsync(Guid id, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.CamposPersonalizados.AsNoTracking().Include(c => c.Opcoes).FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<bool> NomeEmUsoAsync(EntidadePersonalizavel entidade, string nome, Guid ignorarId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        // A ordenação do banco (padrão do SQL Server) já ignora maiúsculas/minúsculas.
        return await db.CamposPersonalizados.AnyAsync(c => c.Entidade == entidade && c.Nome == nome && c.Id != ignorarId, ct);
    }

    public async Task<HashSet<Guid>> ComValoresAsync(IReadOnlyCollection<Guid> campoIds, CancellationToken ct)
    {
        if (campoIds.Count == 0) return new();
        await using var db = await AbrirAsync(ct);
        var ids = campoIds.ToList();
        var usados = await db.PessoaValoresPersonalizados.AsNoTracking()
            .Where(v => ids.Contains(v.CampoId)).Select(v => v.CampoId).Distinct().ToListAsync(ct);
        return usados.ToHashSet();
    }

    public async Task<int> ProximaOrdemAsync(EntidadePersonalizavel entidade, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return (await db.CamposPersonalizados.Where(c => c.Entidade == entidade).MaxAsync(c => (int?)c.Ordem, ct) ?? -1) + 1;
    }

    public async Task SalvarAsync(CampoPersonalizado campo, bool novo, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);

        if (novo)
        {
            db.CamposPersonalizados.Add(campo);
        }
        else
        {
            var atual = await db.CamposPersonalizados.Include(c => c.Opcoes).FirstOrDefaultAsync(c => c.Id == campo.Id, ct)
                        ?? throw new ConflitoDeEdicaoException();

            var versaoAberta = campo.Versao;
            campo.Versao = atual.Versao;
            campo.CriadoEm = atual.CriadoEm;
            campo.AtualizadoEm = atual.AtualizadoEm;

            var entrada = db.Entry(atual);
            entrada.CurrentValues.SetValues(campo);
            entrada.Property(c => c.Versao).OriginalValue = versaoAberta;
            atual.ReceberEventosDe(campo);

            // Opções: inclui as novas e altera as existentes; nenhuma é apagada.
            foreach (var opcao in campo.Opcoes)
            {
                opcao.CampoId = atual.Id;
                var existente = atual.Opcoes.FirstOrDefault(o => o.Id == opcao.Id);
                if (existente is null)
                {
                    if (opcao.Id == Guid.Empty) opcao.Id = IdSequencial.Novo();
                    atual.Opcoes.Add(opcao);
                    db.Add(opcao);
                }
                else
                {
                    opcao.CriadoEm = existente.CriadoEm;
                    opcao.AtualizadoEm = existente.AtualizadoEm;
                    db.Entry(existente).CurrentValues.SetValues(opcao);
                }
            }

            entrada.Property(c => c.AtualizadoEm).IsModified = true; // confere e troca a versão
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

    public async Task ReordenarAsync(EntidadePersonalizavel entidade, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var campos = await db.CamposPersonalizados.Where(c => c.Entidade == entidade).ToListAsync(ct);

        var ordenados = ids.Select(id => campos.FirstOrDefault(c => c.Id == id)).OfType<CampoPersonalizado>()
            .Concat(campos.Where(c => !ids.Contains(c.Id)).OrderBy(c => c.Ordem).ThenBy(c => c.Nome))
            .ToList();
        for (var i = 0; i < ordenados.Count; i++)
            ordenados[i].Ordem = i;

        await db.SaveChangesAsync(ct);
    }
}
