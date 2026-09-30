using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Territorios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lone.Infrastructure.Persistencia.Configuracoes;

// Territórios (Motor Comercial, Fase 2b-1a). Nada apaga em cascata; o banco garante o que não pode depender só da
// aplicação: pai do mesmo mapa (chave estrangeira composta), uma posição aberta por território, períodos coerentes,
// responsável que é pessoa OU equipe e, por gatilho (SqlMigracaoTerritorios), árvore sem ciclo e com até 12 níveis,
// posições sem cruzamento e responsáveis sem sobreposição — também contra gravações feitas direto no banco.

public class TipoTerritorioConfiguration : IEntityTypeConfiguration<TipoTerritorio>
{
    public void Configure(EntityTypeBuilder<TipoTerritorio> b)
    {
        b.ToTable("TiposTerritorio");
        b.HasKey(x => x.Id);
        b.Property(x => x.Codigo).IsRequired().HasMaxLength(TipoTerritorio.TamanhoMaximoCodigo).IsUnicode(false);
        b.Property(x => x.Nome).IsRequired().HasMaxLength(TipoTerritorio.TamanhoMaximoNome).UseCollation(EtiquetaConfiguration.CollationNome);
        b.Property(x => x.Descricao).HasMaxLength(TipoTerritorio.TamanhoMaximoDescricao);
        b.HasIndex(x => x.Codigo).IsUnique();
        b.HasIndex(x => x.Nome).IsUnique();

        // Tipos que nascem com a base (padrão pronto para a empresa pequena), com Ids fixos.
        var criacao = new DateTime(2026, 9, 29, 0, 0, 0, DateTimeKind.Utc);
        b.HasData(TiposTerritorioIniciais.Todos.Select(t => new TipoTerritorio
        {
            Id = t.Id, Codigo = t.Codigo, Nome = t.Nome, Descricao = t.Descricao, Ordem = t.Ordem, Ativo = true, CriadoEm = criacao
        }).ToArray());
    }
}

public class MapaTerritorialConfiguration : IEntityTypeConfiguration<MapaTerritorial>
{
    public const string IndiceIdExclusivo = "UX_MapasTerritoriais_Id_Exclusivo";

    public void Configure(EntityTypeBuilder<MapaTerritorial> b)
    {
        b.ToTable("MapasTerritoriais");
        b.HasKey(x => x.Id);
        b.Property(x => x.Codigo).IsRequired().HasMaxLength(MapaTerritorial.TamanhoMaximoCodigo).IsUnicode(false);
        b.Property(x => x.Nome).IsRequired().HasMaxLength(MapaTerritorial.TamanhoMaximoNome).UseCollation(EtiquetaConfiguration.CollationNome);
        b.Property(x => x.Descricao).HasMaxLength(MapaTerritorial.TamanhoMaximoDescricao);
        b.HasIndex(x => x.Codigo).IsUnique();
        b.HasIndex(x => x.Nome).IsUnique();
        b.HasOne<Pessoa>().WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<FinalidadeEnderecoCadastro>().WithMany().HasForeignKey(x => x.FinalidadeEnderecoReferenciaId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Classificacoes).WithOne().HasForeignKey(x => x.MapaId).OnDelete(DeleteBehavior.Restrict);
        b.Ignore(x => x.ClassificacoesAceitas);

        // Fase 2b-1b: alvo das FKs (MapaId, Exclusivo) de exceções e atribuições, criadas pela migration
        // (SqlMigracaoTerritorios.CriarChavesExclusivo). Índice e não chave alternativa: no EF, a chave alternativa tornaria
        // Exclusivo imutável até nos mapas sem uso.
        b.HasIndex(x => new { x.Id, x.Exclusivo }).IsUnique().HasDatabaseName(IndiceIdExclusivo);
    }
}

