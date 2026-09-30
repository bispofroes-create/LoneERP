using Lone.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lone.Infrastructure.Persistencia.Configuracoes;

// Motor territorial (Fase 2b-1b; plano, seção P). Nada apaga em cascata (todas as chaves estrangeiras com Restrict). O
// banco garante o que não pode depender só da aplicação: regra, exceção, atribuição e mudança no mesmo mapa do território
// (FK composta (MapaId, TerritorioId) → Territorios(MapaId, Id), o padrão das posições); a cópia de Exclusivo amarrada a
// MapasTerritoriais(Id, Exclusivo) (FK criada pela migration: ver SqlMigracaoTerritorios.CriarChavesExclusivo); uma linha
// aberta onde só pode haver uma (índices únicos filtrados); períodos e valores coerentes (CHECK); e, por gatilho com
// READCOMMITTEDLOCK, versões da regra, exceções e atribuições sem sobreposição, regra publicada e item aplicado imutáveis.

/// <summary>Parâmetros do motor territorial (DN-08): um registro só, criado com 30 dias retroativos.</summary>
public class ParametrosTerritoriaisConfiguration : IEntityTypeConfiguration<ParametrosTerritoriais>
{
    public const string CheckUnico = "CK_ParametrosTerritoriais_Unico";
    public const string CheckDias = "CK_ParametrosTerritoriais_DiasRetroativos";

