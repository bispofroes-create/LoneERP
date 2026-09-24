using System.ComponentModel;
using Lone.Domain.Auditoria;
using Lone.Domain.Enums;

namespace Lone.Domain.Entidades;

/// <summary>Documento adicional da pessoa (RG, CNH, passaporte...).</summary>
[DisplayName("Documento")]
public class PessoaDocumento : EntidadePessoaFilha
{
    [DisplayName("Tipo")]
    public TipoDocumento Tipo { get; set; } = TipoDocumento.Rg;

    [DisplayName("Número"), DadoSensivel]
    public string Numero { get; set; } = string.Empty;

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
