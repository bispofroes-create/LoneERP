using Lone.Domain.Entidades;

namespace Lone.Application.Seguranca;

/// <summary>
/// Confere a senha de um usuário. Hoje: senha própria do Lone (AutenticadorLocal).
/// Ponto de integração futuro: uma implementação que valide no Active Directory do cliente,
/// registrada no lugar desta, sem mudar a tela nem o AutenticacaoService.
/// </summary>
public interface IAutenticador
{
    Task<bool> ValidarAsync(Usuario usuario, string senha, CancellationToken ct);
}

public sealed class AutenticadorLocal : IAutenticador
{
    private readonly IHasherSenha _hasher;

    public AutenticadorLocal(IHasherSenha hasher)
    {
        _hasher = hasher;
    }

    // PBKDF2 é propositalmente lento; roda fora da thread da tela.
    public Task<bool> ValidarAsync(Usuario usuario, string senha, CancellationToken ct) =>
        Task.Run(() => _hasher.Verificar(senha, usuario.SenhaHash), ct);
}
