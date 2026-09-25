using Lone.Application.Consultas;
using Lone.Application.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Infrastructure.Persistencia.Servicos;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Repositorios;

/// <summary>Filtros salvos da consulta avançada. Nunca apaga (remover desativa).</summary>
public class FiltroSalvoRepositorio : ServicoDadosBase, IFiltroSalvoRepositorio
{
    public FiltroSalvoRepositorio(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario) : base(fabrica, usuario) { }

    public async Task<List<FiltroSalvo>> ListarVisiveisAsync(Guid usuarioId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.FiltrosSalvos.AsNoTracking().Where(f => f.Ativo && (f.UsuarioId == usuarioId || f.Compartilhado)).ToListAsync(ct);
    }

    public async Task<FiltroSalvo?> ObterAsync(Guid id, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.FiltrosSalvos.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, ct);
    }

    public async Task SalvarAsync(FiltroSalvo filtro, bool novo, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        if (novo)
        {
            if (filtro.Id == Guid.Empty) filtro.Id = IdSequencial.Novo();
            db.FiltrosSalvos.Add(filtro);
        }
        else
        {
            var atual = await db.FiltrosSalvos.FirstOrDefaultAsync(f => f.Id == filtro.Id, ct) ?? throw new ConflitoDeEdicaoException();
            var versaoAberta = filtro.Versao;
            filtro.Versao = atual.Versao;
            filtro.CriadoEm = atual.CriadoEm;
            filtro.AtualizadoEm = atual.AtualizadoEm;
            var entrada = db.Entry(atual);
            entrada.CurrentValues.SetValues(filtro);
            entrada.Property(x => x.Versao).OriginalValue = versaoAberta;
            atual.ReceberEventosDe(filtro);
            entrada.Property(x => x.AtualizadoEm).IsModified = true;
        }
        await Filhos.GravarAsync(db, "Você já tem um filtro com este nome.", ct);
    }
}
