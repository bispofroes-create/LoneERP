using Lone.Core.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lone.Data.Configuracoes;

public class PessoaEnderecoConfiguration : IEntityTypeConfiguration<PessoaEndereco>
{
    public void Configure(EntityTypeBuilder<PessoaEndereco> b)
    {
        b.ToTable("PessoaEnderecos");
        b.HasKey(e => e.Id);

        b.Property(e => e.Descricao).HasMaxLength(60);
        b.Property(e => e.Finalidades).HasConversion<short>();
        b.Property(e => e.Cep).HasMaxLength(8).IsUnicode(false);
        b.Property(e => e.Logradouro).IsRequired().HasMaxLength(150);
        b.Property(e => e.Numero).HasMaxLength(10);
        b.Property(e => e.Complemento).HasMaxLength(60);
        b.Property(e => e.Bairro).HasMaxLength(80);
        b.Property(e => e.Cidade).IsRequired().HasMaxLength(80);
        b.Property(e => e.Uf).HasMaxLength(2).IsFixedLength().IsUnicode(false);
        b.Property(e => e.CodigoMunicipioIbge).HasMaxLength(7).IsFixedLength().IsUnicode(false);
        b.Property(e => e.CodigoPais).IsRequired().HasMaxLength(4).IsUnicode(false);
        b.Property(e => e.Pais).IsRequired().HasMaxLength(60);

        b.HasIndex(e => new { e.PessoaId, e.Ordem });
    }
}
