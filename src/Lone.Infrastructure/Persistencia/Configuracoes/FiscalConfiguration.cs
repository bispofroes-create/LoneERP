using Lone.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lone.Infrastructure.Persistencia.Configuracoes;

public class CnaeConfiguration : IEntityTypeConfiguration<Cnae>
{
    public void Configure(EntityTypeBuilder<Cnae> b)
    {
        b.ToTable("Cnaes");
        b.HasKey(c => c.Id);
        b.Property(c => c.Id).ValueGeneratedNever(); // código de 7 dígitos
        b.Property(c => c.Descricao).IsRequired().HasMaxLength(250);
    }
}

public class EstabelecimentoCnaeConfiguration : IEntityTypeConfiguration<EstabelecimentoCnae>
{
    public void Configure(EntityTypeBuilder<EstabelecimentoCnae> b)
    {
        b.ToTable("EstabelecimentoCnaes");
        b.HasKey(c => c.Id);
        // Sem chave estrangeira para Cnaes: a tabela oficial pode ainda não ter sido carregada (o código é guardado igual).
        b.HasOne<Estabelecimento>().WithMany().HasForeignKey(c => c.EstabelecimentoId).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(c => new { c.EstabelecimentoId, c.Codigo }).IsUnique();
        b.HasIndex(c => new { c.Codigo, c.Principal }); // "clientes do CNAE X" (filtro avançado)
        b.HasIndex(c => c.PessoaId);
    }
}

public class HistoricoFiscalConfiguration : IEntityTypeConfiguration<HistoricoFiscal>
{
    public void Configure(EntityTypeBuilder<HistoricoFiscal> b)
    {
        b.ToTable("HistoricoFiscal");
        b.HasKey(h => h.Id);
        b.Property(h => h.RegimeTributario).HasConversion<byte>();
        b.Property(h => h.IndicadorIE).HasConversion<byte>();
        b.Property(h => h.InscricaoEstadual).HasMaxLength(14).IsUnicode(false);
        b.Property(h => h.SituacaoReceita).HasMaxLength(40);
        b.HasOne<Estabelecimento>().WithMany().HasForeignKey(h => h.EstabelecimentoId).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(h => new { h.EstabelecimentoId, h.InicioEm });
        b.HasIndex(h => h.PessoaId);
    }
}
