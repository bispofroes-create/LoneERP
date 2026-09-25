using Lone.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lone.Infrastructure.Persistencia.Configuracoes;

public class ProfissaoConfiguration : IEntityTypeConfiguration<Profissao>
{
    public void Configure(EntityTypeBuilder<Profissao> b)
    {
        b.ToTable("Profissoes");
        b.HasKey(p => p.Id);
        // Sem diferenciar maiúsculas nem acentos: "Advogado", "ADVOGADO" e "advogádo" são o mesmo nome.
        b.Property(p => p.Nome).IsRequired().HasMaxLength(Profissao.TamanhoMaximoNome).UseCollation(EtiquetaConfiguration.CollationNome);
        b.Property(p => p.Descricao).HasMaxLength(Profissao.TamanhoMaximoDescricao);
        b.HasIndex(p => p.Nome).IsUnique();
        b.HasOne<OcupacaoCbo>().WithMany().HasForeignKey(p => p.OcupacaoCboId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(p => p.OcupacaoCboId);
    }
}

public class OcupacaoCboConfiguration : IEntityTypeConfiguration<OcupacaoCbo>
{
    public void Configure(EntityTypeBuilder<OcupacaoCbo> b)
    {
        b.ToTable("OcupacoesCbo");
        b.HasKey(o => o.Id);
        b.Property(o => o.Id).ValueGeneratedNever(); // o código oficial, não um número gerado pelo banco
        b.Property(o => o.Titulo).IsRequired().HasMaxLength(OcupacaoCbo.TamanhoMaximoTitulo).UseCollation(EtiquetaConfiguration.CollationNome);
        b.HasIndex(o => new { o.Ativo, o.Titulo });
    }
}
