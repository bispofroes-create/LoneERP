using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lone.Infrastructure.Persistencia.Configuracoes;

public class PessoaConfiguration : IEntityTypeConfiguration<Pessoa>
{
    public const string SequenciaCodigo = "SeqPessoaCodigo";
    public const string ColunaProfissaoAntiga = "Profissao";

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
        b.Property(p => p.SituacaoMotivo).HasMaxLength(Pessoa.TamanhoMaximoMotivo);
        b.HasIndex(p => p.Situacao);

        b.Property(p => p.Nome).IsRequired().HasMaxLength(150);
        b.Property(p => p.NomeSocial).HasMaxLength(150);
        b.Property(p => p.NomeExibicao).HasMaxLength(80);
        b.Property(p => p.Apelido).HasMaxLength(60);
        b.Property(p => p.DocumentoPrincipal).HasMaxLength(20).IsUnicode(false);
        b.Property(p => p.Observacoes).HasMaxLength(2000);

        // Dados pessoais (PF)
        b.Property(p => p.Sexo).HasConversion<byte>();
        b.Property(p => p.IdentidadeGenero).HasConversion<byte>();
        b.Property(p => p.CorRaca).HasConversion<byte>();
        b.Property(p => p.EstadoCivil).HasConversion<byte>();
        b.Property(p => p.Escolaridade).HasConversion<byte>();
        b.Property(p => p.Nacionalidade).HasMaxLength(60);
        b.HasOne<Municipio>().WithMany().HasForeignKey(p => p.NaturalidadeMunicipioId).OnDelete(DeleteBehavior.Restrict);
        b.Property(p => p.NomeMae).HasMaxLength(150);
        b.Property(p => p.NomePai).HasMaxLength(150);
        // Texto livre de antes do cadastro de profissões: fica só como cópia do dado original (não é mais usado).
        b.Property<string?>(ColunaProfissaoAntiga).HasMaxLength(80);
        b.HasOne<Profissao>().WithMany().HasForeignKey(p => p.ProfissaoId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(p => p.ProfissaoId);

        // Dados da empresa (PJ)
        b.Property(p => p.Porte).HasMaxLength(60);
        b.Property(p => p.CapitalSocial).HasPrecision(18, 2);

        // Relacionamento comercial
        b.Property(p => p.OrigemCadastro).HasMaxLength(60);
        b.HasIndex(p => p.DataNascimento); // indicador de faixa etária

        // CPF e raiz de CNPJ não se repetem. Estrangeiros ficam de fora (documentos de países diferentes podem coincidir).
        b.HasIndex(p => new { p.Natureza, p.DocumentoPrincipal })
            .IsUnique()
            .HasFilter($"[DocumentoPrincipal] IS NOT NULL AND [Natureza] <> {(byte)NaturezaPessoa.Estrangeiro}");
        b.HasIndex(p => p.Nome);
        b.HasIndex(p => p.CriadoEm); // filtro avançado: cadastrados no período

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
        b.HasMany(p => p.Socios).WithOne().HasForeignKey(x => x.PessoaId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(p => p.Consentimentos).WithOne().HasForeignKey(x => x.PessoaId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(p => p.Etiquetas).WithOne().HasForeignKey(x => x.PessoaId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(p => p.ValoresPersonalizados).WithOne().HasForeignKey(x => x.PessoaId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(p => p.ValoresDocumentos).WithOne().HasForeignKey(x => x.PessoaId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(p => p.Vinculos).WithOne().HasForeignKey(x => x.PessoaId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(p => p.ExcecoesComerciais).WithOne().HasForeignKey(x => x.PessoaId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(p => p.Carteira).WithOne().HasForeignKey(x => x.PessoaId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(p => p.HistoricoFiscal).WithOne().HasForeignKey(x => x.PessoaId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(p => p.Cnaes).WithOne().HasForeignKey(x => x.PessoaId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(p => p.Lotacoes).WithOne().HasForeignKey(x => x.PessoaId).OnDelete(DeleteBehavior.Cascade);
    }
}
