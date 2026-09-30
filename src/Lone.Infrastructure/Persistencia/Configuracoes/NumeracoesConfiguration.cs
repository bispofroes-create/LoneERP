using Lone.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lone.Infrastructure.Persistencia.Configuracoes;

/// <summary>
/// Numerador de documentos por prefixo e ano (DN-13): TE- agora; TR- e documentos futuros depois. Não é de territórios, por
/// isso tem migration própria (Fase2b1bNumeracao). O número sai de um incremento atômico no banco (NumeracaoDocumentoSql),
/// nunca de MAX + 1: dois rascunhos criados no mesmo instante recebem números diferentes.
/// </summary>
public class NumeracaoDocumentoConfiguration : IEntityTypeConfiguration<NumeracaoDocumento>
{
    public const string CheckUltimo = "CK_NumeracoesDocumento_Ultimo";

    public void Configure(EntityTypeBuilder<NumeracaoDocumento> b)
    {
        b.ToTable("NumeracoesDocumento", t => t.HasCheckConstraint(CheckUltimo, "[Ultimo] >= 0"));
        b.HasKey(x => new { x.Prefixo, x.Ano });
        b.Property(x => x.Prefixo).HasMaxLength(NumeracaoDocumento.TamanhoMaximoPrefixo).IsUnicode(false);
    }
}
