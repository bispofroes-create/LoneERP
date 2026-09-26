using Lone.Application.Relacionamentos;
using Lone.Application.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Pessoas;
using Lone.Domain.Relacionamentos;
using Lone.Domain.Validacao;
using Lone.Infrastructure.Persistencia.Servicos;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Repositorios;

/// <summary>Relacionamentos entre pessoas (SQL Server via EF Core). Nada é apagado: encerra ou desativa.</summary>
public class PessoaRelacionamentoRepositorio : ServicoDadosBase, IPessoaRelacionamentoRepositorio
{
    public PessoaRelacionamentoRepositorio(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario) : base(fabrica, usuario) { }

    public async Task<List<PessoaRelacionamento>> ListarDaPessoaAsync(Guid pessoaId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.PessoaRelacionamentos.AsNoTracking()
            .Where(r => r.PessoaId == pessoaId || r.PessoaDestinoId == pessoaId) // índices em PessoaId e PessoaDestinoId
            .ToListAsync(ct);
    }

    public async Task<List<PessoaRelacionamento>> ListarDaOrigemAsync(Guid origemId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.PessoaRelacionamentos.AsNoTracking().Where(r => r.PessoaId == origemId).ToListAsync(ct);
    }

    public async Task<PessoaRelacionamento?> ObterAsync(Guid id, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.PessoaRelacionamentos.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<int> ContarSocietariosComoEmpresaAsync(Guid pessoaId, CancellationToken ct)
    {
        var societarios = TiposRelacionamentoSistema.Societarios.ToList();
        await using var db = await AbrirAsync(ct);
        return await db.PessoaRelacionamentos.AsNoTracking()
            .CountAsync(r => r.PessoaDestinoId == pessoaId && r.Ativo && r.FimEm == null && societarios.Contains(r.TipoRelacionamentoId), ct);
    }

    public async Task<List<TipoRelacionamento>> ListarTiposAsync(CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.TiposRelacionamento.AsNoTracking().ToListAsync(ct);
    }

    public async Task<Dictionary<Guid, PessoaNoRelacionamento>> PessoasAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return new();
        var lista = ids.Distinct().ToList();
        await using var db = await AbrirAsync(ct);
        var linhas = await db.Pessoas.AsNoTracking()
            .Where(p => lista.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                p.Natureza,
                p.Situacao,
                p.Nome,
                p.NomeExibicao,
                p.NomeSocial,
                Fantasia = p.Estabelecimentos.Where(e => e.Principal).Select(e => e.NomeFantasia).FirstOrDefault()
            })
            .ToListAsync(ct);
        return linhas.ToDictionary(
            l => l.Id,
            l => new PessoaNoRelacionamento(l.Id, l.Natureza, l.Situacao,
                NomePessoa.ParaExibir(l.Natureza, l.Nome, l.NomeExibicao, l.NomeSocial, l.Fantasia)));
    }

    public async Task IncluirAsync(PessoaRelacionamento relacionamento, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        db.PessoaRelacionamentos.Add(relacionamento);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new ValidacaoException(["Este relacionamento já está registrado e em aberto (outro usuário acabou de gravar)."]);
        }
    }

    public async Task AlterarAsync(Guid id, DateOnly? fimEm, bool ativo, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var atual = await db.PessoaRelacionamentos.FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new ConflitoDeEdicaoException();
        atual.FimEm = fimEm;
        atual.Ativo = ativo;
        await db.SaveChangesAsync(ct);
    }
}
