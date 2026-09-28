using Lone.Domain.Entidades;
using Lone.Infrastructure.Persistencia;
using Lone.Infrastructure.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Lone.Tests.Infraestrutura;

/// <summary>
/// Proteção da carteira no banco (Etapa 4, D3): o gatilho está declarado no modelo (o EF não usa OUTPUT na tabela), o SQL
/// da migração cria e retira o mesmo gatilho, e o erro dele vira conflito com mensagem clara. Não abre conexão.
/// </summary>
public class ModeloCarteiraTests
{
    private static IModel Modelo()
    {
        using var db = new LoneDbContext(new DbContextOptionsBuilder<LoneDbContext>()
            .UseSqlServer("Server=(sem-conexao);Database=Lone;Trusted_Connection=True").Options);
        return db.GetService<IDesignTimeModel>().Model;
    }

    [Fact]
    public void Gatilho_declarado_no_modelo_e_o_mesmo_do_sql_da_migracao()
    {
        var carteira = Modelo().FindEntityType(typeof(CarteiraCliente))!;
        Assert.Contains(carteira.GetDeclaredTriggers(), t => t.ModelName == SqlMigracaoCarteira.Gatilho);
        Assert.Contains("CREATE TRIGGER " + SqlMigracaoCarteira.Gatilho + " ON CarteiraClientes", SqlMigracaoCarteira.CriarProtecao);
        Assert.Contains("DROP TRIGGER IF EXISTS " + SqlMigracaoCarteira.Gatilho, SqlMigracaoCarteira.RemoverProtecao);
        Assert.Contains("THROW " + SqlMigracaoCarteira.ErroSobreposicao, SqlMigracaoCarteira.CriarProtecao);
    }

    [Fact]
    public void Erro_do_gatilho_vira_conflito_com_mensagem_da_carteira()
    {
        Assert.Equal(ConflitosEnderecoFinalidade.CarteiraSobreposta,
            ConflitosEnderecoFinalidade.Mensagem(SqlMigracaoCarteira.ErroSobreposicao, "..."));
    }

    // ---- Motor Comercial, Fase 1a ----

    [Fact]
    public void Gatilho_novo_confere_um_por_vez_e_limite_e_o_da_etapa_4_fica_congelado()
    {
        var novo = SqlMigracaoCarteira.CriarProtecaoPorLimite;
        Assert.Contains("CREATE TRIGGER " + SqlMigracaoCarteira.Gatilho + " ON CarteiraClientes", novo);
        Assert.Contains("THROW " + SqlMigracaoCarteira.ErroSobreposicao, novo);
        Assert.Contains("THROW " + SqlMigracaoCarteira.ErroAcimaDoLimite, novo);
        Assert.Contains("t.LimitePorVez = 1", novo);
        Assert.DoesNotContain("Principal", novo);
        // O texto da migração CarteiraSemSobreposicao não muda (é o que o Down da Fase 1a recria).
        Assert.Contains("t.Principal = 1", SqlMigracaoCarteira.CriarProtecao);
        Assert.Contains("WHERE ResponsavelDaConta = 1", SqlMigracaoCarteira.ConverterPolitica);
        Assert.Contains("DECLARE @faixas TABLE", novo); // só o que a gravação acrescenta
        Assert.Contains("IF NOT EXISTS (SELECT 1 FROM @faixas) RETURN;", novo);
    }

    [Fact]
    public void Papel_comercial_no_modelo_tem_a_politica_o_check_e_um_so_responsavel()
    {
        var papel = Modelo().FindEntityType(typeof(TipoCarteira))!;
        Assert.Contains(papel.GetCheckConstraints(), c => c.ModelName == SqlMigracaoCarteira.CheckPolitica && c.Sql == SqlMigracaoCarteira.RegraPolitica);
        Assert.Contains(papel.GetIndexes(), i => i.IsUnique && i.GetFilter() == "[ResponsavelDaConta] = 1");
        Assert.Equal(typeof(byte), papel.FindProperty(nameof(TipoCarteira.TipoCredito))!.GetProviderClrType());
        Assert.Null(papel.FindProperty("Principal"));

        var vinculo = Modelo().FindEntityType(typeof(CarteiraCliente))!;
        Assert.Equal(2, vinculo.FindProperty(nameof(CarteiraCliente.PercentualCredito))!.GetScale());
        Assert.Equal(typeof(byte), vinculo.FindProperty(nameof(CarteiraCliente.Origem))!.GetProviderClrType());
    }

