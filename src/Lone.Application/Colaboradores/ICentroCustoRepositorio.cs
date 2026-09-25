using Lone.Domain.Entidades;

namespace Lone.Application.Colaboradores;

/// <summary>Persistência dos centros de custo. Nada é apagado: são desativados.</summary>
public interface ICentroCustoRepositorio
{
    /// <summary>Todos (ativos e desativados), sem rastreamento.</summary>
    Task<List<CentroCusto>> ListarAsync(CancellationToken ct);

    Task<CentroCusto?> ObterAsync(Guid id, CancellationToken ct);

    Task<Dictionary<Guid, CentroCusto>> ObterVariosAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);

    /// <summary>Quantas lotações em aberto usam cada um (ou só o informado).</summary>
    Task<Dictionary<Guid, int>> ContarUsosAsync(Guid? somenteId, CancellationToken ct);

    Task SalvarAsync(CentroCusto item, bool novo, CancellationToken ct);
}
