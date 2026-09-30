using Lone.Domain.Auditoria;
using Lone.Domain.Entidades;
using Lone.Infrastructure.Persistencia;
using Lone.Infrastructure.Persistencia.Configuracoes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Lone.Tests.Infraestrutura;

/// <summary>
/// Banco da Fase 2b-1b (motor territorial) conferido no modelo do EF, sem conexão (plano, seção Q, "EF / modelo"): FKs
/// compostas (mesmo mapa), índices únicos filtrados (uma linha aberta), CHECKs, gatilhos declarados, [NaoAuditar] onde deve
/// e nada em cascata.
/// </summary>
public class ModeloOperacoesTerritoriaisTests
{
    private static IModel Modelo()
    {
        using var db = new LoneDbContext(new DbContextOptionsBuilder<LoneDbContext>()
            .UseSqlServer("Server=(sem-conexao);Database=Lone;Trusted_Connection=True").Options);
        return db.GetService<IDesignTimeModel>().Model;
    }

    private static string[] Nomes(IEnumerable<IReadOnlyProperty> propriedades) => propriedades.Select(p => p.Name).ToArray();

    private static readonly Type[] Tabelas =
    [
        typeof(ParametrosTerritoriais), typeof(MapaTerritorialMotor), typeof(NumeracaoDocumento), typeof(RegraTerritorio), typeof(ExcecaoTerritorio),
        typeof(AtribuicaoTerritorio), typeof(OperacaoTerritorial), typeof(OperacaoTerritorialMudanca), typeof(OperacaoTerritorialSimulacao),
        typeof(OperacaoTerritorialSimulacaoItem), typeof(OperacaoTerritorialItem), typeof(OperacaoTerritorialFechamento)
    ];

    [Fact]
    public void Nada_do_motor_apaga_em_cascata()
    {
        var modelo = Modelo();
        var fks = Tabelas.Concat([typeof(TerritorioPosicao), typeof(TerritorioResponsavel)]).SelectMany(t => modelo.FindEntityType(t)!.GetForeignKeys()).ToList();
        Assert.NotEmpty(fks);
        Assert.All(fks, f => Assert.NotEqual(DeleteBehavior.Cascade, f.DeleteBehavior));
    }

    [Fact]
    public void Regra_excecao_atribuicao_e_mudanca_sao_do_mesmo_mapa_do_territorio_pela_chave_composta()
    {
        var modelo = Modelo();
        foreach (var tipo in new[] { typeof(RegraTerritorio), typeof(ExcecaoTerritorio), typeof(AtribuicaoTerritorio), typeof(OperacaoTerritorialMudanca) })
            Assert.Contains(modelo.FindEntityType(tipo)!.GetForeignKeys(), f => f.PrincipalEntityType.ClrType == typeof(Territorio) &&
                Nomes(f.Properties).SequenceEqual(["MapaId", "TerritorioId"]) && Nomes(f.PrincipalKey.Properties).SequenceEqual(["MapaId", "Id"]));
        // A mudança é do mapa da operação.
        Assert.Contains(modelo.FindEntityType(typeof(OperacaoTerritorialMudanca))!.GetForeignKeys(), f => f.PrincipalEntityType.ClrType == typeof(OperacaoTerritorial) &&
            Nomes(f.Properties).SequenceEqual(["OperacaoId", "MapaId"]));
    }

    [Fact]
    public void Uma_linha_aberta_onde_so_pode_haver_uma()
    {
        var modelo = Modelo();
        Assert.Contains(modelo.FindEntityType(typeof(RegraTerritorio))!.GetIndexes(), i => i.IsUnique && i.GetDatabaseName() == RegraTerritorioConfiguration.IndiceAberta
            && i.GetFilter() == "[FimEm] IS NULL AND [Ativo] = 1");
        Assert.Contains(modelo.FindEntityType(typeof(AtribuicaoTerritorio))!.GetIndexes(), i => i.IsUnique && i.GetDatabaseName() == AtribuicaoTerritorioConfiguration.IndiceAberta
            && i.GetFilter()!.Contains("[Exclusivo] = 1", StringComparison.Ordinal));
        Assert.Contains(modelo.FindEntityType(typeof(ExcecaoTerritorio))!.GetIndexes(), i => i.IsUnique && i.GetDatabaseName() == ExcecaoTerritorioConfiguration.IndiceFixarAberto
            && i.GetFilter()!.Contains("[Tipo] = 1", StringComparison.Ordinal));
        Assert.Contains(modelo.FindEntityType(typeof(OperacaoTerritorial))!.GetIndexes(), i => i.IsUnique && Nomes(i.Properties).SequenceEqual(["Ano", "Sequencia"]));
        Assert.Contains(modelo.FindEntityType(typeof(OperacaoTerritorialItem))!.GetIndexes(), i => i.IsUnique && Nomes(i.Properties).SequenceEqual(["OperacaoId", "PessoaId"]));
        Assert.Equal(new[] { "Prefixo", "Ano" }, Nomes(modelo.FindEntityType(typeof(NumeracaoDocumento))!.FindPrimaryKey()!.Properties));
    }

