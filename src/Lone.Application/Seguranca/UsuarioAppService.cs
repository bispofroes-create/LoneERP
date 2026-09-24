using Lone.Application.Empresas;
using Lone.Contracts.Empresas;
using Lone.Contracts.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.ObjetosDeValor;
using Lone.Domain.Validacao;

namespace Lone.Application.Seguranca;

public interface IUsuarioAppService
{
    Task<List<UsuarioResumo>> ListarAsync(CancellationToken ct = default);
    Task<UsuarioDto?> ObterAsync(Guid id, CancellationToken ct = default);

    /// <summary>Inclui ou altera. A senha é obrigatória na inclusão; na alteração, só para redefinir.</summary>
    Task<UsuarioDto> SalvarAsync(SalvarUsuarioRequisicao requisicao, CancellationToken ct = default);

    Task DesbloquearAsync(Guid id, CancellationToken ct = default);

    /// <summary>Perfis que podem ser atribuídos (quem gerencia usuários vê a lista mesmo sem gerenciar perfis).</summary>
    Task<List<PerfilResumo>> ListarPerfisAsync(CancellationToken ct = default);

    /// <summary>Empresas do grupo, para atribuir perfis por empresa.</summary>
    Task<List<EmpresaResumo>> ListarEmpresasAsync(CancellationToken ct = default);
}

public sealed class UsuarioAppService : IUsuarioAppService
{
    private readonly IUsuarioRepositorio _usuarios;
    private readonly IPerfilRepositorio _perfis;
    private readonly ITokenRenovacaoRepositorio _tokens;
    private readonly IHasherSenha _hasher;
    private readonly IAutorizacao _autorizacao;
    private readonly IUsuarioAtual _usuarioAtual;
    private readonly IEmpresaConsultas _empresas;

    public UsuarioAppService(IUsuarioRepositorio usuarios, IPerfilRepositorio perfis, ITokenRenovacaoRepositorio tokens,
                             IHasherSenha hasher, IAutorizacao autorizacao, IUsuarioAtual usuarioAtual, IEmpresaConsultas empresas)
    {
        _usuarios = usuarios;
        _perfis = perfis;
        _tokens = tokens;
        _hasher = hasher;
        _autorizacao = autorizacao;
        _usuarioAtual = usuarioAtual;
        _empresas = empresas;
    }

