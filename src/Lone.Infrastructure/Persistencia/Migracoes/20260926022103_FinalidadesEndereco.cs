using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Lone.Infrastructure.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class FinalidadesEndereco : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RevisarFinalidadesEndereco",
                table: "Pessoas",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "MescladoEmId",
                table: "PessoaEnderecos",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "RevisaoMigracao",
                table: "PessoaEnderecos",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_PessoaEnderecos_Id_PessoaId",
                table: "PessoaEnderecos",
                columns: new[] { "Id", "PessoaId" });

            migrationBuilder.CreateTable(
                name: "FinalidadesEndereco",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Codigo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false, collation: "Latin1_General_CI_AI"),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    DoSistema = table.Column<bool>(type: "bit", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinalidadesEndereco", x => x.Id);
                    table.CheckConstraint("CK_FinalidadesEndereco_SistemaAtiva", "[DoSistema] = 0 OR [Ativo] = 1");
                });

            migrationBuilder.CreateTable(
                name: "PessoaEnderecoFinalidades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PessoaEnderecoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinalidadeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Principal = table.Column<bool>(type: "bit", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PessoaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PessoaEnderecoFinalidades", x => x.Id);
                    table.CheckConstraint("CK_PessoaEnderecoFinalidades_PrincipalAtivo", "[Principal] = 0 OR [Ativo] = 1");
                    table.ForeignKey(
                        name: "FK_PessoaEnderecoFinalidades_FinalidadesEndereco_FinalidadeId",
                        column: x => x.FinalidadeId,
                        principalTable: "FinalidadesEndereco",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PessoaEnderecoFinalidades_PessoaEnderecos_PessoaEnderecoId_PessoaId",
                        columns: x => new { x.PessoaEnderecoId, x.PessoaId },
                        principalTable: "PessoaEnderecos",
                        principalColumns: new[] { "Id", "PessoaId" });
                    table.ForeignKey(
                        name: "FK_PessoaEnderecoFinalidades_Pessoas_PessoaId",
                        column: x => x.PessoaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "FinalidadesEndereco",
                columns: new[] { "Id", "Ativo", "AtualizadoEm", "Codigo", "CriadoEm", "DoSistema", "Nome", "Ordem" },
                values: new object[,]
                {
                    { new Guid("7a9e1c07-0000-0000-0000-000000000001"), true, null, "COMERCIAL", new DateTime(2026, 9, 26, 0, 0, 0, 0, DateTimeKind.Utc), true, "Comercial", 1 },
                    { new Guid("7a9e1c07-0000-0000-0000-000000000002"), true, null, "RESIDENCIAL", new DateTime(2026, 9, 26, 0, 0, 0, 0, DateTimeKind.Utc), true, "Residencial", 2 },
                    { new Guid("7a9e1c07-0000-0000-0000-000000000003"), true, null, "FISCAL", new DateTime(2026, 9, 26, 0, 0, 0, 0, DateTimeKind.Utc), true, "Fiscal", 3 },
                    { new Guid("7a9e1c07-0000-0000-0000-000000000004"), true, null, "ENTREGA", new DateTime(2026, 9, 26, 0, 0, 0, 0, DateTimeKind.Utc), true, "Entrega", 4 },
                    { new Guid("7a9e1c07-0000-0000-0000-000000000005"), true, null, "COBRANCA", new DateTime(2026, 9, 26, 0, 0, 0, 0, DateTimeKind.Utc), true, "Cobrança", 5 },
                    { new Guid("7a9e1c07-0000-0000-0000-000000000006"), true, null, "CORRESPONDENCIA", new DateTime(2026, 9, 26, 0, 0, 0, 0, DateTimeKind.Utc), true, "Correspondência", 6 }
                });

            migrationBuilder.Sql(SqlMigracaoFinalidadesEndereco.MigrarFinalidades);


            migrationBuilder.CreateIndex(
                name: "IX_PessoaEnderecos_MescladoEmId_PessoaId",
                table: "PessoaEnderecos",
                columns: new[] { "MescladoEmId", "PessoaId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_PessoaEnderecos_Consolidado",
                table: "PessoaEnderecos",
                sql: "[MescladoEmId] IS NULL OR ([Ativo] = 0 AND [MescladoEmId] <> [Id])");

            migrationBuilder.CreateIndex(
                name: "IX_FinalidadesEndereco_Codigo",
                table: "FinalidadesEndereco",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinalidadesEndereco_Nome",
                table: "FinalidadesEndereco",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PessoaEnderecoFinalidades_FinalidadeId",
                table: "PessoaEnderecoFinalidades",
                column: "FinalidadeId");

            migrationBuilder.CreateIndex(
                name: "IX_PessoaEnderecoFinalidades_PessoaEnderecoId_FinalidadeId",
                table: "PessoaEnderecoFinalidades",
                columns: new[] { "PessoaEnderecoId", "FinalidadeId" },
                unique: true,
                filter: "[Ativo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_PessoaEnderecoFinalidades_PessoaEnderecoId_PessoaId",
                table: "PessoaEnderecoFinalidades",
                columns: new[] { "PessoaEnderecoId", "PessoaId" });

            migrationBuilder.CreateIndex(
                name: "IX_PessoaEnderecoFinalidades_PessoaId_FinalidadeId",
                table: "PessoaEnderecoFinalidades",
                columns: new[] { "PessoaId", "FinalidadeId" },
                unique: true,
                filter: "[Principal] = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_PessoaEnderecos_PessoaEnderecos_MescladoEmId_PessoaId",
                table: "PessoaEnderecos",
                columns: new[] { "MescladoEmId", "PessoaId" },
                principalTable: "PessoaEnderecos",
                principalColumns: new[] { "Id", "PessoaId" });

            migrationBuilder.Sql(SqlMigracaoFinalidadesEndereco.CriarProtecoes);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SqlMigracaoFinalidadesEndereco.RemoverProtecoes);

            migrationBuilder.DropForeignKey(
                name: "FK_PessoaEnderecos_PessoaEnderecos_MescladoEmId_PessoaId",
                table: "PessoaEnderecos");

            migrationBuilder.DropTable(
                name: "PessoaEnderecoFinalidades");

            migrationBuilder.DropTable(
                name: "FinalidadesEndereco");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_PessoaEnderecos_Id_PessoaId",
                table: "PessoaEnderecos");

            migrationBuilder.DropIndex(
                name: "IX_PessoaEnderecos_MescladoEmId_PessoaId",
                table: "PessoaEnderecos");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PessoaEnderecos_Consolidado",
                table: "PessoaEnderecos");

            migrationBuilder.DropColumn(
                name: "RevisarFinalidadesEndereco",
                table: "Pessoas");

            migrationBuilder.DropColumn(
                name: "MescladoEmId",
                table: "PessoaEnderecos");

            migrationBuilder.DropColumn(
                name: "RevisaoMigracao",
                table: "PessoaEnderecos");
        }
    }
}