    [Fact]
    public void Erro_de_limite_do_gatilho_vira_conflito_com_mensagem()
    {
        Assert.Equal(ConflitosEnderecoFinalidade.CarteiraAcimaDoLimite,
            ConflitosEnderecoFinalidade.Mensagem(SqlMigracaoCarteira.ErroAcimaDoLimite, "..."));
    }

    [Fact]
    public void Migracao_renomeia_converte_antes_do_check_e_troca_o_gatilho()
    {
        var up = new Lone.Infrastructure.Persistencia.Migracoes.PapeisComerciais().UpOperations;
        int Posicao<T>(Func<T, bool> qual) where T : Microsoft.EntityFrameworkCore.Migrations.Operations.MigrationOperation =>
            up.Select((o, i) => (o, i)).Single(x => x.o is T t && qual(t)).i;
        int Sql(string texto) => Posicao<Microsoft.EntityFrameworkCore.Migrations.Operations.SqlOperation>(o => o.Sql == texto);

        var renomeia = Posicao<Microsoft.EntityFrameworkCore.Migrations.Operations.RenameColumnOperation>(o => o.Name == "Principal" && o.NewName == "ResponsavelDaConta");
        var check = Posicao<Microsoft.EntityFrameworkCore.Migrations.Operations.AddCheckConstraintOperation>(o => o.Name == SqlMigracaoCarteira.CheckPolitica);

        Assert.True(Sql(SqlMigracaoCarteira.RemoverProtecao) < renomeia);
        Assert.True(renomeia < Sql(SqlMigracaoCarteira.ConverterPolitica));
        Assert.True(Sql(SqlMigracaoCarteira.ConverterPolitica) < check);
        Assert.True(check < Sql(SqlMigracaoCarteira.CriarProtecaoPorLimite));
        Assert.DoesNotContain(up, o => o is Microsoft.EntityFrameworkCore.Migrations.Operations.DropColumnOperation);
        Assert.DoesNotContain(up, o => o is Microsoft.EntityFrameworkCore.Migrations.Operations.UpdateDataOperation);

        var down = new Lone.Infrastructure.Persistencia.Migracoes.PapeisComerciais().DownOperations;
        Assert.Equal(SqlMigracaoCarteira.RemoverProtecao, Assert.IsType<Microsoft.EntityFrameworkCore.Migrations.Operations.SqlOperation>(down[0]).Sql);
        Assert.Equal(SqlMigracaoCarteira.CriarProtecao, Assert.IsType<Microsoft.EntityFrameworkCore.Migrations.Operations.SqlOperation>(down[^1]).Sql);
    }

    [Fact]
    public void Quem_pode_ser_tem_tabela_propria_unica_por_papel_e_classificacao_e_comeca_com_vendedor_e_representante()
    {
        var entidade = Modelo().FindEntityType(typeof(TipoCarteiraClassificacao))!;
        Assert.Equal("TiposCarteiraClassificacoes", entidade.GetTableName());
        Assert.Contains(entidade.GetIndexes(), i => i.IsUnique &&
            i.Properties.Select(p => p.Name).SequenceEqual([nameof(TipoCarteiraClassificacao.TipoCarteiraId), nameof(TipoCarteiraClassificacao.PapelId)]));

        var sql = SqlMigracaoCarteira.ClassificacoesIniciais;
        Assert.Contains(Lone.Domain.Papeis.PapeisSistema.Id(Lone.Domain.Enums.TipoPapel.Vendedor).ToString(), sql);
        Assert.Contains(Lone.Domain.Papeis.PapeisSistema.Id(Lone.Domain.Enums.TipoPapel.Representante).ToString(), sql);
        Assert.Contains("NOT EXISTS", sql); // não duplica
    }

    [Fact]
    public void Migracao_quem_pode_ser_cria_a_tabela_e_depois_marca_vendedor_e_representante()
    {
        var up = new Lone.Infrastructure.Persistencia.Migracoes.QuemPodeSerPapel().UpOperations.ToList();
        var cria = up.FindIndex(o => o is Microsoft.EntityFrameworkCore.Migrations.Operations.CreateTableOperation t && t.Name == "TiposCarteiraClassificacoes");
        var marca = up.FindIndex(o => o is Microsoft.EntityFrameworkCore.Migrations.Operations.SqlOperation s && s.Sql == SqlMigracaoCarteira.ClassificacoesIniciais);
        Assert.True(cria >= 0 && marca > cria);
    }
}
