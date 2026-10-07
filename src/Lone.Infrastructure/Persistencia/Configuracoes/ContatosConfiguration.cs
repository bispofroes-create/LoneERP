using Lone.Domain.Contatos;
using Lone.Domain.Documentos;
using Lone.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lone.Infrastructure.Persistencia.Configuracoes;

public class MeioContatoConfiguration : IEntityTypeConfiguration<MeioContato>
{
    public void Configure(EntityTypeBuilder<MeioContato> b)
    {
        b.ToTable("PessoaMeiosContato");
        b.HasKey(m => m.Id);

        b.Property(m => m.Tipo).HasConversion<byte>();
        b.Property(m => m.Valor).IsRequired().HasMaxLength(150);
        b.Property(m => m.Descricao).HasMaxLength(80);
        b.Property(m => m.Ramal).HasMaxLength(RegrasMeioContato.TamanhoMaximoRamal).IsUnicode(false);
        b.Property(m => m.Finalidades).HasConversion<short>();

        // DDD calculado pelo banco a partir do número (telefone brasileiro com DDD), só para filtrar por DDD com índice.
        // O número continua completo em Valor: não há dois campos para manter em sincronia.
        b.Property<string?>(ColunaDdd)
            .HasMaxLength(2).IsUnicode(false)
            .HasComputedColumnSql(
                "CAST(CASE WHEN [Tipo] IN (0, 1, 2) AND LEN([Valor]) IN (10, 11) AND LEFT([Valor], 1) NOT IN ('+', '0') " +
                "THEN LEFT([Valor], 2) END AS varchar(2))", stored: true);

        b.HasOne<TipoMeioContato>().WithMany().HasForeignKey(m => m.TipoMeioContatoId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(m => m.PessoaId);
        b.HasIndex(m => m.Valor); // busca por telefone/e-mail e aviso de duplicidade
        b.HasIndex(ColunaDdd);
        b.HasIndex(m => m.TipoMeioContatoId);
    }

    public const string ColunaDdd = "Ddd";
}

public class TipoMeioContatoConfiguration : IEntityTypeConfiguration<TipoMeioContato>
{
    public void Configure(EntityTypeBuilder<TipoMeioContato> b)
    {
        b.ToTable("TiposMeioContato");
        b.HasKey(t => t.Id);
        b.Property(t => t.Nome).IsRequired().HasMaxLength(TipoMeioContato.TamanhoMaximoNome).UseCollation(EtiquetaConfiguration.CollationNome);
        b.Property(t => t.Categoria).HasConversion<byte>();
        b.HasIndex(t => new { t.Categoria, t.Nome }).IsUnique();

        // Tipos iniciais (o usuário pode criar outros). Ids fixos: os mesmos em todo banco do Lone.
        var criacao = new DateTime(2026, 9, 25, 0, 0, 0, DateTimeKind.Utc);
        b.HasData(TiposMeioContatoIniciais.Todos.Select(t => new TipoMeioContato
        {
            Id = t.Id,
            Categoria = t.Categoria,
            Nome = t.Nome,
            Ordem = t.Ordem,
            Ativo = true,
            CriadoEm = criacao
        }).ToArray());
    }
}

public class ContatoConfiguration : IEntityTypeConfiguration<Contato>
{
    public void Configure(EntityTypeBuilder<Contato> b)
    {
        b.ToTable("PessoaContatos");
        b.HasKey(c => c.Id);

        b.Property(c => c.Nome).IsRequired().HasMaxLength(100);
        b.Property(c => c.Cargo).HasMaxLength(60);
        b.Property(c => c.Departamento).HasMaxLength(60);
        b.Property(c => c.Telefone).HasMaxLength(20).IsUnicode(false);
        b.Property(c => c.Celular).HasMaxLength(20).IsUnicode(false);
        b.Property(c => c.Email).HasMaxLength(150);
        b.Property(c => c.Observacoes).HasMaxLength(500);

        // P0: removido depois de gravado fica inativo (nunca apagado pela ficha). Padrão 1 no banco: os registros que já
        // existem nascem ativos sem UPDATE. Sentinela "true": o EF só manda o valor quando ele é falso (o padrão do banco
        // já é verdadeiro), então um contato gravado como inativo nunca vira ativo pelo padrão.
        b.Property(c => c.Ativo).HasDefaultValue(true).HasSentinel(true);

        b.HasOne<Pessoa>().WithMany().HasForeignKey(c => c.PessoaVinculadaId).OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(c => c.PessoaId);
        b.HasIndex(c => c.Telefone);
        b.HasIndex(c => c.Celular);
        b.HasIndex(c => c.Email);
    }
}

public class PessoaDocumentoConfiguration : IEntityTypeConfiguration<PessoaDocumento>
{
    public void Configure(EntityTypeBuilder<PessoaDocumento> b)
    {
        b.ToTable("PessoaDocumentos");
        b.HasKey(d => d.Id);

        b.Property(d => d.Tipo).HasConversion<byte>();
        b.Property(d => d.Numero).IsRequired().HasMaxLength(30);
        b.Property(d => d.OrgaoEmissor).HasMaxLength(20);
        b.Property(d => d.Uf).HasMaxLength(2).IsFixedLength().IsUnicode(false);
        b.Property(d => d.Observacoes).HasMaxLength(250);

        b.HasIndex(d => d.PessoaId);

        // Tipo do cadastro; índice com a validade para a consulta de vencidos / a vencer por tipo.
        b.HasOne<TipoDocumentoCadastro>().WithMany().HasForeignKey(d => d.TipoDocumentoId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(d => new { d.TipoDocumentoId, d.ValidoAte }).HasFilter("[Ativo] = 1");
        b.HasIndex(d => new { d.ValidoAte, d.PessoaId }).HasFilter("[Ativo] = 1 AND [ValidoAte] IS NOT NULL"); // filtro avançado: vencidos / a vencer

        // P1-8: número comparável (só A–Z e 0–9) e a chave da unicidade entre pessoas. O índice do número não é filtrado
        // porque a busca rápida também acha documentos inativos (como sempre achou); a conferência de repetidos filtra Ativo.
        b.Property(d => d.NumeroNormalizado).IsRequired().HasMaxLength(NumeroDocumento.TamanhoMaximo).IsUnicode(false);
        b.Property(d => d.ChaveUnicidade).HasMaxLength(NumeroDocumento.TamanhoChave).IsUnicode(false);
        b.HasIndex(d => new { d.NumeroNormalizado, d.TipoDocumentoId });
        // Garantia no banco para os tipos que bloqueiam número repetido: só documentos ativos com chave entram.
        b.HasIndex(d => d.ChaveUnicidade).IsUnique().HasFilter("[ChaveUnicidade] IS NOT NULL AND [Ativo] = 1");
    }
}

public class AnexoDocumentoConfiguration : IEntityTypeConfiguration<AnexoDocumento>
{
    public void Configure(EntityTypeBuilder<AnexoDocumento> b)
    {
        b.ToTable("AnexosDocumento");
        b.HasKey(a => a.Id);
        b.Property(a => a.NomeArquivo).IsRequired().HasMaxLength(AnexoDocumento.TamanhoMaximoNome);
        b.Property(a => a.TipoConteudo).IsRequired().HasMaxLength(100).IsUnicode(false);
        b.Property(a => a.Hash).IsRequired().HasMaxLength(64).IsFixedLength().IsUnicode(false);
        b.Property(a => a.Caminho).IsRequired().HasMaxLength(260);
        b.Property(a => a.EnviadoPor).IsRequired().HasMaxLength(100);

        // Sem exclusão em cascata: nem pessoa nem documento são apagados, e o anexo nunca some junto.
        b.HasOne<Pessoa>().WithMany().HasForeignKey(a => a.PessoaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PessoaDocumento>().WithMany().HasForeignKey(a => a.PessoaDocumentoId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(a => a.PessoaId);
        b.HasIndex(a => a.PessoaDocumentoId);
    }
}

public class TipoDocumentoCadastroConfiguration : IEntityTypeConfiguration<TipoDocumentoCadastro>
{
    public void Configure(EntityTypeBuilder<TipoDocumentoCadastro> b)
    {
        b.ToTable("TiposDocumento");
        b.HasKey(t => t.Id);
        b.Property(t => t.Nome).IsRequired().HasMaxLength(TipoDocumentoCadastro.TamanhoMaximoNome).UseCollation(EtiquetaConfiguration.CollationNome);
        b.HasIndex(t => t.Nome).IsUnique();
        b.Property(t => t.TipoSistema).HasConversion<byte?>();
        b.HasIndex(t => t.TipoSistema).IsUnique().HasFilter("[TipoSistema] IS NOT NULL");

        // P1-8: regras do tipo. Na migração, os tipos do usuário recebem o comportamento de sempre (todos podem usar,
        // órgão e UF ocultos, emissão opcional, formato livre, sem verificar repetido); os de sistema, a semente.
        b.Property(t => t.UsoOrgaoEmissor).HasConversion<byte>();
        b.Property(t => t.UsoUf).HasConversion<byte>();
        b.Property(t => t.UsoEmissao).HasConversion<byte>();
        b.Property(t => t.FormatoNumero).HasConversion<byte>();
        b.Property(t => t.Unicidade).HasConversion<byte>();

        // Os cinco de sistema, ligados ao enum TipoDocumento. Ids fixos: os mesmos em todo banco do Lone.
        var criacao = new DateTime(2026, 9, 25, 0, 0, 0, DateTimeKind.Utc);
        b.HasData(TiposDocumentoSistema.Todos.Select(t => TiposDocumentoSistema.ComSemente(new TipoDocumentoCadastro
        {
            Id = t.Id,
            Nome = t.Nome,
            Ordem = t.Ordem,
            Ativo = true,
            TipoSistema = t.Tipo,
            ExigeValidade = t.ExigeValidade,
            DiasAvisoVencimento = TipoDocumentoCadastro.DiasAvisoPadrao,
            CriadoEm = criacao
        }, t.Tipo)).ToArray());
    }
}
