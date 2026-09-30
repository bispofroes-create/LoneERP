using Lone.Domain.Entidades;
using Lone.Domain.Territorios;
using Lone.Infrastructure.Persistencia;
using Lone.Infrastructure.Persistencia.Configuracoes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Lone.Tests.Infraestrutura;

/// <summary>
/// Banco da Fase 2b-1a (territórios), conferido no modelo do EF (não abre conexão): chave alternativa (MapaId, Id) e chaves
/// estrangeiras compostas que obrigam pai e posições a ficarem no mesmo mapa, índices únicos (código no mapa, nome entre
/// irmãos ativos, uma posição aberta), restrições de período e de pessoa-ou-equipe, nada em cascata e os tipos iniciais.
/// </summary>
public class ModeloTerritoriosTests
{
    private static IModel Modelo()
    {
        using var db = new LoneDbContext(new DbContextOptionsBuilder<LoneDbContext>()
            .UseSqlServer("Server=(sem-conexao);Database=Lone;Trusted_Connection=True").Options);
        return db.GetService<IDesignTimeModel>().Model;
    }

    private static string[] Nomes(IEnumerable<IReadOnlyProperty> propriedades) => propriedades.Select(p => p.Name).ToArray();

    [Fact]
    public void Pai_do_territorio_e_do_mesmo_mapa_pela_chave_composta()
    {
        var territorio = Modelo().FindEntityType(typeof(Territorio))!;
        Assert.Contains(territorio.GetKeys(), k => Nomes(k.Properties).SequenceEqual(new[] { nameof(Territorio.MapaId), nameof(Territorio.Id) }));

        var pai = territorio.GetForeignKeys().Single(f => f.PrincipalEntityType == territorio);
        Assert.Equal(new[] { nameof(Territorio.MapaId), nameof(Territorio.PaiId) }, Nomes(pai.Properties));
        Assert.Equal(new[] { nameof(Territorio.MapaId), nameof(Territorio.Id) }, Nomes(pai.PrincipalKey.Properties));
        Assert.Equal(DeleteBehavior.NoAction, pai.DeleteBehavior);
    }

    [Fact]
    public void Posicao_aponta_para_territorio_e_pai_do_mesmo_mapa_e_so_uma_fica_aberta()
    {
        var posicao = Modelo().FindEntityType(typeof(TerritorioPosicao))!;
        var fks = posicao.GetForeignKeys().Where(f => f.PrincipalEntityType.ClrType == typeof(Territorio)).ToList();
        Assert.Equal(2, fks.Count);
        Assert.Contains(fks, f => Nomes(f.Properties).SequenceEqual(new[] { nameof(TerritorioPosicao.MapaId), nameof(TerritorioPosicao.TerritorioId) }));
        Assert.Contains(fks, f => Nomes(f.Properties).SequenceEqual(new[] { nameof(TerritorioPosicao.MapaId), nameof(TerritorioPosicao.PaiId) }));
        Assert.All(fks, f => Assert.Equal(new[] { nameof(Territorio.MapaId), nameof(Territorio.Id) }, Nomes(f.PrincipalKey.Properties)));

        var aberta = posicao.GetIndexes().Single(i => i.GetDatabaseName() == TerritorioPosicaoConfiguration.IndicePosicaoAberta);
        Assert.True(aberta.IsUnique);
        Assert.Equal("[FimEm] IS NULL AND [Ativo] = 1", aberta.GetFilter());
        Assert.Contains(posicao.GetCheckConstraints(), c => c.Name == TerritorioPosicaoConfiguration.CheckPeriodo);
    }

    [Fact]
    public void Codigo_unico_no_mapa_e_nome_unico_entre_irmaos_ativos()
    {
        var territorio = Modelo().FindEntityType(typeof(Territorio))!;
        var codigo = territorio.GetIndexes().Single(i => Nomes(i.Properties).SequenceEqual(new[] { nameof(Territorio.MapaId), nameof(Territorio.Codigo) }));
        Assert.True(codigo.IsUnique);
        Assert.Null(codigo.GetFilter()); // inclusive entre os encerrados

        var nome = territorio.GetIndexes().Single(i => i.GetDatabaseName() == "UX_Territorios_NomeEntreIrmaosAtivos");
        Assert.True(nome.IsUnique);
        Assert.Equal(new[] { nameof(Territorio.MapaId), nameof(Territorio.PaiId), nameof(Territorio.Nome) }, Nomes(nome.Properties));
        Assert.Equal("[Situacao] = 0", nome.GetFilter());
        Assert.Contains(territorio.GetCheckConstraints(), c => c.Name == TerritorioConfiguration.CheckSituacao);
        Assert.Equal(typeof(byte), territorio.FindProperty(nameof(Territorio.Situacao))!.GetProviderClrType());
    }

