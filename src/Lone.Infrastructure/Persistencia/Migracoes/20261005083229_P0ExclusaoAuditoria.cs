using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lone.Infrastructure.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class P0ExclusaoAuditoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Ativo",
                table: "PessoaSocios",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "SaiuEm",
                table: "PessoaSocios",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Ativo",
                table: "PessoaContatos",
                type: "bit",
                nullable: false,
                defaultValue: true);

            // P0 (D4): a tabela Auditoria só aceita inclusão. Só estrutura: não mexe em dados.
            migrationBuilder.Sql(SqlMigracaoAuditoria.CriarProtecao);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SqlMigracaoAuditoria.RemoverProtecao);

            migrationBuilder.DropColumn(
                name: "Ativo",
                table: "PessoaSocios");

            migrationBuilder.DropColumn(
                name: "SaiuEm",
                table: "PessoaSocios");

            migrationBuilder.DropColumn(
                name: "Ativo",
                table: "PessoaContatos");
        }
    }
}
