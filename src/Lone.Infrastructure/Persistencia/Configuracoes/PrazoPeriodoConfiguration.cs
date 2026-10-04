using Lone.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lone.Infrastructure.Persistencia.Configuracoes;

/// <summary>Prazos de período (03/10/2026): quantidade + unidade únicas; a migração cria os prazos de antes do cadastro.</summary>
public class PrazoPeriodoConfiguration : IEntityTypeConfiguration<PrazoPeriodo>
{
    public void Configure(EntityTypeBuilder<PrazoPeriodo> b)
    {
        b.ToTable("PrazosPeriodo", t => t.HasCheckConstraint("CK_PrazosPeriodo_Quantidade", "[Quantidade] >= 1"));
        b.HasKey(x => x.Id);
        b.Ignore(x => x.Nome);
        b.Property(x => x.Unidade).HasConversion<int>();
        b.HasIndex(x => new { x.Quantidade, x.Unidade }).IsUnique();

        var criacao = new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc);
        b.HasData(RegrasPrazoPeriodo.Iniciais.Select(p => new PrazoPeriodo
        {
            Id = p.Id, Quantidade = p.Quantidade, Unidade = p.Unidade, Ativo = true, CriadoEm = criacao
        }).ToArray());
    }
}
