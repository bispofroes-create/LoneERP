using System.ComponentModel;
using Lone.Domain.Auditoria;
using Lone.Domain.Enums;

namespace Lone.Domain.Entidades;

/// <summary>
/// Documento adicional da pessoa (RG, CNH, passaporte, alvará...). O tipo vem do cadastro de tipos de documento;
/// <see cref="Tipo"/> (enum, coluna antiga) é uma cópia do tipo de sistema, feita pela API. Nunca é apagado:
/// removido na ficha, fica inativo (histórico).
/// </summary>
[DisplayName("Documento")]
public class PessoaDocumento : EntidadePessoaFilha
{
    /// <summary>Tipo do cadastro de tipos de documento.</summary>
    [DisplayName("Tipo")]
    public Guid TipoDocumentoId { get; set; }

    /// <summary>Cópia do tipo de sistema (Outro para os tipos criados pelo usuário). Mantida para compatibilidade.</summary>
    [DisplayName("Tipo (sistema)")]
    public TipoDocumento Tipo { get; set; } = TipoDocumento.Rg;

    [DisplayName("Ativo")]
    public bool Ativo { get; set; } = true;

    [DisplayName("Número"), DadoSensivel]
    public string Numero { get; set; } = string.Empty;

    /// <summary>
    /// Técnico (P1-8): o número na forma de comparar e buscar (<see cref="Documentos.NumeroDocumento.Normalizar"/>),
    /// calculado na gravação. Não aparece na tela nem no histórico (a mudança do número já é registrada, mascarada).
    /// </summary>
    public string NumeroNormalizado { get; set; } = string.Empty;

    /// <summary>
    /// Técnico (P1-8): chave da unicidade entre pessoas quando o tipo bloqueia número repetido (nula nos demais casos);
    /// índice único filtrado nos ativos. Ver <see cref="Documentos.NumeroDocumento.ChaveUnicidade"/>.
    /// </summary>
    public string? ChaveUnicidade { get; set; }

    [DisplayName("Órgão emissor")]
    public string? OrgaoEmissor { get; set; }

    [DisplayName("UF")]
    public string? Uf { get; set; }

    [DisplayName("Emissão")]
    public DateOnly? EmitidoEm { get; set; }

    [DisplayName("Validade")]
    public DateOnly? ValidoAte { get; set; }

    [DisplayName("Observações")]
    public string? Observacoes { get; set; }
}
