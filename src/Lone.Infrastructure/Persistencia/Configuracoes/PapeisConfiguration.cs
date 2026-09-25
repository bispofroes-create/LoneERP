using Lone.Domain.Entidades;
using Lone.Domain.Papeis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lone.Infrastructure.Persistencia.Configuracoes;

public class PessoaPapelConfiguration : IEntityTypeConfiguration<PessoaPapel>
{
    public void Configure(EntityTypeBuilder<PessoaPapel> b)
    {
        b.ToTable("PessoaPapeis");
        b.HasKey(p => p.Id);

        b.Property(p => p.Papel).HasConversion<byte>();
        b.Property(p => p.Observacoes).HasMaxLength(500);

        b.HasOne<Papel>().WithMany().HasForeignKey(p => p.PapelId).OnDelete(DeleteBehavior.Restrict);
        // Um período ativo por papel; os encerrados ficam (histórico), por isso o índice único só olha os ativos.
        b.HasIndex(p => new { p.PessoaId, p.PapelId }).IsUnique().HasFilter("[Ativo] = 1");
        b.HasIndex(p => new { p.PapelId, p.Ativo }); // filtro da lista por papel e contagem de uso
        b.HasIndex(p => new { p.Papel, p.Ativo });   // regras que usam o papel de sistema (empresas do grupo, clientes ativos)
    }
}

public class PapelConfiguration : IEntityTypeConfiguration<Papel>
{
    public void Configure(EntityTypeBuilder<Papel> b)
    {
        b.ToTable("Papeis");
        b.HasKey(p => p.Id);
        b.Property(p => p.Codigo).IsRequired().HasMaxLength(Papel.TamanhoMaximoCodigo).IsUnicode(false);
        b.Property(p => p.Nome).IsRequired().HasMaxLength(Papel.TamanhoMaximoNome).UseCollation(EtiquetaConfiguration.CollationNome);
        b.Property(p => p.Descricao).HasMaxLength(Papel.TamanhoMaximoDescricao);
        b.Property(p => p.PapelSistema).HasConversion<byte?>();
        b.HasIndex(p => p.Codigo).IsUnique();
        b.HasIndex(p => p.Nome).IsUnique();
        b.HasIndex(p => p.PapelSistema).IsUnique().HasFilter("[PapelSistema] IS NOT NULL");

        // Os oito papéis de sistema nascem com a base, com Ids fixos (os mesmos em todo banco do Lone).
        var criacao = new DateTime(2026, 9, 25, 0, 0, 0, DateTimeKind.Utc);
        b.HasData(PapeisSistema.Todos.Select(p => new Papel
        {
            Id = p.Id,
            Codigo = p.Codigo,
            Nome = p.Nome,
            Ordem = p.Ordem,
            Ativo = true,
            PapelSistema = p.Tipo,
            CriadoEm = criacao
        }).ToArray());
    }
}

public class ContaClienteConfiguration : IEntityTypeConfiguration<ContaCliente>
{
    public void Configure(EntityTypeBuilder<ContaCliente> b)
    {
        b.ToTable("ContasCliente");
        b.HasKey(c => c.Id);

        b.Property(c => c.LimiteCredito).HasPrecision(15, 2);
        b.Property(c => c.DescontoMaximo).HasPrecision(5, 2);
        b.Property(c => c.CondicaoPagamento).HasMaxLength(60);
        b.HasOne<PerfilComercial>().WithMany().HasForeignKey(c => c.PerfilComercialId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CondicaoPagamento>().WithMany().HasForeignKey(c => c.CondicaoPagamentoId).OnDelete(DeleteBehavior.Restrict);
        b.Property(c => c.Observacoes).HasMaxLength(1000);

        b.HasOne<Pessoa>().WithMany().HasForeignKey(c => c.EmpresaId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<Pessoa>().WithMany().HasForeignKey(c => c.VendedorPadraoId).OnDelete(DeleteBehavior.NoAction);

        // Uma conta por empresa, e uma só conta padrão (EmpresaId nulo) — por isso sem filtro de nulos.
        b.HasIndex(c => new { c.PessoaId, c.EmpresaId }).IsUnique().HasFilter(null);
    }
}

public class ContaFornecedorConfiguration : IEntityTypeConfiguration<ContaFornecedor>
{
    public void Configure(EntityTypeBuilder<ContaFornecedor> b)
    {
        b.ToTable("ContasFornecedor");
        b.HasKey(f => f.Id);

        b.Property(f => f.CondicaoPagamento).HasMaxLength(60);
        b.Property(f => f.Observacoes).HasMaxLength(1000);

        b.HasOne<Pessoa>().WithMany().HasForeignKey(f => f.EmpresaId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<Pessoa>().WithMany().HasForeignKey(f => f.TransportadoraPadraoId).OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(f => new { f.PessoaId, f.EmpresaId }).IsUnique().HasFilter(null);
    }
}

public class BloqueioConfiguration : IEntityTypeConfiguration<Bloqueio>
{
    public void Configure(EntityTypeBuilder<Bloqueio> b)
    {
        b.ToTable("PessoaBloqueios");
        b.HasKey(x => x.Id);

        b.Property(x => x.Escopo).HasConversion<byte>();
        b.Property(x => x.Origem).HasConversion<byte>();
        b.Property(x => x.Motivo).IsRequired().HasMaxLength(250);
        b.Property(x => x.InicioPor).IsRequired().HasMaxLength(100);
        b.Property(x => x.FimPor).HasMaxLength(100);
        b.Property(x => x.MotivoLiberacao).HasMaxLength(250);

        b.HasOne<Pessoa>().WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(x => new { x.PessoaId, x.FimEm });
    }
}
