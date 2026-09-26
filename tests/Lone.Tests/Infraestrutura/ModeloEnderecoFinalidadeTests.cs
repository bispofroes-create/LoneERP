using Lone.Domain.Entidades;
using Lone.Infrastructure.Persistencia;
using Lone.Infrastructure.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Lone.Tests.Infraestrutura;

/// <summary>
/// Proteções de endereço × finalidade no MODELO do EF (o que a migration gera). Não abre conexão: lê o modelo de
/// projeto (design-time), que tem FKs, índices, CHECKs e gatilhos declarados.
/// </summary>
public class ModeloEnderecoFinalidadeTests
{
    private static readonly IModel Modelo = CriarModelo();

    private static IModel CriarModelo()
    {
        using var db = new LoneDbContext(new DbContextOptionsBuilder<LoneDbContext>()
            .UseSqlServer("Server=(sem-conexao);Database=Lone;Trusted_Connection=True").Options);
        return db.GetService<IDesignTimeModel>().Model;
    }

    private static IEntityType Entidade<T>() => Modelo.FindEntityType(typeof(T))!;

    private static string[] Nomes(IEnumerable<IProperty> propriedades) => propriedades.Select(p => p.Name).ToArray();

    [Fact]
    public void Relacao_aponta_para_endereco_da_mesma_pessoa_por_FK_composta()
    {
        var fk = Entidade<PessoaEnderecoFinalidade>().GetForeignKeys().Single(f => f.PrincipalEntityType.ClrType == typeof(PessoaEndereco));

        Assert.Equal(new[] { "PessoaEnderecoId", "PessoaId" }, Nomes(fk.Properties));
        Assert.Equal(new[] { "Id", "PessoaId" }, Nomes(fk.PrincipalKey.Properties));
        Assert.Equal(DeleteBehavior.NoAction, fk.DeleteBehavior);
    }

    [Fact]
    public void MescladoEmId_usa_FK_composta_para_endereco_da_mesma_pessoa()
    {
        var fk = Entidade<PessoaEndereco>().GetForeignKeys().Single(f => f.PrincipalEntityType.ClrType == typeof(PessoaEndereco));

        Assert.Equal(new[] { "MescladoEmId", "PessoaId" }, Nomes(fk.Properties));
        Assert.Equal(new[] { "Id", "PessoaId" }, Nomes(fk.PrincipalKey.Properties));
        Assert.False(fk.PrincipalKey.IsPrimaryKey()); // a chave alternativa (Id, PessoaId)
        Assert.Equal(DeleteBehavior.NoAction, fk.DeleteBehavior);
        Assert.False(fk.IsRequired);
    }

    [Fact]
    public void Relacao_tem_FK_para_o_cadastro_de_finalidades_e_para_a_pessoa()
    {
        var fks = Entidade<PessoaEnderecoFinalidade>().GetForeignKeys().ToList();
        var finalidade = fks.Single(f => f.PrincipalEntityType.ClrType == typeof(FinalidadeEnderecoCadastro));
        Assert.Equal(new[] { "FinalidadeId" }, Nomes(finalidade.Properties));
        Assert.Equal(DeleteBehavior.Restrict, finalidade.DeleteBehavior);
        Assert.Contains(fks, f => f.PrincipalEntityType.ClrType == typeof(Pessoa) && Nomes(f.Properties).SequenceEqual(new[] { "PessoaId" }));
    }

    [Theory]
    [InlineData(SqlMigracaoFinalidadesEndereco.IndiceFinalidadeAtiva, "PessoaEnderecoId", "[Ativo] = 1")]
    [InlineData(SqlMigracaoFinalidadesEndereco.IndicePrincipal, "PessoaId", "[Principal] = 1")]
    public void Indices_unicos_filtrados_com_nome_fixo(string nome, string primeira, string filtro)
    {
        var indice = Entidade<PessoaEnderecoFinalidade>().GetIndexes().Single(i => i.GetDatabaseName() == nome);

        Assert.True(indice.IsUnique);
        Assert.Equal(new[] { primeira, "FinalidadeId" }, Nomes(indice.Properties));
        Assert.Equal(filtro, indice.GetFilter());
    }

    [Fact]
    public void Checks_e_gatilhos_declarados()
    {
        Assert.Contains(Entidade<PessoaEnderecoFinalidade>().GetCheckConstraints(),
            c => c.Sql == "[Principal] = 0 OR [Ativo] = 1");
        Assert.Contains(Entidade<PessoaEndereco>().GetCheckConstraints(),
            c => c.ModelName == SqlMigracaoFinalidadesEndereco.CheckConsolidado && c.Sql.Contains("[Ativo] = 0") && c.Sql.Contains("[MescladoEmId] <> [Id]"));
        Assert.Contains(Entidade<FinalidadeEnderecoCadastro>().GetCheckConstraints(),
            c => c.ModelName == SqlMigracaoFinalidadesEndereco.CheckSistemaAtiva);

        Assert.Contains(Entidade<PessoaEnderecoFinalidade>().GetDeclaredTriggers(), t => t.ModelName == SqlMigracaoFinalidadesEndereco.GatilhoPrincipalEnderecoAtivo);
        Assert.Contains(Entidade<PessoaEndereco>().GetDeclaredTriggers(), t => t.ModelName == SqlMigracaoFinalidadesEndereco.GatilhoEnderecoInativo);
        Assert.Contains(Entidade<FinalidadeEnderecoCadastro>().GetDeclaredTriggers(), t => t.ModelName == SqlMigracaoFinalidadesEndereco.GatilhoFinalidadeSistema);

        // Os gatilhos declarados no modelo são os que o SQL da migração cria.
        foreach (var nome in new[] { SqlMigracaoFinalidadesEndereco.GatilhoPrincipalEnderecoAtivo, SqlMigracaoFinalidadesEndereco.GatilhoEnderecoInativo,
                                     SqlMigracaoFinalidadesEndereco.GatilhoFinalidadeSistema })
        {
            Assert.Contains("CREATE TRIGGER " + nome, SqlMigracaoFinalidadesEndereco.CriarProtecoes);
            Assert.Contains("DROP TRIGGER IF EXISTS " + nome, SqlMigracaoFinalidadesEndereco.RemoverProtecoes);
        }
    }

