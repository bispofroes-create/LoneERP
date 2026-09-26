using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Lone.Infrastructure.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class PrivacidadeConsentimentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PessoaConsentimentos_PessoaId_Canal",
                table: "PessoaConsentimentos");

            migrationBuilder.AlterColumn<byte>(
                name: "Canal",
                table: "PessoaConsentimentos",
                type: "tinyint",
                nullable: true,
                oldClrType: typeof(byte),
                oldType: "tinyint");

            migrationBuilder.AddColumn<string>(
                name: "ConcedidoPor",
                table: "PessoaConsentimentos",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FinalidadeId",
                table: "PessoaConsentimentos",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "Motivo",
                table: "PessoaConsentimentos",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoRevogacao",
                table: "PessoaConsentimentos",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RevogadoPor",
                table: "PessoaConsentimentos",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VersaoTermo",
                table: "PessoaConsentimentos",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FinalidadesTratamento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Codigo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false, collation: "Latin1_General_CI_AI"),
                    Descricao = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    BaseLegal = table.Column<byte>(type: "tinyint", nullable: false),
                    ClassificacaoExigida = table.Column<byte>(type: "tinyint", nullable: false),
                    SomenteHistorico = table.Column<bool>(type: "bit", nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    DoSistema = table.Column<bool>(type: "bit", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinalidadesTratamento", x => x.Id);
                    table.CheckConstraint("CK_FinalidadesTratamento_SistemaAtiva", "[DoSistema] = 0 OR [Ativo] = 1");
                });

            migrationBuilder.InsertData(
                table: "FinalidadesTratamento",
                columns: new[] { "Id", "Ativo", "AtualizadoEm", "BaseLegal", "ClassificacaoExigida", "Codigo", "CriadoEm", "Descricao", "DoSistema", "Nome", "Ordem", "SomenteHistorico" },
                values: new object[,]
                {
                    { new Guid("7a9e1c08-0000-0000-0000-000000000001"), true, null, (byte)1, (byte)1, "MARKETING", new DateTime(2026, 9, 26, 0, 0, 0, 0, DateTimeKind.Utc), "Envio de ofertas, novidades e campanhas.", true, "Marketing", 1, false },
                    { new Guid("7a9e1c08-0000-0000-0000-000000000099"), true, null, (byte)0, (byte)0, "REGISTRO_ANTERIOR", new DateTime(2026, 9, 26, 0, 0, 0, 0, DateTimeKind.Utc), "Consentimentos registrados antes do cadastro de finalidades (por canal, sem finalidade). Somente histórico: não autoriza nenhuma comunicação.", true, "Registro anterior", 99, true }
                });

            migrationBuilder.CreateIndex(
                name: "IX_PessoaConsentimentos_EmVigor",
                table: "PessoaConsentimentos",
                columns: new[] { "PessoaId", "FinalidadeId", "Canal" },
                unique: true,
                filter: "[Concedido] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_PessoaConsentimentos_FinalidadeId",
                table: "PessoaConsentimentos",
                column: "FinalidadeId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PessoaConsentimentos_RevogadoForaDeVigor",
                table: "PessoaConsentimentos",
                sql: "[Concedido] = 0 OR [RevogadoEm] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FinalidadesTratamento_Codigo",
                table: "FinalidadesTratamento",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinalidadesTratamento_Nome",
                table: "FinalidadesTratamento",
                column: "Nome",
                unique: true);

            migrationBuilder.Sql(SqlMigracaoPrivacidade.ConsentimentosAnteriores);

            migrationBuilder.AddForeignKey(
                name: "FK_PessoaConsentimentos_FinalidadesTratamento_FinalidadeId",
                table: "PessoaConsentimentos",
                column: "FinalidadeId",
                principalTable: "FinalidadesTratamento",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PessoaConsentimentos_FinalidadesTratamento_FinalidadeId",
                table: "PessoaConsentimentos");

            migrationBuilder.DropTable(
                name: "FinalidadesTratamento");

            migrationBuilder.DropIndex(
                name: "IX_PessoaConsentimentos_EmVigor",
                table: "PessoaConsentimentos");

            migrationBuilder.DropIndex(
                name: "IX_PessoaConsentimentos_FinalidadeId",
                table: "PessoaConsentimentos");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PessoaConsentimentos_RevogadoForaDeVigor",
                table: "PessoaConsentimentos");

            migrationBuilder.DropColumn(
                name: "ConcedidoPor",
                table: "PessoaConsentimentos");

            migrationBuilder.DropColumn(
                name: "FinalidadeId",
                table: "PessoaConsentimentos");

            migrationBuilder.DropColumn(
                name: "Motivo",
                table: "PessoaConsentimentos");

            migrationBuilder.DropColumn(
                name: "MotivoRevogacao",
                table: "PessoaConsentimentos");

            migrationBuilder.DropColumn(
                name: "RevogadoPor",
                table: "PessoaConsentimentos");

            migrationBuilder.DropColumn(
                name: "VersaoTermo",
                table: "PessoaConsentimentos");

            migrationBuilder.AlterColumn<byte>(
                name: "Canal",
                table: "PessoaConsentimentos",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0,
                oldClrType: typeof(byte),
                oldType: "tinyint",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PessoaConsentimentos_PessoaId_Canal",
                table: "PessoaConsentimentos",
                columns: new[] { "PessoaId", "Canal" },
                unique: true);
        }
    }
}
