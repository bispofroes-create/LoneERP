using Lone.Domain.Entidades;

namespace Lone.Application.Papeis;

/// <summary>Persistência do cadastro de papéis. Nada é apagado: papéis são desativados.</summary>
public interface IPapelRepositorio
{
    /// <summary>Na ordem do cadastro, sem rastreamento.</summary>
    Task<List<Papel>> ListarAsync(bool incluirInativos, CancellationToken ct);

    Task<Papel?> ObterAsync(Guid id, CancellationToken ct);

    /// <summary>Os papéis com estes Ids (os que existirem), para conferir os marcados numa pessoa.</summary>
    Task<Dictionary<Guid, Papel>> ObterVariosAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);

    /// <summary>Nome ou código já usado por outro papel (nome sem diferenciar maiúsculas nem acentos).</summary>
    Task<(bool Nome, bool Codigo)> EmUsoAsync(string nome, string codigo, Guid ignorarId, CancellationToken ct);

    /// <summary>Quantos cadastros têm cada papel ativo (ou só o informado).</summary>
    Task<Dictionary<Guid, int>> ContarPessoasAsync(Guid? somentePapelId, CancellationToken ct);

    Task<int> ProximaOrdemAsync(CancellationToken ct);

    /// <summary>Inclui ou altera, com auditoria e eventos; confere a versão.</summary>
    Task SalvarAsync(Papel papel, bool novo, CancellationToken ct);
}
