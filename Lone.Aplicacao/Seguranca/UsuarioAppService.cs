using Lone.Aplicacao.Empresas;
using Lone.Core.Entidades;
using Lone.Core.ObjetosDeValor;
using Lone.Core.Validacao;

namespace Lone.Aplicacao.Seguranca;

public interface IUsuarioAppService
{
    Task<List<UsuarioResumo>> ListarAsync(CancellationToken ct = default);
    Task<Usuario?> ObterAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Inclui ou altera. <paramref name="novaSenha"/> é obrigatória na inclusão; na alteração, só se for redefinir.
    /// </summary>
    Task<Usuario> SalvarAsync(Usuario dados, string? novaSenha, CancellationToken ct = default);

    Task DesbloquearAsync(int id, CancellationToken ct = default);

    /// <summary>Perfis que podem ser atribuídos (quem gerencia usuários vê a lista mesmo sem gerenciar perfis).</summary>
    Task<List<PerfilResumo>> ListarPerfisAsync(CancellationToken ct = default);

    /// <summary>Empresas do grupo, para atribuir perfis por empresa.</summary>
    Task<List<EmpresaResumo>> ListarEmpresasAsync(CancellationToken ct = default);
}

public sealed class UsuarioAppService : IUsuarioAppService
{
    private readonly IUsuarioRepositorio _usuarios;
    private readonly IPerfilRepositorio _perfis;
    private readonly IHasherSenha _hasher;
    private readonly IAutorizacao _autorizacao;
    private readonly ISessao _sessao;
    private readonly IEmpresaConsultas _empresas;

    public UsuarioAppService(IUsuarioRepositorio usuarios, IPerfilRepositorio perfis, IHasherSenha hasher,
                             IAutorizacao autorizacao, ISessao sessao, IEmpresaConsultas empresas)
    {
        _empresas = empresas;
        _usuarios = usuarios;
        _perfis = perfis;
        _hasher = hasher;
        _autorizacao = autorizacao;
        _sessao = sessao;
    }

    public Task<List<UsuarioResumo>> ListarAsync(CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Seguranca.GerenciarUsuarios);
        return _usuarios.ListarAsync(ct);
    }

    public Task<Usuario?> ObterAsync(int id, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Seguranca.GerenciarUsuarios);
        return _usuarios.ObterAsync(id, ct);
    }

    public async Task<Usuario> SalvarAsync(Usuario dados, string? novaSenha, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Seguranca.GerenciarUsuarios);

        var anterior = dados.Id == 0 ? null : await _usuarios.ObterAsync(dados.Id, ct)
                       ?? throw new ValidacaoException(["Este usuário não existe mais."]);

        dados.Login = PoliticaSenha.NormalizarLogin(dados.Login);
        dados.Nome = dados.Nome.Trim();
        dados.Email = string.IsNullOrWhiteSpace(dados.Email) ? null : dados.Email.Trim();
        dados.Perfis = dados.Perfis.DistinctBy(p => (p.PerfilId, p.EmpresaId)).ToList();

        var erros = new List<string>();
        if (dados.Nome.Length == 0) erros.Add("Informe o nome.");
        if (!PoliticaSenha.LoginValido(dados.Login))
            erros.Add("Login inválido: use de 3 a 60 letras sem acento, números, ponto, hífen ou sublinhado.");
        else if (await _usuarios.LoginEmUsoAsync(dados.Login, dados.Id, ct))
            erros.Add("Este login já está em uso.");
        if (dados.Email is not null)
        {
            if (Email.TentarCriar(dados.Email, out var email)) dados.Email = email!.Valor;
            else erros.Add("E-mail inválido.");
        }
        if (dados.Perfis.Count == 0) erros.Add("Escolha ao menos um perfil.");
        if (dados.Perfis.Any(p => p.EmpresaId is not null))
        {
            var empresas = (await _empresas.ListarEmpresasAsync(ct)).Select(e => e.Id).ToHashSet();
            if (dados.Perfis.Any(p => p.EmpresaId is int id && !empresas.Contains(id)))
                erros.Add("Perfil atribuído a uma empresa que não é do grupo.");
        }

        if (anterior is null && string.IsNullOrEmpty(novaSenha))
            erros.Add("Informe a senha inicial do usuário.");
        if (!string.IsNullOrEmpty(novaSenha))
            erros.AddRange(PoliticaSenha.ValidarSenha(novaSenha, dados.Login));

        if (anterior is not null && anterior.Id == _sessao.Id && !dados.Ativo)
            erros.Add("Você não pode desativar o seu próprio usuário.");

        await VerificarUltimoAdministradorAsync(dados, anterior, erros, ct);

        if (erros.Count > 0) throw new ValidacaoException(erros);

        // Campos que a tela não edita são preservados.
        if (anterior is not null)
        {
            dados.SenhaHash = anterior.SenhaHash;
        }

        if (!string.IsNullOrEmpty(novaSenha))
        {
            dados.SenhaHash = await Task.Run(() => _hasher.Gerar(novaSenha), ct);
            dados.DeveTrocarSenha = true; // senha definida por outra pessoa: o usuário troca no próximo login
        }

        return await _usuarios.SalvarAsync(dados, ct);
    }

    public Task<List<PerfilResumo>> ListarPerfisAsync(CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Seguranca.GerenciarUsuarios);
        return _perfis.ListarAsync(ct);
    }

    public Task<List<EmpresaResumo>> ListarEmpresasAsync(CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Seguranca.GerenciarUsuarios);
        return _empresas.ListarEmpresasAsync(ct);
    }

    public async Task DesbloquearAsync(int id, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Seguranca.GerenciarUsuarios);
        var usuario = await _usuarios.ObterAsync(id, ct) ?? throw new ValidacaoException(["Este usuário não existe mais."]);
        await _usuarios.RegistrarAcessoAsync(id, 0, null, usuario.Acesso?.UltimoAcessoEm, ct);
    }

    /// <summary>Impede que o sistema fique sem nenhum administrador ativo.</summary>
    private async Task VerificarUltimoAdministradorAsync(Usuario dados, Usuario? anterior, List<string> erros, CancellationToken ct)
    {
        if (anterior is null) return;

        var administradores = await _perfis.IdsAdministradoresAtivosAsync(ct);
        var eraAdministrador = anterior.Ativo && anterior.Perfis.Any(p => administradores.Contains(p.PerfilId));
        var continuaAdministrador = dados.Ativo && dados.Perfis.Any(p => administradores.Contains(p.PerfilId));

        if (eraAdministrador && !continuaAdministrador &&
            await _usuarios.ContarAdministradoresAtivosAsync(anterior.Id, ct) == 0)
            erros.Add("Este é o último administrador ativo. Defina outro administrador antes de mudar este usuário.");
    }
}
