using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lone.Infrastructure.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class Fase2b1bNumeracao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NumeracoesDocumento",
                columns: table => new
                {
                    Prefixo = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    Ano = table.Column<int>(type: "int", nullable: false),
                    Ultimo = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NumeracoesDocumento", x => new { x.Prefixo, x.Ano });
                    table.CheckConstraint("CK_NumeracoesDocumento_Ultimo", "[Ultimo] >= 0");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NumeracoesDocumento");
        }
    }
}
