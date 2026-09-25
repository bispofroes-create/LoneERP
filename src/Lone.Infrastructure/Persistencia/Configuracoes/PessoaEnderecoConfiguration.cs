using Lone.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lone.Infrastructure.Persistencia.Configuracoes;

public class PessoaEnderecoConfiguration : IEntityTypeConfiguration<PessoaEndereco>
{
    public void Configure(EntityTypeBuilder<PessoaEndereco> b)
    {
        b.ToTable("PessoaEnderecos");
        b.HasKey(e => e.Id);

        b.Property(e => e.Descricao).HasMaxLength(60);
        b.Property(e => e.Finalidades).HasConversion<short>();
        b.Property(e => e.Cep).HasMaxLength(8).IsUnicode(false);
        b.Property(e => e.Logradouro).IsRequired().HasMaxLength(150);
        b.Property(e => e.Numero).HasMaxLength(10);
        b.Property(e => e.Complemento).HasMaxLength(60);
        b.Property(e => e.Bairro).HasMaxLength(80);
        b.Property(e => e.Cidade).IsRequired().HasMaxLength(80);
        b.Property(e => e.Uf).HasMaxLength(2).IsFixedLength().IsUnicode(false);
        b.Property(e => e.CodigoMunicipioIbge).HasMaxLength(7).IsFixedLength().IsUnicode(false);
        b.Property(e => e.CodigoPais).IsRequired().HasMaxLength(4).IsUnicode(false);
        b.Property(e => e.Pais).IsRequired().HasMaxLength(60);

        b.Property(e => e.Observacoes).HasMaxLength(global::Lone.Domain.Enderecos.RegrasEndereco.TamanhoMaximoObservacoes);
        b.HasOne<TipoEndereco>().WithMany().HasForeignKey(e => e.TipoEnderecoId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(e => e.TipoEnderecoId);
        b.HasIndex(e => new { e.PessoaId, e.Ordem });

        // Município da tabela do IBGE (relatórios, filtros e NF-e por município).
        b.HasOne<Municipio>().WithMany().HasForeignKey(e => e.MunicipioId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class TipoEnderecoConfiguration : IEntityTypeConfiguration<TipoEndereco>
{
    public void Configure(EntityTypeBuilder<TipoEndereco> b)
    {
        b.ToTable("TiposEndereco");
        b.HasKey(t => t.Id);
        b.Property(t => t.Nome).IsRequired().HasMaxLength(TipoEndereco.TamanhoMaximoNome).UseCollation(EtiquetaConfiguration.CollationNome);
        b.HasIndex(t => t.Nome).IsUnique();

        // Tipos iniciais (o usuário pode criar outros). Ids fixos: os mesmos em todo banco do Lone.
        var criacao = new DateTime(2026, 9, 25, 0, 0, 0, DateTimeKind.Utc);
        b.HasData(global::Lone.Domain.Enderecos.TiposEnderecoIniciais.Todos.Select(t => new TipoEndereco
        {
            Id = t.Id,
            Nome = t.Nome,
            Ordem = t.Ordem,
            Ativo = true,
            CriadoEm = criacao
        }).ToArray());
    }
}
