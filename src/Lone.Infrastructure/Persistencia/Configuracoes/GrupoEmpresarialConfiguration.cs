using Lone.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lone.Infrastructure.Persistencia.Configuracoes;

/// <summary>Grupos empresariais: nome único sem diferenciar maiúsculas nem acentos. Nunca apagados (desativados).</summary>
public class GrupoEmpresarialConfiguration : IEntityTypeConfiguration<GrupoEmpresarial>
{
    public void Configure(EntityTypeBuilder<GrupoEmpresarial> b)
    {
        b.ToTable("GruposEmpresariais");
        b.HasKey(g => g.Id);
        b.Property(g => g.Nome).IsRequired().HasMaxLength(GrupoEmpresarial.TamanhoMaximoNome).UseCollation(EtiquetaConfiguration.CollationNome);
        b.Property(g => g.Descricao).HasMaxLength(GrupoEmpresarial.TamanhoMaximoDescricao);
        b.HasIndex(g => g.Nome).IsUnique();
    }
}
