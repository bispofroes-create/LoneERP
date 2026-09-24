using Lone.Core.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lone.Data.Configuracoes;

public class RegistroAuditoriaConfiguration : IEntityTypeConfiguration<RegistroAuditoria>
{
    public void Configure(EntityTypeBuilder<RegistroAuditoria> b)
    {
        b.ToTable("Auditoria");
        b.HasKey(a => a.Id);

        b.Property(a => a.Usuario).IsRequired().HasMaxLength(100);
        b.Property(a => a.Origem).HasConversion<byte>();
        b.Property(a => a.Entidade).IsRequired().HasMaxLength(60).IsUnicode(false);
        b.Property(a => a.RegistroId).IsRequired().HasMaxLength(40).IsUnicode(false);
        b.Property(a => a.RaizEntidade).IsRequired().HasMaxLength(60).IsUnicode(false);
        b.Property(a => a.Acao).HasConversion<byte>();
        b.Property(a => a.Campo).HasMaxLength(60).IsUnicode(false);
        b.Property(a => a.ValorAnterior).HasMaxLength(500);
        b.Property(a => a.ValorNovo).HasMaxLength(500);

        // Histórico de um agregado (a consulta mais comum), da mais recente para a mais antiga.
        b.HasIndex(a => new { a.RaizEntidade, a.RaizId, a.DataHora });
        b.HasIndex(a => new { a.Entidade, a.RegistroId });
        b.HasIndex(a => a.OperacaoId);
    }
}
