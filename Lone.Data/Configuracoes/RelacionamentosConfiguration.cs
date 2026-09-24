using Lone.Core.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lone.Data.Configuracoes;

public class GrupoEconomicoConfiguration : IEntityTypeConfiguration<GrupoEconomico>
{
    public void Configure(EntityTypeBuilder<GrupoEconomico> b)
    {
        b.ToTable("GruposEconomicos");
        b.HasKey(g => g.Id);
        b.Property(g => g.Nome).IsRequired().HasMaxLength(100);
        b.Property(g => g.Observacoes).HasMaxLength(1000);
        b.HasIndex(g => g.Nome).IsUnique();
    }
}

public class TipoRelacionamentoConfiguration : IEntityTypeConfiguration<TipoRelacionamento>
{
    public void Configure(EntityTypeBuilder<TipoRelacionamento> b)
    {
        b.ToTable("TiposRelacionamento");
        b.HasKey(t => t.Id);
        b.Property(t => t.Nome).IsRequired().HasMaxLength(60);
        b.Property(t => t.NomeInverso).IsRequired().HasMaxLength(60);
        b.HasIndex(t => t.Nome).IsUnique();

        // Tipos iniciais (o usuário pode cadastrar outros).
        var criacao = new DateTime(2026, 1, 1);
        b.HasData(
            new TipoRelacionamento { Id = 1, Nome = "Sócio de", NomeInverso = "Tem como sócio", Sistema = true, CriadoEm = criacao },
            new TipoRelacionamento { Id = 2, Nome = "Responsável por", NomeInverso = "Tem como responsável", Sistema = true, CriadoEm = criacao },
            new TipoRelacionamento { Id = 3, Nome = "Representante de", NomeInverso = "Representada por", Sistema = true, CriadoEm = criacao },
            new TipoRelacionamento { Id = 4, Nome = "Contato de", NomeInverso = "Tem como contato", Sistema = true, CriadoEm = criacao },
            new TipoRelacionamento { Id = 5, Nome = "Dependente de", NomeInverso = "Tem como dependente", Sistema = true, CriadoEm = criacao },
            new TipoRelacionamento { Id = 6, Nome = "Funcionário de", NomeInverso = "Tem como funcionário", Sistema = true, CriadoEm = criacao });
    }
}

public class PessoaRelacionamentoConfiguration : IEntityTypeConfiguration<PessoaRelacionamento>
{
    public void Configure(EntityTypeBuilder<PessoaRelacionamento> b)
    {
        b.ToTable("PessoaRelacionamentos");
        b.HasKey(r => r.Id);
        b.Property(r => r.Observacoes).HasMaxLength(250);

        b.HasOne<Pessoa>().WithMany().HasForeignKey(r => r.PessoaDestinoId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(r => r.TipoRelacionamento).WithMany().HasForeignKey(r => r.TipoRelacionamentoId).OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(r => r.PessoaId);
        b.HasIndex(r => r.PessoaDestinoId);
    }
}
