using Lone.Core.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lone.Data.Configuracoes;

/// <summary>Papel de cliente: tabela 1 para 1 com Pessoas.</summary>
public class ClienteConfiguration : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> b)
    {
        b.ToTable("Clientes");
        b.HasKey(c => c.PessoaId);
        b.Property(c => c.PessoaId).ValueGeneratedNever();

        b.Property(c => c.LimiteCredito).HasPrecision(15, 2);
        b.Property(c => c.CondicaoPagamento).HasMaxLength(60);
        b.Property(c => c.MotivoBloqueio).HasMaxLength(250);
        b.Property(c => c.Observacoes).HasMaxLength(1000);
    }
}
