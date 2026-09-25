using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Lone.Infrastructure.Persistencia.Migracoes
{
    /// <summary>
    /// Cadastro de papéis. Gerada pelo EF e REORDENADA À MÃO (se for gerada de novo, refazer): a tabela Papeis e os oito
    /// papéis de sistema nascem primeiro; PapelId entra aceitando nulo; o SQL de <see cref="SqlMigracaoPapeis"/> liga cada
    /// período ao papel; só então PapelId passa a obrigatório e entram os índices e a chave estrangeira.
    /// </summary>
    public partial class CadastroPapeis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Papeis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Codigo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false, collation: "Latin1_General_CI_AI"),
                    Descricao = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    PapelSistema = table.Column<byte>(type: "tinyint", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Papeis", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Papeis",
                columns: new[] { "Id", "Ativo", "AtualizadoEm", "Codigo", "CriadoEm", "Descricao", "Nome", "Ordem", "PapelSistema" },
                values: new object[,]
                {
                    { new Guid("7a9e1c00-0000-0000-0000-000000000001"), true, null, "CLIENTE", new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), null, "Cliente", 1, (byte)1 },
                    { new Guid("7a9e1c00-0000-0000-0000-000000000002"), true, null, "FORNECEDOR", new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), null, "Fornecedor", 2, (byte)2 },
                    { new Guid("7a9e1c00-0000-0000-0000-000000000003"), true, null, "EMPRESA_DO_GRUPO", new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), null, "Empresa do grupo", 8, (byte)3 },
                    { new Guid("7a9e1c00-0000-0000-0000-000000000004"), true, null, "VENDEDOR", new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), null, "Vendedor", 3, (byte)4 },
                    { new Guid("7a9e1c00-0000-0000-0000-000000000005"), true, null, "FUNCIONARIO", new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), null, "Funcionário", 7, (byte)5 },
                    { new Guid("7a9e1c00-0000-0000-0000-000000000006"), true, null, "TRANSPORTADORA", new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), null, "Transportadora", 4, (byte)6 },
                    { new Guid("7a9e1c00-0000-0000-0000-000000000007"), true, null, "REPRESENTANTE", new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), null, "Representante", 5, (byte)7 },
                    { new Guid("7a9e1c00-0000-0000-0000-000000000008"), true, null, "PRESTADOR_SERVICO", new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), null, "Prestador de serviço", 6, (byte)8 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Papeis_Codigo",
                table: "Papeis",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Papeis_Nome",
                table: "Papeis",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Papeis_PapelSistema",
                table: "Papeis",
                column: "PapelSistema",
                unique: true,
                filter: "[PapelSistema] IS NOT NULL");

            migrationBuilder.DropIndex(
                name: "IX_PessoaPapeis_PessoaId_Papel",
                table: "PessoaPapeis");

            migrationBuilder.AlterColumn<byte>(
                name: "Papel",
                table: "PessoaPapeis",
                type: "tinyint",
                nullable: true,
                oldClrType: typeof(byte),
                oldType: "tinyint");

            // Nula por enquanto: é preenchida pelo SQL abaixo (cada período ligado ao papel de sistema correspondente).
            migrationBuilder.AddColumn<Guid>(
                name: "PapelId",
                table: "PessoaPapeis",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(SqlMigracaoPapeis.LigarPeriodosAosPapeis);

            migrationBuilder.AlterColumn<Guid>(
                name: "PapelId",
                table: "PessoaPapeis",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PessoaPapeis_PapelId_Ativo",
                table: "PessoaPapeis",
                columns: new[] { "PapelId", "Ativo" });

            migrationBuilder.CreateIndex(
                name: "IX_PessoaPapeis_PessoaId_PapelId",
                table: "PessoaPapeis",
                columns: new[] { "PessoaId", "PapelId" },
                unique: true,
                filter: "[Ativo] = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_PessoaPapeis_Papeis_PapelId",
                table: "PessoaPapeis",
                column: "PapelId",
                principalTable: "Papeis",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PessoaPapeis_Papeis_PapelId",
                table: "PessoaPapeis");

            migrationBuilder.DropTable(
                name: "Papeis");

            migrationBuilder.DropIndex(
                name: "IX_PessoaPapeis_PapelId_Ativo",
                table: "PessoaPapeis");

            migrationBuilder.DropIndex(
                name: "IX_PessoaPapeis_PessoaId_PapelId",
                table: "PessoaPapeis");

            migrationBuilder.DropColumn(
                name: "PapelId",
                table: "PessoaPapeis");

            migrationBuilder.AlterColumn<byte>(
                name: "Papel",
                table: "PessoaPapeis",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0,
                oldClrType: typeof(byte),
                oldType: "tinyint",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PessoaPapeis_PessoaId_Papel",
                table: "PessoaPapeis",
                columns: new[] { "PessoaId", "Papel" },
                unique: true);
        }
    }
}
