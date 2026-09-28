namespace Lone.Application.Seguranca;

/// <summary>Usuário ativo e o que ele pode fazer na empresa da requisição.</summary>
public sealed record AcessoDoUsuario(Guid UsuarioId, string Nome, bool DeveTrocarSenha, AcessoEfetivo Acesso)
{
    /// <summary>A pessoa do cadastro ligada ao usuário (Fase 2a): base do alcance "Minha carteira" e "Minha equipe".</summary>
    public Guid? PessoaId { get; init; }
}

/// <summary>
/// Calcula as permissões do usuário numa empresa. A API chama no início de cada requisição autenticada
/// (com cache curto), então mudanças de perfil feitas pelo administrador valem em instantes, sem novo login.
/// </summary>
public interface IAcessoService
{
    /// <summary>Nulo se o usuário não existe mais ou foi inativado (a requisição é recusada com 401).</summary>
    Task<AcessoDoUsuario?> CarregarAsync(Guid usuarioId, Guid? empresaId, CancellationToken ct = default);
}

public sealed class AcessoService : IAcessoService
{
    private readonly IUsuarioRepositorio _usuarios;

    public AcessoService(IUsuarioRepositorio usuarios)
    {
        _usuarios = usuarios;
    }

    public async Task<AcessoDoUsuario?> CarregarAsync(Guid usuarioId, Guid? empresaId, CancellationToken ct = default)
    {
        var usuario = await _usuarios.ObterParaAcessoAsync(usuarioId, ct);
        if (usuario is not { Ativo: true })
            return null;

        var efetivo = RegrasDeAcesso.Efetivo(RegrasDeAcesso.PerfisAtivos(usuario), empresaId);
        return new AcessoDoUsuario(usuario.Id, usuario.Nome, usuario.DeveTrocarSenha, efetivo) { PessoaId = usuario.PessoaId };
    }
}
