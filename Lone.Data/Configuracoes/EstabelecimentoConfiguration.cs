using Lone.Core.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lone.Data.Configuracoes;

public class EstabelecimentoConfiguration : IEntityTypeConfiguration<Estabelecimento>
{
    public void Configure(EntityTypeBuilder<Estabelecimento> b)
    {
        b.ToTable("Estabelecimentos");
        b.HasKey(e => e.Id);

        b.Property(e => e.Cnpj).HasMaxLength(14).IsFixedLength().IsUnicode(false);
        b.Property(e => e.NomeFantasia).HasMaxLength(150);
        b.Property(e => e.SituacaoReceita).HasMaxLength(40);
        b.Property(e => e.IndicadorIE).HasConversion<byte>();
        b.Property(e => e.InscricaoEstadual).HasMaxLength(14).IsUnicode(false);
        b.Property(e => e.InscricaoMunicipal).HasMaxLength(20).IsUnicode(false);
        b.Property(e => e.InscricaoSuframa).HasMaxLength(9).IsUnicode(false);
        b.Property(e => e.RegimeTributario).HasConversion<byte>();
        b.Property(e => e.CnaePrincipal).HasMaxLength(7).IsFixedLength().IsUnicode(false);
        b.Property(e => e.NaturezaJuridica).HasMaxLength(10).IsUnicode(false);

        // Cada CNPJ existe uma vez só no sistema.
        b.HasIndex(e => e.Cnpj).IsUnique().HasFilter("[Cnpj] IS NOT NULL");
        b.HasIndex(e => e.PessoaId);

        // Endereço excluído: o estabelecimento passa a usar o endereço padrão (FK anulada pelo EF).
        b.HasOne(e => e.EnderecoFiscal).WithMany().HasForeignKey(e => e.EnderecoFiscalId).OnDelete(DeleteBehavior.ClientSetNull);
    }
}
