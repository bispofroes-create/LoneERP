using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lone.Infrastructure.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class DadosComplementaresPessoa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CapitalSocial",
                table: "Pessoas",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "CorRaca",
                table: "Pessoas",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DataAbertura",
                table: "Pessoas",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "Escolaridade",
                table: "Pessoas",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<byte>(
                name: "EstadoCivil",
                table: "Pessoas",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<byte>(
                name: "IdentidadeGenero",
                table: "Pessoas",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<string>(
                name: "Nacionalidade",
                table: "Pessoas",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NaturalidadeCidade",
                table: "Pessoas",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NaturalidadeUf",
                table: "Pessoas",
                type: "char(2)",
                unicode: false,
                fixedLength: true,
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NomeMae",
                table: "Pessoas",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NomePai",
                table: "Pessoas",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrigemCadastro",
                table: "Pessoas",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Porte",
                table: "Pessoas",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PrimeiroContatoEm",
                table: "Pessoas",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Profissao",
                table: "Pessoas",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "Sexo",
                table: "Pessoas",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<string>(
                name: "CnaesSecundarios",
                table: "Estabelecimentos",
                type: "varchar(1000)",
                unicode: false,
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PessoaConsentimentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Canal = table.Column<byte>(type: "tinyint", nullable: false),
                    Concedido = table.Column<bool>(type: "bit", nullable: false),
                    ConcedidoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevogadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Origem = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PessoaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PessoaConsentimentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PessoaConsentimentos_Pessoas_PessoaId",
                        column: x => x.PessoaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PessoaEtiquetas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Texto = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PessoaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PessoaEtiquetas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PessoaEtiquetas_Pessoas_PessoaId",
                        column: x => x.PessoaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PessoaSocios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Qualificacao = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Documento = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    EntradaEm = table.Column<DateOnly>(type: "date", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PessoaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PessoaSocios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PessoaSocios_Pessoas_PessoaId",
                        column: x => x.PessoaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Pessoas_DataNascimento",
                table: "Pessoas",
                column: "DataNascimento");

            migrationBuilder.CreateIndex(
                name: "IX_PessoaConsentimentos_PessoaId_Canal",
                table: "PessoaConsentimentos",
                columns: new[] { "PessoaId", "Canal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PessoaEtiquetas_PessoaId_Texto",
                table: "PessoaEtiquetas",
                columns: new[] { "PessoaId", "Texto" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PessoaEtiquetas_Texto",
                table: "PessoaEtiquetas",
                column: "Texto");

            migrationBuilder.CreateIndex(
                name: "IX_PessoaSocios_PessoaId",
                table: "PessoaSocios",
                column: "PessoaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PessoaConsentimentos");

            migrationBuilder.DropTable(
                name: "PessoaEtiquetas");

            migrationBuilder.DropTable(
                name: "PessoaSocios");

            migrationBuilder.DropIndex(
                name: "IX_Pessoas_DataNascimento",
                table: "Pessoas");

            migrationBuilder.DropColumn(
                name: "CapitalSocial",
                table: "Pessoas");

            migrationBuilder.DropColumn(
                name: "CorRaca",
                table: "Pessoas");

            migrationBuilder.DropColumn(
                name: "DataAbertura",
                table: "Pessoas");

            migrationBuilder.DropColumn(
                name: "Escolaridade",
                table: "Pessoas");

            migrationBuilder.DropColumn(
                name: "EstadoCivil",
                table: "Pessoas");

            migrationBuilder.DropColumn(
                name: "IdentidadeGenero",
                table: "Pessoas");

            migrationBuilder.DropColumn(
                name: "Nacionalidade",
                table: "Pessoas");

            migrationBuilder.DropColumn(
                name: "NaturalidadeCidade",
                table: "Pessoas");

            migrationBuilder.DropColumn(
                name: "NaturalidadeUf",
                table: "Pessoas");

            migrationBuilder.DropColumn(
                name: "NomeMae",
                table: "Pessoas");

            migrationBuilder.DropColumn(
                name: "NomePai",
                table: "Pessoas");

            migrationBuilder.DropColumn(
                name: "OrigemCadastro",
                table: "Pessoas");

            migrationBuilder.DropColumn(
                name: "Porte",
                table: "Pessoas");

            migrationBuilder.DropColumn(
                name: "PrimeiroContatoEm",
                table: "Pessoas");

            migrationBuilder.DropColumn(
                name: "Profissao",
                table: "Pessoas");

            migrationBuilder.DropColumn(
                name: "Sexo",
                table: "Pessoas");

            migrationBuilder.DropColumn(
                name: "CnaesSecundarios",
                table: "Estabelecimentos");
        }
    }
}
