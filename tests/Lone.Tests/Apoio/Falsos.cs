using Lone.Application.Empresas;
using Lone.Application.Seguranca;
using Lone.Contracts.Colaboradores;
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

    /// <summary>Pessoas que o teste coloca à disposição: Id → (nome, ativa).</summary>
    public Dictionary<Guid, (string Nome, bool Ativa)> Pessoas { get; } = new();

    public Task<bool> PessoaEmUsoAsync(Guid pessoaId, Guid ignorarUsuarioId, CancellationToken ct) =>
        Task.FromResult(Usuarios.Any(u => u.PessoaId == pessoaId && u.Id != ignorarUsuarioId));

    public Task<(string Nome, bool Ativa)?> PessoaAsync(Guid pessoaId, CancellationToken ct) =>
        Task.FromResult(Pessoas.TryGetValue(pessoaId, out var p) ? p : ((string Nome, bool Ativa)?)null);

    public Task<List<PessoaOpcaoDto>> BuscarPessoasAsync(string texto, int limite, CancellationToken ct) =>
        Task.FromResult(Pessoas.Where(p => p.Value.Ativa && p.Value.Nome.Contains(texto, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Value.Nome).Take(limite).Select(p => new PessoaOpcaoDto(p.Key, p.Value.Nome)).ToList());
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

/// <summary>
/// Escopo de acesso para os testes (Fase 2a-2/2a-3). Padrão: alcance Tudo (o comportamento de antes). Com
/// <see cref="Restrito"/>, só as pessoas em <see cref="Gerenciadas"/> (e o que estiver em <see cref="NoEscopo"/>) contam.
/// </summary>
internal sealed class EscopoFixo : IEscopoPessoas, IPessoasNoEscopo
{
    private static readonly DateOnly Hoje = new(2026, 9, 28);
    private readonly Guid _euPadrao = Guid.NewGuid();

    public bool Restrito { get; init; }
    public Guid? Eu { get; init; }
    private Guid EuEfetivo => Eu ?? _euPadrao;

    /// <summary>Pessoas no alcance (e que o usuário gerencia em qualquer data).</summary>
    public HashSet<Guid> Gerenciadas { get; } = new();

    /// <summary>Cadastros de Pessoas no escopo (clientes diretos); os demais existem e ficam fora.</summary>
    public HashSet<Guid> NoEscopo { get; } = new();

    public Task<Lone.Domain.Comercial.EscopoResolvido> ObterAsync(CancellationToken ct = default) =>
        Task.FromResult(Restrito
            ? Lone.Domain.Comercial.RegrasEscopo.Resolver(Lone.Domain.Enums.AlcanceComercial.MinhaEquipe, EuEfetivo, null,
                [EquipeDoTeste()], [], Hoje)
            : Lone.Domain.Comercial.EscopoResolvido.Todos(Hoje));

    /// <summary>Uma equipe liderada por <see cref="Eu"/> com as <see cref="Gerenciadas"/> como membros.</summary>
    private Equipe EquipeDoTeste()
    {
        var id = Guid.NewGuid();
        var eu = EuEfetivo;
        var membros = Gerenciadas.Where(g => g != eu)
            .Select(g => new MembroEquipe { Id = Guid.NewGuid(), EquipeId = id, PessoaId = g, InicioEm = new DateOnly(2020, 1, 1) }).ToList();
        membros.Add(new MembroEquipe
        {
            Id = Guid.NewGuid(), EquipeId = id, PessoaId = eu, InicioEm = new DateOnly(2020, 1, 1),
            Papel = Lone.Domain.Enums.PapelNaEquipe.Lider
        });
        return new Equipe { Id = id, Nome = "Equipe do teste", Membros = membros };
    }

    public async Task ExigirAsync(Guid pessoaId, bool podeSerNovo = false, CancellationToken ct = default)
    {
        var escopo = await ObterAsync(ct);
        if (escopo.Tudo) return;
        if (!NoEscopo.Contains(pessoaId)) throw new ForaDoEscopoException();
    }

    public Task<bool> GerenciaEmAsync(Guid pessoaId, DateOnly data, CancellationToken ct = default) =>
        Task.FromResult(!Restrito || pessoaId == EuEfetivo || Gerenciadas.Contains(pessoaId));

    public Task<SituacaoNoEscopo> SituacaoAsync(Guid pessoaId, Lone.Domain.Comercial.EscopoResolvido escopo, CancellationToken ct) =>
        Task.FromResult(escopo.Tudo || NoEscopo.Contains(pessoaId) ? SituacaoNoEscopo.NoEscopo : SituacaoNoEscopo.ForaDoEscopo);

    public Task<HashSet<Guid>> ClientesDiretosAsync(IReadOnlyCollection<Guid> ids, Lone.Domain.Comercial.EscopoResolvido escopo, CancellationToken ct) =>
        Task.FromResult(ids.Where(i => escopo.Tudo || NoEscopo.Contains(i)).ToHashSet());
}