    public Task<List<UsuarioResumo>> ListarAsync(CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Seguranca.GerenciarUsuarios);
        return _usuarios.ListarAsync(ct);
    }

    public async Task<UsuarioDto?> ObterAsync(Guid id, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Seguranca.GerenciarUsuarios);
        var usuario = await _usuarios.ObterAsync(id, ct);
        return usuario is null ? null : ParaDto(usuario);
    }

    public async Task<UsuarioDto> SalvarAsync(SalvarUsuarioRequisicao requisicao, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Seguranca.GerenciarUsuarios);

        var dto = requisicao.Usuario;
        var novaSenha = string.IsNullOrEmpty(requisicao.NovaSenha) ? null : requisicao.NovaSenha;
        var anterior = dto.Id == Guid.Empty ? null : await _usuarios.ObterAsync(dto.Id, ct);
        var novo = anterior is null;
        var id = dto.Id == Guid.Empty ? IdSequencial.Novo() : dto.Id;

        var dados = new Usuario
        {
            Id = id,
            Versao = dto.Versao,
            Login = PoliticaSenha.NormalizarLogin(dto.Login),
            Nome = (dto.Nome ?? string.Empty).Trim(),
            Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim(),
            Ativo = dto.Ativo,
            DeveTrocarSenha = dto.DeveTrocarSenha,
            Perfis = dto.Perfis
                .DistinctBy(p => (p.PerfilId, p.EmpresaId))
                .Select(p => new UsuarioPerfil { Id = IdSequencial.Novo(), UsuarioId = id, PerfilId = p.PerfilId, EmpresaId = p.EmpresaId })
                .ToList()
        };

        var erros = new List<string>();
        if (dados.Nome.Length == 0) erros.Add("Informe o nome.");
        if (!PoliticaSenha.LoginValido(dados.Login))
            erros.Add(PoliticaSenha.MensagemLoginInvalido);
        else if (await _usuarios.LoginEmUsoAsync(dados.Login, id, ct))
            erros.Add("Este login já está em uso.");
        if (dados.Email is not null)
        {
            if (Email.TentarCriar(dados.Email, out var email)) dados.Email = email!.Valor;
            else erros.Add("E-mail inválido.");
        }

        if (dados.Perfis.Count == 0) erros.Add("Escolha ao menos um perfil.");
        await ValidarPerfisEEmpresasAsync(dados, erros, ct);

        if (novo && novaSenha is null)
            erros.Add("Informe a senha inicial do usuário.");
        if (novaSenha is not null)
            erros.AddRange(PoliticaSenha.ValidarSenha(novaSenha, dados.Login));

        if (!novo && anterior!.Id == _usuarioAtual.Id && !dados.Ativo)
            erros.Add("Você não pode desativar o seu próprio usuário.");

        await VerificarUltimoAdministradorAsync(dados, anterior, erros, ct);

        if (erros.Count > 0) throw new ValidacaoException(erros);

        // A senha só muda quando redefinida; senão fica a gravada.
        dados.SenhaHash = anterior?.SenhaHash ?? string.Empty;
        if (novaSenha is not null)
        {
            dados.SenhaHash = await Task.Run(() => _hasher.Gerar(novaSenha), ct);
            dados.DeveTrocarSenha = true; // senha definida por outra pessoa: o usuário troca no próximo login
        }

        await _usuarios.SalvarAsync(dados, novo, ct);

        // Senha redefinida ou usuário inativado: as sessões abertas dele deixam de valer.
        if (!novo && (novaSenha is not null || !dados.Ativo))
            await _tokens.RevogarTodosDoUsuarioAsync(id, ct);

        var salvo = await _usuarios.ObterAsync(id, ct) ?? throw new ConflitoDeEdicaoException();
        return ParaDto(salvo);
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

    public async Task DesbloquearAsync(Guid id, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Seguranca.GerenciarUsuarios);
        var usuario = await _usuarios.ObterAsync(id, ct) ?? throw new ValidacaoException(["Este usuário não existe mais."]);
        await _usuarios.RegistrarAcessoAsync(id, 0, null, usuario.Acesso?.UltimoAcessoEm, ct);
    }

    // ---------------------------------------------------------------- Apoio

    /// <summary>Perfis precisam existir; empresas precisam ser do grupo.</summary>
    private async Task ValidarPerfisEEmpresasAsync(Usuario dados, List<string> erros, CancellationToken ct)
    {
        if (dados.Perfis.Count == 0) return;

        var perfis = (await _perfis.ListarAsync(ct)).Select(p => p.Id).ToHashSet();
        if (dados.Perfis.Any(p => !perfis.Contains(p.PerfilId)))
            erros.Add("Um dos perfis escolhidos não existe mais.");

        if (dados.Perfis.Any(p => p.EmpresaId is not null))
        {
            var empresas = (await _empresas.ListarEmpresasAsync(ct)).Select(e => e.Id).ToHashSet();
            if (dados.Perfis.Any(p => p.EmpresaId is Guid empresa && !empresas.Contains(empresa)))
                erros.Add("Perfil atribuído a uma empresa que não é do grupo.");
        }
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

    private static UsuarioDto ParaDto(Usuario u) => new()
    {
        Id = u.Id,
        Versao = u.Versao,
        Nome = u.Nome,
        Login = u.Login,
        Email = u.Email,
        Ativo = u.Ativo,
        DeveTrocarSenha = u.DeveTrocarSenha,
        Perfis = u.Perfis.Select(p => new UsuarioPerfilDto(p.PerfilId, p.EmpresaId)).ToList(),
        BloqueadoAte = Utc(u.Acesso?.BloqueadoAte),
        UltimoAcessoEm = Utc(u.Acesso?.UltimoAcessoEm)
    };

    /// <summary>O banco devolve DateTime sem fuso; as datas de acesso são gravadas em UTC.</summary>
    private static DateTime? Utc(DateTime? data) => data is { } d ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : null;
}
