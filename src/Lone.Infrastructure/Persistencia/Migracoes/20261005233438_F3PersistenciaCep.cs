using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lone.Infrastructure.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class F3PersistenciaCep : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CepConferidoEm",
                table: "PessoaEnderecos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "CepFonte",
                table: "PessoaEnderecos",
                type: "tinyint",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "CepSituacao",
                table: "PessoaEnderecos",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.CreateTable(
                name: "CacheCep",
                columns: table => new
                {
                    Chave = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Tipo = table.Column<byte>(type: "tinyint", nullable: false),
                    Cep = table.Column<string>(type: "varchar(8)", unicode: false, maxLength: 8, nullable: true),
                    Situacao = table.Column<byte>(type: "tinyint", nullable: false),
                    Fonte = table.Column<byte>(type: "tinyint", nullable: false),
                    ConsultadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiraEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UtilizavelAte = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LimiteAtingido = table.Column<bool>(type: "bit", nullable: false),
                    Registros = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CacheCep", x => x.Chave);
                });

            migrationBuilder.CreateTable(
                name: "ConsultasCep",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Operacao = table.Column<byte>(type: "tinyint", nullable: false),
                    Tipo = table.Column<byte>(type: "tinyint", nullable: false),
                    Chave = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Cep = table.Column<string>(type: "varchar(8)", unicode: false, maxLength: 8, nullable: true),
                    Fonte = table.Column<byte>(type: "tinyint", nullable: true),
                    OcorridoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DuracaoMs = table.Column<int>(type: "int", nullable: false),
                    Resultado = table.Column<byte>(type: "tinyint", nullable: false),
                    Origem = table.Column<byte>(type: "tinyint", nullable: false),
                    LimiteAtingido = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsultasCep", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConsultasCep_OcorridoEm",
                table: "ConsultasCep",
                column: "OcorridoEm");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CacheCep");

            migrationBuilder.DropTable(
                name: "ConsultasCep");

            migrationBuilder.DropColumn(
                name: "CepConferidoEm",
                table: "PessoaEnderecos");

            migrationBuilder.DropColumn(
                name: "CepFonte",
                table: "PessoaEnderecos");

            migrationBuilder.DropColumn(
                name: "CepSituacao",
                table: "PessoaEnderecos");
        }
    }
}