    [Fact]
    public void Gatilhos_declarados_para_o_EF_nao_usar_OUTPUT()
    {
        var modelo = Modelo();
        Assert.Contains(modelo.FindEntityType(typeof(RegraTerritorio))!.GetDeclaredTriggers(), t => t.GetDatabaseName() == SqlMigracaoTerritorios.GatilhoRegras);
        Assert.Contains(modelo.FindEntityType(typeof(ExcecaoTerritorio))!.GetDeclaredTriggers(), t => t.GetDatabaseName() == SqlMigracaoTerritorios.GatilhoExcecoes);
        Assert.Contains(modelo.FindEntityType(typeof(AtribuicaoTerritorio))!.GetDeclaredTriggers(), t => t.GetDatabaseName() == SqlMigracaoTerritorios.GatilhoAtribuicoes);
        Assert.Contains(modelo.FindEntityType(typeof(OperacaoTerritorialItem))!.GetDeclaredTriggers(), t => t.GetDatabaseName() == SqlMigracaoTerritorios.GatilhoItens);
    }

    [Fact]
    public void Volume_e_controles_tecnicos_ficam_fora_da_auditoria_e_os_fatos_com_decisao_humana_dentro()
    {
        static bool NaoAuditado(Type t) => t.IsDefined(typeof(NaoAuditarAttribute), inherit: false);
        Assert.All(new[] { typeof(AtribuicaoTerritorio), typeof(OperacaoTerritorialItem), typeof(OperacaoTerritorialSimulacao),
                           typeof(OperacaoTerritorialSimulacaoItem), typeof(OperacaoTerritorialFechamento), typeof(MapaTerritorialMotor), typeof(NumeracaoDocumento) },
                   t => Assert.True(NaoAuditado(t), t.Name));
        Assert.All(new[] { typeof(RegraTerritorio), typeof(ExcecaoTerritorio), typeof(OperacaoTerritorial), typeof(OperacaoTerritorialMudanca), typeof(ParametrosTerritoriais) },
                   t => Assert.False(NaoAuditado(t), t.Name));
    }

    [Fact]
    public void Checks_de_periodo_prioridade_json_e_situacao()
    {
        var modelo = Modelo();
        string[] Checks(Type t) => [.. modelo.FindEntityType(t)!.GetCheckConstraints().Select(c => c.ModelName)];
        Assert.Contains(RegraTerritorioConfiguration.CheckPrioridade, Checks(typeof(RegraTerritorio)));
        Assert.Contains(RegraTerritorioConfiguration.CheckGrupos, Checks(typeof(RegraTerritorio)));
        Assert.Contains(AtribuicaoTerritorioConfiguration.CheckOrigem, Checks(typeof(AtribuicaoTerritorio)));
        Assert.Contains(OperacaoTerritorialConfiguration.CheckTransicoes, Checks(typeof(OperacaoTerritorial)));
        Assert.Contains(ParametrosTerritoriaisConfiguration.CheckDias, Checks(typeof(ParametrosTerritoriais)));
        var sementes = modelo.FindEntityType(typeof(ParametrosTerritoriais))!.GetSeedData().Single();
        Assert.Equal(30, sementes[nameof(ParametrosTerritoriais.DiasRetroativosMaximo)]);
        Assert.False(modelo.FindEntityType(typeof(MapaTerritorial))!.FindProperty(nameof(MapaTerritorial.RegistrarNosDocumentos))!.IsNullable);
    }
}
