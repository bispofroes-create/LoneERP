using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Lone.Infrastructure.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class EstruturaEmpresarial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GrupoEmpresarialId",
                table: "Pessoas",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Ativo",
                table: "PessoaRelacionamentos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "CondicaoPagamentoId",
                table: "ContasFornecedor",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GruposEmpresariais",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_CI_AI"),
                    Descricao = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GruposEmpresariais", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "TiposRelacionamento",
                columns: new[] { "Id", "AtualizadoEm", "CriadoEm", "Nome", "NomeInverso", "Sistema" },
                values: new object[,]
                {
                    { new Guid("5a0e6f10-0000-0000-0000-000000000007"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Administrador de", "Tem como administrador", true },
                    { new Guid("5a0e6f10-0000-0000-0000-000000000008"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Parceiro de", "Parceiro de", true }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Pessoas_GrupoEmpresarialId",
                table: "Pessoas",
                column: "GrupoEmpresarialId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Pessoas_GrupoEmpresarialSoPJ",
                table: "Pessoas",
                sql: "[GrupoEmpresarialId] IS NULL OR [Natureza] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_PessoaRelacionamentos_Aberto",
                table: "PessoaRelacionamentos",
                columns: new[] { "PessoaId", "PessoaDestinoId", "TipoRelacionamentoId" },
                unique: true,
                filter: "[Ativo] = 1 AND [FimEm] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ContasFornecedor_CondicaoPagamentoId",
                table: "ContasFornecedor",
                column: "CondicaoPagamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_GruposEmpresariais_Nome",
                table: "GruposEmpresariais",
                column: "Nome",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ContasFornecedor_CondicoesPagamento_CondicaoPagamentoId",
                table: "ContasFornecedor",
                column: "CondicaoPagamentoId",
                principalTable: "CondicoesPagamento",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Pessoas_GruposEmpresariais_GrupoEmpresarialId",
                table: "Pessoas",
                column: "GrupoEmpresarialId",
                principalTable: "GruposEmpresariais",
                principalColumn: "Id");

            migrationBuilder.Sql(SqlMigracaoEstruturaEmpresarial.Dados);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContasFornecedor_CondicoesPagamento_CondicaoPagamentoId",
                table: "ContasFornecedor");

            migrationBuilder.DropForeignKey(
                name: "FK_Pessoas_GruposEmpresariais_GrupoEmpresarialId",
                table: "Pessoas");

            migrationBuilder.DropTable(
                name: "GruposEmpresariais");

            migrationBuilder.DropIndex(
                name: "IX_Pessoas_GrupoEmpresarialId",
                table: "Pessoas");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Pessoas_GrupoEmpresarialSoPJ",
                table: "Pessoas");

            migrationBuilder.DropIndex(
                name: "IX_PessoaRelacionamentos_Aberto",
                table: "PessoaRelacionamentos");

            migrationBuilder.DropIndex(
                name: "IX_ContasFornecedor_CondicaoPagamentoId",
                table: "ContasFornecedor");

            migrationBuilder.DeleteData(
                table: "TiposRelacionamento",
                keyColumn: "Id",
                keyValue: new Guid("5a0e6f10-0000-0000-0000-000000000007"));

            migrationBuilder.DeleteData(
                table: "TiposRelacionamento",
                keyColumn: "Id",
                keyValue: new Guid("5a0e6f10-0000-0000-0000-000000000008"));

            migrationBuilder.DropColumn(
                name: "GrupoEmpresarialId",
                table: "Pessoas");

            migrationBuilder.DropColumn(
                name: "Ativo",
                table: "PessoaRelacionamentos");

            migrationBuilder.DropColumn(
                name: "CondicaoPagamentoId",
                table: "ContasFornecedor");
        }
    }
}
