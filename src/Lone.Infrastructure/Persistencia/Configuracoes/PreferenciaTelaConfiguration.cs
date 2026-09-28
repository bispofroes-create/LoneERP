using Lone.Contracts.Menu;
using Lone.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lone.Infrastructure.Persistencia.Configuracoes;

/// <summary>Preferências de tela: uma linha por usuário + tela (índice único), ligada ao usuário.</summary>
public class PreferenciaTelaConfiguration : IEntityTypeConfiguration<PreferenciaTela>
{
    public const string IndiceUsuarioTela = "IX_PreferenciasTela_UsuarioId_Tela";

    public void Configure(EntityTypeBuilder<PreferenciaTela> b)
    {
        b.ToTable("PreferenciasTela");
        b.HasKey(x => x.Id);
        b.Property(x => x.Tela).IsRequired().HasMaxLength(LimitesMenu.TamanhoMaximoRota).IsUnicode(false);
        b.Property(x => x.Conteudo).IsRequired().HasMaxLength(LimitesMenu.TamanhoMaximoPreferenciaTela);
        b.HasIndex(x => new { x.UsuarioId, x.Tela }).IsUnique().HasDatabaseName(IndiceUsuarioTela);

        // Dado técnico do próprio usuário, como PreferenciasMenu (usuários não são excluídos; só desativados).
        b.HasOne<Usuario>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Cascade);
    }
}
