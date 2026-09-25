using Lone.Domain.Entidades;

namespace Lone.Application.Colaboradores;

/// <summary>Persistência dos departamentos. Nada é apagado: são desativados.</summary>
public interface IDepartamentoRepositorio
{
    /// <summary>Todos (ativos e desativados), sem rastreamento.</summary>
    Task<List<Departamento>> ListarAsync(CancellationToken ct);

    Task<Departamento?> ObterAsync(Guid id, CancellationToken ct);

    Task<Dictionary<Guid, Departamento>> ObterVariosAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);

    /// <summary>Quantas lotações em aberto usam cada um (ou só o informado).</summary>
    Task<Dictionary<Guid, int>> ContarUsosAsync(Guid? somenteId, CancellationToken ct);

    Task SalvarAsync(Departamento item, bool novo, CancellationToken ct);
}
