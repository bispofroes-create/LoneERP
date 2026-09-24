using Lone.Contracts.Seguranca;
using Lone.Domain.Entidades;

namespace Lone.Application.Seguranca;

public interface IUsuarioRepositorio
{
    Task<bool> ExisteAlgumAsync(CancellationToken ct);

    /// <summary>Usuário com acesso, perfis e permissões, sem rastreamento. Login já normalizado.</summary>
    Task<Usuario?> ObterParaLoginAsync(string login, CancellationToken ct);

    /// <summary>Mesmo conteúdo de ObterParaLoginAsync, pelo Id (renovação de sessão e permissões por requisição).</summary>
    Task<Usuario?> ObterParaAcessoAsync(Guid id, CancellationToken ct);

    /// <summary>Atualiza tentativas, bloqueio e último acesso (fora da auditoria de cadastro). Datas em UTC.</summary>
    Task RegistrarAcessoAsync(Guid usuarioId, int tentativasFalhas, DateTime? bloqueadoAte, DateTime? ultimoAcessoEm, CancellationToken ct);

    Task<List<UsuarioResumo>> ListarAsync(CancellationToken ct);

    /// <summary>Usuário com acesso e perfis (sem permissões), sem rastreamento.</summary>
    Task<Usuario?> ObterAsync(Guid id, CancellationToken ct);

    Task<bool> LoginEmUsoAsync(string login, Guid ignorarId, CancellationToken ct);

    /// <summary>Usuários ativos, exceto o informado, que têm algum perfil administrador ativo.</summary>
    Task<int> ContarAdministradoresAtivosAsync(Guid ignorarUsuarioId, CancellationToken ct);

    /// <summary>
    /// Inclui (<paramref name="novo"/>) ou altera (perfis sincronizados), com auditoria.
    /// Lança ConflitoDeEdicaoException se outro usuário gravou antes.
    /// </summary>
    Task SalvarAsync(Usuario usuario, bool novo, CancellationToken ct);

    /// <summary>Grava novo hash de senha (auditado sem o valor).</summary>
    Task AlterarSenhaAsync(Guid usuarioId, string senhaHash, bool deveTrocarSenha, CancellationToken ct);
}

public interface IPerfilRepositorio
{
    Task<List<PerfilResumo>> ListarAsync(CancellationToken ct);
    Task<Perfil?> ObterAsync(Guid id, CancellationToken ct);
    Task<bool> NomeEmUsoAsync(string nome, Guid ignorarId, CancellationToken ct);
    Task<IReadOnlySet<Guid>> IdsAdministradoresAtivosAsync(CancellationToken ct);

    /// <summary>Inclui (<paramref name="novo"/>) ou altera (permissões sincronizadas), com auditoria.</summary>
    Task SalvarAsync(Perfil perfil, bool novo, CancellationToken ct);
}
