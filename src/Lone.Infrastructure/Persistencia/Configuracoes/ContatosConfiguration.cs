using Lone.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lone.Infrastructure.Persistencia.Configuracoes;

public class MeioContatoConfiguration : IEntityTypeConfiguration<MeioContato>
{
    public void Configure(EntityTypeBuilder<MeioContato> b)
    {
        b.ToTable("PessoaMeiosContato");
        b.HasKey(m => m.Id);

        b.Property(m => m.Tipo).HasConversion<byte>();
        b.Property(m => m.Valor).IsRequired().HasMaxLength(150);
        b.Property(m => m.Descricao).HasMaxLength(80);

        b.HasIndex(m => m.PessoaId);
        b.HasIndex(m => m.Valor); // busca por telefone/e-mail e aviso de duplicidade
    }
}

public class ContatoConfiguration : IEntityTypeConfiguration<Contato>
{
    public void Configure(EntityTypeBuilder<Contato> b)
    {
        b.ToTable("PessoaContatos");
        b.HasKey(c => c.Id);

        b.Property(c => c.Nome).IsRequired().HasMaxLength(100);
        b.Property(c => c.Cargo).HasMaxLength(60);
        b.Property(c => c.Departamento).HasMaxLength(60);
        b.Property(c => c.Telefone).HasMaxLength(20).IsUnicode(false);
        b.Property(c => c.Celular).HasMaxLength(20).IsUnicode(false);
        b.Property(c => c.Email).HasMaxLength(150);
        b.Property(c => c.Observacoes).HasMaxLength(500);

        b.HasOne<Pessoa>().WithMany().HasForeignKey(c => c.PessoaVinculadaId).OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(c => c.PessoaId);
        b.HasIndex(c => c.Telefone);
        b.HasIndex(c => c.Celular);
        b.HasIndex(c => c.Email);
    }
}

public class PessoaDocumentoConfiguration : IEntityTypeConfiguration<PessoaDocumento>
{
    public void Configure(EntityTypeBuilder<PessoaDocumento> b)
    {
        b.ToTable("PessoaDocumentos");
        b.HasKey(d => d.Id);

        b.Property(d => d.Tipo).HasConversion<byte>();
        b.Property(d => d.Numero).IsRequired().HasMaxLength(30);
        b.Property(d => d.OrgaoEmissor).HasMaxLength(20);
        b.Property(d => d.Uf).HasMaxLength(2).IsFixedLength().IsUnicode(false);
        b.Property(d => d.Observacoes).HasMaxLength(250);

        b.HasIndex(d => d.PessoaId);
    }
}
