using Lone.Domain.Entidades;

namespace Lone.Application.Colaboradores;

/// <summary>Persistência dos cargos. Nada é apagado: são desativados.</summary>
public interface ICargoRepositorio
{
    /// <summary>Todos (ativos e desativados), sem rastreamento.</summary>
    Task<List<Cargo>> ListarAsync(CancellationToken ct);

    Task<Cargo?> ObterAsync(Guid id, CancellationToken ct);

    Task<Dictionary<Guid, Cargo>> ObterVariosAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);

    /// <summary>Quantas lotações em aberto usam cada um (ou só o informado).</summary>
    Task<Dictionary<Guid, int>> ContarUsosAsync(Guid? somenteId, CancellationToken ct);

    Task SalvarAsync(Cargo item, bool novo, CancellationToken ct);
}
