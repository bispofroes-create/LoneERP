using Lone.Domain.Entidades;

namespace Lone.Application.Documentos;

/// <summary>Persistência dos tipos de documento. Nada é apagado: tipos são desativados.</summary>
public interface ITipoDocumentoRepositorio
{
    /// <summary>Na ordem do cadastro, sem rastreamento.</summary>
    Task<List<TipoDocumentoCadastro>> ListarAsync(bool incluirInativos, CancellationToken ct);

    Task<TipoDocumentoCadastro?> ObterAsync(Guid id, CancellationToken ct);

    Task<Dictionary<Guid, TipoDocumentoCadastro>> ObterVariosAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);

    /// <summary>Nome já usado por outro tipo (sem diferenciar maiúsculas nem acentos).</summary>
    Task<bool> NomeEmUsoAsync(string nome, Guid ignorarId, CancellationToken ct);

    /// <summary>Quantos documentos ativos usam cada tipo (ou só o informado).</summary>
    Task<Dictionary<Guid, int>> ContarUsosAsync(Guid? somenteTipoId, CancellationToken ct);

    Task<int> ProximaOrdemAsync(CancellationToken ct);

    Task SalvarAsync(TipoDocumentoCadastro tipo, bool novo, CancellationToken ct);
}
