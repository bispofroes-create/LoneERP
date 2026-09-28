using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lone.Infrastructure.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class Fase2aEscopoEquipes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PessoaId",
                table: "Usuarios",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "AlcanceComercial",
                table: "Perfis",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<byte>(
                name: "Papel",
                table: "MembrosEquipe",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            // Fase 2a (F3): o líder gravado em Equipes.LiderId vira membro com papel Líder. Só marca e inclui; não apaga nada.
            migrationBuilder.Sql(SqlMigracaoEquipes.LiderComoMembro);

            migrationBuilder.AddColumn<Guid>(
                name: "EquipePaiId",
                table: "Equipes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_PessoaId",
                table: "Usuarios",
                column: "PessoaId",
                unique: true,
                filter: "[PessoaId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Equipes_EquipePaiId",
                table: "Equipes",
                column: "EquipePaiId");

            migrationBuilder.AddForeignKey(
                name: "FK_Equipes_Equipes_EquipePaiId",
                table: "Equipes",
                column: "EquipePaiId",
                principalTable: "Equipes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Usuarios_Pessoas_PessoaId",
                table: "Usuarios",
                column: "PessoaId",
                principalTable: "Pessoas",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Equipes_Equipes_EquipePaiId",
                table: "Equipes");

            migrationBuilder.DropForeignKey(
                name: "FK_Usuarios_Pessoas_PessoaId",
                table: "Usuarios");

            migrationBuilder.DropIndex(
                name: "IX_Usuarios_PessoaId",
                table: "Usuarios");

            migrationBuilder.DropIndex(
                name: "IX_Equipes_EquipePaiId",
                table: "Equipes");

            migrationBuilder.DropColumn(
                name: "PessoaId",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "AlcanceComercial",
                table: "Perfis");

            migrationBuilder.DropColumn(
                name: "Papel",
                table: "MembrosEquipe");

            migrationBuilder.DropColumn(
                name: "EquipePaiId",
                table: "Equipes");
        }
    }
}
