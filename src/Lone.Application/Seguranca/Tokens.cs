using System.Security.Cryptography;
using System.Text;
using Lone.Contracts.Empresas;
using Lone.Domain.Entidades;

namespace Lone.Application.Seguranca;

/// <summary>Token de acesso emitido (curto: a API confere a assinatura e a validade a cada requisição).</summary>
public sealed record TokenAcesso(string Token, DateTimeOffset ExpiraEm);

/// <summary>Emite o token de acesso. Implementado em Lone.Infrastructure (JWT assinado).</summary>
public interface IEmissorToken
{
    TokenAcesso Emitir(Guid usuarioId, string nome, string login, EmpresaAtiva? empresa);

    /// <summary>Por quanto tempo um token de renovação vale sem uso.</summary>
    TimeSpan ValidadeRenovacao { get; }
}

/// <summary>Persistência dos tokens de renovação (só o hash é gravado).</summary>
public interface ITokenRenovacaoRepositorio
{
    Task<TokenRenovacao?> ObterPorHashAsync(string hash, CancellationToken ct);
    Task IncluirAsync(TokenRenovacao token, CancellationToken ct);

    /// <summary>Revoga o token usado e inclui o novo, na mesma transação.</summary>
    Task SubstituirAsync(Guid usadoId, TokenRenovacao novo, CancellationToken ct);

    Task RevogarAsync(Guid id, CancellationToken ct);

    /// <summary>Encerra todas as sessões do usuário (senha redefinida, usuário inativado, suspeita de roubo).</summary>
    Task RevogarTodosDoUsuarioAsync(Guid usuarioId, CancellationToken ct);
}

/// <summary>Gera tokens de renovação aleatórios (256 bits) e o hash que vai para o banco.</summary>
public static class GeradorTokenRenovacao
{
    public static (string Token, string Hash) Novo()
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return (token, Hash(token));
    }

    public static string Hash(string token) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

/// <summary>Sessão ausente, expirada ou revogada: o aplicativo precisa pedir o login de novo (HTTP 401).</summary>
public sealed class SessaoInvalidaException : Exception
{
    public SessaoInvalidaException(string mensagem = "Sua sessão expirou. Entre novamente.") : base(mensagem) { }
}
