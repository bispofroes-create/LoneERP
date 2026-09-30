using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lone.Infrastructure.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class Fase2b1bMotor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OperacaoEncerramentoId",
                table: "TerritorioResponsaveis",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OperacaoAnulacaoId",
                table: "TerritorioPosicoes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OperacaoEncerramentoId",
                table: "TerritorioPosicoes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OperacaoId",
                table: "TerritorioPosicoes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OperacaoMudancaId",
                table: "TerritorioPosicoes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RegistrarNosDocumentos",
                table: "MapasTerritoriais",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ParametrosTerritoriais",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DiasRetroativosMaximo = table.Column<int>(type: "int", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParametrosTerritoriais", x => x.Id);
                    table.CheckConstraint("CK_ParametrosTerritoriais_DiasRetroativos", "[DiasRetroativosMaximo] BETWEEN 0 AND 365");
                    table.CheckConstraint("CK_ParametrosTerritoriais_Unico", "[Id] = '7a9e1c08-0000-0000-0000-000000000001'");
                });

            migrationBuilder.CreateTable(
                name: "AtribuicoesTerritorio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MapaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Exclusivo = table.Column<bool>(type: "bit", nullable: false),
                    TerritorioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PessoaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InicioEm = table.Column<DateOnly>(type: "date", nullable: false),
                    FimEm = table.Column<DateOnly>(type: "date", nullable: true),
                    Origem = table.Column<byte>(type: "tinyint", nullable: false),
                    RegraId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExcecaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    OperacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperacaoEncerramentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OperacaoAnulacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AtribuicoesTerritorio", x => x.Id);
                    table.CheckConstraint("CK_AtribuicoesTerritorio_Origem", "([Origem] = 1 AND [RegraId] IS NOT NULL AND [ExcecaoId] IS NULL) OR ([Origem] = 2 AND [ExcecaoId] IS NOT NULL AND [RegraId] IS NULL)");
                    table.CheckConstraint("CK_AtribuicoesTerritorio_Periodo", "[FimEm] IS NULL OR [FimEm] >= [InicioEm]");
                    table.ForeignKey(
                        name: "FK_AtribuicoesTerritorio_Pessoas_PessoaId",
                        column: x => x.PessoaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AtribuicoesTerritorio_Territorios_MapaId_TerritorioId",
                        columns: x => new { x.MapaId, x.TerritorioId },
                        principalTable: "Territorios",
                        principalColumns: new[] { "MapaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExcecoesTerritorio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MapaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Exclusivo = table.Column<bool>(type: "bit", nullable: false),
                    TerritorioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PessoaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<byte>(type: "tinyint", nullable: false),
                    InicioEm = table.Column<DateOnly>(type: "date", nullable: false),
                    FimEm = table.Column<DateOnly>(type: "date", nullable: true),
                    Motivo = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Origem = table.Column<byte>(type: "tinyint", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    OperacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperacaoMudancaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OperacaoEncerramentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OperacaoAnulacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExcecoesTerritorio", x => x.Id);
                    table.CheckConstraint("CK_ExcecoesTerritorio_Motivo", "LEN([Motivo]) > 0");
                    table.CheckConstraint("CK_ExcecoesTerritorio_Origem", "[Origem] IN (1, 2, 3)");
                    table.CheckConstraint("CK_ExcecoesTerritorio_Periodo", "[FimEm] IS NULL OR [FimEm] >= [InicioEm]");
                    table.CheckConstraint("CK_ExcecoesTerritorio_Tipo", "[Tipo] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_ExcecoesTerritorio_Pessoas_PessoaId",
                        column: x => x.PessoaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExcecoesTerritorio_Territorios_MapaId_TerritorioId",
                        columns: x => new { x.MapaId, x.TerritorioId },
                        principalTable: "Territorios",
                        principalColumns: new[] { "MapaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MapaTerritorialMotor",
                columns: table => new
                {
                    MapaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    UltimaOperacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UltimoEfeitoEm = table.Column<DateOnly>(type: "date", nullable: true),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MapaTerritorialMotor", x => x.MapaId);
                    table.ForeignKey(
                        name: "FK_MapaTerritorialMotor_MapasTerritoriais_MapaId",
                        column: x => x.MapaId,
                        principalTable: "MapasTerritoriais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OperacaoTerritorialFechamentos",
                columns: table => new
                {
                    OperacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tabela = table.Column<byte>(type: "tinyint", nullable: false),
                    LinhaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FimAnterior = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperacaoTerritorialFechamentos", x => new { x.OperacaoId, x.Tabela, x.LinhaId });
                    table.CheckConstraint("CK_OperacaoTerritorialFechamentos_Tabela", "[Tabela] BETWEEN 1 AND 5");
                });

            migrationBuilder.CreateTable(
                name: "OperacaoTerritorialItens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PessoaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Resultado = table.Column<byte>(type: "tinyint", nullable: false),
                    Efeito = table.Column<byte>(type: "tinyint", nullable: false),
                    TerritorioAnteriorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TerritorioNovoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AtribuicaoEncerradaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AtribuicaoNovaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Explicacao = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperacaoTerritorialItens", x => x.Id);
                    table.CheckConstraint("CK_OperacaoTerritorialItens_Explicacao", "ISJSON([Explicacao]) = 1");
                    table.ForeignKey(
                        name: "FK_OperacaoTerritorialItens_AtribuicoesTerritorio_AtribuicaoEncerradaId",
                        column: x => x.AtribuicaoEncerradaId,
                        principalTable: "AtribuicoesTerritorio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperacaoTerritorialItens_AtribuicoesTerritorio_AtribuicaoNovaId",
                        column: x => x.AtribuicaoNovaId,
                        principalTable: "AtribuicoesTerritorio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperacaoTerritorialItens_Pessoas_PessoaId",
                        column: x => x.PessoaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperacaoTerritorialItens_Territorios_TerritorioAnteriorId",
                        column: x => x.TerritorioAnteriorId,
                        principalTable: "Territorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperacaoTerritorialItens_Territorios_TerritorioNovoId",
                        column: x => x.TerritorioNovoId,
                        principalTable: "Territorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OperacaoTerritorialMudancas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MapaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<byte>(type: "tinyint", nullable: false),
                    TerritorioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PessoaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RegraBaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExcecaoBaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PosicaoBaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BaseVersao = table.Column<byte[]>(type: "binary(8)", fixedLength: true, maxLength: 8, nullable: true),
                    Antes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Depois = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperacaoTerritorialMudancas", x => x.Id);
                    table.CheckConstraint("CK_OperacaoTerritorialMudancas_Json", "ISJSON([Depois]) = 1 AND ([Antes] IS NULL OR ISJSON([Antes]) = 1)");
                    table.CheckConstraint("CK_OperacaoTerritorialMudancas_Ordem", "[Ordem] >= 1");
                    table.CheckConstraint("CK_OperacaoTerritorialMudancas_Tipo", "[Tipo] BETWEEN 1 AND 8");
                    table.ForeignKey(
                        name: "FK_OperacaoTerritorialMudancas_ExcecoesTerritorio_ExcecaoBaseId",
                        column: x => x.ExcecaoBaseId,
                        principalTable: "ExcecoesTerritorio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperacaoTerritorialMudancas_Pessoas_PessoaId",
                        column: x => x.PessoaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperacaoTerritorialMudancas_TerritorioPosicoes_PosicaoBaseId",
                        column: x => x.PosicaoBaseId,
                        principalTable: "TerritorioPosicoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperacaoTerritorialMudancas_Territorios_MapaId_TerritorioId",
                        columns: x => new { x.MapaId, x.TerritorioId },
                        principalTable: "Territorios",
                        principalColumns: new[] { "MapaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OperacaoTerritorialSimulacaoItens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SimulacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PessoaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Resultado = table.Column<byte>(type: "tinyint", nullable: false),
                    Efeito = table.Column<byte>(type: "tinyint", nullable: false),
                    OrigemEfeito = table.Column<byte>(type: "tinyint", nullable: false),
                    TerritorioAtualId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TerritorioPropostoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Explicacao = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperacaoTerritorialSimulacaoItens", x => x.Id);
                    table.CheckConstraint("CK_OperacaoTerritorialSimulacaoItens_Explicacao", "ISJSON([Explicacao]) = 1");
                    table.ForeignKey(
                        name: "FK_OperacaoTerritorialSimulacaoItens_Pessoas_PessoaId",
                        column: x => x.PessoaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperacaoTerritorialSimulacaoItens_Territorios_TerritorioAtualId",
                        column: x => x.TerritorioAtualId,
                        principalTable: "Territorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperacaoTerritorialSimulacaoItens_Territorios_TerritorioPropostoId",
                        column: x => x.TerritorioPropostoId,
                        principalTable: "Territorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OperacaoTerritorialSimulacoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EfeitoEm = table.Column<DateOnly>(type: "date", nullable: false),
                    SimuladaEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SimuladaPorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SimuladaPor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    VersaoMotor = table.Column<byte[]>(type: "binary(8)", fixedLength: true, maxLength: 8, nullable: false),
                    VersaoArvore = table.Column<byte[]>(type: "binary(8)", fixedLength: true, maxLength: 8, nullable: false),
                    Assinatura = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: false),
                    AtributosAvaliadosEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Entram = table.Column<int>(type: "int", nullable: false),
                    Saem = table.Column<int>(type: "int", nullable: false),
                    Mudam = table.Column<int>(type: "int", nullable: false),
                    OrigemAtualizada = table.Column<int>(type: "int", nullable: false),
                    EmConflito = table.Column<int>(type: "int", nullable: false),
                    Inconsistencias = table.Column<int>(type: "int", nullable: false),
                    DaOperacao = table.Column<int>(type: "int", nullable: false),
                    Divergencias = table.Column<int>(type: "int", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperacaoTerritorialSimulacoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperacaoTerritorialSimulacoes_Usuarios_SimuladaPorId",
                        column: x => x.SimuladaPorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OperacoesTerritoriais",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ano = table.Column<int>(type: "int", nullable: false),
                    Sequencia = table.Column<int>(type: "int", nullable: false),
                    MapaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EfeitoEm = table.Column<DateOnly>(type: "date", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Observacao = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Situacao = table.Column<byte>(type: "tinyint", nullable: false),
                    CriadaPorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CriadaPor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SimulacaoAtualId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AplicadaEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AplicadaPorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AplicadaPor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CanceladaEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CanceladaPorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CanceladaPor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CanceladaMotivo = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    DesfeitaEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DesfeitaPorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DesfeitaPor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DesfeitaMotivo = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    OperacaoAnteriorDoMapaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EfeitoAnteriorDoMapa = table.Column<DateOnly>(type: "date", nullable: true),
                    Entraram = table.Column<int>(type: "int", nullable: false),
                    Sairam = table.Column<int>(type: "int", nullable: false),
                    Mudaram = table.Column<int>(type: "int", nullable: false),
                    OrigemAtualizada = table.Column<int>(type: "int", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperacoesTerritoriais", x => x.Id);
                    table.UniqueConstraint("AK_OperacoesTerritoriais_Id_MapaId", x => new { x.Id, x.MapaId });
                    table.CheckConstraint("CK_OperacoesTerritoriais_Motivo", "LEN([Motivo]) > 0");
                    table.CheckConstraint("CK_OperacoesTerritoriais_Numero", "[Ano] BETWEEN 2000 AND 9999 AND [Sequencia] >= 1");
                    table.CheckConstraint("CK_OperacoesTerritoriais_Situacao", "[Situacao] BETWEEN 0 AND 4");
                    table.CheckConstraint("CK_OperacoesTerritoriais_Transicoes", "([Situacao] NOT IN (2, 4) OR ([AplicadaEm] IS NOT NULL AND [AplicadaPor] IS NOT NULL)) AND ([Situacao] <> 3 OR ([CanceladaEm] IS NOT NULL AND [CanceladaPor] IS NOT NULL AND [CanceladaMotivo] IS NOT NULL AND LEN([CanceladaMotivo]) > 0)) AND ([Situacao] <> 4 OR ([DesfeitaEm] IS NOT NULL AND [DesfeitaPor] IS NOT NULL AND [DesfeitaMotivo] IS NOT NULL AND LEN([DesfeitaMotivo]) > 0))");
                    table.ForeignKey(
                        name: "FK_OperacoesTerritoriais_MapasTerritoriais_MapaId",
                        column: x => x.MapaId,
                        principalTable: "MapasTerritoriais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperacoesTerritoriais_OperacaoTerritorialSimulacoes_SimulacaoAtualId",
                        column: x => x.SimulacaoAtualId,
                        principalTable: "OperacaoTerritorialSimulacoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperacoesTerritoriais_OperacoesTerritoriais_OperacaoAnteriorDoMapaId",
                        column: x => x.OperacaoAnteriorDoMapaId,
                        principalTable: "OperacoesTerritoriais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperacoesTerritoriais_Usuarios_AplicadaPorId",
                        column: x => x.AplicadaPorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperacoesTerritoriais_Usuarios_CanceladaPorId",
                        column: x => x.CanceladaPorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperacoesTerritoriais_Usuarios_CriadaPorId",
                        column: x => x.CriadaPorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperacoesTerritoriais_Usuarios_DesfeitaPorId",
                        column: x => x.DesfeitaPorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RegrasTerritorio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MapaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TerritorioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    Grupos = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Criterios = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Prioridade = table.Column<int>(type: "int", nullable: true),
                    InicioEm = table.Column<DateOnly>(type: "date", nullable: false),
                    FimEm = table.Column<DateOnly>(type: "date", nullable: true),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    OperacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperacaoMudancaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OperacaoEncerramentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OperacaoAnulacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegrasTerritorio", x => x.Id);
                    table.CheckConstraint("CK_RegrasTerritorio_Grupos", "ISJSON([Grupos]) = 1 AND LEN([Grupos]) <= 20000");
                    table.CheckConstraint("CK_RegrasTerritorio_Numero", "[Numero] >= 1");
                    table.CheckConstraint("CK_RegrasTerritorio_Periodo", "[FimEm] IS NULL OR [FimEm] >= [InicioEm]");
                    table.CheckConstraint("CK_RegrasTerritorio_Prioridade", "[Prioridade] IS NULL OR [Prioridade] >= 1");
                    table.ForeignKey(
                        name: "FK_RegrasTerritorio_OperacaoTerritorialMudancas_OperacaoMudancaId",
                        column: x => x.OperacaoMudancaId,
                        principalTable: "OperacaoTerritorialMudancas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RegrasTerritorio_OperacoesTerritoriais_OperacaoAnulacaoId",
                        column: x => x.OperacaoAnulacaoId,
                        principalTable: "OperacoesTerritoriais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RegrasTerritorio_OperacoesTerritoriais_OperacaoEncerramentoId",
                        column: x => x.OperacaoEncerramentoId,
                        principalTable: "OperacoesTerritoriais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RegrasTerritorio_OperacoesTerritoriais_OperacaoId",
                        column: x => x.OperacaoId,
                        principalTable: "OperacoesTerritoriais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RegrasTerritorio_Territorios_MapaId_TerritorioId",
                        columns: x => new { x.MapaId, x.TerritorioId },
                        principalTable: "Territorios",
                        principalColumns: new[] { "MapaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "ParametrosTerritoriais",
                columns: new[] { "Id", "AtualizadoEm", "CriadoEm", "DiasRetroativosMaximo" },
                values: new object[] { new Guid("7a9e1c08-0000-0000-0000-000000000001"), null, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), 30 });

            migrationBuilder.CreateIndex(
                name: "IX_TerritorioResponsaveis_OperacaoEncerramentoId",
                table: "TerritorioResponsaveis",
                column: "OperacaoEncerramentoId");

            migrationBuilder.CreateIndex(
                name: "IX_TerritorioPosicoes_OperacaoAnulacaoId",
                table: "TerritorioPosicoes",
                column: "OperacaoAnulacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_TerritorioPosicoes_OperacaoEncerramentoId",
                table: "TerritorioPosicoes",
                column: "OperacaoEncerramentoId");

            migrationBuilder.CreateIndex(
                name: "IX_TerritorioPosicoes_OperacaoId",
                table: "TerritorioPosicoes",
                column: "OperacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_TerritorioPosicoes_OperacaoMudancaId",
                table: "TerritorioPosicoes",
                column: "OperacaoMudancaId");

            migrationBuilder.CreateIndex(
                name: "UX_MapasTerritoriais_Id_Exclusivo",
                table: "MapasTerritoriais",
                columns: new[] { "Id", "Exclusivo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AtribuicoesTerritorio_ExcecaoId",
                table: "AtribuicoesTerritorio",
                column: "ExcecaoId");

            migrationBuilder.CreateIndex(
                name: "IX_AtribuicoesTerritorio_MapaId_TerritorioId_InicioEm",
                table: "AtribuicoesTerritorio",
                columns: new[] { "MapaId", "TerritorioId", "InicioEm" });

            migrationBuilder.CreateIndex(
                name: "IX_AtribuicoesTerritorio_OperacaoAnulacaoId",
                table: "AtribuicoesTerritorio",
                column: "OperacaoAnulacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_AtribuicoesTerritorio_OperacaoEncerramentoId",
                table: "AtribuicoesTerritorio",
                column: "OperacaoEncerramentoId");

            migrationBuilder.CreateIndex(
                name: "IX_AtribuicoesTerritorio_OperacaoId",
                table: "AtribuicoesTerritorio",
                column: "OperacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_AtribuicoesTerritorio_PessoaId_MapaId_InicioEm",
                table: "AtribuicoesTerritorio",
                columns: new[] { "PessoaId", "MapaId", "InicioEm" });

            migrationBuilder.CreateIndex(
                name: "IX_AtribuicoesTerritorio_RegraId",
                table: "AtribuicoesTerritorio",
                column: "RegraId");

            migrationBuilder.CreateIndex(
                name: "UX_AtribuicoesTerritorio_AbertaNoExclusivo",
                table: "AtribuicoesTerritorio",
                columns: new[] { "PessoaId", "MapaId" },
                unique: true,
                filter: "[FimEm] IS NULL AND [Ativo] = 1 AND [Exclusivo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ExcecoesTerritorio_MapaId_TerritorioId_InicioEm",
                table: "ExcecoesTerritorio",
                columns: new[] { "MapaId", "TerritorioId", "InicioEm" });

            migrationBuilder.CreateIndex(
                name: "IX_ExcecoesTerritorio_OperacaoAnulacaoId",
                table: "ExcecoesTerritorio",
                column: "OperacaoAnulacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_ExcecoesTerritorio_OperacaoEncerramentoId",
                table: "ExcecoesTerritorio",
                column: "OperacaoEncerramentoId");

            migrationBuilder.CreateIndex(
                name: "IX_ExcecoesTerritorio_OperacaoId",
                table: "ExcecoesTerritorio",
                column: "OperacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_ExcecoesTerritorio_OperacaoMudancaId",
                table: "ExcecoesTerritorio",
                column: "OperacaoMudancaId");

            migrationBuilder.CreateIndex(
                name: "IX_ExcecoesTerritorio_PessoaId_MapaId_InicioEm",
                table: "ExcecoesTerritorio",
                columns: new[] { "PessoaId", "MapaId", "InicioEm" });

            migrationBuilder.CreateIndex(
                name: "UX_ExcecoesTerritorio_FixarAbertoNoExclusivo",
                table: "ExcecoesTerritorio",
                columns: new[] { "PessoaId", "MapaId" },
                unique: true,
                filter: "[Tipo] = 1 AND [Exclusivo] = 1 AND [FimEm] IS NULL AND [Ativo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_MapaTerritorialMotor_UltimaOperacaoId",
                table: "MapaTerritorialMotor",
                column: "UltimaOperacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_OperacaoTerritorialItens_AtribuicaoEncerradaId",
                table: "OperacaoTerritorialItens",
                column: "AtribuicaoEncerradaId");

            migrationBuilder.CreateIndex(
                name: "IX_OperacaoTerritorialItens_AtribuicaoNovaId",
                table: "OperacaoTerritorialItens",
                column: "AtribuicaoNovaId");

            migrationBuilder.CreateIndex(
                name: "IX_OperacaoTerritorialItens_OperacaoId_PessoaId",
                table: "OperacaoTerritorialItens",
                columns: new[] { "OperacaoId", "PessoaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperacaoTerritorialItens_PessoaId",
                table: "OperacaoTerritorialItens",
                column: "PessoaId");

            migrationBuilder.CreateIndex(
                name: "IX_OperacaoTerritorialItens_TerritorioAnteriorId",
                table: "OperacaoTerritorialItens",
                column: "TerritorioAnteriorId");

            migrationBuilder.CreateIndex(
                name: "IX_OperacaoTerritorialItens_TerritorioNovoId",
                table: "OperacaoTerritorialItens",
                column: "TerritorioNovoId");

            migrationBuilder.CreateIndex(
                name: "IX_OperacaoTerritorialMudancas_ExcecaoBaseId",
                table: "OperacaoTerritorialMudancas",
                column: "ExcecaoBaseId");

            migrationBuilder.CreateIndex(
                name: "IX_OperacaoTerritorialMudancas_MapaId_TerritorioId",
                table: "OperacaoTerritorialMudancas",
                columns: new[] { "MapaId", "TerritorioId" });

            migrationBuilder.CreateIndex(
                name: "IX_OperacaoTerritorialMudancas_OperacaoId_MapaId",
                table: "OperacaoTerritorialMudancas",
                columns: new[] { "OperacaoId", "MapaId" });

            migrationBuilder.CreateIndex(
                name: "IX_OperacaoTerritorialMudancas_OperacaoId_Ordem",
                table: "OperacaoTerritorialMudancas",
                columns: new[] { "OperacaoId", "Ordem" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperacaoTerritorialMudancas_PessoaId",
                table: "OperacaoTerritorialMudancas",
                column: "PessoaId");

            migrationBuilder.CreateIndex(
                name: "IX_OperacaoTerritorialMudancas_PosicaoBaseId",
                table: "OperacaoTerritorialMudancas",
                column: "PosicaoBaseId");

            migrationBuilder.CreateIndex(
                name: "IX_OperacaoTerritorialMudancas_RegraBaseId",
                table: "OperacaoTerritorialMudancas",
                column: "RegraBaseId");

            migrationBuilder.CreateIndex(
                name: "IX_OperacaoTerritorialSimulacaoItens_PessoaId",
                table: "OperacaoTerritorialSimulacaoItens",
                column: "PessoaId");

            migrationBuilder.CreateIndex(
                name: "IX_OperacaoTerritorialSimulacaoItens_SimulacaoId_PessoaId",
                table: "OperacaoTerritorialSimulacaoItens",
                columns: new[] { "SimulacaoId", "PessoaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperacaoTerritorialSimulacaoItens_TerritorioAtualId",
                table: "OperacaoTerritorialSimulacaoItens",
                column: "TerritorioAtualId");

            migrationBuilder.CreateIndex(
                name: "IX_OperacaoTerritorialSimulacaoItens_TerritorioPropostoId",
                table: "OperacaoTerritorialSimulacaoItens",
                column: "TerritorioPropostoId");

            migrationBuilder.CreateIndex(
                name: "IX_OperacaoTerritorialSimulacoes_OperacaoId_SimuladaEm",
                table: "OperacaoTerritorialSimulacoes",
                columns: new[] { "OperacaoId", "SimuladaEm" });

            migrationBuilder.CreateIndex(
                name: "IX_OperacaoTerritorialSimulacoes_SimuladaPorId",
                table: "OperacaoTerritorialSimulacoes",
                column: "SimuladaPorId");

            migrationBuilder.CreateIndex(
                name: "IX_OperacoesTerritoriais_Ano_Sequencia",
                table: "OperacoesTerritoriais",
                columns: new[] { "Ano", "Sequencia" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperacoesTerritoriais_AplicadaPorId",
                table: "OperacoesTerritoriais",
                column: "AplicadaPorId");

            migrationBuilder.CreateIndex(
                name: "IX_OperacoesTerritoriais_CanceladaPorId",
                table: "OperacoesTerritoriais",
                column: "CanceladaPorId");

            migrationBuilder.CreateIndex(
                name: "IX_OperacoesTerritoriais_CriadaPorId",
                table: "OperacoesTerritoriais",
                column: "CriadaPorId");

            migrationBuilder.CreateIndex(
                name: "IX_OperacoesTerritoriais_DesfeitaPorId",
                table: "OperacoesTerritoriais",
                column: "DesfeitaPorId");

            migrationBuilder.CreateIndex(
                name: "IX_OperacoesTerritoriais_MapaId_Situacao_EfeitoEm",
                table: "OperacoesTerritoriais",
                columns: new[] { "MapaId", "Situacao", "EfeitoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_OperacoesTerritoriais_OperacaoAnteriorDoMapaId",
                table: "OperacoesTerritoriais",
                column: "OperacaoAnteriorDoMapaId");

            migrationBuilder.CreateIndex(
                name: "IX_OperacoesTerritoriais_SimulacaoAtualId",
                table: "OperacoesTerritoriais",
                column: "SimulacaoAtualId");

            migrationBuilder.CreateIndex(
                name: "IX_RegrasTerritorio_MapaId_TerritorioId_InicioEm",
                table: "RegrasTerritorio",
                columns: new[] { "MapaId", "TerritorioId", "InicioEm" });

            migrationBuilder.CreateIndex(
                name: "IX_RegrasTerritorio_OperacaoAnulacaoId",
                table: "RegrasTerritorio",
                column: "OperacaoAnulacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_RegrasTerritorio_OperacaoEncerramentoId",
                table: "RegrasTerritorio",
                column: "OperacaoEncerramentoId");

            migrationBuilder.CreateIndex(
                name: "IX_RegrasTerritorio_OperacaoId",
                table: "RegrasTerritorio",
                column: "OperacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_RegrasTerritorio_OperacaoMudancaId",
                table: "RegrasTerritorio",
                column: "OperacaoMudancaId");

            migrationBuilder.CreateIndex(
                name: "IX_RegrasTerritorio_TerritorioId_Numero",
                table: "RegrasTerritorio",
                columns: new[] { "TerritorioId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_RegrasTerritorio_Aberta",
                table: "RegrasTerritorio",
                column: "TerritorioId",
                unique: true,
                filter: "[FimEm] IS NULL AND [Ativo] = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_TerritorioPosicoes_OperacaoTerritorialMudancas_OperacaoMudancaId",
                table: "TerritorioPosicoes",
                column: "OperacaoMudancaId",
                principalTable: "OperacaoTerritorialMudancas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TerritorioPosicoes_OperacoesTerritoriais_OperacaoAnulacaoId",
                table: "TerritorioPosicoes",
                column: "OperacaoAnulacaoId",
                principalTable: "OperacoesTerritoriais",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TerritorioPosicoes_OperacoesTerritoriais_OperacaoEncerramentoId",
                table: "TerritorioPosicoes",
                column: "OperacaoEncerramentoId",
                principalTable: "OperacoesTerritoriais",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TerritorioPosicoes_OperacoesTerritoriais_OperacaoId",
                table: "TerritorioPosicoes",
                column: "OperacaoId",
                principalTable: "OperacoesTerritoriais",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TerritorioResponsaveis_OperacoesTerritoriais_OperacaoEncerramentoId",
                table: "TerritorioResponsaveis",
                column: "OperacaoEncerramentoId",
                principalTable: "OperacoesTerritoriais",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AtribuicoesTerritorio_ExcecoesTerritorio_ExcecaoId",
                table: "AtribuicoesTerritorio",
                column: "ExcecaoId",
                principalTable: "ExcecoesTerritorio",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AtribuicoesTerritorio_OperacoesTerritoriais_OperacaoAnulacaoId",
                table: "AtribuicoesTerritorio",
                column: "OperacaoAnulacaoId",
                principalTable: "OperacoesTerritoriais",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AtribuicoesTerritorio_OperacoesTerritoriais_OperacaoEncerramentoId",
                table: "AtribuicoesTerritorio",
                column: "OperacaoEncerramentoId",
                principalTable: "OperacoesTerritoriais",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AtribuicoesTerritorio_OperacoesTerritoriais_OperacaoId",
                table: "AtribuicoesTerritorio",
                column: "OperacaoId",
                principalTable: "OperacoesTerritoriais",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AtribuicoesTerritorio_RegrasTerritorio_RegraId",
                table: "AtribuicoesTerritorio",
                column: "RegraId",
                principalTable: "RegrasTerritorio",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExcecoesTerritorio_OperacaoTerritorialMudancas_OperacaoMudancaId",
                table: "ExcecoesTerritorio",
                column: "OperacaoMudancaId",
                principalTable: "OperacaoTerritorialMudancas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExcecoesTerritorio_OperacoesTerritoriais_OperacaoAnulacaoId",
                table: "ExcecoesTerritorio",
                column: "OperacaoAnulacaoId",
                principalTable: "OperacoesTerritoriais",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExcecoesTerritorio_OperacoesTerritoriais_OperacaoEncerramentoId",
                table: "ExcecoesTerritorio",
                column: "OperacaoEncerramentoId",
                principalTable: "OperacoesTerritoriais",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExcecoesTerritorio_OperacoesTerritoriais_OperacaoId",
                table: "ExcecoesTerritorio",
                column: "OperacaoId",
                principalTable: "OperacoesTerritoriais",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MapaTerritorialMotor_OperacoesTerritoriais_UltimaOperacaoId",
                table: "MapaTerritorialMotor",
                column: "UltimaOperacaoId",
                principalTable: "OperacoesTerritoriais",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OperacaoTerritorialFechamentos_OperacoesTerritoriais_OperacaoId",
                table: "OperacaoTerritorialFechamentos",
                column: "OperacaoId",
                principalTable: "OperacoesTerritoriais",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OperacaoTerritorialItens_OperacoesTerritoriais_OperacaoId",
                table: "OperacaoTerritorialItens",
                column: "OperacaoId",
                principalTable: "OperacoesTerritoriais",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OperacaoTerritorialMudancas_OperacoesTerritoriais_OperacaoId_MapaId",
                table: "OperacaoTerritorialMudancas",
                columns: new[] { "OperacaoId", "MapaId" },
                principalTable: "OperacoesTerritoriais",
                principalColumns: new[] { "Id", "MapaId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OperacaoTerritorialMudancas_RegrasTerritorio_RegraBaseId",
                table: "OperacaoTerritorialMudancas",
                column: "RegraBaseId",
                principalTable: "RegrasTerritorio",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OperacaoTerritorialSimulacaoItens_OperacaoTerritorialSimulacoes_SimulacaoId",
                table: "OperacaoTerritorialSimulacaoItens",
                column: "SimulacaoId",
                principalTable: "OperacaoTerritorialSimulacoes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OperacaoTerritorialSimulacoes_OperacoesTerritoriais_OperacaoId",
                table: "OperacaoTerritorialSimulacoes",
                column: "OperacaoId",
                principalTable: "OperacoesTerritoriais",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Fase 2b-1b (SqlMigracaoTerritorios): a linha do motor de cada mapa existente, as FKs (MapaId, Exclusivo) de
            // exceções e atribuições e os gatilhos 50073–50076 (READCOMMITTEDLOCK). Depois das tabelas e dos índices do EF.
            migrationBuilder.Sql(SqlMigracaoTerritorios.PreencherMotores);
            migrationBuilder.Sql(SqlMigracaoTerritorios.CriarChavesExclusivo);
            migrationBuilder.Sql(SqlMigracaoTerritorios.CriarProtecaoRegras);
            migrationBuilder.Sql(SqlMigracaoTerritorios.CriarProtecaoExcecoes);
            migrationBuilder.Sql(SqlMigracaoTerritorios.CriarProtecaoAtribuicoes);
            migrationBuilder.Sql(SqlMigracaoTerritorios.CriarProtecaoItens);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Antes de tudo: gatilhos e FKs de Exclusivo criados por SQL no Up (as tabelas, colunas e índices saem pelo EF).
            migrationBuilder.Sql(SqlMigracaoTerritorios.RemoverProtecoesMotor);

            migrationBuilder.DropForeignKey(
                name: "FK_TerritorioPosicoes_OperacaoTerritorialMudancas_OperacaoMudancaId",
                table: "TerritorioPosicoes");

            migrationBuilder.DropForeignKey(
                name: "FK_TerritorioPosicoes_OperacoesTerritoriais_OperacaoAnulacaoId",
                table: "TerritorioPosicoes");

            migrationBuilder.DropForeignKey(
                name: "FK_TerritorioPosicoes_OperacoesTerritoriais_OperacaoEncerramentoId",
                table: "TerritorioPosicoes");

            migrationBuilder.DropForeignKey(
                name: "FK_TerritorioPosicoes_OperacoesTerritoriais_OperacaoId",
                table: "TerritorioPosicoes");

            migrationBuilder.DropForeignKey(
                name: "FK_TerritorioResponsaveis_OperacoesTerritoriais_OperacaoEncerramentoId",
                table: "TerritorioResponsaveis");

            migrationBuilder.DropForeignKey(
                name: "FK_OperacaoTerritorialMudancas_ExcecoesTerritorio_ExcecaoBaseId",
                table: "OperacaoTerritorialMudancas");

            migrationBuilder.DropForeignKey(
                name: "FK_OperacaoTerritorialMudancas_OperacoesTerritoriais_OperacaoId_MapaId",
                table: "OperacaoTerritorialMudancas");

            migrationBuilder.DropForeignKey(
                name: "FK_OperacaoTerritorialSimulacoes_OperacoesTerritoriais_OperacaoId",
                table: "OperacaoTerritorialSimulacoes");

            migrationBuilder.DropForeignKey(
                name: "FK_RegrasTerritorio_OperacoesTerritoriais_OperacaoAnulacaoId",
                table: "RegrasTerritorio");

            migrationBuilder.DropForeignKey(
                name: "FK_RegrasTerritorio_OperacoesTerritoriais_OperacaoEncerramentoId",
                table: "RegrasTerritorio");

            migrationBuilder.DropForeignKey(
                name: "FK_RegrasTerritorio_OperacoesTerritoriais_OperacaoId",
                table: "RegrasTerritorio");

            migrationBuilder.DropForeignKey(
                name: "FK_OperacaoTerritorialMudancas_RegrasTerritorio_RegraBaseId",
                table: "OperacaoTerritorialMudancas");

            migrationBuilder.DropTable(
                name: "MapaTerritorialMotor");

            migrationBuilder.DropTable(
                name: "OperacaoTerritorialFechamentos");

            migrationBuilder.DropTable(
                name: "OperacaoTerritorialItens");

            migrationBuilder.DropTable(
                name: "OperacaoTerritorialSimulacaoItens");

            migrationBuilder.DropTable(
                name: "ParametrosTerritoriais");

            migrationBuilder.DropTable(
                name: "AtribuicoesTerritorio");

            migrationBuilder.DropTable(
                name: "ExcecoesTerritorio");

            migrationBuilder.DropTable(
                name: "OperacoesTerritoriais");

            migrationBuilder.DropTable(
                name: "OperacaoTerritorialSimulacoes");

            migrationBuilder.DropTable(
                name: "RegrasTerritorio");

            migrationBuilder.DropTable(
                name: "OperacaoTerritorialMudancas");

            migrationBuilder.DropIndex(
                name: "IX_TerritorioResponsaveis_OperacaoEncerramentoId",
                table: "TerritorioResponsaveis");

            migrationBuilder.DropIndex(
                name: "IX_TerritorioPosicoes_OperacaoAnulacaoId",
                table: "TerritorioPosicoes");

            migrationBuilder.DropIndex(
                name: "IX_TerritorioPosicoes_OperacaoEncerramentoId",
                table: "TerritorioPosicoes");

            migrationBuilder.DropIndex(
                name: "IX_TerritorioPosicoes_OperacaoId",
                table: "TerritorioPosicoes");

            migrationBuilder.DropIndex(
                name: "IX_TerritorioPosicoes_OperacaoMudancaId",
                table: "TerritorioPosicoes");

            migrationBuilder.DropIndex(
                name: "UX_MapasTerritoriais_Id_Exclusivo",
                table: "MapasTerritoriais");

            migrationBuilder.DropColumn(
                name: "OperacaoEncerramentoId",
                table: "TerritorioResponsaveis");

            migrationBuilder.DropColumn(
                name: "OperacaoAnulacaoId",
                table: "TerritorioPosicoes");

            migrationBuilder.DropColumn(
                name: "OperacaoEncerramentoId",
                table: "TerritorioPosicoes");

            migrationBuilder.DropColumn(
                name: "OperacaoId",
                table: "TerritorioPosicoes");

            migrationBuilder.DropColumn(
                name: "OperacaoMudancaId",
                table: "TerritorioPosicoes");

            migrationBuilder.DropColumn(
                name: "RegistrarNosDocumentos",
                table: "MapasTerritoriais");
        }
    }
}
