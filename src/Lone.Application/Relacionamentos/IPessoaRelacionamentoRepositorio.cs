using Lone.Domain.Entidades;
using Lone.Domain.Relacionamentos;

namespace Lone.Application.Relacionamentos;

/// <summary>Persistência dos relacionamentos entre pessoas. Nada é apagado: encerra (fim) ou desativa (engano).</summary>
public interface IPessoaRelacionamentoRepositorio
{
    /// <summary>Os vínculos em que a pessoa é origem OU destino (os dois sentidos), sem rastreamento.</summary>
    Task<List<PessoaRelacionamento>> ListarDaPessoaAsync(Guid pessoaId, CancellationToken ct);

    /// <summary>Os vínculos gravados com esta origem (para conferir duplicidade).</summary>
    Task<List<PessoaRelacionamento>> ListarDaOrigemAsync(Guid origemId, CancellationToken ct);

    Task<PessoaRelacionamento?> ObterAsync(Guid id, CancellationToken ct);

    /// <summary>Vínculos societários (sócio, administrador) ativos e em aberto em que a pessoa é a empresa (destino).</summary>
    Task<int> ContarSocietariosComoEmpresaAsync(Guid pessoaId, CancellationToken ct);

    Task<List<TipoRelacionamento>> ListarTiposAsync(CancellationToken ct);

    /// <summary>Natureza, situação e nome para exibir das pessoas informadas (as que existirem).</summary>
    Task<Dictionary<Guid, PessoaNoRelacionamento>> PessoasAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);

    Task IncluirAsync(PessoaRelacionamento relacionamento, CancellationToken ct);

    /// <summary>Encerra (fim) ou desativa (engano) um vínculo gravado, na mesma gravação da auditoria.</summary>
    Task AlterarAsync(Guid id, DateOnly? fimEm, bool ativo, CancellationToken ct);
}
