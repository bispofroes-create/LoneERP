using Lone.Application.Empresas;
using Lone.Application.Seguranca;
using Lone.Contracts.Empresas;
using Lone.Contracts.Seguranca;
using Lone.Domain.Entidades;

namespace Lone.Tests.Apoio;

/// <summary>Hash trivial: os testes não precisam esperar 600 mil iterações de PBKDF2.</summary>
internal sealed class HasherRapido : IHasherSenha
{
    public string Gerar(string senha) => "teste:" + senha;
    public bool Verificar(string senha, string hashGravado) => hashGravado == "teste:" + senha;
}

internal sealed class UsuariosEmMemoria : IUsuarioRepositorio
{
    public List<Usuario> Usuarios { get; } = new();

    public Task<bool> ExisteAlgumAsync(CancellationToken ct) => Task.FromResult(Usuarios.Count > 0);

    public Task<Usuario?> ObterParaLoginAsync(string login, CancellationToken ct) =>
        Task.FromResult(Usuarios.FirstOrDefault(u => u.Login == login));

    public Task<Usuario?> ObterParaAcessoAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Usuarios.FirstOrDefault(u => u.Id == id));

    public Task RegistrarAcessoAsync(Guid usuarioId, int tentativasFalhas, DateTime? bloqueadoAte, DateTime? ultimoAcessoEm, CancellationToken ct)
    {
        var usuario = Usuarios.Single(u => u.Id == usuarioId);
        usuario.Acesso = new UsuarioAcesso
        {
            UsuarioId = usuarioId,
            TentativasFalhas = tentativasFalhas,
            BloqueadoAte = bloqueadoAte,
            UltimoAcessoEm = ultimoAcessoEm
        };
        return Task.CompletedTask;
    }

    public Task<List<UsuarioResumo>> ListarAsync(CancellationToken ct) =>
        Task.FromResult(Usuarios.Select(u => new UsuarioResumo { Id = u.Id, Login = u.Login, Nome = u.Nome, Ativo = u.Ativo }).ToList());

    public Task<Usuario?> ObterAsync(Guid id, CancellationToken ct) => ObterParaAcessoAsync(id, ct);

    public Task<bool> LoginEmUsoAsync(string login, Guid ignorarId, CancellationToken ct) =>
        Task.FromResult(Usuarios.Any(u => u.Login == login && u.Id != ignorarId));

    public Task<int> ContarAdministradoresAtivosAsync(Guid ignorarUsuarioId, CancellationToken ct) =>
        Task.FromResult(Usuarios.Count(u => u.Id != ignorarUsuarioId && u.Ativo && u.Perfis.Any(p => p.Perfil is { Administrador: true, Ativo: true })));

    public Task SalvarAsync(Usuario usuario, bool novo, CancellationToken ct)
    {
        Usuarios.RemoveAll(u => u.Id == usuario.Id);
        Usuarios.Add(usuario);
        return Task.CompletedTask;
    }

    public Task AlterarSenhaAsync(Guid usuarioId, string senhaHash, bool deveTrocarSenha, CancellationToken ct)
    {
        var usuario = Usuarios.Single(u => u.Id == usuarioId);
        usuario.SenhaHash = senhaHash;
        usuario.DeveTrocarSenha = deveTrocarSenha;
        return Task.CompletedTask;
    }
}

internal sealed class TokensEmMemoria : ITokenRenovacaoRepositorio
{
    private readonly TimeProvider _relogio;

    public TokensEmMemoria(TimeProvider relogio) => _relogio = relogio;

    public List<TokenRenovacao> Tokens { get; } = new();

    public Task<TokenRenovacao?> ObterPorHashAsync(string hash, CancellationToken ct) =>
        Task.FromResult(Tokens.FirstOrDefault(t => t.Hash == hash));

    public Task IncluirAsync(TokenRenovacao token, CancellationToken ct)
    {
        Tokens.Add(token);
        return Task.CompletedTask;
    }

    public Task SubstituirAsync(Guid usadoId, TokenRenovacao novo, CancellationToken ct)
    {
        var usado = Tokens.Single(t => t.Id == usadoId);
        if (usado.RevogadoEm is not null) throw new SessaoInvalidaException();
        usado.RevogadoEm = _relogio.GetUtcNow().UtcDateTime;
        usado.SubstituidoPorId = novo.Id;
        Tokens.Add(novo);
        return Task.CompletedTask;
    }

    public Task RevogarAsync(Guid id, CancellationToken ct)
    {
        var token = Tokens.Single(t => t.Id == id);
        token.RevogadoEm ??= _relogio.GetUtcNow().UtcDateTime;
        return Task.CompletedTask;
    }

    public Task RevogarTodosDoUsuarioAsync(Guid usuarioId, CancellationToken ct)
    {
        foreach (var token in Tokens.Where(t => t.UsuarioId == usuarioId && t.RevogadoEm is null))
            token.RevogadoEm = _relogio.GetUtcNow().UtcDateTime;
        return Task.CompletedTask;
    }
}

internal sealed class EmpresasFixas : IEmpresaConsultas
{
    public List<EmpresaAtiva> Estabelecimentos { get; } = new();

    public Task<List<EmpresaAtiva>> ListarEstabelecimentosAsync(CancellationToken ct = default) =>
        Task.FromResult(Estabelecimentos.ToList());

    public Task<List<EmpresaResumo>> ListarEmpresasAsync(CancellationToken ct = default) =>
        Task.FromResult(Estabelecimentos.GroupBy(e => e.EmpresaId).Select(g => new EmpresaResumo(g.Key, g.First().Nome, true)).ToList());
}

internal sealed class EmissorDeTeste : IEmissorToken
{
    private readonly TimeProvider _relogio;

    public EmissorDeTeste(TimeProvider relogio) => _relogio = relogio;

    public TimeSpan ValidadeRenovacao => TimeSpan.FromDays(14);

    public TokenAcesso Emitir(Guid usuarioId, string nome, string login, EmpresaAtiva? empresa) =>
        new($"acesso:{usuarioId}:{empresa?.EstabelecimentoId}", _relogio.GetUtcNow().AddMinutes(15));
}
