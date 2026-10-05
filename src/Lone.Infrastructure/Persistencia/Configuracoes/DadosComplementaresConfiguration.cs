using Lone.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lone.Infrastructure.Persistencia.Configuracoes;

public class PessoaSocioConfiguration : IEntityTypeConfiguration<PessoaSocio>
{
    public void Configure(EntityTypeBuilder<PessoaSocio> b)
    {
        b.ToTable("PessoaSocios");
        b.HasKey(s => s.Id);
        b.Property(s => s.Nome).IsRequired().HasMaxLength(150);
        b.Property(s => s.Qualificacao).HasMaxLength(80);
        b.Property(s => s.Documento).HasMaxLength(20).IsUnicode(false);
        // P0 (D7): ex-sócio fica inativo com a data de saída (nunca apagado). Padrão 1 no banco: os que já existem nascem
        // ativos sem UPDATE; sentinela "true" pelo mesmo motivo do contato (o EF só manda o valor quando é falso).
        b.Property(s => s.Ativo).HasDefaultValue(true).HasSentinel(true);
        b.HasIndex(s => s.PessoaId);
    }
}

public class PessoaEtiquetaConfiguration : IEntityTypeConfiguration<PessoaEtiqueta>
{
    public const string ColunaTextoAntigo = "Texto";

    public void Configure(EntityTypeBuilder<PessoaEtiqueta> b)
    {
        b.ToTable("PessoaEtiquetas");
        b.HasKey(e => e.Id);

        // Texto livre de antes do cadastro de etiquetas: fica só como cópia do dado original (não é mais usado).
        b.Property<string?>(ColunaTextoAntigo).HasMaxLength(Etiqueta.TamanhoMaximoNome);

        b.HasOne<Etiqueta>().WithMany().HasForeignKey(e => e.EtiquetaId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(e => new { e.PessoaId, e.EtiquetaId }).IsUnique(); // a mesma etiqueta uma vez por pessoa
        b.HasIndex(e => new { e.EtiquetaId, e.PessoaId }); // filtro da lista por etiqueta e contagem de uso
    }
}
