using Lone.Application.Comercial;
using Lone.Application.Seguranca;
using Lone.Contracts.Comercial;
using Lone.Domain.Comercial;
using Lone.Domain.Enums;
using Lone.Infrastructure.Persistencia.Servicos;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Consultas;

public class ComercialConsultas : ServicoDadosBase, IComercialConsultas
{
    public ComercialConsultas(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario) : base(fabrica, usuario) { }

    public async Task<List<AtendenteOpcaoDto>> ListarAtendentesAsync(IReadOnlyCollection<Guid> classificacoes, CancellationToken ct)
    {
        if (classificacoes.Count == 0) return [];
        await using var db = await AbrirAsync(ct);
        var aceitas = classificacoes.Distinct().ToList();
        var linhas = await db.Pessoas.AsNoTracking()
            .Where(p => p.Situacao == SituacaoPessoa.Ativo && p.Papeis.Any(x => x.Ativo && aceitas.Contains(x.PapelId)))
            .OrderBy(p => p.NomeExibicao ?? p.Nome)
            .Select(p => new
            {
                p.Id,
                Nome = p.NomeExibicao ?? p.Nome,
                Classificacoes = p.Papeis.Where(x => x.Ativo && aceitas.Contains(x.PapelId)).Select(x => x.PapelId).ToList()
            })
            .AsSplitQuery()
            .ToListAsync(ct);
        return [.. linhas.Select(l => new AtendenteOpcaoDto(l.Id, l.Nome, [.. l.Classificacoes.Distinct()]))];
    }

    public async Task<Dictionary<Guid, PessoaElegivel>> PessoasElegiveisAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return new();
        await using var db = await AbrirAsync(ct);
        var lista = ids.Distinct().ToList();
        var linhas = await db.Pessoas.AsNoTracking()
            .Where(p => lista.Contains(p.Id) && p.Situacao == SituacaoPessoa.Ativo)
            .Select(p => new
            {
                p.Id,
                Nome = p.NomeExibicao ?? p.Nome,
                Classificacoes = p.Papeis.Where(x => x.Ativo).Select(x => x.PapelId).ToList()
            })
            .AsSplitQuery()
            .ToListAsync(ct);
        return linhas.ToDictionary(l => l.Id, l => new PessoaElegivel(l.Nome, l.Classificacoes.ToHashSet()));
    }

    public async Task<Dictionary<Guid, string>> NomesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return new();
        await using var db = await AbrirAsync(ct);
        var lista = ids.Distinct().ToList();
        return await db.Pessoas.AsNoTracking().Where(p => lista.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.NomeExibicao ?? p.Nome, ct);
    }
}