/// <summary>Trava da árvore (D1 = B): uma linha por mapa, criada junto com ele (e pela migração, para os que já existiam).</summary>
public class MapaTerritorialArvoreConfiguration : IEntityTypeConfiguration<MapaTerritorialArvore>
{
    public void Configure(EntityTypeBuilder<MapaTerritorialArvore> b)
    {
        b.ToTable("MapaTerritorialArvores");
        b.HasKey(x => x.MapaId);
        b.Property(x => x.Versao).IsRowVersion();
        b.HasOne<MapaTerritorial>().WithOne().HasForeignKey<MapaTerritorialArvore>(x => x.MapaId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class MapaTerritorialClassificacaoConfiguration : IEntityTypeConfiguration<MapaTerritorialClassificacao>
{
    public void Configure(EntityTypeBuilder<MapaTerritorialClassificacao> b)
    {
        b.ToTable("MapaTerritorialClassificacoes");
        b.HasKey(x => x.Id);
        b.HasOne<Papel>().WithMany().HasForeignKey(x => x.PapelId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.MapaId, x.PapelId }).IsUnique();
    }
}

public class TerritorioConfiguration : IEntityTypeConfiguration<Territorio>
{
    public const string CheckSituacao = "CK_Territorios_Situacao";

    public void Configure(EntityTypeBuilder<Territorio> b)
    {
        b.ToTable("Territorios", t =>
        {
            // Encerrado sempre com o último dia; ativo nunca com ele.
            t.HasCheckConstraint(CheckSituacao, "([Situacao] = 0 AND [FimEm] IS NULL) OR ([Situacao] = 1 AND [FimEm] IS NOT NULL)");
            // Sem ciclo e no máximo 12 níveis, também para gravações feitas direto no banco: gatilho (um CHECK não percorre
            // a árvore). Declarado para o EF não usar OUTPUT nesta tabela; criado pela migração (SqlMigracaoTerritorios).
            t.HasTrigger(SqlMigracaoTerritorios.GatilhoArvore);
        });
        b.HasKey(x => x.Id);
        b.Ignore(x => x.Ativo);

        // (MapaId, Id) é chave alternativa: é o alvo das chaves estrangeiras compostas que obrigam pai, posições (e, na
        // 2b-1b, atribuições e exceções) a ficarem no mesmo mapa do território.
        b.HasAlternateKey(x => new { x.MapaId, x.Id });

        b.Property(x => x.Codigo).IsRequired().HasMaxLength(Territorio.TamanhoMaximoCodigo).IsUnicode(false);
        b.Property(x => x.Nome).IsRequired().HasMaxLength(Territorio.TamanhoMaximoNome).UseCollation(EtiquetaConfiguration.CollationNome);
        b.Property(x => x.Descricao).HasMaxLength(Territorio.TamanhoMaximoDescricao);
        b.Property(x => x.Situacao).HasConversion<byte>(); // 0 = Ativo

        b.HasOne<MapaTerritorial>().WithMany().HasForeignKey(x => x.MapaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<TipoTerritorio>().WithMany().HasForeignKey(x => x.TipoId).OnDelete(DeleteBehavior.Restrict);

        // Pai no mesmo mapa, garantido pelo banco (FK composta; PaiId nulo = raiz, e o SQL Server não confere FK com nulo).
        b.HasOne<Territorio>().WithMany().HasForeignKey(x => new { x.MapaId, x.PaiId }).HasPrincipalKey(x => new { x.MapaId, x.Id })
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => new { x.MapaId, x.Codigo }).IsUnique();
        // Nome único entre irmãos ativos (no SQL Server, nulos contam como iguais no índice único: vale também para a raiz).
        b.HasIndex(x => new { x.MapaId, x.PaiId, x.Nome }).IsUnique().HasFilter("[Situacao] = 0")
            .HasDatabaseName("UX_Territorios_NomeEntreIrmaosAtivos");

        b.HasMany(x => x.Posicoes).WithOne().HasForeignKey(p => new { p.MapaId, p.TerritorioId }).HasPrincipalKey(t => new { t.MapaId, t.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Responsaveis).WithOne().HasForeignKey(r => r.TerritorioId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class TerritorioPosicaoConfiguration : IEntityTypeConfiguration<TerritorioPosicao>
{
    public const string CheckPeriodo = "CK_TerritorioPosicoes_Periodo";
    public const string IndicePosicaoAberta = "UX_TerritorioPosicoes_Aberta";

    public void Configure(EntityTypeBuilder<TerritorioPosicao> b)
    {
        b.ToTable("TerritorioPosicoes", t =>
        {
            t.HasCheckConstraint(CheckPeriodo, "[FimEm] IS NULL OR [FimEm] >= [InicioEm]");
            // Posições válidas do mesmo território não se cruzam (o índice abaixo só garante uma aberta): gatilho.
            t.HasTrigger(SqlMigracaoTerritorios.GatilhoPosicoes);
        });
        b.HasKey(x => x.Id);

        // Pai da posição também no mesmo mapa (a posição histórica não pode apontar para outro mapa).
        b.HasOne<Territorio>().WithMany().HasForeignKey(x => new { x.MapaId, x.PaiId }).HasPrincipalKey(t => new { t.MapaId, t.Id })
            .OnDelete(DeleteBehavior.NoAction);

        // Uma posição aberta por território (as anuladas não contam).
        b.HasIndex(x => x.TerritorioId).IsUnique().HasFilter("[FimEm] IS NULL AND [Ativo] = 1").HasDatabaseName(IndicePosicaoAberta);
        // "A árvore em D": filhos de um nó por data.
        b.HasIndex(x => new { x.MapaId, x.PaiId, x.InicioEm });

        // Fase 2b-1b: a operação e a mudança que abriram a posição, e as que a encerraram ou anularam (nulas nas posições
        // gravadas pela 2b-1a, que não passam por operação).
        b.HasOne<OperacaoTerritorial>().WithMany().HasForeignKey(x => x.OperacaoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OperacaoTerritorialMudanca>().WithMany().HasForeignKey(x => x.OperacaoMudancaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OperacaoTerritorial>().WithMany().HasForeignKey(x => x.OperacaoEncerramentoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OperacaoTerritorial>().WithMany().HasForeignKey(x => x.OperacaoAnulacaoId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class TerritorioResponsavelConfiguration : IEntityTypeConfiguration<TerritorioResponsavel>
{
    public const string CheckQuem = "CK_TerritorioResponsaveis_PessoaOuEquipe";
    public const string CheckPeriodo = "CK_TerritorioResponsaveis_Periodo";

    public void Configure(EntityTypeBuilder<TerritorioResponsavel> b)
    {
        b.ToTable("TerritorioResponsaveis", t =>
        {
            t.HasCheckConstraint(CheckQuem,
                "([PessoaId] IS NOT NULL AND [EquipeId] IS NULL) OR ([PessoaId] IS NULL AND [EquipeId] IS NOT NULL)");
            t.HasCheckConstraint(CheckPeriodo, "[FimEm] IS NULL OR [FimEm] >= [InicioEm]");
            // Sem sobreposição (mesmo território, função e pessoa/equipe): gatilho, porque um CHECK não compara linhas.
            // Declarado para o EF não usar OUTPUT nesta tabela; criado pela migração (SqlMigracaoTerritorios).
            t.HasTrigger(SqlMigracaoTerritorios.Gatilho);
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Observacao).HasMaxLength(TerritorioResponsavel.TamanhoMaximoObservacao);
        b.HasOne<Pessoa>().WithMany().HasForeignKey(x => x.PessoaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Equipe>().WithMany().HasForeignKey(x => x.EquipeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<TipoCarteira>().WithMany().HasForeignKey(x => x.TipoCarteiraId).OnDelete(DeleteBehavior.Restrict);
        // "De que territórios a pessoa/equipe é responsável" (2b-2: Meus territórios).
        b.HasIndex(x => new { x.PessoaId, x.FimEm });
        b.HasIndex(x => new { x.EquipeId, x.FimEm });
        // Fase 2b-1b: responsável encerrado como consequência do encerramento do território por operação (DN-09).
        b.HasOne<OperacaoTerritorial>().WithMany().HasForeignKey(x => x.OperacaoEncerramentoId).OnDelete(DeleteBehavior.Restrict);
    }
}
