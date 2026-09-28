using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lone.Infrastructure.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class CarteiraSemSobreposicao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Etapa 4 (D3, aprovada em 28/09/2026): a regra "um principal/exclusivo por vez" da carteira também no banco.
            // Sem operação de esquema (o modelo só declara o gatilho); não mexe em dados. SQL em SqlMigracaoCarteira.
            migrationBuilder.Sql(SqlMigracaoCarteira.CriarProtecao);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SqlMigracaoCarteira.RemoverProtecao);
        }
    }
}
