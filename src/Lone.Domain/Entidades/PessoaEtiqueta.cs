using System.ComponentModel;
using Lone.Domain.Auditoria;

namespace Lone.Domain.Entidades;

/// <summary>
/// Etiqueta marcada numa pessoa (referência ao cadastro <see cref="Etiqueta"/>).
/// Auditada como um valor só ("Etiqueta: vazio → Cliente VIP"); a consulta do histórico troca o Id pelo nome.
/// A antiga coluna de texto livre continua no banco (propriedade de sombra "Texto"), só como cópia do dado anterior.
/// </summary>
[DisplayName("Etiqueta")]
public class PessoaEtiqueta : EntidadePessoaFilha, IValorAuditavel
{
    [DisplayName("Etiqueta")]
    public Guid EtiquetaId { get; set; }

    string IValorAuditavel.CampoAuditado => nameof(EtiquetaId);

    string? IValorAuditavel.DescreverValor(Func<string, object?> coluna) =>
        coluna(nameof(EtiquetaId)) is Guid id && id != Guid.Empty ? id.ToString() : null;
}
