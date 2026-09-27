using System.Reflection;
using Lone.Contracts.Menu;
using Lone.Domain.Auditoria;
using Lone.Domain.Entidades;
using Lone.Infrastructure.Persistencia;
using Lone.Infrastructure.Persistencia.Configuracoes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Lone.Tests.Infraestrutura;

/// <summary>Favoritos e recentes do menu no MODELO do EF (o que a migration gera), sem banco.</summary>
public class ModeloPreferenciasMenuTests
{
    private static readonly IModel Modelo = CriarModelo();

    private static IModel CriarModelo()
    {
        using var db = new LoneDbContext(new DbContextOptionsBuilder<LoneDbContext>()
            .UseSqlServer("Server=(sem-conexao);Database=Lone;Trusted_Connection=True").Options);
        return db.GetService<IDesignTimeModel>().Model;
    }

    [Fact]
    public void Uma_linha_por_usuario_e_rota_ligada_ao_usuario_e_fora_da_auditoria()
    {
        var entidade = Modelo.FindEntityType(typeof(PreferenciaMenu))!;

        Assert.Equal("PreferenciasMenu", entidade.GetTableName());
        var indice = entidade.GetIndexes().Single(i => i.GetDatabaseName() == PreferenciaMenuConfiguration.IndiceUsuarioRota);
        Assert.True(indice.IsUnique);
        Assert.Equal(new[] { nameof(PreferenciaMenu.UsuarioId), nameof(PreferenciaMenu.Rota) }, indice.Properties.Select(p => p.Name));
        Assert.Null(indice.GetFilter()); // único entre todas as linhas: nada é apagado, desfavoritar só desmarca

        var rota = entidade.FindProperty(nameof(PreferenciaMenu.Rota))!;
        Assert.Equal(LimitesMenu.TamanhoMaximoRota, rota.GetMaxLength());
        Assert.False(rota.IsNullable);

        var usuario = entidade.GetForeignKeys().Single();
        Assert.Equal(typeof(Usuario), usuario.PrincipalEntityType.ClrType);
        Assert.Equal(nameof(PreferenciaMenu.UsuarioId), usuario.Properties.Single().Name);

        Assert.NotNull(typeof(PreferenciaMenu).GetCustomAttribute<NaoAuditarAttribute>()); // muda a cada tela aberta
    }
}
