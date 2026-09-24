using Lone.Application.Empresas;
using Lone.Contracts.Empresas;
using Lone.Contracts.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Validacao;

namespace Lone.Application.Seguranca;

/// <summary>Resultado do login: sessão aberta, ou o motivo da recusa (mensagem pronta para o usuário).</summary>
public sealed record ResultadoEntrada(SituacaoLogin Situacao, string Mensagem, SessaoDto? Sessao)
{
    public bool Sucesso => Situacao == SituacaoLogin.Sucesso;
}

/// <summary>Login, sessão (tokens), troca de empresa e troca de senha. Roda na API.</summary>
public interface IAutenticacaoService
{
    Task<SituacaoSistema> ObterSituacaoAsync(CancellationToken ct = default);

    /// <summary>Só funciona com o banco sem usuários: cria o perfil Administrador e o primeiro usuário, e abre a sessão.</summary>
    Task<SessaoDto> CriarPrimeiroAdministradorAsync(PrimeiroAcessoRequisicao requisicao, CancellationToken ct = default);

    Task<ResultadoEntrada> EntrarAsync(EntrarRequisicao requisicao, CancellationToken ct = default);

    /// <summary>Troca o token de renovação por uma sessão nova. Lança SessaoInvalidaException.</summary>
    Task<SessaoDto> RenovarAsync(RenovarSessaoRequisicao requisicao, CancellationToken ct = default);

    /// <summary>Muda a empresa ativa do usuário logado. Lança SessaoInvalidaException ou ValidacaoException.</summary>
    Task<SessaoDto> SelecionarEmpresaAsync(Guid usuarioId, SelecionarEmpresaRequisicao requisicao, CancellationToken ct = default);

    /// <summary>Troca a senha do usuário logado. Lança ValidacaoException se algo estiver errado.</summary>
    Task TrocarSenhaAsync(Guid usuarioId, TrocarSenhaRequisicao requisicao, CancellationToken ct = default);

    /// <summary>Encerra a sessão deste aparelho (revoga o token de renovação). Pode ser chamado mais de uma vez.</summary>
    Task SairAsync(SairRequisicao requisicao, CancellationToken ct = default);
}

public sealed class AutenticacaoService : IAutenticacaoService
{
    private const string MensagemCredenciais = "Login ou senha incorretos.";

    private readonly IUsuarioRepositorio _usuarios;
    private readonly ITokenRenovacaoRepositorio _tokens;
    private readonly IAutenticador _autenticador;
    private readonly IHasherSenha _hasher;
    private readonly IEmissorToken _emissor;
    private readonly IEmpresaConsultas _empresas;
    private readonly TimeProvider _relogio;

    // Usado quando o login não existe, para a resposta demorar o mesmo tempo (não revela logins válidos).
    private static readonly Lazy<string> HashFicticio = new(() => new HasherSenhaPbkdf2().Gerar(Guid.NewGuid().ToString()));

    public AutenticacaoService(IUsuarioRepositorio usuarios, ITokenRenovacaoRepositorio tokens, IAutenticador autenticador,
                               IHasherSenha hasher, IEmissorToken emissor, IEmpresaConsultas empresas, TimeProvider relogio)
    {
        _usuarios = usuarios;
        _tokens = tokens;
        _autenticador = autenticador;
        _hasher = hasher;
        _emissor = emissor;
        _empresas = empresas;
        _relogio = relogio;
    }

    public async Task<SituacaoSistema> ObterSituacaoAsync(CancellationToken ct = default) =>
        new(await _usuarios.ExisteAlgumAsync(ct));

