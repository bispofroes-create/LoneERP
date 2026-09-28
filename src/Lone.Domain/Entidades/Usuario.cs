using System.ComponentModel;
using Lone.Domain.Auditoria;

namespace Lone.Domain.Entidades;

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

    /// <summary>
    /// Quem é este usuário no cadastro de Pessoas (ex.: o vendedor Rafael). Opcional; uma pessoa tem no máximo um usuário.
    /// É a base do alcance "Minha carteira" e "Minha equipe" (Motor Comercial, Fase 2a; decisão F1).
    /// </summary>
    [DisplayName("Pessoa no cadastro")]
    public Guid? PessoaId { get; set; }

    public List<UsuarioPerfil> Perfis { get; set; } = new();

    /// <summary>Tentativas, bloqueio e último acesso (tabela própria; nulo até o primeiro login).</summary>
    public UsuarioAcesso? Acesso { get; set; }
}
