using Lone.Domain.Entidades;
using Lone.Domain.Enums;

namespace Lone.Application.Contatos;

/// <summary>Persistência dos tipos de telefone/e-mail. Nada é apagado: tipos são desativados.</summary>
public interface ITipoMeioContatoRepositorio
{
    /// <summary>Por categoria e ordem, sem rastreamento.</summary>
    Task<List<TipoMeioContato>> ListarAsync(bool incluirInativos, CancellationToken ct);

    Task<TipoMeioContato?> ObterAsync(Guid id, CancellationToken ct);

    Task<Dictionary<Guid, TipoMeioContato>> ObterVariosAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);

    /// <summary>Nome já usado por outro tipo da mesma categoria (sem diferenciar maiúsculas nem acentos).</summary>
    Task<bool> NomeEmUsoAsync(CategoriaMeioContato categoria, string nome, Guid ignorarId, CancellationToken ct);

    /// <summary>Quantos telefones/e-mails ativos usam cada tipo (ou só o informado).</summary>
    Task<Dictionary<Guid, int>> ContarUsosAsync(Guid? somenteTipoId, CancellationToken ct);

    Task<int> ProximaOrdemAsync(CategoriaMeioContato categoria, CancellationToken ct);

    Task SalvarAsync(TipoMeioContato tipo, bool novo, CancellationToken ct);
}
