using Lone.Core.Entidades;

namespace Lone.Aplicacao.Seguranca;

public interface IUsuarioRepositorio
{
    Task<bool> ExisteAlgumAsync(CancellationToken ct);

    /// <summary>Usuário com perfis e permissões, sem rastreamento. Login já normalizado.</summary>
    Task<Usuario?> ObterParaLoginAsync(string login, CancellationToken ct);

    /// <summary>Atualiza tentativas, bloqueio e último acesso (fora da auditoria de cadastro).</summary>
    Task RegistrarAcessoAsync(int usuarioId, int tentativasFalhas, DateTime? bloqueadoAte, DateTime? ultimoAcessoEm, CancellationToken ct);

    Task<List<UsuarioResumo>> ListarAsync(CancellationToken ct);
    Task<Usuario?> ObterAsync(int id, CancellationToken ct);
    Task<bool> LoginEmUsoAsync(string login, int ignorarId, CancellationToken ct);

    /// <summary>Usuários ativos, exceto o informado, que têm algum perfil administrador ativo.</summary>
    Task<int> ContarAdministradoresAtivosAsync(int ignorarUsuarioId, CancellationToken ct);

    /// <summary>Inclui ou altera (perfis sincronizados), com auditoria. Lança ConflitoDeEdicaoException.</summary>
    Task<Usuario> SalvarAsync(Usuario usuario, CancellationToken ct);

    /// <summary>Grava novo hash de senha (auditado sem o valor).</summary>
    Task AlterarSenhaAsync(int usuarioId, string senhaHash, bool deveTrocarSenha, CancellationToken ct);
}

public interface IPerfilRepositorio
{
    Task<List<PerfilResumo>> ListarAsync(CancellationToken ct);
    Task<Perfil?> ObterAsync(int id, CancellationToken ct);
    Task<bool> NomeEmUsoAsync(string nome, int ignorarId, CancellationToken ct);
    Task<IReadOnlySet<int>> IdsAdministradoresAtivosAsync(CancellationToken ct);

    /// <summary>Inclui ou altera (permissões sincronizadas), com auditoria. Lança ConflitoDeEdicaoException.</summary>
    Task<Perfil> SalvarAsync(Perfil perfil, CancellationToken ct);
}
