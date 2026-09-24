using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lone.Infrastructure.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class MunicipiosECamposPersonalizados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Guarda os textos de naturalidade antes de as colunas saírem (viram pendências no fim).
            migrationBuilder.Sql(SqlMigracaoMunicipios.GuardarNaturalidade);

            migrationBuilder.DropColumn(
                name: "NaturalidadeCidade",
                table: "Pessoas");

            migrationBuilder.DropColumn(
                name: "NaturalidadeUf",
                table: "Pessoas");

            migrationBuilder.AddColumn<int>(
                name: "NaturalidadeMunicipioId",
                table: "Pessoas",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SituacaoAlteradaEm",
                table: "Pessoas",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SituacaoMotivo",
                table: "Pessoas",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MunicipioId",
                table: "PessoaEnderecos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Descricao",
                table: "Auditoria",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CamposPersonalizados",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Entidade = table.Column<byte>(type: "tinyint", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Tipo = table.Column<byte>(type: "tinyint", nullable: false),
                    Obrigatorio = table.Column<bool>(type: "bit", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    Dica = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CasasDecimais = table.Column<byte>(type: "tinyint", nullable: true),
                    Minimo = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: true),
                    Maximo = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CamposPersonalizados", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Municipios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    NomeBusca = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false),
                    Uf = table.Column<string>(type: "char(2)", unicode: false, fixedLength: true, maxLength: 2, nullable: false),
                    CodigoUf = table.Column<byte>(type: "tinyint", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Municipios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CampoPersonalizadoOpcoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Texto = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    Ativa = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampoPersonalizadoOpcoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampoPersonalizadoOpcoes_CamposPersonalizados_CampoId",
                        column: x => x.CampoId,
                        principalTable: "CamposPersonalizados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PendenciasMunicipio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PessoaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Origem = table.Column<byte>(type: "tinyint", nullable: false),
                    RegistroId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TextoOriginal = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UfOriginal = table.Column<string>(type: "char(2)", unicode: false, fixedLength: true, maxLength: 2, nullable: true),
                    CodigoIbgeOriginal = table.Column<string>(type: "varchar(7)", unicode: false, maxLength: 7, nullable: true),
                    Observacao = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CriadaEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvidaEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvidaPor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MunicipioId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendenciasMunicipio", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PendenciasMunicipio_Municipios_MunicipioId",
                        column: x => x.MunicipioId,
                        principalTable: "Municipios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PendenciasMunicipio_Pessoas_PessoaId",
                        column: x => x.PessoaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PessoaValoresPersonalizados",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ValorTexto = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ValorNumero = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: true),
                    ValorData = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ValorLogico = table.Column<bool>(type: "bit", nullable: true),
                    OpcaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PessoaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PessoaValoresPersonalizados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PessoaValoresPersonalizados_CampoPersonalizadoOpcoes_OpcaoId",
                        column: x => x.OpcaoId,
                        principalTable: "CampoPersonalizadoOpcoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PessoaValoresPersonalizados_CamposPersonalizados_CampoId",
                        column: x => x.CampoId,
                        principalTable: "CamposPersonalizados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PessoaValoresPersonalizados_Pessoas_PessoaId",
                        column: x => x.PessoaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Pessoas_NaturalidadeMunicipioId",
                table: "Pessoas",
                column: "NaturalidadeMunicipioId");

            migrationBuilder.CreateIndex(
                name: "IX_Pessoas_Situacao",
                table: "Pessoas",
                column: "Situacao");

            migrationBuilder.CreateIndex(
                name: "IX_PessoaEnderecos_MunicipioId",
                table: "PessoaEnderecos",
                column: "MunicipioId");

            migrationBuilder.CreateIndex(
                name: "IX_CampoPersonalizadoOpcoes_CampoId_Ordem",
                table: "CampoPersonalizadoOpcoes",
                columns: new[] { "CampoId", "Ordem" });

            migrationBuilder.CreateIndex(
                name: "IX_CamposPersonalizados_Entidade_Ativo_Ordem",
                table: "CamposPersonalizados",
                columns: new[] { "Entidade", "Ativo", "Ordem" });

            migrationBuilder.CreateIndex(
                name: "IX_CamposPersonalizados_Entidade_Nome",
                table: "CamposPersonalizados",
                columns: new[] { "Entidade", "Nome" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Municipios_NomeBusca",
                table: "Municipios",
                column: "NomeBusca");

            migrationBuilder.CreateIndex(
                name: "IX_Municipios_Uf_NomeBusca",
                table: "Municipios",
                columns: new[] { "Uf", "NomeBusca" });

            migrationBuilder.CreateIndex(
                name: "IX_PendenciasMunicipio_MunicipioId",
                table: "PendenciasMunicipio",
                column: "MunicipioId");

            migrationBuilder.CreateIndex(
                name: "IX_PendenciasMunicipio_PessoaId_ResolvidaEm",
                table: "PendenciasMunicipio",
                columns: new[] { "PessoaId", "ResolvidaEm" });

            migrationBuilder.CreateIndex(
                name: "IX_PendenciasMunicipio_ResolvidaEm",
                table: "PendenciasMunicipio",
                column: "ResolvidaEm");

            migrationBuilder.CreateIndex(
                name: "IX_PessoaValoresPersonalizados_CampoId_OpcaoId",
                table: "PessoaValoresPersonalizados",
                columns: new[] { "CampoId", "OpcaoId" });

            migrationBuilder.CreateIndex(
                name: "IX_PessoaValoresPersonalizados_CampoId_ValorData",
                table: "PessoaValoresPersonalizados",
                columns: new[] { "CampoId", "ValorData" });

            migrationBuilder.CreateIndex(
                name: "IX_PessoaValoresPersonalizados_CampoId_ValorNumero",
                table: "PessoaValoresPersonalizados",
                columns: new[] { "CampoId", "ValorNumero" });

            migrationBuilder.CreateIndex(
                name: "IX_PessoaValoresPersonalizados_OpcaoId",
                table: "PessoaValoresPersonalizados",
                column: "OpcaoId");

            migrationBuilder.CreateIndex(
                name: "IX_PessoaValoresPersonalizados_PessoaId_CampoId",
                table: "PessoaValoresPersonalizados",
                columns: new[] { "PessoaId", "CampoId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PessoaEnderecos_Municipios_MunicipioId",
                table: "PessoaEnderecos",
                column: "MunicipioId",
                principalTable: "Municipios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Pessoas_Municipios_NaturalidadeMunicipioId",
                table: "Pessoas",
                column: "NaturalidadeMunicipioId",
                principalTable: "Municipios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Cada texto guardado vira pendência de município (a conciliação resolve o que conseguir).
            migrationBuilder.Sql(SqlMigracaoMunicipios.CriarPendenciasDeNaturalidade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PessoaEnderecos_Municipios_MunicipioId",
                table: "PessoaEnderecos");

            migrationBuilder.DropForeignKey(
                name: "FK_Pessoas_Municipios_NaturalidadeMunicipioId",
                table: "Pessoas");

            migrationBuilder.DropTable(
                name: "PendenciasMunicipio");

            migrationBuilder.DropTable(
                name: "PessoaValoresPersonalizados");

            migrationBuilder.DropTable(
                name: "Municipios");

            migrationBuilder.DropTable(
                name: "CampoPersonalizadoOpcoes");

            migrationBuilder.DropTable(
                name: "CamposPersonalizados");

            migrationBuilder.DropIndex(
                name: "IX_Pessoas_NaturalidadeMunicipioId",
                table: "Pessoas");

            migrationBuilder.DropIndex(
                name: "IX_Pessoas_Situacao",
                table: "Pessoas");

            migrationBuilder.DropIndex(
                name: "IX_PessoaEnderecos_MunicipioId",
                table: "PessoaEnderecos");

            migrationBuilder.DropColumn(
                name: "NaturalidadeMunicipioId",
                table: "Pessoas");

            migrationBuilder.DropColumn(
                name: "SituacaoAlteradaEm",
                table: "Pessoas");

            migrationBuilder.DropColumn(
                name: "SituacaoMotivo",
                table: "Pessoas");

            migrationBuilder.DropColumn(
                name: "MunicipioId",
                table: "PessoaEnderecos");

            migrationBuilder.DropColumn(
                name: "Descricao",
                table: "Auditoria");

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
        }
    }
}
