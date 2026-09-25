using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lone.Infrastructure.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class CadastroProfissoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProfissaoId",
                table: "Pessoas",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OcupacoesCbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, collation: "Latin1_General_CI_AI"),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OcupacoesCbo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Profissoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false, collation: "Latin1_General_CI_AI"),
                    Descricao = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    OcupacaoCboId = table.Column<int>(type: "int", nullable: true),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Profissoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Profissoes_OcupacoesCbo_OcupacaoCboId",
                        column: x => x.OcupacaoCboId,
                        principalTable: "OcupacoesCbo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Pessoas_ProfissaoId",
                table: "Pessoas",
                column: "ProfissaoId");

            migrationBuilder.CreateIndex(
                name: "IX_OcupacoesCbo_Ativo_Titulo",
                table: "OcupacoesCbo",
                columns: new[] { "Ativo", "Titulo" });

            migrationBuilder.CreateIndex(
                name: "IX_Profissoes_Nome",
                table: "Profissoes",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Profissoes_OcupacaoCboId",
                table: "Profissoes",
                column: "OcupacaoCboId");

            migrationBuilder.AddForeignKey(
                name: "FK_Pessoas_Profissoes_ProfissaoId",
                table: "Pessoas",
                column: "ProfissaoId",
                principalTable: "Profissoes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // AJUSTE MANUAL (refazer se a migração for gerada de novo): cria as profissões a partir dos textos
            // de Pessoas.Profissao e liga cada pessoa; confere as contagens (se não conferir, desfaz tudo).
            migrationBuilder.Sql(SqlMigracaoProfissoes.CriarProfissoesELigarPessoas);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Pessoas_Profissoes_ProfissaoId",
                table: "Pessoas");

            migrationBuilder.DropTable(
                name: "Profissoes");

            migrationBuilder.DropTable(
                name: "OcupacoesCbo");

            migrationBuilder.DropIndex(
                name: "IX_Pessoas_ProfissaoId",
                table: "Pessoas");

            migrationBuilder.DropColumn(
                name: "ProfissaoId",
                table: "Pessoas");
        }
    }
}
