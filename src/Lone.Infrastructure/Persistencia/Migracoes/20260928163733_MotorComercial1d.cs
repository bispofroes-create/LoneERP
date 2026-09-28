using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lone.Infrastructure.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class MotorComercial1d : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DiasRetroativosMaximo",
                table: "ParametrosComerciais",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "TransferenciaId",
                table: "CarteiraClientes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TransferenciasCarteira",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ano = table.Column<int>(type: "int", nullable: false),
                    Sequencia = table.Column<int>(type: "int", nullable: false),
                    EfeitoEm = table.Column<DateOnly>(type: "date", nullable: false),
                    OrigemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoCarteiraId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Motivo = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Observacao = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Usuario = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Transferidos = table.Column<int>(type: "int", nullable: false),
                    NaoProcessados = table.Column<int>(type: "int", nullable: false),
                    Erros = table.Column<int>(type: "int", nullable: false),
                    Concluida = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferenciasCarteira", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransferenciasCarteira_Pessoas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TransferenciasCarteira_Pessoas_OrigemId",
                        column: x => x.OrigemId,
                        principalTable: "Pessoas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TransferenciasCarteira_TiposCarteira_TipoCarteiraId",
                        column: x => x.TipoCarteiraId,
                        principalTable: "TiposCarteira",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TransferenciaCarteiraItens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransferenciaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VinculoOrigemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VinculoNovoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DestinoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TipoCarteiraId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InicioOrigem = table.Column<DateOnly>(type: "date", nullable: true),
                    FimOrigem = table.Column<DateOnly>(type: "date", nullable: true),
                    Resultado = table.Column<byte>(type: "tinyint", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferenciaCarteiraItens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransferenciaCarteiraItens_CarteiraClientes_VinculoNovoId",
                        column: x => x.VinculoNovoId,
                        principalTable: "CarteiraClientes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TransferenciaCarteiraItens_CarteiraClientes_VinculoOrigemId",
                        column: x => x.VinculoOrigemId,
                        principalTable: "CarteiraClientes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TransferenciaCarteiraItens_Pessoas_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Pessoas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TransferenciaCarteiraItens_Pessoas_DestinoId",
                        column: x => x.DestinoId,
                        principalTable: "Pessoas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TransferenciaCarteiraItens_TiposCarteira_TipoCarteiraId",
                        column: x => x.TipoCarteiraId,
                        principalTable: "TiposCarteira",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransferenciaCarteiraItens_TransferenciasCarteira_TransferenciaId",
                        column: x => x.TransferenciaId,
                        principalTable: "TransferenciasCarteira",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "ParametrosComerciais",
                keyColumn: "Id",
                keyValue: new Guid("7a9e1c06-0000-0000-0000-000000000001"),
                column: "DiasRetroativosMaximo",
                value: 30);

            migrationBuilder.CreateIndex(
                name: "IX_CarteiraClientes_TransferenciaId",
                table: "CarteiraClientes",
                column: "TransferenciaId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferenciaCarteiraItens_ClienteId",
                table: "TransferenciaCarteiraItens",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferenciaCarteiraItens_DestinoId",
                table: "TransferenciaCarteiraItens",
                column: "DestinoId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferenciaCarteiraItens_TipoCarteiraId",
                table: "TransferenciaCarteiraItens",
                column: "TipoCarteiraId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferenciaCarteiraItens_TransferenciaId_ClienteId",
                table: "TransferenciaCarteiraItens",
                columns: new[] { "TransferenciaId", "ClienteId" });

            migrationBuilder.CreateIndex(
                name: "IX_TransferenciaCarteiraItens_VinculoNovoId",
                table: "TransferenciaCarteiraItens",
                column: "VinculoNovoId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferenciaCarteiraItens_VinculoOrigemId",
                table: "TransferenciaCarteiraItens",
                column: "VinculoOrigemId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferenciasCarteira_Ano_Sequencia",
                table: "TransferenciasCarteira",
                columns: new[] { "Ano", "Sequencia" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransferenciasCarteira_EmpresaId",
                table: "TransferenciasCarteira",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferenciasCarteira_OrigemId_EfeitoEm",
                table: "TransferenciasCarteira",
                columns: new[] { "OrigemId", "EfeitoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_TransferenciasCarteira_TipoCarteiraId",
                table: "TransferenciasCarteira",
                column: "TipoCarteiraId");

            migrationBuilder.AddForeignKey(
                name: "FK_CarteiraClientes_TransferenciasCarteira_TransferenciaId",
                table: "CarteiraClientes",
                column: "TransferenciaId",
                principalTable: "TransferenciasCarteira",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CarteiraClientes_TransferenciasCarteira_TransferenciaId",
                table: "CarteiraClientes");

            migrationBuilder.DropTable(
                name: "TransferenciaCarteiraItens");

            migrationBuilder.DropTable(
                name: "TransferenciasCarteira");

            migrationBuilder.DropIndex(
                name: "IX_CarteiraClientes_TransferenciaId",
                table: "CarteiraClientes");

            migrationBuilder.DropColumn(
                name: "DiasRetroativosMaximo",
                table: "ParametrosComerciais");

            migrationBuilder.DropColumn(
                name: "TransferenciaId",
                table: "CarteiraClientes");
        }
    }
}
