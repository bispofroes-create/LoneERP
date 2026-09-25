using Lone.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lone.Infrastructure.Persistencia.Configuracoes;

public class CargoConfiguration : IEntityTypeConfiguration<Cargo>
{
    public void Configure(EntityTypeBuilder<Cargo> b)
    {
        b.ToTable("Cargos");
        b.HasKey(x => x.Id);
        b.Property(x => x.Nome).IsRequired().HasMaxLength(Cargo.TamanhoMaximoNome).UseCollation(EtiquetaConfiguration.CollationNome);
        b.HasIndex(x => x.Nome).IsUnique();
        b.HasOne<OcupacaoCbo>().WithMany().HasForeignKey(x => x.OcupacaoCboId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class DepartamentoConfiguration : IEntityTypeConfiguration<Departamento>
{
    public void Configure(EntityTypeBuilder<Departamento> b)
    {
        b.ToTable("Departamentos");
        b.HasKey(x => x.Id);
        b.Property(x => x.Nome).IsRequired().HasMaxLength(Departamento.TamanhoMaximoNome).UseCollation(EtiquetaConfiguration.CollationNome);
        b.HasIndex(x => x.Nome).IsUnique();
    }
}

public class SetorConfiguration : IEntityTypeConfiguration<Setor>
{
    public void Configure(EntityTypeBuilder<Setor> b)
    {
        b.ToTable("Setores");
        b.HasKey(x => x.Id);
        b.Property(x => x.Nome).IsRequired().HasMaxLength(Setor.TamanhoMaximoNome).UseCollation(EtiquetaConfiguration.CollationNome);
        b.HasOne<Departamento>().WithMany().HasForeignKey(x => x.DepartamentoId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.DepartamentoId, x.Nome }).IsUnique(); // nome único dentro do departamento
    }
}

public class CentroCustoConfiguration : IEntityTypeConfiguration<CentroCusto>
{
    public void Configure(EntityTypeBuilder<CentroCusto> b)
    {
        b.ToTable("CentrosCusto");
        b.HasKey(x => x.Id);
        b.Ignore(x => x.Descricao);
        b.Property(x => x.Codigo).IsRequired().HasMaxLength(CentroCusto.TamanhoMaximoCodigo).IsUnicode(false);
        b.Property(x => x.Nome).IsRequired().HasMaxLength(CentroCusto.TamanhoMaximoNome);
        b.HasIndex(x => x.Codigo).IsUnique();
        b.HasOne<CentroCusto>().WithMany().HasForeignKey(x => x.PaiId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.PaiId);
    }
}

public class VinculoColaboradorConfiguration : IEntityTypeConfiguration<VinculoColaborador>
{
    public void Configure(EntityTypeBuilder<VinculoColaborador> b)
    {
        b.ToTable("VinculosColaborador");
        b.HasKey(x => x.Id);
        b.Property(x => x.Tipo).HasConversion<byte>();
        b.Property(x => x.Matricula).HasMaxLength(VinculoColaborador.TamanhoMaximoMatricula);
        b.Property(x => x.MotivoDesligamento).HasMaxLength(VinculoColaborador.TamanhoMaximoTexto);
        b.Property(x => x.Observacoes).HasMaxLength(VinculoColaborador.TamanhoMaximoTexto);
        b.Property(x => x.JornadaSemanal).HasPrecision(5, 2);

        // Empresa = pessoa do grupo. NoAction: a pessoa (colaborador) já apaga em cascata; dois caminhos não são permitidos.
        b.HasOne<Pessoa>().WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(x => new { x.EmpresaId, x.Matricula }).IsUnique().HasFilter("[Matricula] IS NOT NULL");
        b.HasIndex(x => x.PessoaId);
    }
}

public class LotacaoColaboradorConfiguration : IEntityTypeConfiguration<LotacaoColaborador>
{
    public void Configure(EntityTypeBuilder<LotacaoColaborador> b)
    {
        b.ToTable("LotacoesColaborador");
        b.HasKey(x => x.Id);
        b.HasOne<VinculoColaborador>().WithMany().HasForeignKey(x => x.VinculoId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<Cargo>().WithMany().HasForeignKey(x => x.CargoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Departamento>().WithMany().HasForeignKey(x => x.DepartamentoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Setor>().WithMany().HasForeignKey(x => x.SetorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CentroCusto>().WithMany().HasForeignKey(x => x.CentroCustoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Pessoa>().WithMany().HasForeignKey(x => x.GestorId).OnDelete(DeleteBehavior.NoAction);

        // Consultas por período (metas, "quem estava no departamento X em março") e equipe do gestor.
        b.HasIndex(x => new { x.VinculoId, x.InicioEm });
        b.HasIndex(x => new { x.DepartamentoId, x.InicioEm });
        b.HasIndex(x => new { x.GestorId, x.FimEm });
        b.HasIndex(x => x.PessoaId);
    }
}
