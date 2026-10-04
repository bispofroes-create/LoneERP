using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Lone.Infrastructure.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class PrazosPeriodo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PrazosPeriodo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantidade = table.Column<int>(type: "int", nullable: false),
                    Unidade = table.Column<int>(type: "int", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrazosPeriodo", x => x.Id);
                    table.CheckConstraint("CK_PrazosPeriodo_Quantidade", "[Quantidade] >= 1");
                });

            migrationBuilder.InsertData(
                table: "PrazosPeriodo",
                columns: new[] { "Id", "Ativo", "AtualizadoEm", "CriadoEm", "Quantidade", "Unidade" },
                values: new object[,]
                {
                    { new Guid("5a2e0d10-0000-0000-0000-000000000001"), true, null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), 7, 1 },
                    { new Guid("5a2e0d10-0000-0000-0000-000000000002"), true, null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), 15, 1 },
                    { new Guid("5a2e0d10-0000-0000-0000-000000000003"), true, null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), 30, 1 },
                    { new Guid("5a2e0d10-0000-0000-0000-000000000004"), true, null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), 60, 1 },
                    { new Guid("5a2e0d10-0000-0000-0000-000000000005"), true, null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), 90, 1 },
                    { new Guid("5a2e0d10-0000-0000-0000-000000000006"), true, null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), 180, 1 },
                    { new Guid("5a2e0d10-0000-0000-0000-000000000007"), true, null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), 1, 3 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrazosPeriodo_Quantidade_Unidade",
                table: "PrazosPeriodo",
                columns: new[] { "Quantidade", "Unidade" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrazosPeriodo");
        }
    }
}
