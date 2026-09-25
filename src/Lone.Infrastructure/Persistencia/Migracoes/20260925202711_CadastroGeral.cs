using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Lone.Infrastructure.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class CadastroGeral : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CamposPersonalizados_Entidade_Nome",
                table: "CamposPersonalizados");

            migrationBuilder.AddColumn<bool>(
                name: "Ativo",
                table: "PessoaEnderecos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(SqlMigracaoCadastroGeral.AtivarEnderecos);


            migrationBuilder.AddColumn<string>(
                name: "Observacoes",
                table: "PessoaEnderecos",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TipoEnderecoId",
                table: "PessoaEnderecos",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Ativo",
                table: "PessoaDocumentos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "TipoDocumentoId",
                table: "PessoaDocumentos",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "ProdutorRural",
                table: "Estabelecimentos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "CondicaoPagamentoId",
                table: "ContasCliente",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PerfilComercialId",
                table: "ContasCliente",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Pesquisavel",
                table: "CamposPersonalizados",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "TipoDocumentoId",
                table: "CamposPersonalizados",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Visivel",
                table: "CamposPersonalizados",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(SqlMigracaoCadastroGeral.CamposVisiveis);


            migrationBuilder.AddColumn<string>(
                name: "Motivo",
                table: "Auditoria",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValorTextoBusca",
                table: "PessoaValoresPersonalizados",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                computedColumnSql: "CAST(LEFT([ValorTexto], 200) AS nvarchar(200))",
                stored: true);

            migrationBuilder.CreateTable(
                name: "AnexosDocumento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PessoaDocumentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NomeArquivo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TipoConteudo = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Tamanho = table.Column<long>(type: "bigint", nullable: false),
                    Hash = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    Caminho = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    EnviadoPor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PessoaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnexosDocumento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnexosDocumento_PessoaDocumentos_PessoaDocumentoId",
                        column: x => x.PessoaDocumentoId,
                        principalTable: "PessoaDocumentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnexosDocumento_Pessoas_PessoaId",
                        column: x => x.PessoaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Cargos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false, collation: "Latin1_General_CI_AI"),
                    OcupacaoCboId = table.Column<int>(type: "int", nullable: true),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cargos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cargos_OcupacoesCbo_OcupacaoCboId",
                        column: x => x.OcupacaoCboId,
                        principalTable: "OcupacoesCbo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CentrosCusto",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Codigo = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    PaiId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Analitico = table.Column<bool>(type: "bit", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CentrosCusto", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CentrosCusto_CentrosCusto_PaiId",
                        column: x => x.PaiId,
                        principalTable: "CentrosCusto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Cnaes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cnaes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CondicoesPagamento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false, collation: "Latin1_General_CI_AI"),
                    Parcelas = table.Column<string>(type: "varchar(250)", unicode: false, maxLength: 250, nullable: false),
                    AcrescimoPercentual = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: true),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CondicoesPagamento", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Departamentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false, collation: "Latin1_General_CI_AI"),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departamentos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EstabelecimentoCnaes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EstabelecimentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Codigo = table.Column<int>(type: "int", nullable: false),
                    Principal = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PessoaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EstabelecimentoCnaes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EstabelecimentoCnaes_Estabelecimentos_EstabelecimentoId",
                        column: x => x.EstabelecimentoId,
                        principalTable: "Estabelecimentos",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EstabelecimentoCnaes_Pessoas_PessoaId",
                        column: x => x.PessoaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FiltrosSalvos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false, collation: "Latin1_General_CI_AI"),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Autor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Compartilhado = table.Column<bool>(type: "bit", nullable: false),
                    Criterios = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiltrosSalvos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HistoricoFiscal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EstabelecimentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InicioEm = table.Column<DateOnly>(type: "date", nullable: false),
                    FimEm = table.Column<DateOnly>(type: "date", nullable: true),
                    RegimeTributario = table.Column<byte>(type: "tinyint", nullable: false),
                    IndicadorIE = table.Column<byte>(type: "tinyint", nullable: false),
                    InscricaoEstadual = table.Column<string>(type: "varchar(14)", unicode: false, maxLength: 14, nullable: true),
                    SituacaoReceita = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ProdutorRural = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PessoaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricoFiscal", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistoricoFiscal_Pessoas_PessoaId",
                        column: x => x.PessoaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(SqlMigracaoCadastroGeral.HistoricoFiscalECnaes);


            migrationBuilder.CreateTable(
                name: "Indicadores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Codigo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false, collation: "Latin1_General_CI_AI"),
                    Fonte = table.Column<byte>(type: "tinyint", nullable: false),
                    Unidade = table.Column<byte>(type: "tinyint", nullable: false),
                    Sentido = table.Column<byte>(type: "tinyint", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Indicadores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Interacoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DataHora = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Tipo = table.Column<byte>(type: "tinyint", nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Usuario = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PessoaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Interacoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Interacoes_Pessoas_PessoaId",
                        column: x => x.PessoaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Metas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    InicioEm = table.Column<DateOnly>(type: "date", nullable: false),
                    FimEm = table.Column<DateOnly>(type: "date", nullable: false),
                    Situacao = table.Column<byte>(type: "tinyint", nullable: false),
                    LimiteAtingimento = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    FechadaEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechadaPor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Metas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ParametrosRelacionamento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DiasEmRisco = table.Column<int>(type: "int", nullable: false),
                    DiasInativo = table.Column<int>(type: "int", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParametrosRelacionamento", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PessoaDocumentoValoresPersonalizados",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PessoaDocumentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ValorTextoBusca = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true, computedColumnSql: "CAST(LEFT([ValorTexto], 200) AS nvarchar(200))", stored: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PessoaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ValorTexto = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ValorNumero = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: true),
                    ValorData = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ValorLogico = table.Column<bool>(type: "bit", nullable: true),
                    OpcaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PessoaDocumentoValoresPersonalizados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PessoaDocumentoValoresPersonalizados_CampoPersonalizadoOpcoes_OpcaoId",
                        column: x => x.OpcaoId,
                        principalTable: "CampoPersonalizadoOpcoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PessoaDocumentoValoresPersonalizados_CamposPersonalizados_CampoId",
                        column: x => x.CampoId,
                        principalTable: "CamposPersonalizados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PessoaDocumentoValoresPersonalizados_PessoaDocumentos_PessoaDocumentoId",
                        column: x => x.PessoaDocumentoId,
                        principalTable: "PessoaDocumentos",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PessoaDocumentoValoresPersonalizados_Pessoas_PessoaId",
                        column: x => x.PessoaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TiposCarteira",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false, collation: "Latin1_General_CI_AI"),
                    Principal = table.Column<bool>(type: "bit", nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TiposCarteira", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TiposDocumento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false, collation: "Latin1_General_CI_AI"),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    TipoSistema = table.Column<byte>(type: "tinyint", nullable: true),
                    ExigeValidade = table.Column<bool>(type: "bit", nullable: false),
                    DiasAvisoVencimento = table.Column<int>(type: "int", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TiposDocumento", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TiposEndereco",
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
                    table.PrimaryKey("PK_TiposEndereco", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VinculosColaborador",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Matricula = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Tipo = table.Column<byte>(type: "tinyint", nullable: false),
                    AdmissaoEm = table.Column<DateOnly>(type: "date", nullable: false),
                    DesligamentoEm = table.Column<DateOnly>(type: "date", nullable: true),
                    MotivoDesligamento = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    JornadaSemanal = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    Observacoes = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PessoaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VinculosColaborador", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VinculosColaborador_Pessoas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VinculosColaborador_Pessoas_PessoaId",
                        column: x => x.PessoaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExcecoesComerciais",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InicioEm = table.Column<DateOnly>(type: "date", nullable: false),
                    FimEm = table.Column<DateOnly>(type: "date", nullable: true),
                    LimiteCredito = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: true),
                    DescontoMaximo = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    DiasMaximoAtraso = table.Column<int>(type: "int", nullable: true),
                    CondicaoPagamentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExigeAprovacaoAcimaLimite = table.Column<bool>(type: "bit", nullable: true),
                    Motivo = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PessoaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExcecoesComerciais", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExcecoesComerciais_CondicoesPagamento_CondicaoPagamentoId",
                        column: x => x.CondicaoPagamentoId,
                        principalTable: "CondicoesPagamento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExcecoesComerciais_Pessoas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ExcecoesComerciais_Pessoas_PessoaId",
                        column: x => x.PessoaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PerfisComerciais",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false, collation: "Latin1_General_CI_AI"),
                    LimiteCredito = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: true),
                    DescontoMaximo = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    DiasMaximoAtraso = table.Column<int>(type: "int", nullable: true),
                    CondicaoPagamentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExigeAprovacaoAcimaLimite = table.Column<bool>(type: "bit", nullable: true),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerfisComerciais", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PerfisComerciais_CondicoesPagamento_CondicaoPagamentoId",
                        column: x => x.CondicaoPagamentoId,
                        principalTable: "CondicoesPagamento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Equipes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false, collation: "Latin1_General_CI_AI"),
                    DepartamentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LiderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Equipes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Equipes_Departamentos_DepartamentoId",
                        column: x => x.DepartamentoId,
                        principalTable: "Departamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Equipes_Pessoas_LiderId",
                        column: x => x.LiderId,
                        principalTable: "Pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Setores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DepartamentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false, collation: "Latin1_General_CI_AI"),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Setores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Setores_Departamentos_DepartamentoId",
                        column: x => x.DepartamentoId,
                        principalTable: "Departamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MetaFaixas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MetaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InicioPercentual = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    PercentualPremio = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetaFaixas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MetaFaixas_Metas_MetaId",
                        column: x => x.MetaId,
                        principalTable: "Metas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MetaItens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MetaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IndicadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Peso = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetaItens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MetaItens_Indicadores_IndicadorId",
                        column: x => x.IndicadorId,
                        principalTable: "Indicadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MetaItens_Metas_MetaId",
                        column: x => x.MetaId,
                        principalTable: "Metas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MetaParticipantes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MetaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nivel = table.Column<byte>(type: "tinyint", nullable: false),
                    ReferenciaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NotaFinal = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: true),
                    Faixa = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    PercentualPremio = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetaParticipantes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MetaParticipantes_Metas_MetaId",
                        column: x => x.MetaId,
                        principalTable: "Metas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CarteiraClientes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TipoCarteiraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendedorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InicioEm = table.Column<DateOnly>(type: "date", nullable: false),
                    FimEm = table.Column<DateOnly>(type: "date", nullable: true),
                    Exclusivo = table.Column<bool>(type: "bit", nullable: false),
                    Observacao = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PessoaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarteiraClientes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CarteiraClientes_Pessoas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CarteiraClientes_Pessoas_PessoaId",
                        column: x => x.PessoaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CarteiraClientes_Pessoas_VendedorId",
                        column: x => x.VendedorId,
                        principalTable: "Pessoas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CarteiraClientes_TiposCarteira_TipoCarteiraId",
                        column: x => x.TipoCarteiraId,
                        principalTable: "TiposCarteira",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MembrosEquipe",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EquipeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PessoaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InicioEm = table.Column<DateOnly>(type: "date", nullable: false),
                    FimEm = table.Column<DateOnly>(type: "date", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembrosEquipe", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MembrosEquipe_Equipes_EquipeId",
                        column: x => x.EquipeId,
                        principalTable: "Equipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MembrosEquipe_Pessoas_PessoaId",
                        column: x => x.PessoaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LotacoesColaborador",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VinculoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InicioEm = table.Column<DateOnly>(type: "date", nullable: false),
                    FimEm = table.Column<DateOnly>(type: "date", nullable: true),
                    CargoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DepartamentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SetorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CentroCustoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GestorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PessoaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotacoesColaborador", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LotacoesColaborador_Cargos_CargoId",
                        column: x => x.CargoId,
                        principalTable: "Cargos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LotacoesColaborador_CentrosCusto_CentroCustoId",
                        column: x => x.CentroCustoId,
                        principalTable: "CentrosCusto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LotacoesColaborador_Departamentos_DepartamentoId",
                        column: x => x.DepartamentoId,
                        principalTable: "Departamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LotacoesColaborador_Pessoas_GestorId",
                        column: x => x.GestorId,
                        principalTable: "Pessoas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LotacoesColaborador_Pessoas_PessoaId",
                        column: x => x.PessoaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LotacoesColaborador_Setores_SetorId",
                        column: x => x.SetorId,
                        principalTable: "Setores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LotacoesColaborador_VinculosColaborador_VinculoId",
                        column: x => x.VinculoId,
                        principalTable: "VinculosColaborador",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MetaAlvos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MetaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParticipanteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Alvo = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Realizado = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    OrigemRealizado = table.Column<byte>(type: "tinyint", nullable: true),
                    RealizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RealizadoPor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetaAlvos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MetaAlvos_MetaItens_ItemId",
                        column: x => x.ItemId,
                        principalTable: "MetaItens",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MetaAlvos_MetaParticipantes_ParticipanteId",
                        column: x => x.ParticipanteId,
                        principalTable: "MetaParticipantes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MetaAlvos_Metas_MetaId",
                        column: x => x.MetaId,
                        principalTable: "Metas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Indicadores",
                columns: new[] { "Id", "Ativo", "AtualizadoEm", "Codigo", "CriadoEm", "Fonte", "Nome", "Sentido", "Unidade" },
                values: new object[,]
                {
                    { new Guid("7a9e1c06-0000-0000-0000-000000000001"), true, null, "NOVOS_CLIENTES", new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), (byte)1, "Novos clientes", (byte)0, (byte)0 },
                    { new Guid("7a9e1c06-0000-0000-0000-000000000002"), true, null, "CLIENTES_ATIVOS", new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), (byte)2, "Clientes ativos", (byte)0, (byte)0 },
                    { new Guid("7a9e1c06-0000-0000-0000-000000000003"), true, null, "CLIENTES_REATIVADOS", new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), (byte)3, "Clientes reativados", (byte)0, (byte)0 },
                    { new Guid("7a9e1c06-0000-0000-0000-000000000004"), true, null, "INTERACOES", new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), (byte)4, "Interações com clientes", (byte)0, (byte)0 }
                });

            migrationBuilder.InsertData(
                table: "ParametrosRelacionamento",
                columns: new[] { "Id", "AtualizadoEm", "CriadoEm", "DiasEmRisco", "DiasInativo" },
                values: new object[] { new Guid("7a9e1c05-0000-0000-0000-000000000001"), null, new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), 90, 180 });

            migrationBuilder.InsertData(
                table: "TiposCarteira",
                columns: new[] { "Id", "Ativo", "AtualizadoEm", "CriadoEm", "Nome", "Ordem", "Principal" },
                values: new object[,]
                {
                    { new Guid("7a9e1c04-0000-0000-0000-000000000001"), true, null, new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), "Vendedor", 1, true },
                    { new Guid("7a9e1c04-0000-0000-0000-000000000002"), true, null, new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), "Representante", 2, false },
                    { new Guid("7a9e1c04-0000-0000-0000-000000000003"), true, null, new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), "Televendas", 3, false },
                    { new Guid("7a9e1c04-0000-0000-0000-000000000004"), true, null, new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), "Supervisor", 4, false }
                });

            migrationBuilder.Sql(SqlMigracaoCadastroGeral.CarteiraDosVendedoresPadrao);


            migrationBuilder.InsertData(
                table: "TiposDocumento",
                columns: new[] { "Id", "Ativo", "AtualizadoEm", "CriadoEm", "DiasAvisoVencimento", "ExigeValidade", "Nome", "Ordem", "TipoSistema" },
                values: new object[,]
                {
                    { new Guid("7a9e1c03-0000-0000-0000-000000000001"), true, null, new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), 30, false, "RG", 1, (byte)0 },
                    { new Guid("7a9e1c03-0000-0000-0000-000000000002"), true, null, new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), 30, true, "CNH", 2, (byte)1 },
                    { new Guid("7a9e1c03-0000-0000-0000-000000000003"), true, null, new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), 30, true, "Passaporte", 3, (byte)2 },
                    { new Guid("7a9e1c03-0000-0000-0000-000000000004"), true, null, new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), 30, false, "Documento estrangeiro", 4, (byte)3 },
                    { new Guid("7a9e1c03-0000-0000-0000-000000000009"), true, null, new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), 30, false, "Outro", 9, (byte)9 }
                });

            migrationBuilder.InsertData(
                table: "TiposEndereco",
                columns: new[] { "Id", "Ativo", "AtualizadoEm", "CriadoEm", "Nome", "Ordem" },
                values: new object[,]
                {
                    { new Guid("7a9e1c02-0000-0000-0000-000000000001"), true, null, new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), "Sede", 1 },
                    { new Guid("7a9e1c02-0000-0000-0000-000000000002"), true, null, new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), "Filial", 2 },
                    { new Guid("7a9e1c02-0000-0000-0000-000000000003"), true, null, new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), "Depósito", 3 },
                    { new Guid("7a9e1c02-0000-0000-0000-000000000004"), true, null, new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), "Residência", 4 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_PessoaValoresPersonalizados_CampoId_ValorTextoBusca",
                table: "PessoaValoresPersonalizados",
                columns: new[] { "CampoId", "ValorTextoBusca" });

            migrationBuilder.CreateIndex(
                name: "IX_Pessoas_CriadoEm",
                table: "Pessoas",
                column: "CriadoEm");

            migrationBuilder.CreateIndex(
                name: "IX_PessoaEnderecos_TipoEnderecoId",
                table: "PessoaEnderecos",
                column: "TipoEnderecoId");

            migrationBuilder.CreateIndex(
                name: "IX_PessoaEnderecos_Uf_MunicipioId",
                table: "PessoaEnderecos",
                columns: new[] { "Uf", "MunicipioId" });

            migrationBuilder.CreateIndex(
                name: "IX_PessoaDocumentos_TipoDocumentoId_ValidoAte",
                table: "PessoaDocumentos",
                columns: new[] { "TipoDocumentoId", "ValidoAte" },
                filter: "[Ativo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_PessoaDocumentos_ValidoAte_PessoaId",
                table: "PessoaDocumentos",
                columns: new[] { "ValidoAte", "PessoaId" },
                filter: "[Ativo] = 1 AND [ValidoAte] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ContasCliente_CondicaoPagamentoId",
                table: "ContasCliente",
                column: "CondicaoPagamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_ContasCliente_PerfilComercialId",
                table: "ContasCliente",
                column: "PerfilComercialId");

            migrationBuilder.CreateIndex(
                name: "IX_CamposPersonalizados_Entidade_TipoDocumentoId_Nome",
                table: "CamposPersonalizados",
                columns: new[] { "Entidade", "TipoDocumentoId", "Nome" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CamposPersonalizados_TipoDocumentoId",
                table: "CamposPersonalizados",
                column: "TipoDocumentoId");

            migrationBuilder.CreateIndex(
                name: "IX_AnexosDocumento_PessoaDocumentoId",
                table: "AnexosDocumento",
                column: "PessoaDocumentoId");

            migrationBuilder.CreateIndex(
                name: "IX_AnexosDocumento_PessoaId",
                table: "AnexosDocumento",
                column: "PessoaId");

            migrationBuilder.CreateIndex(
                name: "IX_Cargos_Nome",
                table: "Cargos",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cargos_OcupacaoCboId",
                table: "Cargos",
                column: "OcupacaoCboId");

            migrationBuilder.CreateIndex(
                name: "IX_CarteiraClientes_EmpresaId",
                table: "CarteiraClientes",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_CarteiraClientes_PessoaId_InicioEm",
                table: "CarteiraClientes",
                columns: new[] { "PessoaId", "InicioEm" });

            migrationBuilder.CreateIndex(
                name: "IX_CarteiraClientes_TipoCarteiraId",
                table: "CarteiraClientes",
                column: "TipoCarteiraId");

            migrationBuilder.CreateIndex(
                name: "IX_CarteiraClientes_VendedorId_Ativo_FimEm",
                table: "CarteiraClientes",
                columns: new[] { "VendedorId", "Ativo", "FimEm" });

            migrationBuilder.CreateIndex(
                name: "IX_CentrosCusto_Codigo",
                table: "CentrosCusto",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CentrosCusto_PaiId",
                table: "CentrosCusto",
                column: "PaiId");

            migrationBuilder.CreateIndex(
                name: "IX_CondicoesPagamento_Nome",
                table: "CondicoesPagamento",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Departamentos_Nome",
                table: "Departamentos",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Equipes_DepartamentoId",
                table: "Equipes",
                column: "DepartamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_Equipes_LiderId",
                table: "Equipes",
                column: "LiderId");

            migrationBuilder.CreateIndex(
                name: "IX_Equipes_Nome",
                table: "Equipes",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EstabelecimentoCnaes_Codigo_Principal",
                table: "EstabelecimentoCnaes",
                columns: new[] { "Codigo", "Principal" });

            migrationBuilder.CreateIndex(
                name: "IX_EstabelecimentoCnaes_EstabelecimentoId_Codigo",
                table: "EstabelecimentoCnaes",
                columns: new[] { "EstabelecimentoId", "Codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EstabelecimentoCnaes_PessoaId",
                table: "EstabelecimentoCnaes",
                column: "PessoaId");

            migrationBuilder.CreateIndex(
                name: "IX_ExcecoesComerciais_CondicaoPagamentoId",
                table: "ExcecoesComerciais",
                column: "CondicaoPagamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_ExcecoesComerciais_EmpresaId",
                table: "ExcecoesComerciais",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_ExcecoesComerciais_PessoaId_InicioEm",
                table: "ExcecoesComerciais",
                columns: new[] { "PessoaId", "InicioEm" });

            migrationBuilder.CreateIndex(
                name: "IX_FiltrosSalvos_Compartilhado",
                table: "FiltrosSalvos",
                column: "Compartilhado",
                filter: "[Compartilhado] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_FiltrosSalvos_UsuarioId_Ativo",
                table: "FiltrosSalvos",
                columns: new[] { "UsuarioId", "Ativo" });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricoFiscal_EstabelecimentoId_InicioEm",
                table: "HistoricoFiscal",
                columns: new[] { "EstabelecimentoId", "InicioEm" });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricoFiscal_PessoaId",
                table: "HistoricoFiscal",
                column: "PessoaId");

            migrationBuilder.CreateIndex(
                name: "IX_Indicadores_Codigo",
                table: "Indicadores",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Indicadores_Nome",
                table: "Indicadores",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Interacoes_PessoaId_DataHora",
                table: "Interacoes",
                columns: new[] { "PessoaId", "DataHora" });

            migrationBuilder.CreateIndex(
                name: "IX_LotacoesColaborador_CargoId",
                table: "LotacoesColaborador",
                column: "CargoId");

            migrationBuilder.CreateIndex(
                name: "IX_LotacoesColaborador_CentroCustoId",
                table: "LotacoesColaborador",
                column: "CentroCustoId");

            migrationBuilder.CreateIndex(
                name: "IX_LotacoesColaborador_DepartamentoId_InicioEm",
                table: "LotacoesColaborador",
                columns: new[] { "DepartamentoId", "InicioEm" });

            migrationBuilder.CreateIndex(
                name: "IX_LotacoesColaborador_GestorId_FimEm",
                table: "LotacoesColaborador",
                columns: new[] { "GestorId", "FimEm" });

            migrationBuilder.CreateIndex(
                name: "IX_LotacoesColaborador_PessoaId",
                table: "LotacoesColaborador",
                column: "PessoaId");

            migrationBuilder.CreateIndex(
                name: "IX_LotacoesColaborador_SetorId",
                table: "LotacoesColaborador",
                column: "SetorId");

            migrationBuilder.CreateIndex(
                name: "IX_LotacoesColaborador_VinculoId_InicioEm",
                table: "LotacoesColaborador",
                columns: new[] { "VinculoId", "InicioEm" });

            migrationBuilder.CreateIndex(
                name: "IX_MembrosEquipe_EquipeId_PessoaId",
                table: "MembrosEquipe",
                columns: new[] { "EquipeId", "PessoaId" });

            migrationBuilder.CreateIndex(
                name: "IX_MembrosEquipe_PessoaId",
                table: "MembrosEquipe",
                column: "PessoaId");

            migrationBuilder.CreateIndex(
                name: "IX_MetaAlvos_ItemId",
                table: "MetaAlvos",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MetaAlvos_MetaId_ParticipanteId_ItemId",
                table: "MetaAlvos",
                columns: new[] { "MetaId", "ParticipanteId", "ItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetaAlvos_ParticipanteId",
                table: "MetaAlvos",
                column: "ParticipanteId");

            migrationBuilder.CreateIndex(
                name: "IX_MetaFaixas_MetaId",
                table: "MetaFaixas",
                column: "MetaId");

            migrationBuilder.CreateIndex(
                name: "IX_MetaItens_IndicadorId",
                table: "MetaItens",
                column: "IndicadorId");

            migrationBuilder.CreateIndex(
                name: "IX_MetaItens_MetaId_IndicadorId",
                table: "MetaItens",
                columns: new[] { "MetaId", "IndicadorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetaParticipantes_MetaId_Nivel_ReferenciaId",
                table: "MetaParticipantes",
                columns: new[] { "MetaId", "Nivel", "ReferenciaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetaParticipantes_Nivel_ReferenciaId",
                table: "MetaParticipantes",
                columns: new[] { "Nivel", "ReferenciaId" });

            migrationBuilder.CreateIndex(
                name: "IX_Metas_InicioEm_FimEm",
                table: "Metas",
                columns: new[] { "InicioEm", "FimEm" });

            migrationBuilder.CreateIndex(
                name: "IX_PerfisComerciais_CondicaoPagamentoId",
                table: "PerfisComerciais",
                column: "CondicaoPagamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_PerfisComerciais_Nome",
                table: "PerfisComerciais",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PessoaDocumentoValoresPersonalizados_CampoId_OpcaoId",
                table: "PessoaDocumentoValoresPersonalizados",
                columns: new[] { "CampoId", "OpcaoId" });

            migrationBuilder.CreateIndex(
                name: "IX_PessoaDocumentoValoresPersonalizados_CampoId_ValorData",
                table: "PessoaDocumentoValoresPersonalizados",
                columns: new[] { "CampoId", "ValorData" });

            migrationBuilder.CreateIndex(
                name: "IX_PessoaDocumentoValoresPersonalizados_CampoId_ValorNumero",
                table: "PessoaDocumentoValoresPersonalizados",
                columns: new[] { "CampoId", "ValorNumero" });

            migrationBuilder.CreateIndex(
                name: "IX_PessoaDocumentoValoresPersonalizados_CampoId_ValorTextoBusca",
                table: "PessoaDocumentoValoresPersonalizados",
                columns: new[] { "CampoId", "ValorTextoBusca" });

            migrationBuilder.CreateIndex(
                name: "IX_PessoaDocumentoValoresPersonalizados_OpcaoId",
                table: "PessoaDocumentoValoresPersonalizados",
                column: "OpcaoId");

            migrationBuilder.CreateIndex(
                name: "IX_PessoaDocumentoValoresPersonalizados_PessoaDocumentoId_CampoId",
                table: "PessoaDocumentoValoresPersonalizados",
                columns: new[] { "PessoaDocumentoId", "CampoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PessoaDocumentoValoresPersonalizados_PessoaId",
                table: "PessoaDocumentoValoresPersonalizados",
                column: "PessoaId");

            migrationBuilder.CreateIndex(
                name: "IX_Setores_DepartamentoId_Nome",
                table: "Setores",
                columns: new[] { "DepartamentoId", "Nome" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TiposCarteira_Nome",
                table: "TiposCarteira",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TiposCarteira_Principal",
                table: "TiposCarteira",
                column: "Principal",
                unique: true,
                filter: "[Principal] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_TiposDocumento_Nome",
                table: "TiposDocumento",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TiposDocumento_TipoSistema",
                table: "TiposDocumento",
                column: "TipoSistema",
                unique: true,
                filter: "[TipoSistema] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TiposEndereco_Nome",
                table: "TiposEndereco",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VinculosColaborador_EmpresaId_Matricula",
                table: "VinculosColaborador",
                columns: new[] { "EmpresaId", "Matricula" },
                unique: true,
                filter: "[Matricula] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_VinculosColaborador_PessoaId",
                table: "VinculosColaborador",
                column: "PessoaId");

            migrationBuilder.AddForeignKey(
                name: "FK_CamposPersonalizados_TiposDocumento_TipoDocumentoId",
                table: "CamposPersonalizados",
                column: "TipoDocumentoId",
                principalTable: "TiposDocumento",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ContasCliente_CondicoesPagamento_CondicaoPagamentoId",
                table: "ContasCliente",
                column: "CondicaoPagamentoId",
                principalTable: "CondicoesPagamento",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ContasCliente_PerfisComerciais_PerfilComercialId",
                table: "ContasCliente",
                column: "PerfilComercialId",
                principalTable: "PerfisComerciais",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);


            migrationBuilder.Sql(SqlMigracaoCadastroGeral.LigarDocumentosAosTipos);

            migrationBuilder.AddForeignKey(
                name: "FK_PessoaDocumentos_TiposDocumento_TipoDocumentoId",
                table: "PessoaDocumentos",
                column: "TipoDocumentoId",
                principalTable: "TiposDocumento",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PessoaEnderecos_TiposEndereco_TipoEnderecoId",
                table: "PessoaEnderecos",
                column: "TipoEnderecoId",
                principalTable: "TiposEndereco",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CamposPersonalizados_TiposDocumento_TipoDocumentoId",
                table: "CamposPersonalizados");

            migrationBuilder.DropForeignKey(
                name: "FK_ContasCliente_CondicoesPagamento_CondicaoPagamentoId",
                table: "ContasCliente");

            migrationBuilder.DropForeignKey(
                name: "FK_ContasCliente_PerfisComerciais_PerfilComercialId",
                table: "ContasCliente");

            migrationBuilder.DropForeignKey(
                name: "FK_PessoaDocumentos_TiposDocumento_TipoDocumentoId",
                table: "PessoaDocumentos");

            migrationBuilder.DropForeignKey(
                name: "FK_PessoaEnderecos_TiposEndereco_TipoEnderecoId",
                table: "PessoaEnderecos");

            migrationBuilder.DropTable(
                name: "AnexosDocumento");

            migrationBuilder.DropTable(
                name: "CarteiraClientes");

            migrationBuilder.DropTable(
                name: "Cnaes");

            migrationBuilder.DropTable(
                name: "EstabelecimentoCnaes");

            migrationBuilder.DropTable(
                name: "ExcecoesComerciais");

            migrationBuilder.DropTable(
                name: "FiltrosSalvos");

            migrationBuilder.DropTable(
                name: "HistoricoFiscal");

            migrationBuilder.DropTable(
                name: "Interacoes");

            migrationBuilder.DropTable(
                name: "LotacoesColaborador");

            migrationBuilder.DropTable(
                name: "MembrosEquipe");

            migrationBuilder.DropTable(
                name: "MetaAlvos");

            migrationBuilder.DropTable(
                name: "MetaFaixas");

            migrationBuilder.DropTable(
                name: "ParametrosRelacionamento");

            migrationBuilder.DropTable(
                name: "PerfisComerciais");

            migrationBuilder.DropTable(
                name: "PessoaDocumentoValoresPersonalizados");

            migrationBuilder.DropTable(
                name: "TiposDocumento");

            migrationBuilder.DropTable(
                name: "TiposEndereco");

            migrationBuilder.DropTable(
                name: "TiposCarteira");

            migrationBuilder.DropTable(
                name: "Cargos");

            migrationBuilder.DropTable(
                name: "CentrosCusto");

            migrationBuilder.DropTable(
                name: "Setores");

            migrationBuilder.DropTable(
                name: "VinculosColaborador");

            migrationBuilder.DropTable(
                name: "Equipes");

            migrationBuilder.DropTable(
                name: "MetaItens");

            migrationBuilder.DropTable(
                name: "MetaParticipantes");

            migrationBuilder.DropTable(
                name: "CondicoesPagamento");

            migrationBuilder.DropTable(
                name: "Departamentos");

            migrationBuilder.DropTable(
                name: "Indicadores");

            migrationBuilder.DropTable(
                name: "Metas");

            migrationBuilder.DropIndex(
                name: "IX_PessoaValoresPersonalizados_CampoId_ValorTextoBusca",
                table: "PessoaValoresPersonalizados");

            migrationBuilder.DropIndex(
                name: "IX_Pessoas_CriadoEm",
                table: "Pessoas");

            migrationBuilder.DropIndex(
                name: "IX_PessoaEnderecos_TipoEnderecoId",
                table: "PessoaEnderecos");

            migrationBuilder.DropIndex(
                name: "IX_PessoaEnderecos_Uf_MunicipioId",
                table: "PessoaEnderecos");

            migrationBuilder.DropIndex(
                name: "IX_PessoaDocumentos_TipoDocumentoId_ValidoAte",
                table: "PessoaDocumentos");

            migrationBuilder.DropIndex(
                name: "IX_PessoaDocumentos_ValidoAte_PessoaId",
                table: "PessoaDocumentos");

            migrationBuilder.DropIndex(
                name: "IX_ContasCliente_CondicaoPagamentoId",
                table: "ContasCliente");

            migrationBuilder.DropIndex(
                name: "IX_ContasCliente_PerfilComercialId",
                table: "ContasCliente");

            migrationBuilder.DropIndex(
                name: "IX_CamposPersonalizados_Entidade_TipoDocumentoId_Nome",
                table: "CamposPersonalizados");

            migrationBuilder.DropIndex(
                name: "IX_CamposPersonalizados_TipoDocumentoId",
                table: "CamposPersonalizados");

            migrationBuilder.DropColumn(
                name: "ValorTextoBusca",
                table: "PessoaValoresPersonalizados");

            migrationBuilder.DropColumn(
                name: "Ativo",
                table: "PessoaEnderecos");

            migrationBuilder.DropColumn(
                name: "Observacoes",
                table: "PessoaEnderecos");

            migrationBuilder.DropColumn(
                name: "TipoEnderecoId",
                table: "PessoaEnderecos");

            migrationBuilder.DropColumn(
                name: "Ativo",
                table: "PessoaDocumentos");

            migrationBuilder.DropColumn(
                name: "TipoDocumentoId",
                table: "PessoaDocumentos");

            migrationBuilder.DropColumn(
                name: "ProdutorRural",
                table: "Estabelecimentos");

            migrationBuilder.DropColumn(
                name: "CondicaoPagamentoId",
                table: "ContasCliente");

            migrationBuilder.DropColumn(
                name: "PerfilComercialId",
                table: "ContasCliente");

            migrationBuilder.DropColumn(
                name: "Pesquisavel",
                table: "CamposPersonalizados");

            migrationBuilder.DropColumn(
                name: "TipoDocumentoId",
                table: "CamposPersonalizados");

            migrationBuilder.DropColumn(
                name: "Visivel",
                table: "CamposPersonalizados");

            migrationBuilder.DropColumn(
                name: "Motivo",
                table: "Auditoria");

            migrationBuilder.CreateIndex(
                name: "IX_CamposPersonalizados_Entidade_Nome",
                table: "CamposPersonalizados",
                columns: new[] { "Entidade", "Nome" },
                unique: true);
        }
    }
}
