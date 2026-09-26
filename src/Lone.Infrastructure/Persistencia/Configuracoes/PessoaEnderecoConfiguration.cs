using Lone.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lone.Infrastructure.Persistencia.Configuracoes;

public class PessoaEnderecoConfiguration : IEntityTypeConfiguration<PessoaEndereco>
{
    public void Configure(EntityTypeBuilder<PessoaEndereco> b)
    {
        // CHECK: consolidado fica inativo e aponta para outro endereço. Gatilho: endereço inativo não fica principal
        // de nenhuma finalidade (o CHECK não cruza tabelas). O EF precisa saber do gatilho (não usa OUTPUT puro).
        b.ToTable("PessoaEnderecos", t =>
        {
            t.HasCheckConstraint(SqlMigracaoFinalidadesEndereco.CheckConsolidado,
                "[MescladoEmId] IS NULL OR ([Ativo] = 0 AND [MescladoEmId] <> [Id])");
            t.HasTrigger(SqlMigracaoFinalidadesEndereco.GatilhoEnderecoInativo);
        });
        b.HasKey(e => e.Id);

        b.Property(e => e.Descricao).HasMaxLength(60);
        b.Property(e => e.Finalidades).HasConversion<short>();
        b.Property(e => e.RevisaoMigracao).HasConversion<short>();
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
        b.HasIndex(e => new { e.Uf, e.MunicipioId }); // filtro avançado por UF / município

        // Município da tabela do IBGE (relatórios, filtros e NF-e por município).
        b.HasOne<Municipio>().WithMany().HasForeignKey(e => e.MunicipioId).OnDelete(DeleteBehavior.Restrict);

        // (Id, PessoaId) único: alvo da FK composta das finalidades (relação e endereço sempre da mesma pessoa).
        b.HasAlternateKey(e => new { e.Id, e.PessoaId });

        // Duplicado consolidado em outro endereço da MESMA pessoa (o registro fica, inativo): FK composta para a chave
        // alternativa (Id, PessoaId) — o banco recusa apontar para endereço de outra pessoa. Com MescladoEmId nulo a FK
        // não se aplica (SQL Server não confere FK composta com coluna nula).
        b.HasOne<PessoaEndereco>().WithMany()
            .HasForeignKey(e => new { e.MescladoEmId, e.PessoaId })
            .HasPrincipalKey(e => new { e.Id, e.PessoaId })
            .OnDelete(DeleteBehavior.NoAction);
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

/// <summary>Cadastro de finalidades de endereço (as iniciais com Ids estáveis; código único e imutável).</summary>
public class FinalidadeEnderecoCadastroConfiguration : IEntityTypeConfiguration<FinalidadeEnderecoCadastro>
{
    public void Configure(EntityTypeBuilder<FinalidadeEnderecoCadastro> b)
    {
        // CHECK: finalidade de sistema nunca inativa. Gatilho: finalidade de sistema não muda de código, não deixa de ser
        // de sistema e não é excluída (regra repetida na aplicação: RegrasFinalidadeEnderecoCadastro).
        b.ToTable("FinalidadesEndereco", t =>
        {
            t.HasCheckConstraint(SqlMigracaoFinalidadesEndereco.CheckSistemaAtiva, "[DoSistema] = 0 OR [Ativo] = 1");
            t.HasTrigger(SqlMigracaoFinalidadesEndereco.GatilhoFinalidadeSistema);
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Codigo).IsRequired().HasMaxLength(FinalidadeEnderecoCadastro.TamanhoMaximoCodigo).IsUnicode(false);
        b.Property(x => x.Nome).IsRequired().HasMaxLength(FinalidadeEnderecoCadastro.TamanhoMaximoNome)
            .UseCollation(EtiquetaConfiguration.CollationNome);
        b.HasIndex(x => x.Codigo).IsUnique();
        b.HasIndex(x => x.Nome).IsUnique();

        var criacao = new DateTime(2026, 9, 26, 0, 0, 0, DateTimeKind.Utc);
        b.HasData(global::Lone.Domain.Enderecos.FinalidadesEnderecoIniciais.Todas.Select(f => new FinalidadeEnderecoCadastro
        {
            Id = f.Id, Codigo = f.Codigo, Nome = f.Nome, Ordem = f.Ordem, DoSistema = true, Ativo = true, CriadoEm = criacao
        }).ToArray());
    }
}

/// <summary>
/// Endereço × finalidade. O banco garante:
/// - a relação e o endereço são da mesma pessoa (FK composta para a chave alternativa (Id, PessoaId) do endereço);
/// - uma relação ATIVA por endereço + finalidade (índice único filtrado; retirar e voltar reativa a mesma linha);
/// - um principal por pessoa + finalidade (índice único filtrado em Principal = 1);
/// - relação inativa nunca é principal (CHECK);
/// - endereço inativo nunca é principal (gatilhos nas duas tabelas: o CHECK não cruza tabelas).
/// Os nomes dos índices são fixos: a gravação traduz a violação de cada um numa mensagem de conflito (409).
/// </summary>
public class PessoaEnderecoFinalidadeConfiguration : IEntityTypeConfiguration<PessoaEnderecoFinalidade>
{
    public void Configure(EntityTypeBuilder<PessoaEnderecoFinalidade> b)
    {
        b.ToTable("PessoaEnderecoFinalidades", t =>
        {
            t.HasCheckConstraint("CK_PessoaEnderecoFinalidades_PrincipalAtivo", "[Principal] = 0 OR [Ativo] = 1");
            t.HasTrigger(SqlMigracaoFinalidadesEndereco.GatilhoPrincipalEnderecoAtivo);
        });
        b.HasKey(x => x.Id);
        b.HasOne<PessoaEndereco>().WithMany()
            .HasForeignKey(x => new { x.PessoaEnderecoId, x.PessoaId })
            .HasPrincipalKey(e => new { e.Id, e.PessoaId })
            .OnDelete(DeleteBehavior.NoAction);
        b.HasOne<FinalidadeEnderecoCadastro>().WithMany().HasForeignKey(x => x.FinalidadeId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.PessoaEnderecoId, x.FinalidadeId }).IsUnique().HasFilter("[Ativo] = 1")
            .HasDatabaseName(SqlMigracaoFinalidadesEndereco.IndiceFinalidadeAtiva);
        b.HasIndex(x => new { x.PessoaId, x.FinalidadeId }).IsUnique().HasFilter("[Principal] = 1")
            .HasDatabaseName(SqlMigracaoFinalidadesEndereco.IndicePrincipal);
    }
}
