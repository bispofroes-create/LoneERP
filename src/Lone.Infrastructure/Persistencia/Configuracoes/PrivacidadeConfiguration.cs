using Lone.Domain.Entidades;
using Lone.Domain.Privacidade;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lone.Infrastructure.Persistencia.Configuracoes;

/// <summary>
/// Cadastro de finalidades de tratamento (mesmo padrão de FinalidadesEndereco): código único e imutável, as de sistema
/// com Ids estáveis e nunca inativas (CHECK; regra repetida na aplicação: RegrasFinalidadeTratamento).
/// </summary>
public class FinalidadeTratamentoConfiguration : IEntityTypeConfiguration<FinalidadeTratamento>
{
    public void Configure(EntityTypeBuilder<FinalidadeTratamento> b)
    {
        b.ToTable("FinalidadesTratamento", t => t.HasCheckConstraint(SqlMigracaoPrivacidade.CheckSistemaAtiva, "[DoSistema] = 0 OR [Ativo] = 1"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Codigo).IsRequired().HasMaxLength(FinalidadeTratamento.TamanhoMaximoCodigo).IsUnicode(false);
        b.Property(x => x.Nome).IsRequired().HasMaxLength(FinalidadeTratamento.TamanhoMaximoNome).UseCollation(EtiquetaConfiguration.CollationNome);
        b.Property(x => x.Descricao).HasMaxLength(FinalidadeTratamento.TamanhoMaximoDescricao);
        b.Property(x => x.BaseLegal).HasConversion<byte>();
        b.Property(x => x.ClassificacaoExigida).HasConversion<byte>();
        b.HasIndex(x => x.Codigo).IsUnique();
        b.HasIndex(x => x.Nome).IsUnique();

        var criacao = new DateTime(2026, 9, 26, 0, 0, 0, DateTimeKind.Utc);
        b.HasData(FinalidadesTratamentoIniciais.Todas.Select(f => new FinalidadeTratamento
        {
            Id = f.Id, Codigo = f.Codigo, Nome = f.Nome, Descricao = f.Descricao, BaseLegal = f.BaseLegal,
            ClassificacaoExigida = f.ClassificacaoExigida, SomenteHistorico = f.SomenteHistorico, Ordem = f.Ordem,
            DoSistema = true, Ativo = true, CriadoEm = criacao
        }).ToArray());
    }
}

/// <summary>
/// Consentimento em períodos. O banco garante no máximo UM período em vigor por pessoa + finalidade + canal (índice
/// único filtrado em Concedido = 1; canal nulo conta como um valor: um "qualquer canal" em vigor) e que período
/// revogado não fica em vigor (CHECK). Nada é apagado (FK para a finalidade: Restrict).
/// </summary>
public class PessoaConsentimentoConfiguration : IEntityTypeConfiguration<PessoaConsentimento>
{
    public void Configure(EntityTypeBuilder<PessoaConsentimento> b)
    {
        b.ToTable("PessoaConsentimentos", t => t.HasCheckConstraint(SqlMigracaoPrivacidade.CheckRevogadoForaDeVigor, "[Concedido] = 0 OR [RevogadoEm] IS NULL"));
        b.HasKey(c => c.Id);
        b.Property(c => c.Canal).HasConversion<byte>();
        b.Property(c => c.ConcedidoPor).HasMaxLength(PessoaConsentimento.TamanhoMaximoUsuario);
        b.Property(c => c.RevogadoPor).HasMaxLength(PessoaConsentimento.TamanhoMaximoUsuario);
        b.Property(c => c.Motivo).HasMaxLength(PessoaConsentimento.TamanhoMaximoMotivo);
        b.Property(c => c.MotivoRevogacao).HasMaxLength(PessoaConsentimento.TamanhoMaximoMotivo);
        b.Property(c => c.VersaoTermo).HasMaxLength(PessoaConsentimento.TamanhoMaximoTexto);
        b.Property(c => c.Origem).HasMaxLength(PessoaConsentimento.TamanhoMaximoTexto);

        b.HasOne<FinalidadeTratamento>().WithMany().HasForeignKey(c => c.FinalidadeId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(c => new { c.PessoaId, c.FinalidadeId, c.Canal })
            .IsUnique()
            .HasFilter("[Concedido] = 1")
            .HasDatabaseName(SqlMigracaoPrivacidade.IndiceEmVigor);
        b.HasIndex(c => c.FinalidadeId);
    }
}
