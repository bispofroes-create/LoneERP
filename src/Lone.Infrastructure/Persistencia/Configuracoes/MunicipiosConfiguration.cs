using Lone.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lone.Infrastructure.Persistencia.Configuracoes;

public class MunicipioConfiguration : IEntityTypeConfiguration<Municipio>
{
    public void Configure(EntityTypeBuilder<Municipio> b)
    {
        b.ToTable("Municipios");
        b.HasKey(m => m.Id);
        b.Property(m => m.Id).ValueGeneratedNever(); // código IBGE

        b.Property(m => m.Nome).IsRequired().HasMaxLength(80);
        b.Property(m => m.NomeBusca).IsRequired().HasMaxLength(80).IsUnicode(false);
        b.Property(m => m.Uf).IsRequired().HasMaxLength(2).IsFixedLength().IsUnicode(false);

        // Autocompletar por UF e conciliação de textos antigos (nome + UF, ou só o nome).
        b.HasIndex(m => new { m.Uf, m.NomeBusca });
        b.HasIndex(m => m.NomeBusca);
    }
}

public class PendenciaMunicipioConfiguration : IEntityTypeConfiguration<PendenciaMunicipio>
{
    public void Configure(EntityTypeBuilder<PendenciaMunicipio> b)
    {
        b.ToTable("PendenciasMunicipio");
        b.HasKey(p => p.Id);
        b.Property(p => p.Origem).HasConversion<byte>();
        b.Property(p => p.TextoOriginal).IsRequired().HasMaxLength(100);
        b.Property(p => p.UfOriginal).HasMaxLength(2).IsFixedLength().IsUnicode(false);
        b.Property(p => p.CodigoIbgeOriginal).HasMaxLength(7).IsUnicode(false);
        b.Property(p => p.Observacao).HasMaxLength(200);
        b.Property(p => p.ResolvidaPor).HasMaxLength(100);

        b.HasOne<Pessoa>().WithMany().HasForeignKey(p => p.PessoaId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Municipio>().WithMany().HasForeignKey(p => p.MunicipioId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(p => new { p.PessoaId, p.ResolvidaEm });
        b.HasIndex(p => p.ResolvidaEm); // "município a corrigir"
    }
}
