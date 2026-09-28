using Lone.Domain.Entidades;
using Lone.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Lone.Tests.Infraestrutura;

/// <summary>
/// Banco da Fase 2a (escopo e equipes): usuário ligado a uma pessoa (no máximo um usuário por pessoa), alcance no perfil,
/// equipe acima e papel do membro, e o SQL que transforma o líder gravado em membro com papel Líder. Não abre conexão.
/// </summary>
public class ModeloEquipesTests
{
    private static IModel Modelo()
    {
        using var db = new LoneDbContext(new DbContextOptionsBuilder<LoneDbContext>()
            .UseSqlServer("Server=(sem-conexao);Database=Lone;Trusted_Connection=True").Options);
        return db.GetService<IDesignTimeModel>().Model;
    }

    [Fact]
    public void Usuario_tem_pessoa_opcional_unica_e_sem_exclusao_em_cascata()
    {
        var usuario = Modelo().FindEntityType(typeof(Usuario))!;
        Assert.True(usuario.FindProperty(nameof(Usuario.PessoaId))!.IsNullable);

        var fk = usuario.GetForeignKeys().Single(f => f.PrincipalEntityType.ClrType == typeof(Pessoa));
        Assert.Equal(DeleteBehavior.NoAction, fk.DeleteBehavior);

        var indice = usuario.GetIndexes().Single(i => i.Properties.Select(p => p.Name).SequenceEqual([nameof(Usuario.PessoaId)]));
        Assert.True(indice.IsUnique);
        Assert.Equal("[PessoaId] IS NOT NULL", indice.GetFilter());
    }

    [Fact]
    public void Alcance_do_perfil_e_papel_do_membro_sao_gravados_como_byte()
    {
        var modelo = Modelo();
        Assert.Equal(typeof(byte), modelo.FindEntityType(typeof(Perfil))!.FindProperty(nameof(Perfil.AlcanceComercial))!.GetProviderClrType());
        Assert.Equal(typeof(byte), modelo.FindEntityType(typeof(MembroEquipe))!.FindProperty(nameof(MembroEquipe.Papel))!.GetProviderClrType());
    }

    [Fact]
    public void Equipe_acima_aponta_para_equipe_sem_cascata()
    {
        var equipe = Modelo().FindEntityType(typeof(Equipe))!;
        var fk = equipe.GetForeignKeys().Single(f => f.Properties.Single().Name == nameof(Equipe.EquipePaiId));
        Assert.Equal(typeof(Equipe), fk.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.NoAction, fk.DeleteBehavior);
        Assert.True(equipe.FindProperty(nameof(Equipe.EquipePaiId))!.IsNullable);
    }

    [Fact]
    public void Sql_do_lider_so_inclui_e_marca_sem_apagar_nada()
    {
        var sql = SqlMigracaoEquipes.LiderComoMembro;
        Assert.Contains("UPDATE m SET Papel = 1", sql);
        Assert.Contains("m.FimEm IS NULL", sql);
        Assert.Contains("INSERT INTO MembrosEquipe (Id, EquipeId, PessoaId, InicioEm, FimEm, Papel, CriadoEm)", sql);
        Assert.Contains("NOT EXISTS", sql); // pode rodar de novo sem duplicar
        Assert.Contains("DATEADD(day, 1, ult.UltimaSaida)", sql); // não sobrepõe a última participação
        Assert.DoesNotContain("DELETE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP", sql, StringComparison.OrdinalIgnoreCase);
    }
}