    [Fact]
    public void Responsavel_e_pessoa_ou_equipe_com_periodo_coerente()
    {
        var responsavel = Modelo().FindEntityType(typeof(TerritorioResponsavel))!;
        var checks = responsavel.GetCheckConstraints().ToDictionary(c => c.Name!, c => c.Sql);
        Assert.Contains("[PessoaId] IS NOT NULL AND [EquipeId] IS NULL", checks[TerritorioResponsavelConfiguration.CheckQuem]);
        Assert.Contains("[PessoaId] IS NULL AND [EquipeId] IS NOT NULL", checks[TerritorioResponsavelConfiguration.CheckQuem]);
        Assert.Equal("[FimEm] IS NULL OR [FimEm] >= [InicioEm]", checks[TerritorioResponsavelConfiguration.CheckPeriodo]);
        Assert.Contains(responsavel.GetForeignKeys(), f => f.PrincipalEntityType.ClrType == typeof(TipoCarteira)); // função = papel comercial
        // Sobreposição (mesmo território, função e pessoa/equipe): gatilho declarado, para o EF não usar OUTPUT na tabela.
        Assert.Contains(responsavel.GetDeclaredTriggers(), t => t.ModelName == SqlMigracaoTerritorios.Gatilho);
        Assert.Contains("50070", SqlMigracaoTerritorios.CriarProtecao);
        Assert.DoesNotContain("DELETE", SqlMigracaoTerritorios.CriarProtecao, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Nada_dos_territorios_apaga_em_cascata()
    {
        var modelo = Modelo();
        var tipos = new[]
        {
            typeof(TipoTerritorio), typeof(MapaTerritorial), typeof(MapaTerritorialClassificacao), typeof(Territorio), typeof(TerritorioPosicao),
            typeof(TerritorioResponsavel), typeof(MapaTerritorialArvore)
        };
        var fks = tipos.SelectMany(t => modelo.FindEntityType(t)!.GetForeignKeys()).ToList();
        Assert.NotEmpty(fks);
        Assert.All(fks, f => Assert.NotEqual(DeleteBehavior.Cascade, f.DeleteBehavior));
    }

    [Fact]
    public void Arvore_tem_trava_propria_por_mapa_e_gatilhos_de_ciclo_niveis_e_posicoes()
    {
        var modelo = Modelo();
        var trava = modelo.FindEntityType(typeof(MapaTerritorialArvore))!;
        Assert.Equal(new[] { nameof(MapaTerritorialArvore.MapaId) }, Nomes(trava.FindPrimaryKey()!.Properties)); // uma linha por mapa
        Assert.True(trava.FindProperty(nameof(MapaTerritorialArvore.Versao))!.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, trava.FindProperty(nameof(MapaTerritorialArvore.Versao))!.ValueGenerated); // rowversion
        Assert.Contains(trava.GetForeignKeys(), f => f.PrincipalEntityType.ClrType == typeof(MapaTerritorial));

        // Gatilhos declarados (o EF não usa OUTPUT nessas tabelas); o SQL deles é o da migração.
        Assert.Contains(modelo.FindEntityType(typeof(Territorio))!.GetDeclaredTriggers(), t => t.ModelName == SqlMigracaoTerritorios.GatilhoArvore);
        Assert.Contains(modelo.FindEntityType(typeof(TerritorioPosicao))!.GetDeclaredTriggers(), t => t.ModelName == SqlMigracaoTerritorios.GatilhoPosicoes);
        Assert.Contains("50071", SqlMigracaoTerritorios.CriarProtecaoArvore);
        Assert.Contains("50072", SqlMigracaoTerritorios.CriarProtecaoPosicoes);
        foreach (var sql in new[] { SqlMigracaoTerritorios.CriarProtecaoArvore, SqlMigracaoTerritorios.CriarProtecaoPosicoes, SqlMigracaoTerritorios.PreencherTravasDaArvore })
        {
            Assert.DoesNotContain("DELETE", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("DROP", sql, StringComparison.OrdinalIgnoreCase);
        }
        Assert.DoesNotContain("TABLE", SqlMigracaoTerritorios.RemoverProtecoesArvore.Replace("DROP TRIGGER", string.Empty), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Mapa_guarda_o_endereco_de_referencia_e_tem_codigo_e_nome_unicos()
    {
        var mapa = Modelo().FindEntityType(typeof(MapaTerritorial))!;
        Assert.Contains(mapa.GetForeignKeys(), f => f.PrincipalEntityType.ClrType == typeof(FinalidadeEnderecoCadastro)
                                                   && f.Properties.Single().Name == nameof(MapaTerritorial.FinalidadeEnderecoReferenciaId));
        Assert.False(mapa.FindProperty(nameof(MapaTerritorial.FinalidadeEnderecoReferenciaId))!.IsNullable);
        Assert.True(mapa.FindProperty(nameof(MapaTerritorial.EmpresaId))!.IsNullable);
        // Código, nome e, desde a 2b-1b, (Id, Exclusivo): o alvo das FKs de cópia de exceções e atribuições (índice, não
        // chave alternativa, para a exclusividade continuar mudando nos mapas sem uso).
        Assert.Equal(3, mapa.GetIndexes().Count(i => i.IsUnique));
        Assert.Contains(mapa.GetIndexes(), i => i.IsUnique && i.GetDatabaseName() == Lone.Infrastructure.Persistencia.Configuracoes.MapaTerritorialConfiguration.IndiceIdExclusivo
                                                && Nomes(i.Properties).SequenceEqual(new[] { nameof(MapaTerritorial.Id), nameof(MapaTerritorial.Exclusivo) }));
        Assert.DoesNotContain(mapa.GetKeys(), k => !k.IsPrimaryKey());
        Assert.Null(mapa.FindProperty("ClassificacoesAceitas"));

        var universo = Modelo().FindEntityType(typeof(MapaTerritorialClassificacao))!;
        Assert.Contains(universo.GetIndexes(), i => i.IsUnique && Nomes(i.Properties).SequenceEqual(new[] { nameof(MapaTerritorialClassificacao.MapaId), nameof(MapaTerritorialClassificacao.PapelId) }));
    }

    [Fact]
    public void Tipos_iniciais_nascem_com_a_base()
    {
        var sementes = Modelo().FindEntityType(typeof(TipoTerritorio))!.GetSeedData().ToList();
        Assert.Equal(TiposTerritorioIniciais.Todos.Select(t => t.Id), sementes.Select(s => (Guid)s[nameof(TipoTerritorio.Id)]!));
        Assert.All(sementes, s => Assert.True((bool)s[nameof(TipoTerritorio.Ativo)]!));
    }
}
