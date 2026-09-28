using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lone.Infrastructure.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class PapeisComerciais : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Motor Comercial, Fase 1a (aprovada em 28/09/2026): papéis comerciais com política (quantos ao mesmo tempo,
            // crédito, % padrão, metas), "Principal" renomeado para "ResponsavelDaConta" (sem perder o valor) e crédito/origem
            // no vínculo da carteira. SQL em SqlMigracaoCarteira. Nada é apagado.
            // 1. Tira o gatilho da Etapa 4 (usa a coluna "Principal"); o novo entra no fim.
            migrationBuilder.Sql(SqlMigracaoCarteira.RemoverProtecao);

            migrationBuilder.DropIndex(
                name: "IX_TiposCarteira_Principal",
                table: "TiposCarteira");

            migrationBuilder.RenameColumn(
                name: "Principal",
                table: "TiposCarteira",
                newName: "ResponsavelDaConta");

            migrationBuilder.AddColumn<bool>(
                name: "ContaParaMetas",
                table: "TiposCarteira",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "LimitePorVez",
                table: "TiposCarteira",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PercentualPadrao",
                table: "TiposCarteira",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "TipoCredito",
                table: "TiposCarteira",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<byte>(
                name: "Origem",
                table: "CarteiraClientes",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<decimal>(
                name: "PercentualCredito",
                table: "CarteiraClientes",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            // 2. Política dos papéis que já existem (iniciais e do usuário), antes do check que ela precisa cumprir. No lugar
            //    dos UpdateData dos papéis iniciais: estes seguem o que o banco tem (o responsável pode ter mudado de papel).
            migrationBuilder.Sql(SqlMigracaoCarteira.ConverterPolitica);

            migrationBuilder.CreateIndex(
                name: "IX_TiposCarteira_ResponsavelDaConta",
                table: "TiposCarteira",
                column: "ResponsavelDaConta",
                unique: true,
                filter: "[ResponsavelDaConta] = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TiposCarteira_Politica",
                table: "TiposCarteira",
                sql: "([LimitePorVez] IS NULL OR [LimitePorVez] BETWEEN 1 AND 99) AND ([PercentualPadrao] IS NULL OR [PercentualPadrao] BETWEEN 0 AND 100) AND ([ResponsavelDaConta] = 0 OR [LimitePorVez] = 1)");

            // 3. Gatilho novo, pela política do papel (um por vez/exclusivo e limite), só no que cada gravação acrescenta.
            migrationBuilder.Sql(SqlMigracaoCarteira.CriarProtecaoPorLimite);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SqlMigracaoCarteira.RemoverProtecao);

            migrationBuilder.DropIndex(
                name: "IX_TiposCarteira_ResponsavelDaConta",
                table: "TiposCarteira");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TiposCarteira_Politica",
                table: "TiposCarteira");

            migrationBuilder.DropColumn(
                name: "ContaParaMetas",
                table: "TiposCarteira");

            migrationBuilder.DropColumn(
                name: "LimitePorVez",
                table: "TiposCarteira");

            migrationBuilder.DropColumn(
                name: "PercentualPadrao",
                table: "TiposCarteira");

            migrationBuilder.DropColumn(
                name: "TipoCredito",
                table: "TiposCarteira");

            migrationBuilder.DropColumn(
                name: "Origem",
                table: "CarteiraClientes");

            migrationBuilder.DropColumn(
                name: "PercentualCredito",
                table: "CarteiraClientes");

            migrationBuilder.RenameColumn(
                name: "ResponsavelDaConta",
                table: "TiposCarteira",
                newName: "Principal");

            migrationBuilder.CreateIndex(
                name: "IX_TiposCarteira_Principal",
                table: "TiposCarteira",
                column: "Principal",
                unique: true,
                filter: "[Principal] = 1");

            // Volta o gatilho da Etapa 4 (texto congelado, com a coluna "Principal").
            migrationBuilder.Sql(SqlMigracaoCarteira.CriarProtecao);
        }
    }
}