    public async Task<SessaoDto> CriarPrimeiroAdministradorAsync(PrimeiroAcessoRequisicao requisicao, CancellationToken ct = default)
    {
        if (await _usuarios.ExisteAlgumAsync(ct))
            throw new ValidacaoException(["Já existem usuários cadastrados. Entre com um deles."]);

        var login = PoliticaSenha.NormalizarLogin(requisicao.Login);
        var nome = (requisicao.Nome ?? string.Empty).Trim();
        var erros = new List<string>();
        if (nome.Length == 0) erros.Add("Informe o nome.");
        if (!PoliticaSenha.LoginValido(login)) erros.Add(PoliticaSenha.MensagemLoginInvalido);
        erros.AddRange(PoliticaSenha.ValidarSenha(requisicao.Senha, login));
        if (erros.Count > 0) throw new ValidacaoException(erros);

        var hash = await Task.Run(() => _hasher.Gerar(requisicao.Senha), ct);
        var perfil = new Perfil
        {
            Id = IdSequencial.Novo(),
            Nome = "Administrador",
            Descricao = "Acesso total ao sistema",
            Administrador = true
        };
        var usuario = new Usuario
        {
            Id = IdSequencial.Novo(),
            Nome = nome,
            Login = login,
            SenhaHash = hash,
            Perfis = [new UsuarioPerfil { Id = IdSequencial.Novo(), PerfilId = perfil.Id, Perfil = perfil }]
        };
        usuario.Perfis[0].UsuarioId = usuario.Id;

        await _usuarios.SalvarAsync(usuario, novo: true, ct);

        var resultado = await EntrarAsync(new EntrarRequisicao(login, requisicao.Senha, requisicao.Dispositivo), ct);
        return resultado.Sessao ?? throw new InvalidOperationException(resultado.Mensagem);
    }

    public async Task<ResultadoEntrada> EntrarAsync(EntrarRequisicao requisicao, CancellationToken ct = default)
    {
        var login = PoliticaSenha.NormalizarLogin(requisicao.Login);
        var senha = requisicao.Senha ?? string.Empty;
        var agora = _relogio.GetUtcNow().UtcDateTime;
        var usuario = login.Length == 0 ? null : await _usuarios.ObterParaLoginAsync(login, ct);

        if (usuario is null)
        {
            await Task.Run(() => _hasher.Verificar(senha, HashFicticio.Value), ct);
            return Recusado(SituacaoLogin.CredenciaisInvalidas, MensagemCredenciais);
        }

        if (!usuario.Ativo)
            return Recusado(SituacaoLogin.Inativo, "Este usuário está inativo. Fale com o administrador.");

        var acesso = usuario.Acesso ?? new UsuarioAcesso { UsuarioId = usuario.Id };
        if (acesso.EstaBloqueado(agora))
            return Bloqueado(acesso.BloqueadoAte!.Value);

        if (!await _autenticador.ValidarAsync(usuario, senha, ct))
        {
            var falhas = acesso.TentativasFalhas + 1;
            DateTime? bloqueadoAte = falhas >= Usuario.MaximoTentativas ? agora + Usuario.TempoBloqueio : null;
            await _usuarios.RegistrarAcessoAsync(usuario.Id, bloqueadoAte is null ? falhas : 0, bloqueadoAte, acesso.UltimoAcessoEm, ct);

            return bloqueadoAte is { } ate
                ? Bloqueado(ate)
                : Recusado(SituacaoLogin.CredenciaisInvalidas,
                    $"{MensagemCredenciais} Restam {Usuario.MaximoTentativas - falhas} tentativa(s) antes do bloqueio.");
        }

        await _usuarios.RegistrarAcessoAsync(usuario.Id, 0, null, agora, ct);

        var sessao = await AbrirSessaoAsync(usuario, estabelecimentoPreferido: null, requisicao.Dispositivo, substituirTokenId: null, ct);
        return new ResultadoEntrada(SituacaoLogin.Sucesso, string.Empty, sessao);
    }

