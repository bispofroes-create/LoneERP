using Lone.Aplicacao.Empresas;
using Lone.Core.Entidades;
using Lone.Core.Validacao;

namespace Lone.Aplicacao.Seguranca;

public interface IAutenticacaoService
{
    Task<bool> ExisteUsuarioAsync(CancellationToken ct = default);

    /// <summary>Só funciona com o banco sem usuários: cria o perfil Administrador e o primeiro usuário, e entra.</summary>
    Task CriarPrimeiroAdministradorAsync(string nome, string login, string senha, CancellationToken ct = default);

    Task<ResultadoLogin> EntrarAsync(string login, string senha, CancellationToken ct = default);

    /// <summary>Troca a senha do usuário logado. Lança ValidacaoException se algo estiver errado.</summary>
    Task TrocarSenhaAsync(string senhaAtual, string novaSenha, CancellationToken ct = default);

    /// <summary>Recarrega as empresas disponíveis para o usuário logado (ex.: após cadastrar uma empresa do grupo).</summary>
    Task AtualizarEmpresasAsync(CancellationToken ct = default);

    void Sair();
}

public sealed class AutenticacaoService : IAutenticacaoService
{
    private const string MensagemCredenciais = "Login ou senha incorretos.";

    private readonly IUsuarioRepositorio _usuarios;
    private readonly IAutenticador _autenticador;
    private readonly IHasherSenha _hasher;
    private readonly ISessao _sessao;
    private readonly IEmpresaConsultas _empresas;

    // Usado quando o login não existe, para a resposta demorar o mesmo tempo (não revela logins válidos).
    private readonly Lazy<string> _hashFicticio;

    public AutenticacaoService(IUsuarioRepositorio usuarios, IAutenticador autenticador, IHasherSenha hasher,
                               ISessao sessao, IEmpresaConsultas empresas)
    {
        _empresas = empresas;
        _usuarios = usuarios;
        _autenticador = autenticador;
        _hasher = hasher;
        _sessao = sessao;
        _hashFicticio = new Lazy<string>(() => hasher.Gerar(Guid.NewGuid().ToString()));
    }

    public Task<bool> ExisteUsuarioAsync(CancellationToken ct = default) => _usuarios.ExisteAlgumAsync(ct);

    public async Task CriarPrimeiroAdministradorAsync(string nome, string login, string senha, CancellationToken ct = default)
    {
        if (await _usuarios.ExisteAlgumAsync(ct))
            throw new ValidacaoException(["Já existem usuários cadastrados. Entre com um deles."]);

        login = PoliticaSenha.NormalizarLogin(login);
        var erros = new List<string>();
        if (string.IsNullOrWhiteSpace(nome)) erros.Add("Informe o nome.");
        if (!PoliticaSenha.LoginValido(login)) erros.Add("Login inválido: use de 3 a 60 letras sem acento, números, ponto, hífen ou sublinhado.");
        erros.AddRange(PoliticaSenha.ValidarSenha(senha, login));
        if (erros.Count > 0) throw new ValidacaoException(erros);

        var hash = await Task.Run(() => _hasher.Gerar(senha), ct);
        var perfil = new Perfil { Nome = "Administrador", Descricao = "Acesso total ao sistema", Administrador = true };
        var usuario = new Usuario
        {
            Nome = nome.Trim(),
            Login = login,
            SenhaHash = hash,
            Perfis = [new UsuarioPerfil { Perfil = perfil }]
        };

        await _usuarios.SalvarAsync(usuario, ct);

        var resultado = await EntrarAsync(login, senha, ct);
        if (!resultado.Sucesso)
            throw new InvalidOperationException(resultado.Mensagem);
    }

    public async Task<ResultadoLogin> EntrarAsync(string login, string senha, CancellationToken ct = default)
    {
        login = PoliticaSenha.NormalizarLogin(login);
        var agora = DateTime.Now;
        var usuario = login.Length == 0 ? null : await _usuarios.ObterParaLoginAsync(login, ct);

        if (usuario is null)
        {
            await Task.Run(() => _hasher.Verificar(senha, _hashFicticio.Value), ct);
            return new ResultadoLogin(SituacaoLogin.CredenciaisInvalidas, MensagemCredenciais);
        }

        if (!usuario.Ativo)
            return new ResultadoLogin(SituacaoLogin.Inativo, "Este usuário está inativo. Fale com o administrador.");

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
                : new ResultadoLogin(SituacaoLogin.CredenciaisInvalidas,
                    $"{MensagemCredenciais} Restam {Usuario.MaximoTentativas - falhas} tentativa(s) antes do bloqueio.");
        }

        await _usuarios.RegistrarAcessoAsync(usuario.Id, 0, null, agora, ct);

        var perfis = usuario.Perfis
            .Where(up => up.Perfil is { Ativo: true })
            .Select(up => new PerfilDaSessao(
                up.EmpresaId,
                up.Perfil!.Administrador,
                up.Perfil.Permissoes.Select(p => p.Codigo).ToHashSet()))
            .ToList();

        var estabelecimentos = await _empresas.ListarEstabelecimentosAsync(ct);
        _sessao.Iniciar(new DadosSessao(usuario.Id, usuario.Nome, usuario.Login, usuario.DeveTrocarSenha, perfis), estabelecimentos);

        return new ResultadoLogin(SituacaoLogin.Sucesso, string.Empty);
    }

    public async Task TrocarSenhaAsync(string senhaAtual, string novaSenha, CancellationToken ct = default)
    {
        if (!_sessao.Autenticada || _sessao.Id is not int id)
            throw new InvalidOperationException("Nenhum usuário logado.");

        var usuario = await _usuarios.ObterParaLoginAsync(_sessao.Login, ct)
                      ?? throw new InvalidOperationException("Usuário não encontrado.");

        var erros = new List<string>();
        if (!await _autenticador.ValidarAsync(usuario, senhaAtual, ct))
            erros.Add("A senha atual está incorreta.");
        erros.AddRange(PoliticaSenha.ValidarSenha(novaSenha, usuario.Login));
        if (novaSenha == senhaAtual)
            erros.Add("A nova senha precisa ser diferente da atual.");
        if (erros.Count > 0) throw new ValidacaoException(erros);

        var hash = await Task.Run(() => _hasher.Gerar(novaSenha), ct);
        await _usuarios.AlterarSenhaAsync(id, hash, deveTrocarSenha: false, ct);
        _sessao.MarcarSenhaTrocada();
    }

    public async Task AtualizarEmpresasAsync(CancellationToken ct = default)
    {
        if (!_sessao.Autenticada) return;
        _sessao.AtualizarEmpresas(await _empresas.ListarEstabelecimentosAsync(ct));
    }

    public void Sair() => _sessao.Encerrar();

    private static ResultadoLogin Bloqueado(DateTime ate) =>
        new(SituacaoLogin.Bloqueado,
            $"Acesso bloqueado por excesso de tentativas. Tente de novo após {ate:HH:mm} ou peça ao administrador para desbloquear.");
}
