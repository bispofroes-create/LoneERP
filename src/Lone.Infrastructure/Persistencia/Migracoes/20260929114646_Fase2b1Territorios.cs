using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Lone.Infrastructure.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class Fase2b1Territorios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MapasTerritoriais",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Codigo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false, collation: "Latin1_General_CI_AI"),
                    Descricao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Exclusivo = table.Column<bool>(type: "bit", nullable: false),
                    FinalidadeEnderecoReferenciaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MapasTerritoriais", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MapasTerritoriais_FinalidadesEndereco_FinalidadeEnderecoReferenciaId",
                        column: x => x.FinalidadeEnderecoReferenciaId,
                        principalTable: "FinalidadesEndereco",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MapasTerritoriais_Pessoas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TiposTerritorio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Codigo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false, collation: "Latin1_General_CI_AI"),
                    Descricao = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TiposTerritorio", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MapaTerritorialClassificacoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MapaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PapelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MapaTerritorialClassificacoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MapaTerritorialClassificacoes_MapasTerritoriais_MapaId",
                        column: x => x.MapaId,
                        principalTable: "MapasTerritoriais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MapaTerritorialClassificacoes_Papeis_PapelId",
                        column: x => x.PapelId,
                        principalTable: "Papeis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Territorios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MapaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Codigo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false, collation: "Latin1_General_CI_AI"),
                    TipoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaiId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Descricao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Situacao = table.Column<byte>(type: "tinyint", nullable: false),
                    FimEm = table.Column<DateOnly>(type: "date", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Territorios", x => x.Id);
                    table.UniqueConstraint("AK_Territorios_MapaId_Id", x => new { x.MapaId, x.Id });
                    table.CheckConstraint("CK_Territorios_Situacao", "([Situacao] = 0 AND [FimEm] IS NULL) OR ([Situacao] = 1 AND [FimEm] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_Territorios_MapasTerritoriais_MapaId",
                        column: x => x.MapaId,
                        principalTable: "MapasTerritoriais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Territorios_Territorios_MapaId_PaiId",
                        columns: x => new { x.MapaId, x.PaiId },
                        principalTable: "Territorios",
                        principalColumns: new[] { "MapaId", "Id" });
                    table.ForeignKey(
                        name: "FK_Territorios_TiposTerritorio_TipoId",
                        column: x => x.TipoId,
                        principalTable: "TiposTerritorio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TerritorioPosicoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MapaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TerritorioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaiId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InicioEm = table.Column<DateOnly>(type: "date", nullable: false),
                    FimEm = table.Column<DateOnly>(type: "date", nullable: true),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TerritorioPosicoes", x => x.Id);
                    table.CheckConstraint("CK_TerritorioPosicoes_Periodo", "[FimEm] IS NULL OR [FimEm] >= [InicioEm]");
                    table.ForeignKey(
                        name: "FK_TerritorioPosicoes_Territorios_MapaId_PaiId",
                        columns: x => new { x.MapaId, x.PaiId },
                        principalTable: "Territorios",
                        principalColumns: new[] { "MapaId", "Id" });
                    table.ForeignKey(
                        name: "FK_TerritorioPosicoes_Territorios_MapaId_TerritorioId",
                        columns: x => new { x.MapaId, x.TerritorioId },
                        principalTable: "Territorios",
                        principalColumns: new[] { "MapaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TerritorioResponsaveis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TerritorioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PessoaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EquipeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TipoCarteiraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InicioEm = table.Column<DateOnly>(type: "date", nullable: false),
                    FimEm = table.Column<DateOnly>(type: "date", nullable: true),
                    Observacao = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TerritorioResponsaveis", x => x.Id);
                    table.CheckConstraint("CK_TerritorioResponsaveis_Periodo", "[FimEm] IS NULL OR [FimEm] >= [InicioEm]");
                    table.CheckConstraint("CK_TerritorioResponsaveis_PessoaOuEquipe", "([PessoaId] IS NOT NULL AND [EquipeId] IS NULL) OR ([PessoaId] IS NULL AND [EquipeId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_TerritorioResponsaveis_Equipes_EquipeId",
                        column: x => x.EquipeId,
                        principalTable: "Equipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TerritorioResponsaveis_Pessoas_PessoaId",
                        column: x => x.PessoaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TerritorioResponsaveis_Territorios_TerritorioId",
                        column: x => x.TerritorioId,
                        principalTable: "Territorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TerritorioResponsaveis_TiposCarteira_TipoCarteiraId",
                        column: x => x.TipoCarteiraId,
                        principalTable: "TiposCarteira",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "TiposTerritorio",
                columns: new[] { "Id", "Ativo", "AtualizadoEm", "Codigo", "CriadoEm", "Descricao", "Nome", "Ordem" },
                values: new object[,]
                {
                    { new Guid("7a9e1c0b-0000-0000-0000-000000000001"), true, null, "GEOGRAFICO", new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), "País, região, estado, cidade, bairro...", "Geográfico", 1 },
                    { new Guid("7a9e1c0b-0000-0000-0000-000000000002"), true, null, "SEGMENTO", new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), "Ramo de atividade, porte, canal...", "Segmento", 2 },
                    { new Guid("7a9e1c0b-0000-0000-0000-000000000003"), true, null, "ESTRATEGICO", new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), "Grandes contas, contas-chave, contas especiais...", "Estratégico", 3 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_MapasTerritoriais_Codigo",
                table: "MapasTerritoriais",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MapasTerritoriais_EmpresaId",
                table: "MapasTerritoriais",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_MapasTerritoriais_FinalidadeEnderecoReferenciaId",
                table: "MapasTerritoriais",
                column: "FinalidadeEnderecoReferenciaId");

            migrationBuilder.CreateIndex(
                name: "IX_MapasTerritoriais_Nome",
                table: "MapasTerritoriais",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MapaTerritorialClassificacoes_MapaId_PapelId",
                table: "MapaTerritorialClassificacoes",
                columns: new[] { "MapaId", "PapelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MapaTerritorialClassificacoes_PapelId",
                table: "MapaTerritorialClassificacoes",
                column: "PapelId");

            migrationBuilder.CreateIndex(
                name: "IX_TerritorioPosicoes_MapaId_PaiId_InicioEm",
                table: "TerritorioPosicoes",
                columns: new[] { "MapaId", "PaiId", "InicioEm" });

            migrationBuilder.CreateIndex(
                name: "IX_TerritorioPosicoes_MapaId_TerritorioId",
                table: "TerritorioPosicoes",
                columns: new[] { "MapaId", "TerritorioId" });

            migrationBuilder.CreateIndex(
                name: "UX_TerritorioPosicoes_Aberta",
                table: "TerritorioPosicoes",
                column: "TerritorioId",
                unique: true,
                filter: "[FimEm] IS NULL AND [Ativo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_TerritorioResponsaveis_EquipeId_FimEm",
                table: "TerritorioResponsaveis",
                columns: new[] { "EquipeId", "FimEm" });

            migrationBuilder.CreateIndex(
                name: "IX_TerritorioResponsaveis_PessoaId_FimEm",
                table: "TerritorioResponsaveis",
                columns: new[] { "PessoaId", "FimEm" });

            migrationBuilder.CreateIndex(
                name: "IX_TerritorioResponsaveis_TerritorioId",
                table: "TerritorioResponsaveis",
                column: "TerritorioId");

            migrationBuilder.CreateIndex(
                name: "IX_TerritorioResponsaveis_TipoCarteiraId",
                table: "TerritorioResponsaveis",
                column: "TipoCarteiraId");

            migrationBuilder.CreateIndex(
                name: "IX_Territorios_MapaId_Codigo",
                table: "Territorios",
                columns: new[] { "MapaId", "Codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Territorios_TipoId",
                table: "Territorios",
                column: "TipoId");

            migrationBuilder.CreateIndex(
                name: "UX_Territorios_NomeEntreIrmaosAtivos",
                table: "Territorios",
                columns: new[] { "MapaId", "PaiId", "Nome" },
                unique: true,
                filter: "[Situacao] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TiposTerritorio_Codigo",
                table: "TiposTerritorio",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TiposTerritorio_Nome",
                table: "TiposTerritorio",
                column: "Nome",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MapaTerritorialClassificacoes");

            migrationBuilder.DropTable(
                name: "TerritorioPosicoes");

            migrationBuilder.DropTable(
                name: "TerritorioResponsaveis");

            migrationBuilder.DropTable(
                name: "Territorios");

            migrationBuilder.DropTable(
                name: "MapasTerritoriais");

            migrationBuilder.DropTable(
                name: "TiposTerritorio");
        }
    }
}