    [Fact]
    public void Finalidades_de_sistema_codigo_e_nome_unicos_e_as_seis_iniciais_como_dados()
    {
        var finalidade = Entidade<FinalidadeEnderecoCadastro>();
        Assert.Contains(finalidade.GetIndexes(), i => i.IsUnique && Nomes(i.Properties).SequenceEqual(new[] { "Codigo" }));
        Assert.Contains(finalidade.GetIndexes(), i => i.IsUnique && Nomes(i.Properties).SequenceEqual(new[] { "Nome" }));

        var iniciais = finalidade.GetSeedData().ToList();
        Assert.Equal(6, iniciais.Count);
        Assert.All(iniciais, d => Assert.True((bool)d["DoSistema"]!));
        Assert.Equal(new[] { "COBRANCA", "COMERCIAL", "CORRESPONDENCIA", "ENTREGA", "FISCAL", "RESIDENCIAL" },
            iniciais.Select(d => (string)d["Codigo"]!).Order().ToArray());
        Assert.DoesNotContain(iniciais, d => (string)d["Codigo"]! == "PRINCIPAL"); // principal não é finalidade
    }

    [Fact]
    public void SQL_da_migracao_nao_apaga_dados()
    {
        foreach (var sql in new[] { SqlMigracaoFinalidadesEndereco.MigrarFinalidades, SqlMigracaoFinalidadesEndereco.CriarProtecoes })
        {
            Assert.DoesNotMatch(@"(?im)^\s*DELETE\b", sql);            // nenhum comando DELETE
            Assert.DoesNotContain("DELETE FROM", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("TRUNCATE", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("DROP ", sql, StringComparison.OrdinalIgnoreCase);
        }
        Assert.DoesNotContain("DROP TABLE", SqlMigracaoFinalidadesEndereco.RemoverProtecoes, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------- Tradução dos conflitos (I3)

    [Fact]
    public void Violacao_do_indice_de_principal_vira_mensagem_de_conflito()
    {
        var texto = "Cannot insert duplicate key row in object 'dbo.PessoaEnderecoFinalidades' with unique index " +
                    $"'{SqlMigracaoFinalidadesEndereco.IndicePrincipal}'. The duplicate key value is (...).";
        Assert.Equal(ConflitosEnderecoFinalidade.PrincipalRepetido, ConflitosEnderecoFinalidade.Mensagem(2601, texto));
        Assert.Equal(ConflitosEnderecoFinalidade.PrincipalRepetido, ConflitosEnderecoFinalidade.Mensagem(2627, texto));
    }

    [Fact]
    public void Violacao_do_indice_de_finalidade_ativa_vira_mensagem_propria()
    {
        var texto = $"Não é possível inserir a linha de chave duplicada ... índice exclusivo '{SqlMigracaoFinalidadesEndereco.IndiceFinalidadeAtiva}'.";
        Assert.Equal(ConflitosEnderecoFinalidade.FinalidadeRepetida, ConflitosEnderecoFinalidade.Mensagem(2601, texto));
    }

    [Fact]
    public void Gatilhos_de_endereco_inativo_viram_conflito()
    {
        Assert.Equal(ConflitosEnderecoFinalidade.EnderecoInativoPrincipal,
            ConflitosEnderecoFinalidade.Mensagem(SqlMigracaoFinalidadesEndereco.ErroPrincipalEmEnderecoInativo, "..."));
        Assert.Equal(ConflitosEnderecoFinalidade.EnderecoInativoPrincipal,
            ConflitosEnderecoFinalidade.Mensagem(SqlMigracaoFinalidadesEndereco.ErroEnderecoInativoComPrincipal, "..."));
    }

    [Theory]
    [InlineData(2601, "unique index 'IX_Pessoas_Codigo'")] // outro índice: não é um caso conhecido
    [InlineData(547, "The INSERT statement conflicted with the FOREIGN KEY constraint")]
    [InlineData(1205, "deadlock")]
    public void Outros_erros_de_banco_nao_sao_disfarcados_de_conflito(int numero, string texto) =>
        Assert.Null(ConflitosEnderecoFinalidade.Mensagem(numero, texto));

    [Fact]
    public void Erro_qualquer_nao_e_conflito() =>
        Assert.Null(ConflitosEnderecoFinalidade.Mensagem(new InvalidOperationException("x")));
}
