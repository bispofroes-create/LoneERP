using Lone.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lone.Infrastructure.Persistencia.Configuracoes;

public class EtiquetaConfiguration : IEntityTypeConfiguration<Etiqueta>
{
    /// <summary>
    /// Sem diferenciar maiúsculas nem acentos: "VIP", "vip" e "Víp" são o mesmo nome para o índice único e para
    /// as comparações feitas no banco.
    /// </summary>
    public const string CollationNome = "Latin1_General_CI_AI";

    public void Configure(EntityTypeBuilder<Etiqueta> b)
    {
        b.ToTable("Etiquetas");
        b.HasKey(e => e.Id);
        b.Property(e => e.Nome).IsRequired().HasMaxLength(Etiqueta.TamanhoMaximoNome).UseCollation(CollationNome);
        b.Property(e => e.Descricao).HasMaxLength(Etiqueta.TamanhoMaximoDescricao);
        b.HasIndex(e => e.Nome).IsUnique();
    }
}
