using Lone.Application.Comercial;
using Lone.Application.Seguranca;
using Lone.Contracts.Colaboradores;
using Lone.Domain.Enums;
using Lone.Infrastructure.Persistencia.Servicos;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Consultas;

public class ComercialConsultas : ServicoDadosBase, IComercialConsultas
{
    public ComercialConsultas(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario) : base(fabrica, usuario) { }

    public async Task<List<PessoaOpcaoDto>> ListarVendedoresAsync(CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.Pessoas.AsNoTracking()
            .Where(p => p.Situacao == SituacaoPessoa.Ativo &&
                        p.Papeis.Any(x => x.Ativo && (x.Papel == TipoPapel.Vendedor || x.Papel == TipoPapel.Representante)))
            .OrderBy(p => p.NomeExibicao ?? p.Nome)
            .Select(p => new PessoaOpcaoDto(p.Id, p.NomeExibicao ?? p.Nome))
            .ToListAsync(ct);
    }

    public async Task<HashSet<Guid>> VendedoresValidosAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return new();
        await using var db = await AbrirAsync(ct);
        var lista = ids.Distinct().ToList();
        return (await db.Pessoas.AsNoTracking()
            .Where(p => lista.Contains(p.Id) && p.Situacao == SituacaoPessoa.Ativo &&
                        p.Papeis.Any(x => x.Ativo && (x.Papel == TipoPapel.Vendedor || x.Papel == TipoPapel.Representante)))
            .Select(p => p.Id).ToListAsync(ct)).ToHashSet();
    }

    public async Task<Dictionary<Guid, string>> NomesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return new();
        await using var db = await AbrirAsync(ct);
        var lista = ids.Distinct().ToList();
        return await db.Pessoas.AsNoTracking().Where(p => lista.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.NomeExibicao ?? p.Nome, ct);
    }
}
