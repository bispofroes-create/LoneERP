using Lone.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lone.Infrastructure.Persistencia.Configuracoes;

public class PessoaSocioConfiguration : IEntityTypeConfiguration<PessoaSocio>
{
    public void Configure(EntityTypeBuilder<PessoaSocio> b)
    {
        b.ToTable("PessoaSocios");
        b.HasKey(s => s.Id);
        b.Property(s => s.Nome).IsRequired().HasMaxLength(150);
        b.Property(s => s.Qualificacao).HasMaxLength(80);
        b.Property(s => s.Documento).HasMaxLength(20).IsUnicode(false);
        b.HasIndex(s => s.PessoaId);
    }
}

public class PessoaConsentimentoConfiguration : IEntityTypeConfiguration<PessoaConsentimento>
{
    public void Configure(EntityTypeBuilder<PessoaConsentimento> b)
    {
        b.ToTable("PessoaConsentimentos");
        b.HasKey(c => c.Id);
        b.Property(c => c.Canal).HasConversion<byte>();
        b.Property(c => c.Origem).HasMaxLength(80);
        b.HasIndex(c => new { c.PessoaId, c.Canal }).IsUnique(); // um por canal
    }
}

public class PessoaEtiquetaConfiguration : IEntityTypeConfiguration<PessoaEtiqueta>
{
    public void Configure(EntityTypeBuilder<PessoaEtiqueta> b)
    {
        b.ToTable("PessoaEtiquetas");
        b.HasKey(e => e.Id);
        b.Property(e => e.Texto).IsRequired().HasMaxLength(PessoaEtiqueta.TamanhoMaximo);
        b.HasIndex(e => new { e.PessoaId, e.Texto }).IsUnique();
        b.HasIndex(e => e.Texto); // filtro da lista por etiqueta
    }
}
