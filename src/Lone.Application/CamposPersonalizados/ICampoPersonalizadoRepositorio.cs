using Lone.Domain.Entidades;
using Lone.Domain.Enums;

namespace Lone.Application.CamposPersonalizados;

/// <summary>Persistência das definições de campos personalizados. Nada é apagado: campos e opções são desativados.</summary>
public interface ICampoPersonalizadoRepositorio
{
    /// <summary>Campos do cadastro, em ordem, com as opções (sem rastreamento).</summary>
    Task<List<CampoPersonalizado>> ListarAsync(EntidadePersonalizavel entidade, bool incluirInativos, CancellationToken ct);

    Task<CampoPersonalizado?> ObterAsync(Guid id, CancellationToken ct);

    /// <summary>Nome já usado no mesmo cadastro (e, nos documentos, no mesmo tipo de documento).</summary>
    Task<bool> NomeEmUsoAsync(EntidadePersonalizavel entidade, Guid? tipoDocumentoId, string nome, Guid ignorarId, CancellationToken ct);

    /// <summary>Ids dos campos (da lista) que já têm algum valor gravado.</summary>
    Task<HashSet<Guid>> ComValoresAsync(IReadOnlyCollection<Guid> campoIds, CancellationToken ct);

    Task<int> ProximaOrdemAsync(EntidadePersonalizavel entidade, CancellationToken ct);

    /// <summary>Inclui ou altera (opções sincronizadas sem apagar nenhuma), com auditoria e eventos.</summary>
    Task SalvarAsync(CampoPersonalizado campo, bool novo, CancellationToken ct);

    /// <summary>Grava a ordem dada (os Ids que não estiverem na lista vão para o fim, na ordem atual).</summary>
    Task ReordenarAsync(EntidadePersonalizavel entidade, IReadOnlyList<Guid> ids, CancellationToken ct);
}
