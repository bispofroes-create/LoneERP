using Lone.Core.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lone.Data.Configuracoes;

public class PessoaContatoConfiguration : IEntityTypeConfiguration<PessoaContato>
{
    public void Configure(EntityTypeBuilder<PessoaContato> b)
    {
        b.ToTable("PessoaContatos");
        b.HasKey(c => c.Id);

        b.Property(c => c.Tipo).HasConversion<byte>();
        b.Property(c => c.Valor).IsRequired().HasMaxLength(150);
        b.Property(c => c.Descricao).HasMaxLength(80);

        b.HasIndex(c => c.PessoaId);
        b.HasIndex(c => c.Valor); // busca por telefone/e-mail e aviso de duplicidade
    }
}
