using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lone.Infrastructure.Persistencia.Migracoes
{
    /// <summary>
    /// Cadastro de etiquetas. Gerada pelo EF e REORDENADA À MÃO (se for gerada de novo, refazer):
    /// a tabela Etiquetas nasce primeiro; EtiquetaId entra aceitando nulo; o SQL de <see cref="SqlMigracaoEtiquetas"/>
    /// cria as etiquetas a partir dos textos e liga cada pessoa; só então EtiquetaId passa a obrigatório e entram
    /// os índices e a chave estrangeira. A coluna Texto fica (aceitando nulo) como cópia do dado original.
    /// </summary>
    public partial class CadastroEtiquetas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Etiquetas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false, collation: "Latin1_General_CI_AI"),
                    Descricao = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Etiquetas", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Etiquetas_Nome",
                table: "Etiquetas",
                column: "Nome",
                unique: true);

            migrationBuilder.DropIndex(
                name: "IX_PessoaEtiquetas_PessoaId_Texto",
                table: "PessoaEtiquetas");

            migrationBuilder.DropIndex(
                name: "IX_PessoaEtiquetas_Texto",
                table: "PessoaEtiquetas");

            migrationBuilder.AlterColumn<string>(
                name: "Texto",
                table: "PessoaEtiquetas",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(40)",
                oldMaxLength: 40);

            // Nula por enquanto: é preenchida pelo SQL abaixo.
            migrationBuilder.AddColumn<Guid>(
                name: "EtiquetaId",
                table: "PessoaEtiquetas",
                type: "uniqueidentifier",
                nullable: true);

            // Cria as etiquetas, liga as pessoas, confere as contagens (se não conferir, desfaz tudo).
            migrationBuilder.Sql(SqlMigracaoEtiquetas.CriarEtiquetasELigarPessoas);

            migrationBuilder.AlterColumn<Guid>(
                name: "EtiquetaId",
                table: "PessoaEtiquetas",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PessoaEtiquetas_EtiquetaId_PessoaId",
                table: "PessoaEtiquetas",
                columns: new[] { "EtiquetaId", "PessoaId" });

            migrationBuilder.CreateIndex(
                name: "IX_PessoaEtiquetas_PessoaId_EtiquetaId",
                table: "PessoaEtiquetas",
                columns: new[] { "PessoaId", "EtiquetaId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PessoaEtiquetas_Etiquetas_EtiquetaId",
                table: "PessoaEtiquetas",
                column: "EtiquetaId",
                principalTable: "Etiquetas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PessoaEtiquetas_Etiquetas_EtiquetaId",
                table: "PessoaEtiquetas");

            migrationBuilder.DropIndex(
                name: "IX_PessoaEtiquetas_EtiquetaId_PessoaId",
                table: "PessoaEtiquetas");

            migrationBuilder.DropIndex(
                name: "IX_PessoaEtiquetas_PessoaId_EtiquetaId",
                table: "PessoaEtiquetas");

            // Ligações feitas depois da migração ganham o texto (nome da etiqueta) antes de a referência sair.
            migrationBuilder.Sql(SqlMigracaoEtiquetas.DevolverTexto);

            migrationBuilder.DropColumn(
                name: "EtiquetaId",
                table: "PessoaEtiquetas");

            migrationBuilder.DropTable(
                name: "Etiquetas");

            migrationBuilder.AlterColumn<string>(
                name: "Texto",
                table: "PessoaEtiquetas",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(40)",
                oldMaxLength: 40,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PessoaEtiquetas_PessoaId_Texto",
                table: "PessoaEtiquetas",
                columns: new[] { "PessoaId", "Texto" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PessoaEtiquetas_Texto",
                table: "PessoaEtiquetas",
                column: "Texto");
        }
    }
}
