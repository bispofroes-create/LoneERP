using Lone.Aplicacao.Comum;
using Lone.Aplicacao.Seguranca;
using Lone.Core.Entidades;
using Lone.Data.Servicos;
using Microsoft.EntityFrameworkCore;

namespace Lone.Data.Repositorios;

public class UsuarioRepositorio : ServicoDadosBase, IUsuarioRepositorio
{
    public UsuarioRepositorio(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario)
        : base(fabrica, usuario) { }

    public async Task<bool> ExisteAlgumAsync(CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.Usuarios.AnyAsync(ct);
    }

    public async Task<Usuario?> ObterParaLoginAsync(string login, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.Usuarios.AsNoTracking()
            .Include(u => u.Acesso)
            .Include(u => u.Perfis).ThenInclude(p => p.Perfil!).ThenInclude(p => p.Permissoes)
            .AsSplitQuery()
            .FirstOrDefaultAsync(u => u.Login == login, ct);
    }

    public async Task RegistrarAcessoAsync(int usuarioId, int tentativasFalhas, DateTime? bloqueadoAte,
                                           DateTime? ultimoAcessoEm, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var acesso = await db.Set<UsuarioAcesso>().FirstOrDefaultAsync(a => a.UsuarioId == usuarioId, ct);
        if (acesso is null)
        {
            acesso = new UsuarioAcesso { UsuarioId = usuarioId };
            db.Add(acesso);
        }

        acesso.TentativasFalhas = tentativasFalhas;
        acesso.BloqueadoAte = bloqueadoAte;
        acesso.UltimoAcessoEm = ultimoAcessoEm;
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<UsuarioResumo>> ListarAsync(CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.Usuarios.AsNoTracking()
            .OrderBy(u => u.Nome)
            .Select(u => new UsuarioResumo
            {
                Id = u.Id,
                Login = u.Login,
                Nome = u.Nome,
                Ativo = u.Ativo,
                BloqueadoAte = u.Acesso != null ? u.Acesso.BloqueadoAte : null,
                UltimoAcessoEm = u.Acesso != null ? u.Acesso.UltimoAcessoEm : null,
                Perfis = u.Perfis.Select(p => p.EmpresaId == null
                    ? p.Perfil!.Nome
                    : p.Perfil!.Nome + " (" + db.Pessoas.Where(x => x.Id == p.EmpresaId).Select(x => x.NomeExibicao ?? x.Nome).FirstOrDefault() + ")")
                    .ToList()
            })
            .ToListAsync(ct);
    }

    public async Task<Usuario?> ObterAsync(int id, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.Usuarios.AsNoTracking()
            .Include(u => u.Acesso)
            .Include(u => u.Perfis)
            .FirstOrDefaultAsync(u => u.Id == id, ct);
    }

    public async Task<bool> LoginEmUsoAsync(string login, int ignorarId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.Usuarios.AnyAsync(u => u.Login == login && u.Id != ignorarId, ct);
    }

    public async Task<int> ContarAdministradoresAtivosAsync(int ignorarUsuarioId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.Usuarios.CountAsync(u =>
            u.Id != ignorarUsuarioId && u.Ativo &&
            u.Perfis.Any(p => p.Perfil!.Administrador && p.Perfil.Ativo), ct);
    }

    public async Task<Usuario> SalvarAsync(Usuario usuario, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        Usuario salvo;

        if (usuario.Id == 0)
        {
            usuario.Acesso = null;
            db.Usuarios.Add(usuario);
            salvo = usuario;
        }
        else
        {
            var atual = await db.Usuarios.Include(u => u.Perfis).FirstOrDefaultAsync(u => u.Id == usuario.Id, ct)
                        ?? throw new ConflitoDeEdicaoException();

            var versaoAberta = usuario.Versao;
            usuario.Versao = atual.Versao;
            usuario.CriadoEm = atual.CriadoEm;
            usuario.AtualizadoEm = atual.AtualizadoEm;

            var entrada = db.Entry(atual);
            entrada.CurrentValues.SetValues(usuario);
            entrada.Property(u => u.Versao).OriginalValue = versaoAberta;

            // Perfis por empresa: remove os que saíram, inclui os novos (chave = perfil + empresa).
            foreach (var removido in atual.Perfis.Where(a => !usuario.Perfis.Any(n => Mesmo(n, a))).ToList())
                atual.Perfis.Remove(removido);
            foreach (var novo in usuario.Perfis.Where(n => !atual.Perfis.Any(a => Mesmo(n, a))).ToList())
                atual.Perfis.Add(new UsuarioPerfil { UsuarioId = atual.Id, PerfilId = novo.PerfilId, EmpresaId = novo.EmpresaId });

            entrada.Property(u => u.AtualizadoEm).IsModified = true;
            salvo = atual;
        }

        await GravarAsync(db, ct);
        return salvo;
    }

    public async Task AlterarSenhaAsync(int usuarioId, string senhaHash, bool deveTrocarSenha, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId, ct)
                      ?? throw new ConflitoDeEdicaoException();
        usuario.SenhaHash = senhaHash;
        usuario.DeveTrocarSenha = deveTrocarSenha;
        await GravarAsync(db, ct);
    }

    private static bool Mesmo(UsuarioPerfil a, UsuarioPerfil b) => a.PerfilId == b.PerfilId && a.EmpresaId == b.EmpresaId;

    private static async Task GravarAsync(LoneDbContext db, CancellationToken ct)
    {
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
