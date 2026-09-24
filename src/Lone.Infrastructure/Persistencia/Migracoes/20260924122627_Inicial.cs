using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Lone.Infrastructure.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "SeqPessoaCodigo");

            migrationBuilder.CreateTable(
                name: "Auditoria",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DataHora = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Usuario = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OperacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Origem = table.Column<byte>(type: "tinyint", nullable: false),
                    Entidade = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: false),
                    RegistroId = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    RaizEntidade = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: false),
                    RaizId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Acao = table.Column<byte>(type: "tinyint", nullable: false),
                    Campo = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: true),
                    ValorAnterior = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ValorNovo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auditoria", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GruposEconomicos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Observacoes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GruposEconomicos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Perfis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Administrador = table.Column<bool>(type: "bit", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Perfis", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TiposRelacionamento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    NomeInverso = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Sistema = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TiposRelacionamento", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Login = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    SenhaHash = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: false),
                    DeveTrocarSenha = table.Column<bool>(type: "bit", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Pessoas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Codigo = table.Column<int>(type: "int", nullable: false, defaultValueSql: "NEXT VALUE FOR SeqPessoaCodigo"),
                    Natureza = table.Column<byte>(type: "tinyint", nullable: false),
                    Situacao = table.Column<byte>(type: "tinyint", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NomeSocial = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    NomeExibicao = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Apelido = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    DocumentoPrincipal = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    DataNascimento = table.Column<DateOnly>(type: "date", nullable: true),
                    GrupoEconomicoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MescladaEmId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Observacoes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pessoas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pessoas_GruposEconomicos_GrupoEconomicoId",
                        column: x => x.GrupoEconomicoId,
                        principalTable: "GruposEconomicos",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Pessoas_Pessoas_MescladaEmId",
                        column: x => x.MescladaEmId,
                        principalTable: "Pessoas",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PerfilPermissoes",
                columns: table => new
                {
                    PerfilId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Codigo = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerfilPermissoes", x => new { x.PerfilId, x.Codigo });
                    table.ForeignKey(
                        name: "FK_PerfilPermissoes_Perfis_PerfilId",
                        column: x => x.PerfilId,
                        principalTable: "Perfis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TokensRenovacao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Hash = table.Column<string>(type: "varchar(44)", unicode: false, maxLength: 44, nullable: false),
                    EstabelecimentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiraEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevogadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubstituidoPorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Dispositivo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokensRenovacao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TokensRenovacao_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsuarioAcessos",
                columns: table => new
                {
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TentativasFalhas = table.Column<int>(type: "int", nullable: false),
                    BloqueadoAte = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UltimoAcessoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuarioAcessos", x => x.UsuarioId);
                    table.ForeignKey(
                        name: "FK_UsuarioAcessos_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContasCliente",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LimiteCredito = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: true),
                    DiasMaximoAtraso = table.Column<int>(type: "int", nullable: true),
                    DescontoMaximo = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    CondicaoPagamento = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    ExigeAprovacaoAcimaLimite = table.Column<bool>(type: "bit", nullable: false),
                    VendedorPadraoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Observacoes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PessoaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContasCliente", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContasCliente_Pessoas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ContasCliente_Pessoas_PessoaId",
                        column: x => x.PessoaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContasCliente_Pessoas_VendedorPadraoId",
                        column: x => x.VendedorPadraoId,
                        principalTable: "Pessoas",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ContasFornecedor",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CondicaoPagamento = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    PrazoMedioDias = table.Column<int>(type: "int", nullable: true),
                    LeadTimeDias = table.Column<int>(type: "int", nullable: true),
                    TransportadoraPadraoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Avaliacao = table.Column<byte>(type: "tinyint", nullable: true),
                    Observacoes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PessoaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContasFornecedor", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContasFornecedor_Pessoas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ContasFornecedor_Pessoas_PessoaId",
                        column: x => x.PessoaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContasFornecedor_Pessoas_TransportadoraPadraoId",
                        column: x => x.TransportadoraPadraoId,
                        principalTable: "Pessoas",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PessoaBloqueios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Escopo = table.Column<byte>(type: "tinyint", nullable: false),
                    Origem = table.Column<byte>(type: "tinyint", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    InicioEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InicioPor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FimEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FimPor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MotivoLiberacao = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PessoaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PessoaBloqueios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PessoaBloqueios_Pessoas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PessoaBloqueios_Pessoas_PessoaId",
                        column: x => x.PessoaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PessoaContatos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Cargo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Departamento = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Telefone = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    Celular = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    CelularWhatsApp = table.Column<bool>(type: "bit", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Observacoes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Principal = table.Column<bool>(type: "bit", nullable: false),
                    PessoaVinculadaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PessoaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PessoaContatos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PessoaContatos_Pessoas_PessoaId",
                        column: x => x.PessoaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PessoaContatos_Pessoas_PessoaVinculadaId",
                        column: x => x.PessoaVinculadaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PessoaDocumentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<byte>(type: "tinyint", nullable: false),
                    Numero = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OrgaoEmissor = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Uf = table.Column<string>(type: "char(2)", unicode: false, fixedLength: true, maxLength: 2, nullable: true),
                    EmitidoEm = table.Column<DateOnly>(type: "date", nullable: true),
                    ValidoAte = table.Column<DateOnly>(type: "date", nullable: true),
                    Observacoes = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PessoaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PessoaDocumentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PessoaDocumentos_Pessoas_PessoaId",
                        column: x => x.PessoaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PessoaEnderecos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Finalidades = table.Column<short>(type: "smallint", nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    Cep = table.Column<string>(type: "varchar(8)", unicode: false, maxLength: 8, nullable: true),
                    Logradouro = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Numero = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Complemento = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Bairro = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Cidade = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Uf = table.Column<string>(type: "char(2)", unicode: false, fixedLength: true, maxLength: 2, nullable: true),
                    CodigoMunicipioIbge = table.Column<string>(type: "char(7)", unicode: false, fixedLength: true, maxLength: 7, nullable: true),
                    CodigoPais = table.Column<string>(type: "varchar(4)", unicode: false, maxLength: 4, nullable: false),
                    Pais = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PessoaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PessoaEnderecos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PessoaEnderecos_Pessoas_PessoaId",
                        column: x => x.PessoaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PessoaMeiosContato",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<byte>(type: "tinyint", nullable: false),
                    Valor = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Principal = table.Column<bool>(type: "bit", nullable: false),
                    PermiteComunicacao = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PessoaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PessoaMeiosContato", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PessoaMeiosContato_Pessoas_PessoaId",
                        column: x => x.PessoaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PessoaPapeis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Papel = table.Column<byte>(type: "tinyint", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    InicioEm = table.Column<DateOnly>(type: "date", nullable: false),
                    FimEm = table.Column<DateOnly>(type: "date", nullable: true),
                    Observacoes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PessoaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PessoaPapeis", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PessoaPapeis_Pessoas_PessoaId",
                        column: x => x.PessoaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PessoaRelacionamentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PessoaDestinoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoRelacionamentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InicioEm = table.Column<DateOnly>(type: "date", nullable: true),
                    FimEm = table.Column<DateOnly>(type: "date", nullable: true),
                    Observacoes = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PessoaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PessoaRelacionamentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PessoaRelacionamentos_Pessoas_PessoaDestinoId",
                        column: x => x.PessoaDestinoId,
                        principalTable: "Pessoas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PessoaRelacionamentos_Pessoas_PessoaId",
                        column: x => x.PessoaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PessoaRelacionamentos_TiposRelacionamento_TipoRelacionamentoId",
                        column: x => x.TipoRelacionamentoId,
                        principalTable: "TiposRelacionamento",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UsuarioPerfis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PerfilId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuarioPerfis", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UsuarioPerfis_Perfis_PerfilId",
                        column: x => x.PerfilId,
                        principalTable: "Perfis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UsuarioPerfis_Pessoas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UsuarioPerfis_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Estabelecimentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Cnpj = table.Column<string>(type: "char(14)", unicode: false, fixedLength: true, maxLength: 14, nullable: true),
                    Principal = table.Column<bool>(type: "bit", nullable: false),
                    NomeFantasia = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    SituacaoReceita = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ConsultadoReceitaEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IndicadorIE = table.Column<byte>(type: "tinyint", nullable: false),
                    InscricaoEstadual = table.Column<string>(type: "varchar(14)", unicode: false, maxLength: 14, nullable: true),
                    InscricaoMunicipal = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    InscricaoSuframa = table.Column<string>(type: "varchar(9)", unicode: false, maxLength: 9, nullable: true),
                    RegimeTributario = table.Column<byte>(type: "tinyint", nullable: false),
                    CnaePrincipal = table.Column<string>(type: "char(7)", unicode: false, fixedLength: true, maxLength: 7, nullable: true),
                    NaturezaJuridica = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    EnderecoFiscalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PessoaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Estabelecimentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Estabelecimentos_PessoaEnderecos_EnderecoFiscalId",
                        column: x => x.EnderecoFiscalId,
                        principalTable: "PessoaEnderecos",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Estabelecimentos_Pessoas_PessoaId",
                        column: x => x.PessoaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "TiposRelacionamento",
                columns: new[] { "Id", "AtualizadoEm", "CriadoEm", "Nome", "NomeInverso", "Sistema" },
                values: new object[,]
                {
                    { new Guid("5a0e6f10-0000-0000-0000-000000000001"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Sócio de", "Tem como sócio", true },
                    { new Guid("5a0e6f10-0000-0000-0000-000000000002"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Responsável por", "Tem como responsável", true },
                    { new Guid("5a0e6f10-0000-0000-0000-000000000003"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Representante de", "Representada por", true },
                    { new Guid("5a0e6f10-0000-0000-0000-000000000004"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Contato de", "Tem como contato", true },
                    { new Guid("5a0e6f10-0000-0000-0000-000000000005"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Dependente de", "Tem como dependente", true },
                    { new Guid("5a0e6f10-0000-0000-0000-000000000006"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Funcionário de", "Tem como funcionário", true }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_Entidade_RegistroId",
                table: "Auditoria",
                columns: new[] { "Entidade", "RegistroId" });

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_OperacaoId",
                table: "Auditoria",
                column: "OperacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_RaizEntidade_RaizId_DataHora",
                table: "Auditoria",
                columns: new[] { "RaizEntidade", "RaizId", "DataHora" });

            migrationBuilder.CreateIndex(
                name: "IX_ContasCliente_EmpresaId",
                table: "ContasCliente",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_ContasCliente_PessoaId_EmpresaId",
                table: "ContasCliente",
                columns: new[] { "PessoaId", "EmpresaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContasCliente_VendedorPadraoId",
                table: "ContasCliente",
                column: "VendedorPadraoId");

            migrationBuilder.CreateIndex(
                name: "IX_ContasFornecedor_EmpresaId",
                table: "ContasFornecedor",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_ContasFornecedor_PessoaId_EmpresaId",
                table: "ContasFornecedor",
                columns: new[] { "PessoaId", "EmpresaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContasFornecedor_TransportadoraPadraoId",
                table: "ContasFornecedor",
                column: "TransportadoraPadraoId");

            migrationBuilder.CreateIndex(
                name: "IX_Estabelecimentos_Cnpj",
                table: "Estabelecimentos",
                column: "Cnpj",
                unique: true,
                filter: "[Cnpj] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Estabelecimentos_EnderecoFiscalId",
                table: "Estabelecimentos",
                column: "EnderecoFiscalId");

            migrationBuilder.CreateIndex(
                name: "IX_Estabelecimentos_PessoaId",
                table: "Estabelecimentos",
                column: "PessoaId");

            migrationBuilder.CreateIndex(
                name: "IX_GruposEconomicos_Nome",
                table: "GruposEconomicos",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Perfis_Nome",
                table: "Perfis",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PessoaBloqueios_EmpresaId",
                table: "PessoaBloqueios",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_PessoaBloqueios_PessoaId_FimEm",
                table: "PessoaBloqueios",
                columns: new[] { "PessoaId", "FimEm" });

            migrationBuilder.CreateIndex(
                name: "IX_PessoaContatos_Celular",
                table: "PessoaContatos",
                column: "Celular");

            migrationBuilder.CreateIndex(
                name: "IX_PessoaContatos_Email",
                table: "PessoaContatos",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_PessoaContatos_PessoaId",
                table: "PessoaContatos",
                column: "PessoaId");

            migrationBuilder.CreateIndex(
                name: "IX_PessoaContatos_PessoaVinculadaId",
                table: "PessoaContatos",
                column: "PessoaVinculadaId");

            migrationBuilder.CreateIndex(
                name: "IX_PessoaContatos_Telefone",
                table: "PessoaContatos",
                column: "Telefone");

            migrationBuilder.CreateIndex(
                name: "IX_PessoaDocumentos_PessoaId",
                table: "PessoaDocumentos",
                column: "PessoaId");

            migrationBuilder.CreateIndex(
                name: "IX_PessoaEnderecos_PessoaId_Ordem",
                table: "PessoaEnderecos",
                columns: new[] { "PessoaId", "Ordem" });

            migrationBuilder.CreateIndex(
                name: "IX_PessoaMeiosContato_PessoaId",
                table: "PessoaMeiosContato",
                column: "PessoaId");

            migrationBuilder.CreateIndex(
                name: "IX_PessoaMeiosContato_Valor",
                table: "PessoaMeiosContato",
                column: "Valor");

            migrationBuilder.CreateIndex(
                name: "IX_PessoaPapeis_Papel_Ativo",
                table: "PessoaPapeis",
                columns: new[] { "Papel", "Ativo" });

            migrationBuilder.CreateIndex(
                name: "IX_PessoaPapeis_PessoaId_Papel",
                table: "PessoaPapeis",
                columns: new[] { "PessoaId", "Papel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PessoaRelacionamentos_PessoaDestinoId",
                table: "PessoaRelacionamentos",
                column: "PessoaDestinoId");

            migrationBuilder.CreateIndex(
                name: "IX_PessoaRelacionamentos_PessoaId",
                table: "PessoaRelacionamentos",
                column: "PessoaId");

            migrationBuilder.CreateIndex(
                name: "IX_PessoaRelacionamentos_TipoRelacionamentoId",
                table: "PessoaRelacionamentos",
                column: "TipoRelacionamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_Pessoas_Codigo",
                table: "Pessoas",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pessoas_GrupoEconomicoId",
                table: "Pessoas",
                column: "GrupoEconomicoId");

            migrationBuilder.CreateIndex(
                name: "IX_Pessoas_MescladaEmId",
                table: "Pessoas",
                column: "MescladaEmId");

            migrationBuilder.CreateIndex(
                name: "IX_Pessoas_Natureza_DocumentoPrincipal",
                table: "Pessoas",
                columns: new[] { "Natureza", "DocumentoPrincipal" },
                unique: true,
                filter: "[DocumentoPrincipal] IS NOT NULL AND [Natureza] <> 2");

            migrationBuilder.CreateIndex(
                name: "IX_Pessoas_Nome",
                table: "Pessoas",
                column: "Nome");

            migrationBuilder.CreateIndex(
                name: "IX_TiposRelacionamento_Nome",
                table: "TiposRelacionamento",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TokensRenovacao_Hash",
                table: "TokensRenovacao",
                column: "Hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TokensRenovacao_UsuarioId_RevogadoEm",
                table: "TokensRenovacao",
                columns: new[] { "UsuarioId", "RevogadoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_UsuarioPerfis_EmpresaId",
                table: "UsuarioPerfis",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_UsuarioPerfis_PerfilId",
                table: "UsuarioPerfis",
                column: "PerfilId");

            migrationBuilder.CreateIndex(
                name: "IX_UsuarioPerfis_UsuarioId_PerfilId_EmpresaId",
                table: "UsuarioPerfis",
                columns: new[] { "UsuarioId", "PerfilId", "EmpresaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Login",
                table: "Usuarios",
                column: "Login",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Auditoria");

            migrationBuilder.DropTable(
                name: "ContasCliente");

            migrationBuilder.DropTable(
                name: "ContasFornecedor");

            migrationBuilder.DropTable(
                name: "Estabelecimentos");

            migrationBuilder.DropTable(
                name: "PerfilPermissoes");

            migrationBuilder.DropTable(
                name: "PessoaBloqueios");

            migrationBuilder.DropTable(
                name: "PessoaContatos");

            migrationBuilder.DropTable(
                name: "PessoaDocumentos");

            migrationBuilder.DropTable(
                name: "PessoaMeiosContato");

            migrationBuilder.DropTable(
                name: "PessoaPapeis");

            migrationBuilder.DropTable(
                name: "PessoaRelacionamentos");

            migrationBuilder.DropTable(
                name: "TokensRenovacao");

            migrationBuilder.DropTable(
                name: "UsuarioAcessos");

            migrationBuilder.DropTable(
                name: "UsuarioPerfis");

            migrationBuilder.DropTable(
                name: "PessoaEnderecos");

            migrationBuilder.DropTable(
                name: "TiposRelacionamento");

            migrationBuilder.DropTable(
                name: "Perfis");

            migrationBuilder.DropTable(
                name: "Usuarios");

            migrationBuilder.DropTable(
                name: "Pessoas");

            migrationBuilder.DropTable(
                name: "GruposEconomicos");

            migrationBuilder.DropSequence(
                name: "SeqPessoaCodigo");
        }
    }
}
