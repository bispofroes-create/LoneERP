using Lone.Domain.CamposPersonalizados;
using Lone.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lone.Infrastructure.Persistencia.Configuracoes;

public class CampoPersonalizadoConfiguration : IEntityTypeConfiguration<CampoPersonalizado>
{
    public void Configure(EntityTypeBuilder<CampoPersonalizado> b)
    {
        b.ToTable("CamposPersonalizados");
        b.HasKey(c => c.Id);
        b.Ignore(c => c.Definicao);

        b.Property(c => c.Entidade).HasConversion<byte>();
        b.Property(c => c.Tipo).HasConversion<byte>();
        b.Property(c => c.Nome).IsRequired().HasMaxLength(CampoPersonalizado.TamanhoMaximoNome);
        b.Property(c => c.Dica).HasMaxLength(CampoPersonalizado.TamanhoMaximoDica);
        b.Property(c => c.Minimo).HasPrecision(19, 6);
        b.Property(c => c.Maximo).HasPrecision(19, 6);

        // Nome único por cadastro (inclusive entre os desativados, para o histórico não ficar ambíguo).
        b.HasIndex(c => new { c.Entidade, c.Nome }).IsUnique();
        b.HasIndex(c => new { c.Entidade, c.Ativo, c.Ordem });

        b.HasMany(c => c.Opcoes).WithOne().HasForeignKey(o => o.CampoId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class CampoPersonalizadoOpcaoConfiguration : IEntityTypeConfiguration<CampoPersonalizadoOpcao>
{
    public void Configure(EntityTypeBuilder<CampoPersonalizadoOpcao> b)
    {
        b.ToTable("CampoPersonalizadoOpcoes");
        b.HasKey(o => o.Id);
        b.Property(o => o.Texto).IsRequired().HasMaxLength(CampoPersonalizadoOpcao.TamanhoMaximoTexto);
        b.HasIndex(o => new { o.CampoId, o.Ordem });
    }
}

public class PessoaValorPersonalizadoConfiguration : IEntityTypeConfiguration<PessoaValorPersonalizado>
{
    public void Configure(EntityTypeBuilder<PessoaValorPersonalizado> b)
    {
        b.ToTable("PessoaValoresPersonalizados");
        b.HasKey(v => v.Id);
        b.Ignore(v => v.Vazio);

        b.Property(v => v.ValorTexto).HasMaxLength(TiposCampo.TamanhoMaximoTexto);
        b.Property(v => v.ValorNumero).HasPrecision(19, 6);

        // Campos e opções nunca são apagados (só desativados): Restrict protege os valores gravados.
        b.HasOne<CampoPersonalizado>().WithMany().HasForeignKey(v => v.CampoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CampoPersonalizadoOpcao>().WithMany().HasForeignKey(v => v.OpcaoId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(v => new { v.PessoaId, v.CampoId }).IsUnique(); // um valor por campo
        // Filtros e relatórios por valor ("quantidade de filhos > 2", "time = Atlético").
        b.HasIndex(v => new { v.CampoId, v.ValorNumero });
        b.HasIndex(v => new { v.CampoId, v.ValorData });
        b.HasIndex(v => new { v.CampoId, v.OpcaoId });
    }
}
