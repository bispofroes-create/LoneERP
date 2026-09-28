using Lone.Domain.Comercial;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lone.Infrastructure.Persistencia.Configuracoes;

public class CondicaoPagamentoConfiguration : IEntityTypeConfiguration<CondicaoPagamento>
{
    public void Configure(EntityTypeBuilder<CondicaoPagamento> b)
    {
        b.ToTable("CondicoesPagamento");
        b.HasKey(x => x.Id);
        b.Property(x => x.Nome).IsRequired().HasMaxLength(CondicaoPagamento.TamanhoMaximoNome).UseCollation(EtiquetaConfiguration.CollationNome);
        b.HasIndex(x => x.Nome).IsUnique();
        b.Property(x => x.Parcelas).IsRequired().HasMaxLength(250).IsUnicode(false);
        b.Property(x => x.AcrescimoPercentual).HasPrecision(7, 4);
    }
}

public class PerfilComercialConfiguration : IEntityTypeConfiguration<PerfilComercial>
{
    public void Configure(EntityTypeBuilder<PerfilComercial> b)
    {
        b.ToTable("PerfisComerciais");
        b.HasKey(x => x.Id);
        b.Property(x => x.Nome).IsRequired().HasMaxLength(PerfilComercial.TamanhoMaximoNome).UseCollation(EtiquetaConfiguration.CollationNome);
        b.HasIndex(x => x.Nome).IsUnique();
        b.Property(x => x.LimiteCredito).HasPrecision(15, 2);
        b.Property(x => x.DescontoMaximo).HasPrecision(5, 2);
        b.HasOne<CondicaoPagamento>().WithMany().HasForeignKey(x => x.CondicaoPagamentoId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class TipoCarteiraConfiguration : IEntityTypeConfiguration<TipoCarteira>
{
    public void Configure(EntityTypeBuilder<TipoCarteira> b)
    {
        b.ToTable("TiposCarteira", t => t.HasCheckConstraint(SqlMigracaoCarteira.CheckPolitica, SqlMigracaoCarteira.RegraPolitica));
        b.HasKey(x => x.Id);
        b.Property(x => x.Nome).IsRequired().HasMaxLength(TipoCarteira.TamanhoMaximoNome).UseCollation(EtiquetaConfiguration.CollationNome);
        b.HasIndex(x => x.Nome).IsUnique();
        b.HasIndex(x => x.ResponsavelDaConta).IsUnique().HasFilter("[ResponsavelDaConta] = 1"); // um só responsável da conta
        b.Property(x => x.TipoCredito).HasConversion<byte>();
        b.Property(x => x.PercentualPadrao).HasPrecision(5, 2);
        b.HasMany(x => x.Classificacoes).WithOne().HasForeignKey(x => x.TipoCarteiraId).OnDelete(DeleteBehavior.Restrict);

        // Papéis iniciais (Ids fixos) com a política inicial (TiposCarteiraIniciais).
        var criacao = new DateTime(2026, 9, 25, 0, 0, 0, DateTimeKind.Utc);
        b.HasData(TiposCarteiraIniciais.Todos.Select(t => new TipoCarteira
        {
            Id = t.Id, Nome = t.Nome, Ordem = t.Ordem, ResponsavelDaConta = t.ResponsavelDaConta, LimitePorVez = t.LimitePorVez,
            TipoCredito = t.TipoCredito, ContaParaMetas = t.ContaParaMetas, Ativo = true, CriadoEm = criacao
        }).ToArray());
    }
}

/// <summary>Quem pode ocupar cada papel comercial (Motor Comercial, Fase 1b). Nunca apagada: desmarcar desativa.</summary>
public class TipoCarteiraClassificacaoConfiguration : IEntityTypeConfiguration<TipoCarteiraClassificacao>
{
    public void Configure(EntityTypeBuilder<TipoCarteiraClassificacao> b)
    {
        b.ToTable("TiposCarteiraClassificacoes");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.TipoCarteiraId, x.PapelId }).IsUnique();
        b.HasOne<Papel>().WithMany().HasForeignKey(x => x.PapelId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ExcecaoComercialConfiguration : IEntityTypeConfiguration<ExcecaoComercial>
{
    public void Configure(EntityTypeBuilder<ExcecaoComercial> b)
    {
        b.ToTable("ExcecoesComerciais");
        b.HasKey(x => x.Id);
        b.Property(x => x.LimiteCredito).HasPrecision(15, 2);
        b.Property(x => x.DescontoMaximo).HasPrecision(5, 2);
        b.Property(x => x.Motivo).HasMaxLength(250);
        b.HasOne<CondicaoPagamento>().WithMany().HasForeignKey(x => x.CondicaoPagamentoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Pessoa>().WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(x => new { x.PessoaId, x.InicioEm });
    }
}

public class CarteiraClienteConfiguration : IEntityTypeConfiguration<CarteiraCliente>
{
    public void Configure(EntityTypeBuilder<CarteiraCliente> b)
    {
        // Gatilho da política dos papéis ("um por vez"/exclusivo e limite; SqlMigracaoCarteira): declarado para o EF não usar OUTPUT
        // nas gravações desta tabela, que o SQL Server recusa em tabela com gatilho.
        b.ToTable("CarteiraClientes", t => t.HasTrigger(SqlMigracaoCarteira.Gatilho));
        b.HasKey(x => x.Id);
        b.Property(x => x.Observacao).HasMaxLength(250);
        b.Property(x => x.PercentualCredito).HasPrecision(5, 2);
        b.Property(x => x.Origem).HasConversion<byte>();
        b.HasOne<TipoCarteira>().WithMany().HasForeignKey(x => x.TipoCarteiraId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Pessoa>().WithMany().HasForeignKey(x => x.VendedorId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<Pessoa>().WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<TransferenciaCarteira>().WithMany().HasForeignKey(x => x.TransferenciaId).OnDelete(DeleteBehavior.Restrict);

        // "Carteira do vendedor" (quem ele atende hoje, ou numa data) e o histórico do cliente.
        b.HasIndex(x => new { x.VendedorId, x.Ativo, x.FimEm });
        b.HasIndex(x => new { x.PessoaId, x.InicioEm });
    }
}

/// <summary>Tipos de ausência (Motor Comercial, Fase 1c), com os iniciais de Ids fixos.</summary>
public class TipoAusenciaConfiguration : IEntityTypeConfiguration<TipoAusencia>
{
    public void Configure(EntityTypeBuilder<TipoAusencia> b)
    {
        b.ToTable("TiposAusencia");
        b.HasKey(x => x.Id);
        b.Property(x => x.Nome).IsRequired().HasMaxLength(TipoAusencia.TamanhoMaximoNome).UseCollation(EtiquetaConfiguration.CollationNome);
        b.HasIndex(x => x.Nome).IsUnique();

        var criacao = new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc);
        b.HasData(TiposAusenciaIniciais.Todos.Select(t => new TipoAusencia
        {
            Id = t.Id, Nome = t.Nome, Ordem = t.Ordem, Ativo = true, CriadoEm = criacao
        }).ToArray());
    }
}

/// <summary>Parâmetros do módulo Comercial: um registro só, criado com os padrões (aviso de 30 dias; crédito do titular).</summary>
public class ParametrosComerciaisConfiguration : IEntityTypeConfiguration<ParametrosComerciais>
{
    public void Configure(EntityTypeBuilder<ParametrosComerciais> b)
    {
        b.ToTable("ParametrosComerciais", t => t.HasCheckConstraint("CK_ParametrosComerciais_Unico",
            $"[Id] = '{ParametrosComerciais.IdUnico:D}'"));
        b.HasKey(x => x.Id);
        b.Property(x => x.CreditoNaAusencia).HasConversion<byte>();
        b.Property(x => x.PercentualSubstitutoPadrao).HasPrecision(5, 2);
        b.HasData(new ParametrosComerciais
        {
            Id = ParametrosComerciais.IdUnico, DiasAvisoFimVinculo = 30, DiasRetroativosMaximo = 30, CreditoNaAusencia = RegraCreditoAusencia.Titular,
            CriadoEm = new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}

/// <summary>Coberturas de ausência. Nunca apagadas (canceladas ou encerradas pelo fim).</summary>
public class CoberturaComercialConfiguration : IEntityTypeConfiguration<CoberturaComercial>
{
    public void Configure(EntityTypeBuilder<CoberturaComercial> b)
    {
        b.ToTable("CoberturasComerciais", t =>
        {
            t.HasCheckConstraint("CK_CoberturasComerciais_Periodo", "[FimEm] >= [InicioEm]");
            t.HasCheckConstraint("CK_CoberturasComerciais_QuemCobre",
                "([SubstitutoId] IS NULL AND [EquipeSubstitutaId] IS NOT NULL) OR ([SubstitutoId] IS NOT NULL AND [EquipeSubstitutaId] IS NULL)");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.RegraCredito).HasConversion<byte>();
        b.Property(x => x.PercentualSubstituto).HasPrecision(5, 2);
        b.Property(x => x.Observacao).HasMaxLength(CoberturaComercial.TamanhoMaximoTexto);
        b.Property(x => x.MotivoCancelamento).HasMaxLength(CoberturaComercial.TamanhoMaximoTexto);
        b.HasOne<Pessoa>().WithMany().HasForeignKey(x => x.TitularId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<Pessoa>().WithMany().HasForeignKey(x => x.SubstitutoId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<Pessoa>().WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<Equipe>().WithMany().HasForeignKey(x => x.EquipeSubstitutaId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<TipoAusencia>().WithMany().HasForeignKey(x => x.TipoAusenciaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<TipoCarteira>().WithMany().HasForeignKey(x => x.TipoCarteiraId).OnDelete(DeleteBehavior.Restrict);

        // Por titular (validação de sobreposição, aviso na ficha) e por período (lista e "vigentes hoje").
        b.HasIndex(x => new { x.TitularId, x.InicioEm });
        b.HasIndex(x => new { x.FimEm, x.Cancelada });
    }
}

/// <summary>
/// Transferências de carteira (Motor Comercial, Fase 1d). Nunca apagadas nem desfeitas; número legível único por ano.
/// </summary>
public class TransferenciaCarteiraConfiguration : IEntityTypeConfiguration<TransferenciaCarteira>
{
    public void Configure(EntityTypeBuilder<TransferenciaCarteira> b)
    {
        b.ToTable("TransferenciasCarteira");
        b.HasKey(x => x.Id);
        b.Ignore(x => x.Numero);
        b.HasIndex(x => new { x.Ano, x.Sequencia }).IsUnique();
        b.Property(x => x.Motivo).IsRequired().HasMaxLength(TransferenciaCarteira.TamanhoMaximoTexto);
        b.Property(x => x.Observacao).HasMaxLength(TransferenciaCarteira.TamanhoMaximoTexto);
        b.Property(x => x.Usuario).IsRequired().HasMaxLength(TransferenciaCarteira.TamanhoMaximoUsuario);
        b.HasOne<Pessoa>().WithMany().HasForeignKey(x => x.OrigemId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<Pessoa>().WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<TipoCarteira>().WithMany().HasForeignKey(x => x.TipoCarteiraId).OnDelete(DeleteBehavior.Restrict);

        // "Transferências de João" (origem) na ordem do efeito.
        b.HasIndex(x => new { x.OrigemId, x.EfeitoEm });
    }
}

/// <summary>Resultado por vínculo de cada transferência: gravado uma vez, nunca alterado.</summary>
public class TransferenciaCarteiraItemConfiguration : IEntityTypeConfiguration<TransferenciaCarteiraItem>
{
    public void Configure(EntityTypeBuilder<TransferenciaCarteiraItem> b)
    {
        b.ToTable("TransferenciaCarteiraItens");
        b.HasKey(x => x.Id);
        b.Property(x => x.Resultado).HasConversion<byte>();
        b.Property(x => x.Motivo).HasMaxLength(TransferenciaCarteiraItem.TamanhoMaximoMotivo);
        b.HasOne<TransferenciaCarteira>().WithMany().HasForeignKey(x => x.TransferenciaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Pessoa>().WithMany().HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<Pessoa>().WithMany().HasForeignKey(x => x.DestinoId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<CarteiraCliente>().WithMany().HasForeignKey(x => x.VinculoOrigemId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<CarteiraCliente>().WithMany().HasForeignKey(x => x.VinculoNovoId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<TipoCarteira>().WithMany().HasForeignKey(x => x.TipoCarteiraId).OnDelete(DeleteBehavior.Restrict);

        // Resultado de uma transferência, por cliente; e "transferências deste cliente".
        b.HasIndex(x => new { x.TransferenciaId, x.ClienteId });
        b.HasIndex(x => x.ClienteId);
    }
}
