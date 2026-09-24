using Lone.Core.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lone.Data.Configuracoes;

/// <summary>Papel de fornecedor: tabela 1 para 1 com Pessoas.</summary>
public class FornecedorConfiguration : IEntityTypeConfiguration<Fornecedor>
{
    public void Configure(EntityTypeBuilder<Fornecedor> b)
    {
        b.ToTable("Fornecedores");
        b.HasKey(f => f.PessoaId);
        b.Property(f => f.PessoaId).ValueGeneratedNever();

        b.Property(f => f.CondicaoPagamento).HasMaxLength(60);
        b.Property(f => f.Observacoes).HasMaxLength(1000);
    }
}
