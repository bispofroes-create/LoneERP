using Lone.Domain.Entidades;

namespace Lone.Application.Enderecos;

/// <summary>Persistência dos tipos de endereço. Nada é apagado: tipos são desativados.</summary>
public interface ITipoEnderecoRepositorio
{
    /// <summary>Na ordem do cadastro, sem rastreamento.</summary>
    Task<List<TipoEndereco>> ListarAsync(bool incluirInativos, CancellationToken ct);

    Task<TipoEndereco?> ObterAsync(Guid id, CancellationToken ct);

    Task<Dictionary<Guid, TipoEndereco>> ObterVariosAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);

    /// <summary>Nome já usado por outro tipo (sem diferenciar maiúsculas nem acentos).</summary>
    Task<bool> NomeEmUsoAsync(string nome, Guid ignorarId, CancellationToken ct);

    /// <summary>Quantos endereços ativos usam cada tipo (ou só o informado).</summary>
    Task<Dictionary<Guid, int>> ContarUsosAsync(Guid? somenteTipoId, CancellationToken ct);

    Task<int> ProximaOrdemAsync(CancellationToken ct);

    Task SalvarAsync(TipoEndereco tipo, bool novo, CancellationToken ct);
}
