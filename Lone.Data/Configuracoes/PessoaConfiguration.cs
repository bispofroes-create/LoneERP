using Lone.Core.Entidades;
using Lone.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lone.Data.Configuracoes;

public class PessoaConfiguration : IEntityTypeConfiguration<Pessoa>
{
    public const string SequenciaCodigo = "SeqPessoaCodigo";

    public void Configure(EntityTypeBuilder<Pessoa> b)
    {
        b.ToTable("Pessoas");
        b.HasKey(p => p.Id);

        // Código sequencial gerado pelo SQL Server; nunca muda depois de criado.
        b.Property(p => p.Codigo).HasDefaultValueSql($"NEXT VALUE FOR {SequenciaCodigo}");
        b.Property(p => p.Codigo).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
        b.HasIndex(p => p.Codigo).IsUnique();

        b.Property(p => p.Natureza).HasConversion<byte>();
        b.Property(p => p.Situacao).HasConversion<byte>();

        b.Property(p => p.Nome).IsRequired().HasMaxLength(150);
        b.Property(p => p.NomeSocial).HasMaxLength(150);
        b.Property(p => p.NomeExibicao).HasMaxLength(80);
        b.Property(p => p.Apelido).HasMaxLength(60);
        b.Property(p => p.DocumentoPrincipal).HasMaxLength(20).IsUnicode(false);
        b.Property(p => p.Observacoes).HasMaxLength(2000);

        // CPF e raiz de CNPJ não se repetem. Estrangeiros ficam de fora (documentos de países diferentes podem coincidir).
        b.HasIndex(p => new { p.Natureza, p.DocumentoPrincipal })
            .IsUnique()
            .HasFilter($"[DocumentoPrincipal] IS NOT NULL AND [Natureza] <> {(byte)NaturezaPessoa.Estrangeiro}");
        b.HasIndex(p => p.Nome);

        b.HasOne<GrupoEconomico>().WithMany().HasForeignKey(p => p.GrupoEconomicoId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<Pessoa>().WithMany().HasForeignKey(p => p.MescladaEmId).OnDelete(DeleteBehavior.NoAction);

        // Partes do agregado: apagadas junto com a pessoa (o que na prática não acontece: pessoas são inativadas).
        b.HasMany(p => p.Estabelecimentos).WithOne().HasForeignKey(x => x.PessoaId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(p => p.Documentos).WithOne().HasForeignKey(x => x.PessoaId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(p => p.Enderecos).WithOne().HasForeignKey(x => x.PessoaId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(p => p.MeiosContato).WithOne().HasForeignKey(x => x.PessoaId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(p => p.Contatos).WithOne().HasForeignKey(x => x.PessoaId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(p => p.Papeis).WithOne().HasForeignKey(x => x.PessoaId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(p => p.ContasCliente).WithOne().HasForeignKey(x => x.PessoaId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(p => p.ContasFornecedor).WithOne().HasForeignKey(x => x.PessoaId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(p => p.Bloqueios).WithOne().HasForeignKey(x => x.PessoaId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(p => p.Relacionamentos).WithOne().HasForeignKey(x => x.PessoaId).OnDelete(DeleteBehavior.Cascade);
    }
}
