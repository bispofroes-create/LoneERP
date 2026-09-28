using Lone.Application.Empresas;
using Lone.Application.Seguranca;
using Lone.Contracts.Empresas;
using Lone.Domain.Enums;
using Lone.Infrastructure.Persistencia.Servicos;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Consultas;

public class EmpresaConsultas : ServicoDadosBase, IEmpresaConsultas
{
    public EmpresaConsultas(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario)
        : base(fabrica, usuario) { }

    public async Task<List<EmpresaAtiva>> ListarEstabelecimentosAsync(CancellationToken ct = default)
    {
        await using var db = await AbrirAsync(ct);
        // Sem escopo: as empresas do próprio grupo (sessão e cadastros), não clientes.
        var linhas = await db.Pessoas.AsNoTracking()
            .Where(p => p.Situacao == SituacaoPessoa.Ativo &&
                        p.Papeis.Any(x => x.Papel == TipoPapel.EmpresaDoGrupo && x.Ativo))
            .SelectMany(p => p.Estabelecimentos.Where(e => e.Ativo).Select(e => new
            {
                EstabelecimentoId = e.Id,
                EmpresaId = p.Id,
                Nome = e.NomeFantasia ?? p.NomeExibicao ?? p.Nome,
                e.Cnpj,
                e.Principal
            }))
            .ToListAsync(ct);

        return linhas
            .OrderBy(l => l.Nome).ThenByDescending(l => l.Principal).ThenBy(l => l.Cnpj)
            .Select(l => new EmpresaAtiva(l.EstabelecimentoId, l.EmpresaId, l.Nome, l.Cnpj,
                                          l.Cnpj is { Length: 14 } c && c.Substring(8, 4) == "0001"))
            .ToList();
    }

    public async Task<List<EmpresaResumo>> ListarEmpresasAsync(CancellationToken ct = default)
    {
        await using var db = await AbrirAsync(ct);
        // Sem escopo: as empresas do próprio grupo (sessão e cadastros), não clientes.
        var linhas = await db.Pessoas.AsNoTracking()
            .Where(p => p.Papeis.Any(x => x.Papel == TipoPapel.EmpresaDoGrupo))
            .OrderBy(p => p.NomeExibicao ?? p.Nome)
            .Select(p => new
            {
                p.Id,
                Nome = p.NomeExibicao ?? p.Nome,
                Ativa = p.Situacao == SituacaoPessoa.Ativo && p.Papeis.Any(x => x.Papel == TipoPapel.EmpresaDoGrupo && x.Ativo)
            })
            .ToListAsync(ct);

        return linhas.Select(l => new EmpresaResumo(l.Id, l.Nome, l.Ativa)).ToList();
    }
}
