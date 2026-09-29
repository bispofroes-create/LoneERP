using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lone.Infrastructure.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class Fase2b1ResponsaveisSemSobreposicao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fase 2b-1a (aprovado em 29/09/2026): responsáveis pelo território sem períodos sobrepostos também no banco
            // (mesmo território + função + pessoa ou equipe). Sem operação de esquema (o modelo só declara o gatilho); não
            // mexe em dados. SQL em SqlMigracaoTerritorios. A migração Fase2b1Territorios já estava aplicada: por isso aqui.
            migrationBuilder.Sql(SqlMigracaoTerritorios.CriarProtecao);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SqlMigracaoTerritorios.RemoverProtecao);
        }
    }
}
