using Lone.Domain.Entidades;

namespace Lone.Application.Colaboradores;

/// <summary>Persistência dos setores. Nada é apagado: são desativados.</summary>
public interface ISetorRepositorio
{
    /// <summary>Todos (ativos e desativados), sem rastreamento.</summary>
    Task<List<Setor>> ListarAsync(CancellationToken ct);

    Task<Setor?> ObterAsync(Guid id, CancellationToken ct);

    Task<Dictionary<Guid, Setor>> ObterVariosAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);

    /// <summary>Quantas lotações em aberto usam cada um (ou só o informado).</summary>
    Task<Dictionary<Guid, int>> ContarUsosAsync(Guid? somenteId, CancellationToken ct);

    Task SalvarAsync(Setor item, bool novo, CancellationToken ct);
}