    public void Configure(EntityTypeBuilder<ParametrosTerritoriais> b)
    {
        b.ToTable("ParametrosTerritoriais", t =>
        {
            t.HasCheckConstraint(CheckUnico, $"[Id] = '{ParametrosTerritoriais.IdUnico:D}'");
            t.HasCheckConstraint(CheckDias, $"[DiasRetroativosMaximo] BETWEEN 0 AND {ParametrosTerritoriais.MaximoDiasRetroativos}");
        });
        b.HasKey(x => x.Id);
        b.HasData(new ParametrosTerritoriais
        {
            Id = ParametrosTerritoriais.IdUnico, DiasRetroativosMaximo = ParametrosTerritoriais.PadraoDiasRetroativos,
            CriadoEm = new DateTime(2026, 9, 29, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}

/// <summary>
/// Trava e versão do motor por mapa (DN-02): uma linha por mapa, criada com o mapa (e pela migration, para os que já
/// existiam). A aplicação exige a versão simulada com UPDATE ... WHERE Versao = @vista.
/// </summary>
public class MapaTerritorialMotorConfiguration : IEntityTypeConfiguration<MapaTerritorialMotor>
{
    public void Configure(EntityTypeBuilder<MapaTerritorialMotor> b)
    {
        b.ToTable("MapaTerritorialMotor");
        b.HasKey(x => x.MapaId);
        b.Property(x => x.Versao).IsRowVersion();
        b.HasOne<MapaTerritorial>().WithOne().HasForeignKey<MapaTerritorialMotor>(x => x.MapaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OperacaoTerritorial>().WithMany().HasForeignKey(x => x.UltimaOperacaoId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Versões da regra do território. Publicadas só por operação e imutáveis depois (gatilho 50073).</summary>
public class RegraTerritorioConfiguration : IEntityTypeConfiguration<RegraTerritorio>
{
    public const string CheckPeriodo = "CK_RegrasTerritorio_Periodo";
    public const string CheckPrioridade = "CK_RegrasTerritorio_Prioridade";
    public const string CheckNumero = "CK_RegrasTerritorio_Numero";
    public const string CheckGrupos = "CK_RegrasTerritorio_Grupos";
    public const string IndiceAberta = "UX_RegrasTerritorio_Aberta";

    public void Configure(EntityTypeBuilder<RegraTerritorio> b)
    {
        b.ToTable("RegrasTerritorio", t =>
        {
            t.HasCheckConstraint(CheckPeriodo, "[FimEm] IS NULL OR [FimEm] >= [InicioEm]");
            t.HasCheckConstraint(CheckPrioridade, "[Prioridade] IS NULL OR [Prioridade] >= 1");
            t.HasCheckConstraint(CheckNumero, "[Numero] >= 1");
            t.HasCheckConstraint(CheckGrupos, $"ISJSON([Grupos]) = 1 AND LEN([Grupos]) <= {RegraTerritorio.TamanhoMaximoGrupos}");
            t.HasTrigger(SqlMigracaoTerritorios.GatilhoRegras);
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Grupos).IsRequired(); // nvarchar(max): o limite fica no CHECK
        b.Property(x => x.Criterios).IsRequired().HasMaxLength(RegraTerritorio.TamanhoMaximoCriterios);
        b.Property(x => x.Versao).IsRowVersion();

        b.HasOne<Territorio>().WithMany().HasForeignKey(x => new { x.MapaId, x.TerritorioId }).HasPrincipalKey(t => new { t.MapaId, t.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OperacaoTerritorial>().WithMany().HasForeignKey(x => x.OperacaoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OperacaoTerritorialMudanca>().WithMany().HasForeignKey(x => x.OperacaoMudancaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OperacaoTerritorial>().WithMany().HasForeignKey(x => x.OperacaoEncerramentoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OperacaoTerritorial>().WithMany().HasForeignKey(x => x.OperacaoAnulacaoId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.TerritorioId, x.Numero }).IsUnique();
        // Uma versão aberta (sem fim) e válida por território; as anuladas não contam.
        b.HasIndex(x => x.TerritorioId).IsUnique().HasFilter("[FimEm] IS NULL AND [Ativo] = 1").HasDatabaseName(IndiceAberta);
        // "Regras do mapa em D" (simulação e aplicação leem o mapa inteiro).
        b.HasIndex(x => new { x.MapaId, x.TerritorioId, x.InicioEm });
    }
}

/// <summary>Exceções Fixar/Retirar (gatilho 50074; um Fixar aberto por cliente no mapa exclusivo, por índice).</summary>
public class ExcecaoTerritorioConfiguration : IEntityTypeConfiguration<ExcecaoTerritorio>
{
    public const string CheckPeriodo = "CK_ExcecoesTerritorio_Periodo";
    public const string CheckTipo = "CK_ExcecoesTerritorio_Tipo";
    public const string CheckOrigem = "CK_ExcecoesTerritorio_Origem";
    public const string CheckMotivo = "CK_ExcecoesTerritorio_Motivo";
    public const string IndiceFixarAberto = "UX_ExcecoesTerritorio_FixarAbertoNoExclusivo";

    public void Configure(EntityTypeBuilder<ExcecaoTerritorio> b)
    {
        b.ToTable("ExcecoesTerritorio", t =>
        {
            t.HasCheckConstraint(CheckPeriodo, "[FimEm] IS NULL OR [FimEm] >= [InicioEm]");
            t.HasCheckConstraint(CheckTipo, "[Tipo] IN (1, 2)");
            t.HasCheckConstraint(CheckOrigem, "[Origem] IN (1, 2, 3)");
            t.HasCheckConstraint(CheckMotivo, "LEN([Motivo]) > 0");
            t.HasTrigger(SqlMigracaoTerritorios.GatilhoExcecoes);
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Tipo).HasConversion<byte>();
        b.Property(x => x.Origem).HasConversion<byte>();
        b.Property(x => x.Motivo).IsRequired().HasMaxLength(ExcecaoTerritorio.TamanhoMaximoMotivo);
        b.Property(x => x.Versao).IsRowVersion();

        b.HasOne<Territorio>().WithMany().HasForeignKey(x => new { x.MapaId, x.TerritorioId }).HasPrincipalKey(t => new { t.MapaId, t.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Pessoa>().WithMany().HasForeignKey(x => x.PessoaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OperacaoTerritorial>().WithMany().HasForeignKey(x => x.OperacaoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OperacaoTerritorialMudanca>().WithMany().HasForeignKey(x => x.OperacaoMudancaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OperacaoTerritorial>().WithMany().HasForeignKey(x => x.OperacaoEncerramentoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OperacaoTerritorial>().WithMany().HasForeignKey(x => x.OperacaoAnulacaoId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.PessoaId, x.MapaId }).IsUnique()
            .HasFilter("[Tipo] = 1 AND [Exclusivo] = 1 AND [FimEm] IS NULL AND [Ativo] = 1").HasDatabaseName(IndiceFixarAberto);
        b.HasIndex(x => new { x.PessoaId, x.MapaId, x.InicioEm });
        b.HasIndex(x => new { x.MapaId, x.TerritorioId, x.InicioEm });
    }
}

/// <summary>Atribuições: o resultado gravado do motor (gatilho 50075; uma aberta por cliente no mapa exclusivo, por índice).</summary>
public class AtribuicaoTerritorioConfiguration : IEntityTypeConfiguration<AtribuicaoTerritorio>
{
    public const string CheckPeriodo = "CK_AtribuicoesTerritorio_Periodo";
    public const string CheckOrigem = "CK_AtribuicoesTerritorio_Origem";
    public const string IndiceAberta = "UX_AtribuicoesTerritorio_AbertaNoExclusivo";

    public void Configure(EntityTypeBuilder<AtribuicaoTerritorio> b)
    {
        b.ToTable("AtribuicoesTerritorio", t =>
        {
            t.HasCheckConstraint(CheckPeriodo, "[FimEm] IS NULL OR [FimEm] >= [InicioEm]");
            // Origem e fonte andam juntas: por regra cita a versão da regra; por exceção cita a exceção.
            t.HasCheckConstraint(CheckOrigem,
                "([Origem] = 1 AND [RegraId] IS NOT NULL AND [ExcecaoId] IS NULL) OR ([Origem] = 2 AND [ExcecaoId] IS NOT NULL AND [RegraId] IS NULL)");
            t.HasTrigger(SqlMigracaoTerritorios.GatilhoAtribuicoes);
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Origem).HasConversion<byte>();

        b.HasOne<Territorio>().WithMany().HasForeignKey(x => new { x.MapaId, x.TerritorioId }).HasPrincipalKey(t => new { t.MapaId, t.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Pessoa>().WithMany().HasForeignKey(x => x.PessoaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<RegraTerritorio>().WithMany().HasForeignKey(x => x.RegraId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ExcecaoTerritorio>().WithMany().HasForeignKey(x => x.ExcecaoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OperacaoTerritorial>().WithMany().HasForeignKey(x => x.OperacaoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OperacaoTerritorial>().WithMany().HasForeignKey(x => x.OperacaoEncerramentoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OperacaoTerritorial>().WithMany().HasForeignKey(x => x.OperacaoAnulacaoId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.PessoaId, x.MapaId }).IsUnique()
            .HasFilter("[FimEm] IS NULL AND [Ativo] = 1 AND [Exclusivo] = 1").HasDatabaseName(IndiceAberta);
        b.HasIndex(x => new { x.MapaId, x.TerritorioId, x.InicioEm });
        b.HasIndex(x => new { x.PessoaId, x.MapaId, x.InicioEm });
    }
}

/// <summary>Operações TE-: cabeçalho auditado, com as mudanças como filhas (FK composta: a mudança é do mapa da operação).</summary>
public class OperacaoTerritorialConfiguration : IEntityTypeConfiguration<OperacaoTerritorial>
{
    public const string CheckSituacao = "CK_OperacoesTerritoriais_Situacao";
    public const string CheckNumero = "CK_OperacoesTerritoriais_Numero";
    public const string CheckMotivo = "CK_OperacoesTerritoriais_Motivo";
    public const string CheckTransicoes = "CK_OperacoesTerritoriais_Transicoes";

    public void Configure(EntityTypeBuilder<OperacaoTerritorial> b)
    {
        b.ToTable("OperacoesTerritoriais", t =>
        {
            t.HasCheckConstraint(CheckSituacao, "[Situacao] BETWEEN 0 AND 4");
            t.HasCheckConstraint(CheckNumero, "[Ano] BETWEEN 2000 AND 9999 AND [Sequencia] >= 1");
            t.HasCheckConstraint(CheckMotivo, "LEN([Motivo]) > 0");
            // Cada situação final tem o seu "quem, quando e por quê" (LEN de nulo não passaria: por isso o IS NOT NULL).
            t.HasCheckConstraint(CheckTransicoes,
                "([Situacao] NOT IN (2, 4) OR ([AplicadaEm] IS NOT NULL AND [AplicadaPor] IS NOT NULL)) AND " +
                "([Situacao] <> 3 OR ([CanceladaEm] IS NOT NULL AND [CanceladaPor] IS NOT NULL AND [CanceladaMotivo] IS NOT NULL AND LEN([CanceladaMotivo]) > 0)) AND " +
                "([Situacao] <> 4 OR ([DesfeitaEm] IS NOT NULL AND [DesfeitaPor] IS NOT NULL AND [DesfeitaMotivo] IS NOT NULL AND LEN([DesfeitaMotivo]) > 0))");
        });
        b.HasKey(x => x.Id);
        b.Ignore(x => x.Numero);
        b.Ignore(x => x.Aberta);

        // (Id, MapaId) é chave alternativa: alvo da FK composta das mudanças (a mudança sempre do mapa da operação).
        b.HasAlternateKey(x => new { x.Id, x.MapaId });

        b.Property(x => x.Situacao).HasConversion<byte>();
        b.Property(x => x.Motivo).IsRequired().HasMaxLength(OperacaoTerritorial.TamanhoMaximoMotivo);
        b.Property(x => x.Observacao).HasMaxLength(OperacaoTerritorial.TamanhoMaximoObservacao);
        b.Property(x => x.CriadaPor).IsRequired().HasMaxLength(OperacaoTerritorial.TamanhoMaximoUsuario);
        b.Property(x => x.AplicadaPor).HasMaxLength(OperacaoTerritorial.TamanhoMaximoUsuario);
        b.Property(x => x.CanceladaPor).HasMaxLength(OperacaoTerritorial.TamanhoMaximoUsuario);
        b.Property(x => x.DesfeitaPor).HasMaxLength(OperacaoTerritorial.TamanhoMaximoUsuario);
        b.Property(x => x.CanceladaMotivo).HasMaxLength(OperacaoTerritorial.TamanhoMaximoMotivo);
        b.Property(x => x.DesfeitaMotivo).HasMaxLength(OperacaoTerritorial.TamanhoMaximoMotivo);

        b.HasOne<MapaTerritorial>().WithMany().HasForeignKey(x => x.MapaId).OnDelete(DeleteBehavior.Restrict);
        // Quem de cada transição pelo id (o nome do usuário pode mudar; a auditoria guarda o nome).
        b.HasOne<Usuario>().WithMany().HasForeignKey(x => x.CriadaPorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Usuario>().WithMany().HasForeignKey(x => x.AplicadaPorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Usuario>().WithMany().HasForeignKey(x => x.CanceladaPorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Usuario>().WithMany().HasForeignKey(x => x.DesfeitaPorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OperacaoTerritorialSimulacao>().WithMany().HasForeignKey(x => x.SimulacaoAtualId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OperacaoTerritorial>().WithMany().HasForeignKey(x => x.OperacaoAnteriorDoMapaId).OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Mudancas).WithOne().HasForeignKey(m => new { m.OperacaoId, m.MapaId }).HasPrincipalKey(o => new { o.Id, o.MapaId })
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.Ano, x.Sequencia }).IsUnique();
        b.HasIndex(x => new { x.MapaId, x.Situacao, x.EfeitoEm });
    }
}

/// <summary>Mudanças planejadas: tipo fechado, alvo e base com chaves reais, antes/depois em JSON validado.</summary>
public class OperacaoTerritorialMudancaConfiguration : IEntityTypeConfiguration<OperacaoTerritorialMudanca>
{
    public const string CheckTipo = "CK_OperacaoTerritorialMudancas_Tipo";
    public const string CheckOrdem = "CK_OperacaoTerritorialMudancas_Ordem";
    public const string CheckJson = "CK_OperacaoTerritorialMudancas_Json";

    public void Configure(EntityTypeBuilder<OperacaoTerritorialMudanca> b)
    {
        b.ToTable("OperacaoTerritorialMudancas", t =>
        {
            t.HasCheckConstraint(CheckTipo, "[Tipo] BETWEEN 1 AND 8");
            t.HasCheckConstraint(CheckOrdem, "[Ordem] >= 1");
            t.HasCheckConstraint(CheckJson, "ISJSON([Depois]) = 1 AND ([Antes] IS NULL OR ISJSON([Antes]) = 1)");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Tipo).HasConversion<byte>();
        b.Property(x => x.Depois).IsRequired();
        b.Property(x => x.BaseVersao).HasMaxLength(8).IsFixedLength(); // binary(8): o rowversion da base

        b.HasOne<Territorio>().WithMany().HasForeignKey(x => new { x.MapaId, x.TerritorioId }).HasPrincipalKey(t => new { t.MapaId, t.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Pessoa>().WithMany().HasForeignKey(x => x.PessoaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<RegraTerritorio>().WithMany().HasForeignKey(x => x.RegraBaseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ExcecaoTerritorio>().WithMany().HasForeignKey(x => x.ExcecaoBaseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<TerritorioPosicao>().WithMany().HasForeignKey(x => x.PosicaoBaseId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.OperacaoId, x.Ordem }).IsUnique();
    }
}

/// <summary>Simulações (DN-03): evidência imutável; a operação aponta a atual.</summary>
public class OperacaoTerritorialSimulacaoConfiguration : IEntityTypeConfiguration<OperacaoTerritorialSimulacao>
{
    public void Configure(EntityTypeBuilder<OperacaoTerritorialSimulacao> b)
    {
        b.ToTable("OperacaoTerritorialSimulacoes");
        b.HasKey(x => x.Id);
        b.Property(x => x.SimuladaPor).IsRequired().HasMaxLength(OperacaoTerritorial.TamanhoMaximoUsuario);
        b.Property(x => x.VersaoMotor).IsRequired().HasMaxLength(8).IsFixedLength();
        b.Property(x => x.VersaoArvore).IsRequired().HasMaxLength(8).IsFixedLength();
        b.Property(x => x.Assinatura).IsRequired().HasMaxLength(32).IsFixedLength(); // SHA-256
        b.HasOne<OperacaoTerritorial>().WithMany().HasForeignKey(x => x.OperacaoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Usuario>().WithMany().HasForeignKey(x => x.SimuladaPorId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Itens).WithOne().HasForeignKey(i => i.SimulacaoId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OperacaoId, x.SimuladaEm });
    }
}

public class OperacaoTerritorialSimulacaoItemConfiguration : IEntityTypeConfiguration<OperacaoTerritorialSimulacaoItem>
{
    public const string CheckExplicacao = "CK_OperacaoTerritorialSimulacaoItens_Explicacao";

    public void Configure(EntityTypeBuilder<OperacaoTerritorialSimulacaoItem> b)
    {
        b.ToTable("OperacaoTerritorialSimulacaoItens", t => t.HasCheckConstraint(CheckExplicacao, "ISJSON([Explicacao]) = 1"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Resultado).HasConversion<byte>();
        b.Property(x => x.Efeito).HasConversion<byte>();
        b.Property(x => x.OrigemEfeito).HasConversion<byte>();
        b.Property(x => x.Explicacao).IsRequired();
        b.HasOne<Pessoa>().WithMany().HasForeignKey(x => x.PessoaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Territorio>().WithMany().HasForeignKey(x => x.TerritorioAtualId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Territorio>().WithMany().HasForeignKey(x => x.TerritorioPropostoId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.SimulacaoId, x.PessoaId }).IsUnique();
    }
}

/// <summary>O que a aplicação fez por cliente: gravado uma vez; o gatilho 50076 nega UPDATE e DELETE.</summary>
public class OperacaoTerritorialItemConfiguration : IEntityTypeConfiguration<OperacaoTerritorialItem>
{
    public const string CheckExplicacao = "CK_OperacaoTerritorialItens_Explicacao";

    public void Configure(EntityTypeBuilder<OperacaoTerritorialItem> b)
    {
        b.ToTable("OperacaoTerritorialItens", t =>
        {
            t.HasCheckConstraint(CheckExplicacao, "ISJSON([Explicacao]) = 1");
            t.HasTrigger(SqlMigracaoTerritorios.GatilhoItens);
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Resultado).HasConversion<byte>();
        b.Property(x => x.Efeito).HasConversion<byte>();
        b.Property(x => x.Explicacao).IsRequired();
        b.HasOne<OperacaoTerritorial>().WithMany().HasForeignKey(x => x.OperacaoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Pessoa>().WithMany().HasForeignKey(x => x.PessoaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Territorio>().WithMany().HasForeignKey(x => x.TerritorioAnteriorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Territorio>().WithMany().HasForeignKey(x => x.TerritorioNovoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AtribuicaoTerritorio>().WithMany().HasForeignKey(x => x.AtribuicaoEncerradaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AtribuicaoTerritorio>().WithMany().HasForeignKey(x => x.AtribuicaoNovaId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OperacaoId, x.PessoaId }).IsUnique();
    }
}

/// <summary>Linhas que a operação fechou e o fim de antes: o desfazer (DN-05) as reabre exatamente como eram.</summary>
public class OperacaoTerritorialFechamentoConfiguration : IEntityTypeConfiguration<OperacaoTerritorialFechamento>
{
    public const string CheckTabela = "CK_OperacaoTerritorialFechamentos_Tabela";

    public void Configure(EntityTypeBuilder<OperacaoTerritorialFechamento> b)
    {
        b.ToTable("OperacaoTerritorialFechamentos", t => t.HasCheckConstraint(CheckTabela, "[Tabela] BETWEEN 1 AND 5"));
        b.HasKey(x => new { x.OperacaoId, x.Tabela, x.LinhaId });
        b.Property(x => x.Tabela).HasConversion<byte>();
        b.HasOne<OperacaoTerritorial>().WithMany().HasForeignKey(x => x.OperacaoId).OnDelete(DeleteBehavior.Restrict);
    }
}
