using Lone.Core.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lone.Data.Configuracoes;

public class PerfilConfiguration : IEntityTypeConfiguration<Perfil>
{
    public void Configure(EntityTypeBuilder<Perfil> b)
    {
        b.ToTable("Perfis");
        b.HasKey(p => p.Id);

        b.Property(p => p.Nome).IsRequired().HasMaxLength(60);
        b.Property(p => p.Descricao).HasMaxLength(250);
        b.HasIndex(p => p.Nome).IsUnique();

        b.HasMany(p => p.Permissoes).WithOne().HasForeignKey(x => x.PerfilId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PerfilPermissaoConfiguration : IEntityTypeConfiguration<PerfilPermissao>
{
    public void Configure(EntityTypeBuilder<PerfilPermissao> b)
    {
        b.ToTable("PerfilPermissoes");
        b.HasKey(p => new { p.PerfilId, p.Codigo });
        b.Property(p => p.Codigo).HasMaxLength(80).IsUnicode(false);
    }
}