    public async Task<SessaoDto> RenovarAsync(RenovarSessaoRequisicao requisicao, CancellationToken ct = default)
    {
        var token = await ObterTokenValidoAsync(requisicao.TokenRenovacao, ct);
        var usuario = await ObterUsuarioAtivoAsync(token.UsuarioId, ct);
        return await AbrirSessaoAsync(usuario, token.EstabelecimentoId, token.Dispositivo, token.Id, ct);
    }

    public async Task<SessaoDto> SelecionarEmpresaAsync(Guid usuarioId, SelecionarEmpresaRequisicao requisicao, CancellationToken ct = default)
    {
        var token = await ObterTokenValidoAsync(requisicao.TokenRenovacao, ct);
        if (token.UsuarioId != usuarioId)
            throw new SessaoInvalidaException();

        var usuario = await ObterUsuarioAtivoAsync(usuarioId, ct);
        var disponiveis = await EmpresasDisponiveisAsync(RegrasDeAcesso.PerfisAtivos(usuario), ct);
        if (disponiveis.All(e => e.EstabelecimentoId != requisicao.EstabelecimentoId))
            throw new ValidacaoException(["Esta empresa não está disponível para o seu usuário."]);

        return await AbrirSessaoAsync(usuario, requisicao.EstabelecimentoId, token.Dispositivo, token.Id, ct);
    }

    public async Task TrocarSenhaAsync(Guid usuarioId, TrocarSenhaRequisicao requisicao, CancellationToken ct = default)
    {
        var usuario = await _usuarios.ObterParaAcessoAsync(usuarioId, ct)
                      ?? throw new SessaoInvalidaException();

        var erros = new List<string>();
        if (!await _autenticador.ValidarAsync(usuario, requisicao.SenhaAtual ?? string.Empty, ct))
            erros.Add("A senha atual está incorreta.");
        erros.AddRange(PoliticaSenha.ValidarSenha(requisicao.NovaSenha, usuario.Login));
        if (requisicao.NovaSenha == requisicao.SenhaAtual)
            erros.Add("A nova senha precisa ser diferente da atual.");
        if (erros.Count > 0) throw new ValidacaoException(erros);

        var hash = await Task.Run(() => _hasher.Gerar(requisicao.NovaSenha), ct);
        await _usuarios.AlterarSenhaAsync(usuarioId, hash, deveTrocarSenha: false, ct);
    }

