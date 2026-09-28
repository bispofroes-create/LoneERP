using Lone.Application.Documentos;
using Lone.Application.Seguranca;
using Lone.Domain.Entidades;
using Lone.Infrastructure.Persistencia.Servicos;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Repositorios;

/// <summary>Dados dos anexos (SQL Server via EF Core). Nunca apaga: só ativa/desativa (fica no histórico da pessoa).</summary>
public class AnexoRepositorio : ServicoDadosBase, IAnexoRepositorio
{
    public AnexoRepositorio(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario) : base(fabrica, usuario) { }

    public async Task<List<AnexoDocumento>> ListarPorPessoaAsync(Guid pessoaId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.AnexosDocumento.AsNoTracking().Where(a => a.PessoaId == pessoaId)
            .OrderByDescending(a => a.CriadoEm).ToListAsync(ct);
    }

    public async Task<AnexoDocumento?> ObterAsync(Guid id, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.AnexosDocumento.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    public async Task<DocumentoParaAnexo?> ConferirDocumentoAsync(Guid pessoaId, Guid documentoId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.PessoaDocumentos.AsNoTracking()
            .Where(d => d.Id == documentoId && d.PessoaId == pessoaId)
            // Sem escopo: por id, e a rota do anexo passa antes pelo filtro de escopo da API (pela pessoa do anexo).
            .Join(db.Pessoas, d => d.PessoaId, p => p.Id, (d, p) => new DocumentoParaAnexo(d.Ativo, p.Situacao))
            .FirstOrDefaultAsync(ct);
    }

    public async Task IncluirAsync(AnexoDocumento anexo, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        db.AnexosDocumento.Add(anexo);
        await db.SaveChangesAsync(ct);
    }

    public async Task AlterarAtivoAsync(Guid id, bool ativo, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var anexo = await db.AnexosDocumento.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (anexo is null || anexo.Ativo == ativo) return;
        anexo.Ativo = ativo;
        await db.SaveChangesAsync(ct);
    }
}
