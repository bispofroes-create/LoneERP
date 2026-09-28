using Lone.Application.Seguranca;
using Lone.Contracts.Colaboradores;
using Lone.Contracts.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Infrastructure.Persistencia.Servicos;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Repositorios;

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
        return await ComAcessoEPermissoes(db).FirstOrDefaultAsync(u => u.Login == login, ct);
    }

    public async Task<Usuario?> ObterParaAcessoAsync(Guid id, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await ComAcessoEPermissoes(db).FirstOrDefaultAsync(u => u.Id == id, ct);
    }

    private static IQueryable<Usuario> ComAcessoEPermissoes(LoneDbContext db) =>
        db.Usuarios.AsNoTracking()
            .Include(u => u.Acesso)
            .Include(u => u.Perfis).ThenInclude(p => p.Perfil!).ThenInclude(p => p.Permissoes)
            .AsSplitQuery();

    public async Task RegistrarAcessoAsync(Guid usuarioId, int tentativasFalhas, DateTime? bloqueadoAte,
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
                    // Sem escopo: cadastro de usuários (administração); ligar o usuário à pessoa precisa da base inteira.
                    : p.Perfil!.Nome + " (" + db.Pessoas.Where(x => x.Id == p.EmpresaId).Select(x => x.NomeExibicao ?? x.Nome).FirstOrDefault() + ")")
                    .ToList()
            })
            .ToListAsync(ct);
    }

    public async Task<Usuario?> ObterAsync(Guid id, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.Usuarios.AsNoTracking()
            .Include(u => u.Acesso)
            .Include(u => u.Perfis)
            .FirstOrDefaultAsync(u => u.Id == id, ct);
    }

    public async Task<bool> LoginEmUsoAsync(string login, Guid ignorarId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.Usuarios.AnyAsync(u => u.Login == login && u.Id != ignorarId, ct);
    }

    public async Task<int> ContarAdministradoresAtivosAsync(Guid ignorarUsuarioId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.Usuarios.CountAsync(u =>
            u.Id != ignorarUsuarioId && u.Ativo &&
            u.Perfis.Any(p => p.Perfil!.Administrador && p.Perfil.Ativo), ct);
    }

    public async Task SalvarAsync(Usuario usuario, bool novo, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);

        if (novo)
        {
            usuario.Acesso = null;
            db.Usuarios.Add(usuario);
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
            foreach (var incluido in usuario.Perfis.Where(n => !atual.Perfis.Any(a => Mesmo(n, a))).ToList())
            {
                var perfil = new UsuarioPerfil
                {
                    Id = incluido.Id == Guid.Empty ? IdSequencial.Novo() : incluido.Id,
                    UsuarioId = atual.Id,
                    PerfilId = incluido.PerfilId,
                    EmpresaId = incluido.EmpresaId
                };
                atual.Perfis.Add(perfil);
                db.Add(perfil);
            }

            entrada.Property(u => u.AtualizadoEm).IsModified = true;
        }

        await GravarAsync(db, ct);
    }

    public async Task AlterarSenhaAsync(Guid usuarioId, string senhaHash, bool deveTrocarSenha, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId, ct)
                      ?? throw new ConflitoDeEdicaoException();
        usuario.SenhaHash = senhaHash;
        usuario.DeveTrocarSenha = deveTrocarSenha;
        await GravarAsync(db, ct);
    }

    public async Task<bool> PessoaEmUsoAsync(Guid pessoaId, Guid ignorarUsuarioId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.Usuarios.AnyAsync(u => u.PessoaId == pessoaId && u.Id != ignorarUsuarioId, ct);
    }

    public async Task<(string Nome, bool Ativa)?> PessoaAsync(Guid pessoaId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        // Sem escopo: cadastro de usuários (administração); ligar o usuário à pessoa precisa da base inteira.
        var pessoa = await db.Pessoas.AsNoTracking().Where(p => p.Id == pessoaId)
            .Select(p => new { Nome = p.NomeExibicao ?? p.Nome, p.Situacao })
            .FirstOrDefaultAsync(ct);
        return pessoa is null
            ? null
            : (pessoa.Nome, pessoa.Situacao is SituacaoPessoa.Ativo or SituacaoPessoa.EmAnalise);
    }

    public async Task<List<PessoaOpcaoDto>> BuscarPessoasAsync(string texto, int limite, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        // Sem escopo: cadastro de usuários (administração); ligar o usuário à pessoa precisa da base inteira.
        var consulta = db.Pessoas.AsNoTracking()
            .Where(p => p.Situacao == SituacaoPessoa.Ativo || p.Situacao == SituacaoPessoa.EmAnalise);
        consulta = PessoaRepositorio.AplicarBusca(consulta, texto, db);
        return await consulta
            .OrderBy(p => p.NomeExibicao ?? p.Nome).ThenBy(p => p.Id)
            .Take(limite)
            .Select(p => new PessoaOpcaoDto(p.Id, p.NomeExibicao ?? p.Nome))
            .ToListAsync(ct);
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
