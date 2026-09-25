using Lone.Application.Colaboradores;
using Lone.Application.Seguranca;
using Lone.Contracts.Colaboradores;
using Lone.Domain.Enums;
using Lone.Infrastructure.Persistencia.Servicos;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Consultas;

public class ColaboradorConsultas : ServicoDadosBase, IColaboradorConsultas
{
    public ColaboradorConsultas(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario) : base(fabrica, usuario) { }

    public async Task<List<PessoaOpcaoDto>> ListarGestoresAsync(DateOnly hoje, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.Pessoas.AsNoTracking()
            .Where(p => p.Situacao == SituacaoPessoa.Ativo && p.Natureza == NaturezaPessoa.Fisica &&
                        db.VinculosColaborador.Any(v => v.PessoaId == p.Id && v.AdmissaoEm <= hoje &&
                                                        (v.DesligamentoEm == null || v.DesligamentoEm >= hoje)))
            .OrderBy(p => p.NomeExibicao ?? p.Nome)
            .Select(p => new PessoaOpcaoDto(p.Id, p.NomeExibicao ?? p.Nome))
            .ToListAsync(ct);
    }

    public async Task<Dictionary<Guid, string>> NomesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return new();
        await using var db = await AbrirAsync(ct);
        var lista = ids.Distinct().ToList();
        return await db.Pessoas.AsNoTracking().Where(p => lista.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.NomeExibicao ?? p.Nome, ct);
    }

    public async Task<HashSet<Guid>> PessoasFisicasAtivasAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return new();
        await using var db = await AbrirAsync(ct);
        var lista = ids.Distinct().ToList();
        return (await db.Pessoas.AsNoTracking()
            .Where(p => lista.Contains(p.Id) && p.Natureza == NaturezaPessoa.Fisica && p.Situacao == SituacaoPessoa.Ativo)
            .Select(p => p.Id).ToListAsync(ct)).ToHashSet();
    }
}
