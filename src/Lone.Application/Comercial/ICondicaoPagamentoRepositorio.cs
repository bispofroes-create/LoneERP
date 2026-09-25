using Lone.Domain.Entidades;

namespace Lone.Application.Comercial;

/// <summary>Persistência: condição de pagamento. Nada é apagado: é desativado.</summary>
public interface ICondicaoPagamentoRepositorio
{
    Task<List<CondicaoPagamento>> ListarAsync(CancellationToken ct);
    Task<CondicaoPagamento?> ObterAsync(Guid id, CancellationToken ct);
    Task<Dictionary<Guid, CondicaoPagamento>> ObterVariosAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
    Task<Dictionary<Guid, int>> ContarUsosAsync(Guid? somenteId, CancellationToken ct);
    Task SalvarAsync(CondicaoPagamento item, bool novo, CancellationToken ct);
}
