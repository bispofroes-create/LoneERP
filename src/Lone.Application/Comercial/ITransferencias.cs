using Lone.Domain.Comercial;
using Lone.Domain.Entidades;

namespace Lone.Application.Comercial;

/// <summary>Um cliente que a transferência alcança, com o nome para exibir.</summary>
public sealed record ClienteDaOrigem(Guid Id, string Nome);

/// <summary>
/// Persistência das transferências de carteira (Motor Comercial, Fase 1d) e as consultas que a prévia precisa, feitas no
/// banco (sem trazer a carteira inteira para a memória). Nada é apagado nem alterado depois de concluído.
/// </summary>
public interface ITransferenciaCarteiraRepositorio
{
    /// <summary>
    /// Clientes, em ordem de nome, com vínculo ativo da origem no papel e na empresa do filtro que vale no efeito ou depois
    /// (a mesma condição de <see cref="RegrasTransferencia.Candidatos"/>).
    /// </summary>
    Task<List<ClienteDaOrigem>> ClientesDaOrigemAsync(FiltroTransferencia filtro, CancellationToken ct);

    /// <summary>Quantos clientes distintos cada pessoa atende na data (vínculos ativos vigentes; só no papel, se informado).</summary>
    Task<Dictionary<Guid, int>> CargasAsync(IReadOnlyCollection<Guid> pessoas, Guid? tipoCarteiraId, DateOnly data, CancellationToken ct);

    /// <summary>A próxima sequência do ano (1 no primeiro do ano).</summary>
    Task<int> ProximaSequenciaAsync(int ano, CancellationToken ct);

    /// <summary>Grava o registro da transferência antes dos clientes (os vínculos novos apontam para ele).</summary>
    Task IncluirAsync(TransferenciaCarteira transferencia, CancellationToken ct);

    /// <summary>Registra o resultado: as contagens, "concluída" e os itens (uma vez só).</summary>
    Task ConcluirAsync(TransferenciaCarteira transferencia, IReadOnlyList<TransferenciaCarteiraItem> itens, CancellationToken ct);

    /// <summary>As mais recentes primeiro.</summary>
    Task<List<TransferenciaCarteira>> ListarAsync(int limite, CancellationToken ct);

    Task<TransferenciaCarteira?> ObterAsync(Guid id, CancellationToken ct);

    Task<List<TransferenciaCarteiraItem>> ItensAsync(Guid transferenciaId, CancellationToken ct);

    /// <summary>Os números legíveis ("TR-2026-0001") das transferências informadas.</summary>
    Task<Dictionary<Guid, string>> NumerosAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
}
