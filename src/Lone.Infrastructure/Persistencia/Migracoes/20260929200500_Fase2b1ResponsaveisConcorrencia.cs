using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lone.Infrastructure.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class Fase2b1ResponsaveisConcorrencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Auditoria final da 2b-1a (29/09/2026): o gatilho 50070 passa a ler com READCOMMITTEDLOCK, como os da árvore e
            // das posições, para barrar gravações simultâneas feitas por fora mesmo com READ_COMMITTED_SNAPSHOT ou SNAPSHOT.
            // Mesma regra e mesma mensagem. Sem operação de esquema; não mexe em dados. A Fase2b1ResponsaveisSemSobreposicao
            // (já aplicada) não muda. SQL em SqlMigracaoTerritorios.
            migrationBuilder.Sql(SqlMigracaoTerritorios.ReforcarProtecaoConcorrencia);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Devolve o gatilho exatamente como a Fase2b1ResponsaveisSemSobreposicao o criou (não o apaga).
            migrationBuilder.Sql(SqlMigracaoTerritorios.DesfazerReforcoProtecaoConcorrencia);
        }
    }
}
