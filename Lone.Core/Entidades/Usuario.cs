using System.ComponentModel;
using Lone.Core.Auditoria;

namespace Lone.Core.Entidades;

/// <summary>Usuário do Lone. A senha nunca é guardada: só o hash (PBKDF2 com sal).</summary>
[DisplayName("Usuário")]
public class Usuario : AgregadoRaiz
{
    public const int MaximoTentativas = 5;
    public static readonly TimeSpan TempoBloqueio = TimeSpan.FromMinutes(15);

    /// <summary>Nome de login, sempre em minúsculas. Único.</summary>
    [DisplayName("Login")]
    public string Login { get; set; } = string.Empty;

    [DisplayName("Nome")]
    public string Nome { get; set; } = string.Empty;

    [DisplayName("E-mail")]
    public string? Email { get; set; }

    [DisplayName("Senha"), NaoAuditarValor]
    public string SenhaHash { get; set; } = string.Empty;

    [DisplayName("Deve trocar a senha")]
    public bool DeveTrocarSenha { get; set; }

    [DisplayName("Ativo")]
    public bool Ativo { get; set; } = true;

    public List<UsuarioPerfil> Perfis { get; set; } = new();

    /// <summary>Tentativas, bloqueio e último acesso (tabela própria; nulo até o primeiro login).</summary>
    public UsuarioAcesso? Acesso { get; set; }
}
