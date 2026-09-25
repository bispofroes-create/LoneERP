using Lone.Domain.Entidades;

namespace Lone.Application.Etiquetas;

/// <summary>Persistência do cadastro de etiquetas. Nada é apagado: etiquetas são desativadas ou mescladas.</summary>
public interface IEtiquetaRepositorio
{
    /// <summary>Em ordem alfabética, sem rastreamento.</summary>
    Task<List<Etiqueta>> ListarAsync(bool incluirInativas, CancellationToken ct);

    Task<Etiqueta?> ObterAsync(Guid id, CancellationToken ct);

    /// <summary>As etiquetas com estes Ids (as que existirem), para conferir as marcadas numa pessoa.</summary>
    Task<Dictionary<Guid, Etiqueta>> ObterVariasAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);

    /// <summary>Nome já usado por outra etiqueta, sem diferenciar maiúsculas nem acentos (collation da coluna).</summary>
    Task<bool> NomeEmUsoAsync(string nome, Guid ignorarId, CancellationToken ct);

    /// <summary>Quantos cadastros têm cada etiqueta (ou só a informada); as sem uso não aparecem no dicionário.</summary>
    Task<Dictionary<Guid, int>> ContarPessoasAsync(Guid? somenteEtiquetaId, CancellationToken ct);

    /// <summary>Inclui ou altera, com auditoria e eventos; confere a versão.</summary>
    Task SalvarAsync(Etiqueta etiqueta, bool nova, CancellationToken ct);

    /// <summary>
    /// Numa transação: grava as duas etiquetas (a origem já desativada, com os eventos) e passa para o destino
    /// os cadastros que tinham a origem. Quem já tinha as duas fica só com o destino. Cada cadastro alterado
    /// fica no próprio histórico. Devolve quantos cadastros tinham a origem.
    /// </summary>
    Task<int> MesclarAsync(Etiqueta origem, Etiqueta destino, CancellationToken ct);
}
