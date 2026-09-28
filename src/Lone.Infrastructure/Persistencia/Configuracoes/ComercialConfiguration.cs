using Lone.Domain.Comercial;
using Lone.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lone.Infrastructure.Persistencia.Configuracoes;

public class CondicaoPagamentoConfiguration : IEntityTypeConfiguration<CondicaoPagamento>
{
    public void Configure(EntityTypeBuilder<CondicaoPagamento> b)
    {
        b.ToTable("CondicoesPagamento");
        b.HasKey(x => x.Id);
        b.Property(x => x.Nome).IsRequired().HasMaxLength(CondicaoPagamento.TamanhoMaximoNome).UseCollation(EtiquetaConfiguration.CollationNome);
        b.HasIndex(x => x.Nome).IsUnique();
        b.Property(x => x.Parcelas).IsRequired().HasMaxLength(250).IsUnicode(false);
        b.Property(x => x.AcrescimoPercentual).HasPrecision(7, 4);
    }
}

public class PerfilComercialConfiguration : IEntityTypeConfiguration<PerfilComercial>
{
    public void Configure(EntityTypeBuilder<PerfilComercial> b)
    {
        b.ToTable("PerfisComerciais");
        b.HasKey(x => x.Id);
        b.Property(x => x.Nome).IsRequired().HasMaxLength(PerfilComercial.TamanhoMaximoNome).UseCollation(EtiquetaConfiguration.CollationNome);
        b.HasIndex(x => x.Nome).IsUnique();
        b.Property(x => x.LimiteCredito).HasPrecision(15, 2);
        b.Property(x => x.DescontoMaximo).HasPrecision(5, 2);
        b.HasOne<CondicaoPagamento>().WithMany().HasForeignKey(x => x.CondicaoPagamentoId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class TipoCarteiraConfiguration : IEntityTypeConfiguration<TipoCarteira>
{
    public void Configure(EntityTypeBuilder<TipoCarteira> b)
    {
        b.ToTable("TiposCarteira", t => t.HasCheckConstraint(SqlMigracaoCarteira.CheckPolitica, SqlMigracaoCarteira.RegraPolitica));
        b.HasKey(x => x.Id);
        b.Property(x => x.Nome).IsRequired().HasMaxLength(TipoCarteira.TamanhoMaximoNome).UseCollation(EtiquetaConfiguration.CollationNome);
        b.HasIndex(x => x.Nome).IsUnique();
        b.HasIndex(x => x.ResponsavelDaConta).IsUnique().HasFilter("[ResponsavelDaConta] = 1"); // um só responsável da conta
        b.Property(x => x.TipoCredito).HasConversion<byte>();
        b.Property(x => x.PercentualPadrao).HasPrecision(5, 2);

        // Papéis iniciais (Ids fixos) com a política inicial (TiposCarteiraIniciais).
        var criacao = new DateTime(2026, 9, 25, 0, 0, 0, DateTimeKind.Utc);
        b.HasData(TiposCarteiraIniciais.Todos.Select(t => new TipoCarteira
        {
            Id = t.Id, Nome = t.Nome, Ordem = t.Ordem, ResponsavelDaConta = t.ResponsavelDaConta, LimitePorVez = t.LimitePorVez,
            TipoCredito = t.TipoCredito, ContaParaMetas = t.ContaParaMetas, Ativo = true, CriadoEm = criacao
        }).ToArray());
    }
}

public class ExcecaoComercialConfiguration : IEntityTypeConfiguration<ExcecaoComercial>
{
    public void Configure(EntityTypeBuilder<ExcecaoComercial> b)
    {
        b.ToTable("ExcecoesComerciais");
        b.HasKey(x => x.Id);
        b.Property(x => x.LimiteCredito).HasPrecision(15, 2);
        b.Property(x => x.DescontoMaximo).HasPrecision(5, 2);
        b.Property(x => x.Motivo).HasMaxLength(250);
        b.HasOne<CondicaoPagamento>().WithMany().HasForeignKey(x => x.CondicaoPagamentoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Pessoa>().WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(x => new { x.PessoaId, x.InicioEm });
    }
}

public class CarteiraClienteConfiguration : IEntityTypeConfiguration<CarteiraCliente>
{
    public void Configure(EntityTypeBuilder<CarteiraCliente> b)
    {
        // Gatilho da política dos papéis ("um por vez"/exclusivo e limite; SqlMigracaoCarteira): declarado para o EF não usar OUTPUT
        // nas gravações desta tabela, que o SQL Server recusa em tabela com gatilho.
        b.ToTable("CarteiraClientes", t => t.HasTrigger(SqlMigracaoCarteira.Gatilho));
        b.HasKey(x => x.Id);
        b.Property(x => x.Observacao).HasMaxLength(250);
        b.Property(x => x.PercentualCredito).HasPrecision(5, 2);
        b.Property(x => x.Origem).HasConversion<byte>();
        b.HasOne<TipoCarteira>().WithMany().HasForeignKey(x => x.TipoCarteiraId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Pessoa>().WithMany().HasForeignKey(x => x.VendedorId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<Pessoa>().WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.NoAction);

        // "Carteira do vendedor" (quem ele atende hoje, ou numa data) e o histórico do cliente.
        b.HasIndex(x => new { x.VendedorId, x.Ativo, x.FimEm });
        b.HasIndex(x => new { x.PessoaId, x.InicioEm });
    }
}
