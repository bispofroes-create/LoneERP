using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Lone.Infrastructure.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class MeiosContatoETipos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Ativo",
                table: "PessoaMeiosContato",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<short>(
                name: "Finalidades",
                table: "PessoaMeiosContato",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<string>(
                name: "Ramal",
                table: "PessoaMeiosContato",
                type: "varchar(10)",
                unicode: false,
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Sms",
                table: "PessoaMeiosContato",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "TipoMeioContatoId",
                table: "PessoaMeiosContato",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WhatsApp",
                table: "PessoaMeiosContato",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Ddd",
                table: "PessoaMeiosContato",
                type: "varchar(2)",
                unicode: false,
                maxLength: 2,
                nullable: true,
                computedColumnSql: "CAST(CASE WHEN [Tipo] IN (0, 1, 2) AND LEN([Valor]) IN (10, 11) AND LEFT([Valor], 1) NOT IN ('+', '0') THEN LEFT([Valor], 2) END AS varchar(2))",
                stored: true);

            migrationBuilder.CreateTable(
                name: "TiposMeioContato",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false, collation: "Latin1_General_CI_AI"),
                    Categoria = table.Column<byte>(type: "tinyint", nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TiposMeioContato", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "TiposMeioContato",
                columns: new[] { "Id", "Ativo", "AtualizadoEm", "Categoria", "CriadoEm", "Nome", "Ordem" },
                values: new object[,]
                {
                    { new Guid("7a9e1c01-0000-0000-0000-000000000001"), true, null, (byte)0, new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), "Comercial", 1 },
                    { new Guid("7a9e1c01-0000-0000-0000-000000000002"), true, null, (byte)0, new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), "Residencial", 2 },
                    { new Guid("7a9e1c01-0000-0000-0000-000000000003"), true, null, (byte)0, new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), "Pessoal", 3 },
                    { new Guid("7a9e1c01-0000-0000-0000-000000000004"), true, null, (byte)1, new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), "Comercial", 1 },
                    { new Guid("7a9e1c01-0000-0000-0000-000000000005"), true, null, (byte)1, new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), "Pessoal", 2 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_PessoaMeiosContato_Ddd",
                table: "PessoaMeiosContato",
                column: "Ddd");

            migrationBuilder.CreateIndex(
                name: "IX_PessoaMeiosContato_TipoMeioContatoId",
                table: "PessoaMeiosContato",
                column: "TipoMeioContatoId");

            migrationBuilder.CreateIndex(
                name: "IX_TiposMeioContato_Categoria_Nome",
                table: "TiposMeioContato",
                columns: new[] { "Categoria", "Nome" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PessoaMeiosContato_TiposMeioContato_TipoMeioContatoId",
                table: "PessoaMeiosContato",
                column: "TipoMeioContatoId",
                principalTable: "TiposMeioContato",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // AJUSTE MANUAL (refazer se a migração for gerada de novo): os telefones/e-mails existentes ficam ativos e o
            // antigo tipo "WhatsApp" vira celular com a marcação WhatsApp.
            migrationBuilder.Sql(SqlMigracaoMeiosContato.AtivarEConverterWhatsApp);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PessoaMeiosContato_TiposMeioContato_TipoMeioContatoId",
                table: "PessoaMeiosContato");

            migrationBuilder.DropTable(
                name: "TiposMeioContato");

            migrationBuilder.DropIndex(
                name: "IX_PessoaMeiosContato_Ddd",
                table: "PessoaMeiosContato");

            migrationBuilder.DropIndex(
                name: "IX_PessoaMeiosContato_TipoMeioContatoId",
                table: "PessoaMeiosContato");

            migrationBuilder.DropColumn(
                name: "Ddd",
                table: "PessoaMeiosContato");

            migrationBuilder.DropColumn(
                name: "Ativo",
                table: "PessoaMeiosContato");

            migrationBuilder.DropColumn(
                name: "Finalidades",
                table: "PessoaMeiosContato");

            migrationBuilder.DropColumn(
                name: "Ramal",
                table: "PessoaMeiosContato");

            migrationBuilder.DropColumn(
                name: "Sms",
                table: "PessoaMeiosContato");

            migrationBuilder.DropColumn(
                name: "TipoMeioContatoId",
                table: "PessoaMeiosContato");

            migrationBuilder.DropColumn(
                name: "WhatsApp",
                table: "PessoaMeiosContato");
        }
    }
}
