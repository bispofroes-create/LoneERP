using Lone.Contracts.Empresas;

namespace Lone.Contracts.Seguranca;

/// <summary>Situação do sistema antes do login: sem usuários, o aplicativo abre o primeiro acesso.</summary>
public sealed record SituacaoSistema(bool ExisteUsuario);

public sealed record EntrarRequisicao(string Login, string Senha, string? Dispositivo = null);

/// <summary>Só aceito com o banco sem usuários: cria o perfil Administrador e o primeiro usuário.</summary>
public sealed record PrimeiroAcessoRequisicao(string Nome, string Login, string Senha, string? Dispositivo = null);

public sealed record RenovarSessaoRequisicao(string TokenRenovacao);

/// <summary>Troca a empresa ativa: o token de renovação atual é revogado e uma sessão nova é emitida.</summary>
public sealed record SelecionarEmpresaRequisicao(Guid EstabelecimentoId, string TokenRenovacao);

public sealed record TrocarSenhaRequisicao(string SenhaAtual, string NovaSenha);

public sealed record SairRequisicao(string TokenRenovacao);

public enum SituacaoLogin
{
    Sucesso,
    CredenciaisInvalidas,
    Bloqueado,
    Inativo
}

/// <summary>
/// Sessão aberta: tokens e o que o aplicativo precisa para montar o menu.
/// As permissões aqui servem só para mostrar ou esconder opções; quem decide é a API, a cada requisição.
/// </summary>
public sealed class SessaoDto
{
    public string TokenAcesso { get; set; } = string.Empty;
    public DateTimeOffset AcessoExpiraEm { get; set; }
    public string TokenRenovacao { get; set; } = string.Empty;
    public DateTimeOffset RenovacaoExpiraEm { get; set; }

    public Guid UsuarioId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public bool DeveTrocarSenha { get; set; }

    /// <summary>Nula enquanto nenhuma empresa foi escolhida (ou nenhuma está cadastrada).</summary>
    public EmpresaAtiva? EmpresaAtiva { get; set; }
    public List<EmpresaAtiva> EmpresasDisponiveis { get; set; } = new();

    public bool Administrador { get; set; }
    public List<string> Permissoes { get; set; } = new();

    public bool Possui(string permissao) => Administrador || Permissoes.Contains(permissao);
}
