using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lone.Infrastructure.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class QuemPodeSerPapel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TiposCarteiraClassificacoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoCarteiraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PapelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TiposCarteiraClassificacoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TiposCarteiraClassificacoes_Papeis_PapelId",
                        column: x => x.PapelId,
                        principalTable: "Papeis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TiposCarteiraClassificacoes_TiposCarteira_TipoCarteiraId",
                        column: x => x.TipoCarteiraId,
                        principalTable: "TiposCarteira",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TiposCarteiraClassificacoes_PapelId",
                table: "TiposCarteiraClassificacoes",
                column: "PapelId");

            migrationBuilder.CreateIndex(
                name: "IX_TiposCarteiraClassificacoes_TipoCarteiraId_PapelId",
                table: "TiposCarteiraClassificacoes",
                columns: new[] { "TipoCarteiraId", "PapelId" },
                unique: true);

            // Motor Comercial, Fase 1b (aprovada em 28/09/2026): os papéis comerciais que já existem aceitam Vendedor e
            // Representante, como antes (era a lista da ficha). Só inclui; não mexe em mais nada. SQL em SqlMigracaoCarteira.
            migrationBuilder.Sql(SqlMigracaoCarteira.ClassificacoesIniciais);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TiposCarteiraClassificacoes");
        }
    }
}
