using Lone.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lone.Infrastructure.Persistencia.Configuracoes;

public class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> b)
    {
        b.ToTable("Usuarios");
        b.HasKey(u => u.Id);

        b.Property(u => u.Login).IsRequired().HasMaxLength(60).IsUnicode(false);
        b.Property(u => u.Nome).IsRequired().HasMaxLength(100);
        b.Property(u => u.Email).HasMaxLength(150);
        b.Property(u => u.SenhaHash).IsRequired().HasMaxLength(200).IsUnicode(false);

        b.HasIndex(u => u.Login).IsUnique();

        b.HasMany(u => u.Perfis).WithOne().HasForeignKey(p => p.UsuarioId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(u => u.Acesso).WithOne().HasForeignKey<UsuarioAcesso>(a => a.UsuarioId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class UsuarioAcessoConfiguration : IEntityTypeConfiguration<UsuarioAcesso>
{
    public void Configure(EntityTypeBuilder<UsuarioAcesso> b)
    {
        b.ToTable("UsuarioAcessos");
        b.HasKey(a => a.UsuarioId);
        b.Property(a => a.UsuarioId).ValueGeneratedNever();
    }
}

public class UsuarioPerfilConfiguration : IEntityTypeConfiguration<UsuarioPerfil>
{
    public void Configure(EntityTypeBuilder<UsuarioPerfil> b)
    {
        b.ToTable("UsuarioPerfis");
        b.HasKey(p => p.Id);

        // Perfil em uso não pode ser apagado (perfis são inativados, não excluídos).
        b.HasOne(p => p.Perfil).WithMany().HasForeignKey(p => p.PerfilId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Pessoa>().WithMany().HasForeignKey(p => p.EmpresaId).OnDelete(DeleteBehavior.NoAction);

        // O mesmo perfil uma vez por empresa (e uma vez "em todas") — por isso sem filtro de nulos.
        b.HasIndex(p => new { p.UsuarioId, p.PerfilId, p.EmpresaId }).IsUnique().HasFilter(null);
        b.HasIndex(p => p.PerfilId);
    }
}
