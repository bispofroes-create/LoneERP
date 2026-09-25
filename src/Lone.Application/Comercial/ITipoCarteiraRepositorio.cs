using Lone.Domain.Entidades;

namespace Lone.Application.Comercial;

/// <summary>Persistência: tipo de carteira. Nada é apagado: é desativado.</summary>
public interface ITipoCarteiraRepositorio
{
    Task<List<TipoCarteira>> ListarAsync(CancellationToken ct);
    Task<TipoCarteira?> ObterAsync(Guid id, CancellationToken ct);
    Task<Dictionary<Guid, TipoCarteira>> ObterVariosAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
    Task<Dictionary<Guid, int>> ContarUsosAsync(Guid? somenteId, CancellationToken ct);
    Task SalvarAsync(TipoCarteira item, bool novo, CancellationToken ct);
}