    public async Task SairAsync(SairRequisicao requisicao, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(requisicao.TokenRenovacao)) return;
        var token = await _tokens.ObterPorHashAsync(GeradorTokenRenovacao.Hash(requisicao.TokenRenovacao), ct);
        if (token is { RevogadoEm: null })
            await _tokens.RevogarAsync(token.Id, ct);
    }

    // ---------------------------------------------------------------- Apoio

    /// <summary>
    /// Emite o token de acesso e um token de renovação novo. A empresa preferida é mantida se ainda estiver
    /// disponível; senão, se houver só uma empresa, ela fica ativa sozinha.
    /// </summary>
    private async Task<SessaoDto> AbrirSessaoAsync(Usuario usuario, Guid? estabelecimentoPreferido, string? dispositivo,
                                                    Guid? substituirTokenId, CancellationToken ct)
    {
        var perfis = RegrasDeAcesso.PerfisAtivos(usuario);
        var disponiveis = await EmpresasDisponiveisAsync(perfis, ct);
        var empresa = disponiveis.FirstOrDefault(e => e.EstabelecimentoId == estabelecimentoPreferido)
                      ?? (disponiveis.Count == 1 ? disponiveis[0] : null);
        var efetivo = RegrasDeAcesso.Efetivo(perfis, empresa?.EmpresaId);

        var acesso = _emissor.Emitir(usuario.Id, usuario.Nome, usuario.Login, empresa);
        var (tokenRenovacao, hash) = GeradorTokenRenovacao.Novo();
        var agora = _relogio.GetUtcNow();
        var expiraRenovacao = agora + _emissor.ValidadeRenovacao;

        var registro = new TokenRenovacao
        {
            Id = IdSequencial.Novo(),
            UsuarioId = usuario.Id,
            Hash = hash,
            EstabelecimentoId = empresa?.EstabelecimentoId,
            CriadoEm = agora.UtcDateTime,
            ExpiraEm = expiraRenovacao.UtcDateTime,
            Dispositivo = LimitarDispositivo(dispositivo)
        };

        if (substituirTokenId is Guid usado)
            await _tokens.SubstituirAsync(usado, registro, ct);
        else
            await _tokens.IncluirAsync(registro, ct);

        return new SessaoDto
        {
            TokenAcesso = acesso.Token,
            AcessoExpiraEm = acesso.ExpiraEm,
            TokenRenovacao = tokenRenovacao,
            RenovacaoExpiraEm = expiraRenovacao,
            UsuarioId = usuario.Id,
            Nome = usuario.Nome,
            Login = usuario.Login,
            DeveTrocarSenha = usuario.DeveTrocarSenha,
            EmpresaAtiva = empresa,
            EmpresasDisponiveis = disponiveis.ToList(),
            Administrador = efetivo.Administrador,
            Permissoes = efetivo.Permissoes.Order().ToList()
        };
    }

    /// <summary>
    /// Token existente, não expirado e não revogado. Se um token já substituído voltar a ser usado,
    /// alguém copiou o token: todas as sessões do usuário são encerradas.
    /// </summary>
    private async Task<TokenRenovacao> ObterTokenValidoAsync(string? tokenRenovacao, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(tokenRenovacao))
            throw new SessaoInvalidaException();

        var token = await _tokens.ObterPorHashAsync(GeradorTokenRenovacao.Hash(tokenRenovacao), ct)
                    ?? throw new SessaoInvalidaException();

        if (token.RevogadoEm is not null && token.SubstituidoPorId is not null)
        {
            await _tokens.RevogarTodosDoUsuarioAsync(token.UsuarioId, ct);
            throw new SessaoInvalidaException("Sua sessão foi encerrada por segurança. Entre novamente.");
        }

        if (!token.Valido(_relogio.GetUtcNow().UtcDateTime))
            throw new SessaoInvalidaException();

        return token;
    }

    private async Task<Usuario> ObterUsuarioAtivoAsync(Guid usuarioId, CancellationToken ct)
    {
        var usuario = await _usuarios.ObterParaAcessoAsync(usuarioId, ct);
        if (usuario is { Ativo: true }) return usuario;

        await _tokens.RevogarTodosDoUsuarioAsync(usuarioId, ct);
        throw new SessaoInvalidaException("Seu usuário foi desativado. Fale com o administrador.");
    }

    private async Task<IReadOnlyList<EmpresaAtiva>> EmpresasDisponiveisAsync(IReadOnlyList<PerfilAtribuido> perfis, CancellationToken ct) =>
        RegrasDeAcesso.EmpresasDisponiveis(perfis, await _empresas.ListarEstabelecimentosAsync(ct));

    private static string? LimitarDispositivo(string? dispositivo) =>
        string.IsNullOrWhiteSpace(dispositivo) ? null : dispositivo.Trim() is { Length: > 100 } d ? d[..100] : dispositivo.Trim();

    private static ResultadoEntrada Recusado(SituacaoLogin situacao, string mensagem) => new(situacao, mensagem, null);

    private ResultadoEntrada Bloqueado(DateTime ateUtc)
    {
        // Minutos restantes, e não a hora: o servidor e o aparelho podem estar em fusos diferentes.
        var minutos = Math.Max(1, (int)Math.Ceiling((ateUtc - _relogio.GetUtcNow().UtcDateTime).TotalMinutes));
        return Recusado(SituacaoLogin.Bloqueado,
            $"Acesso bloqueado por excesso de tentativas. Tente de novo em {minutos} minuto(s) ou peça ao administrador para desbloquear.");
    }
}
