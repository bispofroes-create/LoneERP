using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lone.Infrastructure.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class P18DocumentosMetadados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AplicaEstrangeiro",
                table: "TiposDocumento",
                type: "bit",
                nullable: false,
                defaultValue: true); // P1-8: tipos do usuário continuam valendo para todos (como sempre)

            migrationBuilder.AddColumn<bool>(
                name: "AplicaPessoaFisica",
                table: "TiposDocumento",
                type: "bit",
                nullable: false,
                defaultValue: true); // P1-8: tipos do usuário continuam valendo para todos (como sempre)

            migrationBuilder.AddColumn<bool>(
                name: "AplicaPessoaJuridica",
                table: "TiposDocumento",
                type: "bit",
                nullable: false,
                defaultValue: true); // P1-8: tipos do usuário continuam valendo para todos (como sempre)

            migrationBuilder.AddColumn<byte>(
                name: "FormatoNumero",
                table: "TiposDocumento",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<int>(
                name: "TamanhoMaximoNumero",
                table: "TiposDocumento",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TamanhoMinimoNumero",
                table: "TiposDocumento",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "Unicidade",
                table: "TiposDocumento",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<byte>(
                name: "UsoEmissao",
                table: "TiposDocumento",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)1); // P1-8: Opcional — a data de emissão continua aparecendo nos tipos do usuário

            migrationBuilder.AddColumn<byte>(
                name: "UsoOrgaoEmissor",
                table: "TiposDocumento",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<byte>(
                name: "UsoUf",
                table: "TiposDocumento",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<string>(
                name: "ChaveUnicidade",
                table: "PessoaDocumentos",
                type: "varchar(70)",
                unicode: false,
                maxLength: 70,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumeroNormalizado",
                table: "PessoaDocumentos",
                type: "varchar(30)",
                unicode: false,
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "TiposDocumento",
                keyColumn: "Id",
                keyValue: new Guid("7a9e1c03-0000-0000-0000-000000000001"),
                columns: new[] { "AplicaEstrangeiro", "AplicaPessoaFisica", "AplicaPessoaJuridica", "FormatoNumero", "TamanhoMaximoNumero", "TamanhoMinimoNumero", "Unicidade", "UsoEmissao", "UsoOrgaoEmissor", "UsoUf" },
                values: new object[] { false, true, false, (byte)0, null, null, (byte)1, (byte)1, (byte)1, (byte)1 });

            migrationBuilder.UpdateData(
                table: "TiposDocumento",
                keyColumn: "Id",
                keyValue: new Guid("7a9e1c03-0000-0000-0000-000000000002"),
                columns: new[] { "AplicaEstrangeiro", "AplicaPessoaFisica", "AplicaPessoaJuridica", "FormatoNumero", "TamanhoMaximoNumero", "TamanhoMinimoNumero", "Unicidade", "UsoEmissao", "UsoOrgaoEmissor", "UsoUf" },
                values: new object[] { false, true, false, (byte)0, null, null, (byte)1, (byte)1, (byte)1, (byte)1 });

            migrationBuilder.UpdateData(
                table: "TiposDocumento",
                keyColumn: "Id",
                keyValue: new Guid("7a9e1c03-0000-0000-0000-000000000003"),
                columns: new[] { "AplicaEstrangeiro", "AplicaPessoaFisica", "AplicaPessoaJuridica", "FormatoNumero", "TamanhoMaximoNumero", "TamanhoMinimoNumero", "Unicidade", "UsoEmissao", "UsoOrgaoEmissor", "UsoUf" },
                values: new object[] { true, true, false, (byte)0, null, null, (byte)1, (byte)1, (byte)1, (byte)0 });

            migrationBuilder.UpdateData(
                table: "TiposDocumento",
                keyColumn: "Id",
                keyValue: new Guid("7a9e1c03-0000-0000-0000-000000000004"),
                columns: new[] { "AplicaEstrangeiro", "AplicaPessoaFisica", "AplicaPessoaJuridica", "FormatoNumero", "TamanhoMaximoNumero", "TamanhoMinimoNumero", "Unicidade", "UsoEmissao", "UsoOrgaoEmissor", "UsoUf" },
                values: new object[] { true, false, false, (byte)0, null, null, (byte)1, (byte)1, (byte)1, (byte)0 });

            migrationBuilder.UpdateData(
                table: "TiposDocumento",
                keyColumn: "Id",
                keyValue: new Guid("7a9e1c03-0000-0000-0000-000000000009"),
                columns: new[] { "AplicaEstrangeiro", "AplicaPessoaFisica", "AplicaPessoaJuridica", "FormatoNumero", "TamanhoMaximoNumero", "TamanhoMinimoNumero", "Unicidade", "UsoEmissao", "UsoOrgaoEmissor", "UsoUf" },
                values: new object[] { true, true, true, (byte)0, null, null, (byte)0, (byte)1, (byte)0, (byte)0 });

            // P1-8 (à mão): número comparável de todos os documentos, pela mesma regra do domínio, e conferência da
            // semente (cinco tipos de sistema, nenhum bloqueio ligado, nenhuma chave de unicidade). Nada é apagado.
            migrationBuilder.Sql(SqlMigracaoDocumentos.PreencherNumeroNormalizado);
            migrationBuilder.Sql(SqlMigracaoDocumentos.ConferirSementeESemBloqueio);

            migrationBuilder.CreateIndex(
                name: "IX_PessoaDocumentos_ChaveUnicidade",
                table: "PessoaDocumentos",
                column: "ChaveUnicidade",
                unique: true,
                filter: "[ChaveUnicidade] IS NOT NULL AND [Ativo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_PessoaDocumentos_NumeroNormalizado_TipoDocumentoId",
                table: "PessoaDocumentos",
                columns: new[] { "NumeroNormalizado", "TipoDocumentoId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PessoaDocumentos_ChaveUnicidade",
                table: "PessoaDocumentos");

            migrationBuilder.DropIndex(
                name: "IX_PessoaDocumentos_NumeroNormalizado_TipoDocumentoId",
                table: "PessoaDocumentos");

            migrationBuilder.DropColumn(
                name: "AplicaEstrangeiro",
                table: "TiposDocumento");

            migrationBuilder.DropColumn(
                name: "AplicaPessoaFisica",
                table: "TiposDocumento");

            migrationBuilder.DropColumn(
                name: "AplicaPessoaJuridica",
                table: "TiposDocumento");

            migrationBuilder.DropColumn(
                name: "FormatoNumero",
                table: "TiposDocumento");

            migrationBuilder.DropColumn(
                name: "TamanhoMaximoNumero",
                table: "TiposDocumento");

            migrationBuilder.DropColumn(
                name: "TamanhoMinimoNumero",
                table: "TiposDocumento");

            migrationBuilder.DropColumn(
                name: "Unicidade",
                table: "TiposDocumento");

            migrationBuilder.DropColumn(
                name: "UsoEmissao",
                table: "TiposDocumento");

            migrationBuilder.DropColumn(
                name: "UsoOrgaoEmissor",
                table: "TiposDocumento");

            migrationBuilder.DropColumn(
                name: "UsoUf",
                table: "TiposDocumento");

            migrationBuilder.DropColumn(
                name: "ChaveUnicidade",
                table: "PessoaDocumentos");

            migrationBuilder.DropColumn(
                name: "NumeroNormalizado",
                table: "PessoaDocumentos");
        }
    }
}
