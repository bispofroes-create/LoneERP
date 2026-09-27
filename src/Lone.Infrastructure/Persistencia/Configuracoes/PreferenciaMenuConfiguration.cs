using Lone.Contracts.Menu;
using Lone.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lone.Infrastructure.Persistencia.Configuracoes;

/// <summary>Favoritos e recentes do menu: uma linha por usuário + rota (índice único), ligada ao usuário.</summary>
public class PreferenciaMenuConfiguration : IEntityTypeConfiguration<PreferenciaMenu>
{
    public const string IndiceUsuarioRota = "IX_PreferenciasMenu_UsuarioId_Rota";

    public void Configure(EntityTypeBuilder<PreferenciaMenu> b)
    {
        b.ToTable("PreferenciasMenu");
        b.HasKey(x => x.Id);
        b.Property(x => x.Rota).IsRequired().HasMaxLength(LimitesMenu.TamanhoMaximoRota).IsUnicode(false);
        b.HasIndex(x => new { x.UsuarioId, x.Rota }).IsUnique().HasDatabaseName(IndiceUsuarioRota);

        // Dado técnico do próprio usuário, como UsuarioAcessos (usuários não são excluídos; só desativados).
        b.HasOne<Usuario>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Cascade);
    }
}
