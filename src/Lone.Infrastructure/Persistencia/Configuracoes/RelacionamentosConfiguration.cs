using Lone.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lone.Infrastructure.Persistencia.Configuracoes;

public class GrupoEconomicoConfiguration : IEntityTypeConfiguration<GrupoEconomico>
{
    public void Configure(EntityTypeBuilder<GrupoEconomico> b)
    {
        b.ToTable("GruposEconomicos");
        b.HasKey(g => g.Id);
        b.Property(g => g.Nome).IsRequired().HasMaxLength(100);
        b.Property(g => g.Observacoes).HasMaxLength(1000);
        b.HasIndex(g => g.Nome).IsUnique();
    }
}

public class TipoRelacionamentoConfiguration : IEntityTypeConfiguration<TipoRelacionamento>
{
    public void Configure(EntityTypeBuilder<TipoRelacionamento> b)
    {
        b.ToTable("TiposRelacionamento");
        b.HasKey(t => t.Id);
        b.Property(t => t.Nome).IsRequired().HasMaxLength(60);
        b.Property(t => t.NomeInverso).IsRequired().HasMaxLength(60);
        b.HasIndex(t => t.Nome).IsUnique();

        // Tipos iniciais (o usuário pode cadastrar outros). Ids fixos: são os mesmos em todo banco do Lone.
        var criacao = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        b.HasData(
            Tipo(TiposRelacionamentoSistema.SocioDe, "Sócio de", "Tem como sócio", criacao),
            Tipo(TiposRelacionamentoSistema.ResponsavelPor, "Responsável por", "Tem como responsável", criacao),
            Tipo(TiposRelacionamentoSistema.RepresentanteDe, "Representante de", "Representada por", criacao),
            Tipo(TiposRelacionamentoSistema.ContatoDe, "Contato de", "Tem como contato", criacao),
            Tipo(TiposRelacionamentoSistema.DependenteDe, "Dependente de", "Tem como dependente", criacao),
            Tipo(TiposRelacionamentoSistema.FuncionarioDe, "Funcionário de", "Tem como funcionário", criacao),
            Tipo(TiposRelacionamentoSistema.AdministradorDe, "Administrador de", "Tem como administrador", criacao),
            Tipo(TiposRelacionamentoSistema.ParceiroDe, "Parceiro de", "Parceiro de", criacao));
    }

    private static TipoRelacionamento Tipo(Guid id, string nome, string inverso, DateTime criacao) =>
        new() { Id = id, Nome = nome, NomeInverso = inverso, Sistema = true, CriadoEm = criacao };
}

public class PessoaRelacionamentoConfiguration : IEntityTypeConfiguration<PessoaRelacionamento>
{
    public void Configure(EntityTypeBuilder<PessoaRelacionamento> b)
    {
        b.ToTable("PessoaRelacionamentos");
        b.HasKey(r => r.Id);
        b.Property(r => r.Observacoes).HasMaxLength(250);

        b.HasOne<Pessoa>().WithMany().HasForeignKey(r => r.PessoaDestinoId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(r => r.TipoRelacionamento).WithMany().HasForeignKey(r => r.TipoRelacionamentoId).OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(r => r.PessoaId);
        b.HasIndex(r => r.PessoaDestinoId);

        // O mesmo vínculo (origem, destino, tipo) em aberto só uma vez; períodos encerrados e desativados ficam no histórico.
        b.HasIndex(r => new { r.PessoaId, r.PessoaDestinoId, r.TipoRelacionamentoId })
            .IsUnique()
            .HasFilter("[Ativo] = 1 AND [FimEm] IS NULL")
            .HasDatabaseName(SqlMigracaoEstruturaEmpresarial.IndiceRelacionamentoAberto);
    }
}
