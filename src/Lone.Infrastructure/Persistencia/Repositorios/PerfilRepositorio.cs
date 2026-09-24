using Lone.Application.Seguranca;
using Lone.Contracts.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Infrastructure.Persistencia.Servicos;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Repositorios;

public class PerfilRepositorio : ServicoDadosBase, IPerfilRepositorio
{
    public PerfilRepositorio(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario)
        : base(fabrica, usuario) { }

    public async Task<List<PerfilResumo>> ListarAsync(CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.Perfis.AsNoTracking()
            .OrderBy(p => p.Nome)
            .Select(p => new PerfilResumo
            {
                Id = p.Id,
                Nome = p.Nome,
                Administrador = p.Administrador,
                Ativo = p.Ativo,
                QuantidadeUsuarios = db.Set<UsuarioPerfil>().Count(up => up.PerfilId == p.Id)
            })
            .ToListAsync(ct);
    }

    public async Task<Perfil?> ObterAsync(Guid id, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.Perfis.AsNoTracking().Include(p => p.Permissoes).FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<bool> NomeEmUsoAsync(string nome, Guid ignorarId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.Perfis.AnyAsync(p => p.Nome == nome && p.Id != ignorarId, ct);
    }

    public async Task<IReadOnlySet<Guid>> IdsAdministradoresAtivosAsync(CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var ids = await db.Perfis.Where(p => p.Administrador && p.Ativo).Select(p => p.Id).ToListAsync(ct);
        return ids.ToHashSet();
    }

    public async Task SalvarAsync(Perfil perfil, bool novo, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);

        if (novo)
        {
            db.Perfis.Add(perfil);
        }
        else
        {
            var atual = await db.Perfis.Include(p => p.Permissoes).FirstOrDefaultAsync(p => p.Id == perfil.Id, ct)
                        ?? throw new ConflitoDeEdicaoException();

            var versaoAberta = perfil.Versao;
            perfil.Versao = atual.Versao;
            perfil.CriadoEm = atual.CriadoEm;
            perfil.AtualizadoEm = atual.AtualizadoEm;

            var entrada = db.Entry(atual);
            entrada.CurrentValues.SetValues(perfil);
            entrada.Property(p => p.Versao).OriginalValue = versaoAberta;

            // Permissões: remove as desmarcadas, inclui as novas (chave = Codigo).
            foreach (var removida in atual.Permissoes.Where(a => perfil.Permissoes.All(n => n.Codigo != a.Codigo)).ToList())
                atual.Permissoes.Remove(removida);
            foreach (var nova in perfil.Permissoes.Where(n => atual.Permissoes.All(a => a.Codigo != n.Codigo)))
            {
                var permissao = new PerfilPermissao { PerfilId = atual.Id, Codigo = nova.Codigo };
                atual.Permissoes.Add(permissao);
                db.Add(permissao);
            }

            entrada.Property(p => p.AtualizadoEm).IsModified = true;
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
