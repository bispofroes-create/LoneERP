using Lone.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lone.Infrastructure.Persistencia.Configuracoes;

public class RegistroAuditoriaConfiguration : IEntityTypeConfiguration<RegistroAuditoria>
{
    public void Configure(EntityTypeBuilder<RegistroAuditoria> b)
    {
        // P0 (D4): o gatilho que só deixa incluir. Declarado aqui para o EF não usar OUTPUT na inclusão (o SQL Server recusa
        // OUTPUT sem INTO em tabela com gatilho). O gatilho em si é criado pela migração (SqlMigracaoAuditoria).
        b.ToTable("Auditoria", t => t.HasTrigger(SqlMigracaoAuditoria.Gatilho));
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
        b.Property(a => a.Descricao).HasMaxLength(500);
        b.Property(a => a.Motivo).HasMaxLength(RegistroAuditoria.TamanhoMaximoMotivo);

        // Histórico de um agregado (a consulta mais comum), da mais recente para a mais antiga.
        b.HasIndex(a => new { a.RaizEntidade, a.RaizId, a.DataHora });
        b.HasIndex(a => new { a.Entidade, a.RegistroId });
        b.HasIndex(a => a.OperacaoId);
    }
}
