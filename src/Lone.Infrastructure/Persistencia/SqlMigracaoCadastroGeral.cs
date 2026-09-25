namespace Lone.Infrastructure.Persistencia;

/// <summary>
/// SQL das fases 3b em diante (uma migração só, gerada no fim, com estes trechos colocados à mão nos pontos
/// indicados em cada constante). Nada é apagado; cada trecho confere o próprio resultado e desfaz tudo se não conferir.
/// </summary>
public static class SqlMigracaoCadastroGeral
{
    /// <summary>Fase 3b — depois de criar PessoaEnderecos.Ativo: os endereços existentes ficam ativos.</summary>
    public const string AtivarEnderecos = """
        SET NOCOUNT ON;
        UPDATE PessoaEnderecos SET Ativo = 1;
        IF EXISTS (SELECT 1 FROM PessoaEnderecos WHERE Ativo = 0)
            THROW 50006, N'Migração de endereços: a ativação não conferiu. Nada foi alterado.', 1;
        """;

    /// <summary>
    /// Fase 4a — depois de criar a tabela TiposDocumento (com os 5 de sistema) e as colunas PessoaDocumentos.TipoDocumentoId
    /// e .Ativo, e ANTES da chave estrangeira FK_PessoaDocumentos_TiposDocumento_TipoDocumentoId: liga cada documento
    /// existente ao tipo de sistema do seu enum (Tipo) e deixa todos ativos. Enum desconhecido vira "Outro".
    /// </summary>
    public const string LigarDocumentosAosTipos = """
        SET NOCOUNT ON;
        DECLARE @total int = (SELECT COUNT(*) FROM PessoaDocumentos);
        UPDATE PessoaDocumentos SET
            Ativo = 1,
            TipoDocumentoId = CASE Tipo
                WHEN 0 THEN '7a9e1c03-0000-0000-0000-000000000001'
                WHEN 1 THEN '7a9e1c03-0000-0000-0000-000000000002'
                WHEN 2 THEN '7a9e1c03-0000-0000-0000-000000000003'
                WHEN 3 THEN '7a9e1c03-0000-0000-0000-000000000004'
                ELSE '7a9e1c03-0000-0000-0000-000000000009' END;
        UPDATE PessoaDocumentos SET Tipo = 9 WHERE Tipo NOT IN (0, 1, 2, 3, 9);
        IF (SELECT COUNT(*) FROM PessoaDocumentos d JOIN TiposDocumento t ON t.Id = d.TipoDocumentoId WHERE d.Ativo = 1) <> @total
            THROW 50007, N'Migração de documentos: a ligação aos tipos não conferiu. Nada foi alterado.', 1;
        """;

    /// <summary>
    /// Fase 5 — depois de criar CamposPersonalizados.Visivel (nasce 0): os campos que já existem continuam aparecendo
    /// na ficha. Os índices (Entidade, TipoDocumentoId, Nome) e as colunas ValorTextoBusca são criados pelo EF.
    /// </summary>
    public const string CamposVisiveis = """
        SET NOCOUNT ON;
        UPDATE CamposPersonalizados SET Visivel = 1;
        IF EXISTS (SELECT 1 FROM CamposPersonalizados WHERE Visivel = 0)
            THROW 50008, N'Migração de campos personalizados: a marcação de visível não conferiu. Nada foi alterado.', 1;
        """;
}
