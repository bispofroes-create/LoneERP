using Lone.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lone.Infrastructure.Persistencia.Configuracoes;

public class TokenRenovacaoConfiguration : IEntityTypeConfiguration<TokenRenovacao>
{
    public void Configure(EntityTypeBuilder<TokenRenovacao> b)
    {
        b.ToTable("TokensRenovacao");
        b.HasKey(t => t.Id);

        b.Property(t => t.Hash).IsRequired().HasMaxLength(44).IsUnicode(false);
        b.Property(t => t.Dispositivo).HasMaxLength(100);

        b.HasIndex(t => t.Hash).IsUnique();
        b.HasIndex(t => new { t.UsuarioId, t.RevogadoEm });

        b.HasOne<Usuario>().WithMany().HasForeignKey(t => t.UsuarioId).OnDelete(DeleteBehavior.Cascade);
    }
}
