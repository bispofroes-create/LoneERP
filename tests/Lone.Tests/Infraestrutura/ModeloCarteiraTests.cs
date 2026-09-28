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
}
