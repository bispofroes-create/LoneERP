using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Lone.Infrastructure.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class MotorComercial1c : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ParametrosComerciais",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DiasAvisoFimVinculo = table.Column<int>(type: "int", nullable: false),
                    CreditoNaAusencia = table.Column<byte>(type: "tinyint", nullable: false),
                    PercentualSubstitutoPadrao = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParametrosComerciais", x => x.Id);
                    table.CheckConstraint("CK_ParametrosComerciais_Unico", "[Id] = '7a9e1c06-0000-0000-0000-000000000001'");
                });

            migrationBuilder.CreateTable(
                name: "TiposAusencia",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false, collation: "Latin1_General_CI_AI"),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TiposAusencia", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CoberturasComerciais",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TitularId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoAusenciaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InicioEm = table.Column<DateOnly>(type: "date", nullable: false),
                    FimEm = table.Column<DateOnly>(type: "date", nullable: false),
                    SubstitutoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EquipeSubstitutaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TipoCarteiraId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RegraCredito = table.Column<byte>(type: "tinyint", nullable: false),
                    PercentualSubstituto = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    PermiteAcesso = table.Column<bool>(type: "bit", nullable: false),
                    Observacao = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Cancelada = table.Column<bool>(type: "bit", nullable: false),
                    MotivoCancelamento = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoberturasComerciais", x => x.Id);
                    table.CheckConstraint("CK_CoberturasComerciais_Periodo", "[FimEm] >= [InicioEm]");
                    table.CheckConstraint("CK_CoberturasComerciais_QuemCobre", "([SubstitutoId] IS NULL AND [EquipeSubstitutaId] IS NOT NULL) OR ([SubstitutoId] IS NOT NULL AND [EquipeSubstitutaId] IS NULL)");
                    table.ForeignKey(
                        name: "FK_CoberturasComerciais_Equipes_EquipeSubstitutaId",
                        column: x => x.EquipeSubstitutaId,
                        principalTable: "Equipes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CoberturasComerciais_Pessoas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CoberturasComerciais_Pessoas_SubstitutoId",
                        column: x => x.SubstitutoId,
                        principalTable: "Pessoas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CoberturasComerciais_Pessoas_TitularId",
                        column: x => x.TitularId,
                        principalTable: "Pessoas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CoberturasComerciais_TiposAusencia_TipoAusenciaId",
                        column: x => x.TipoAusenciaId,
                        principalTable: "TiposAusencia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CoberturasComerciais_TiposCarteira_TipoCarteiraId",
                        column: x => x.TipoCarteiraId,
                        principalTable: "TiposCarteira",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "ParametrosComerciais",
                columns: new[] { "Id", "AtualizadoEm", "CreditoNaAusencia", "CriadoEm", "DiasAvisoFimVinculo", "PercentualSubstitutoPadrao" },
                values: new object[] { new Guid("7a9e1c06-0000-0000-0000-000000000001"), null, (byte)0, new DateTime(2026, 9, 28, 0, 0, 0, 0, DateTimeKind.Utc), 30, null });

            migrationBuilder.InsertData(
                table: "TiposAusencia",
                columns: new[] { "Id", "Ativo", "AtualizadoEm", "CriadoEm", "Nome", "Ordem" },
                values: new object[,]
                {
                    { new Guid("7a9e1c05-0000-0000-0000-000000000001"), true, null, new DateTime(2026, 9, 28, 0, 0, 0, 0, DateTimeKind.Utc), "Férias", 1 },
                    { new Guid("7a9e1c05-0000-0000-0000-000000000002"), true, null, new DateTime(2026, 9, 28, 0, 0, 0, 0, DateTimeKind.Utc), "Folga", 2 },
                    { new Guid("7a9e1c05-0000-0000-0000-000000000003"), true, null, new DateTime(2026, 9, 28, 0, 0, 0, 0, DateTimeKind.Utc), "Licença", 3 },
                    { new Guid("7a9e1c05-0000-0000-0000-000000000004"), true, null, new DateTime(2026, 9, 28, 0, 0, 0, 0, DateTimeKind.Utc), "Afastamento", 4 },
                    { new Guid("7a9e1c05-0000-0000-0000-000000000005"), true, null, new DateTime(2026, 9, 28, 0, 0, 0, 0, DateTimeKind.Utc), "Treinamento", 5 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_CoberturasComerciais_EmpresaId",
                table: "CoberturasComerciais",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_CoberturasComerciais_EquipeSubstitutaId",
                table: "CoberturasComerciais",
                column: "EquipeSubstitutaId");

            migrationBuilder.CreateIndex(
                name: "IX_CoberturasComerciais_FimEm_Cancelada",
                table: "CoberturasComerciais",
                columns: new[] { "FimEm", "Cancelada" });

            migrationBuilder.CreateIndex(
                name: "IX_CoberturasComerciais_SubstitutoId",
                table: "CoberturasComerciais",
                column: "SubstitutoId");

            migrationBuilder.CreateIndex(
                name: "IX_CoberturasComerciais_TipoAusenciaId",
                table: "CoberturasComerciais",
                column: "TipoAusenciaId");

            migrationBuilder.CreateIndex(
                name: "IX_CoberturasComerciais_TipoCarteiraId",
                table: "CoberturasComerciais",
                column: "TipoCarteiraId");

            migrationBuilder.CreateIndex(
                name: "IX_CoberturasComerciais_TitularId_InicioEm",
                table: "CoberturasComerciais",
                columns: new[] { "TitularId", "InicioEm" });

            migrationBuilder.CreateIndex(
                name: "IX_TiposAusencia_Nome",
                table: "TiposAusencia",
                column: "Nome",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CoberturasComerciais");

            migrationBuilder.DropTable(
                name: "ParametrosComerciais");

            migrationBuilder.DropTable(
                name: "TiposAusencia");
        }
    }
}
