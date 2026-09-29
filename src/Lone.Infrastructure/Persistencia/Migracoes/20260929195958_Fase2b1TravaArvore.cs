using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lone.Infrastructure.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class Fase2b1TravaArvore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MapaTerritorialArvores",
                columns: table => new
                {
                    MapaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MapaTerritorialArvores", x => x.MapaId);
                    table.ForeignKey(
                        name: "FK_MapaTerritorialArvores_MapasTerritoriais_MapaId",
                        column: x => x.MapaId,
                        principalTable: "MapasTerritoriais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Fase 2b-1a, D1 = B (29/09/2026): uma trava por mapa que já existia (os novos nascem com a sua), a árvore sem
            // ciclo e com até 12 níveis e as posições sem cruzamento também no banco. SQL em SqlMigracaoTerritorios.
            migrationBuilder.Sql(SqlMigracaoTerritorios.PreencherTravasDaArvore);
            migrationBuilder.Sql(SqlMigracaoTerritorios.CriarProtecaoArvore);
            migrationBuilder.Sql(SqlMigracaoTerritorios.CriarProtecaoPosicoes);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Só o que esta migration criou: os dois gatilhos (antes) e a tabela da trava.
            migrationBuilder.Sql(SqlMigracaoTerritorios.RemoverProtecoesArvore);

            migrationBuilder.DropTable(
                name: "MapaTerritorialArvores");
        }
    }
}
