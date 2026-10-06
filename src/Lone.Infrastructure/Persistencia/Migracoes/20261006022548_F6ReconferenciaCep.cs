using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lone.Infrastructure.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class F6ReconferenciaCep : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PessoaEnderecos_CepSituacao_CepConferidoEm",
                table: "PessoaEnderecos",
                columns: new[] { "CepSituacao", "CepConferidoEm" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PessoaEnderecos_CepSituacao_CepConferidoEm",
                table: "PessoaEnderecos");
        }
    }
}
