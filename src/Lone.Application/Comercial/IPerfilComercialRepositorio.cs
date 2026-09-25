using Lone.Domain.Entidades;

namespace Lone.Application.Comercial;

/// <summary>Persistência: perfil comercial. Nada é apagado: é desativado.</summary>
public interface IPerfilComercialRepositorio
{
    Task<List<PerfilComercial>> ListarAsync(CancellationToken ct);
    Task<PerfilComercial?> ObterAsync(Guid id, CancellationToken ct);
    Task<Dictionary<Guid, PerfilComercial>> ObterVariosAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
    Task<Dictionary<Guid, int>> ContarUsosAsync(Guid? somenteId, CancellationToken ct);
    Task SalvarAsync(PerfilComercial item, bool novo, CancellationToken ct);
}
