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

    /// <summary>
    /// Fase 8 (D5) — depois de criar CarteiraClientes e inserir os tipos de carteira: cada vendedor padrão que já existia
    /// numa conta de cliente vira um vínculo "Vendedor" (principal) vigente desde a criação da conta. Nada é apagado;
    /// ContasCliente.VendedorPadraoId continua igual. Confere a contagem e desfaz tudo se não conferir.
    /// </summary>
    public const string CarteiraDosVendedoresPadrao = """
        SET NOCOUNT ON;
        DECLARE @esperado int = (SELECT COUNT(*) FROM ContasCliente c
            WHERE c.VendedorPadraoId IS NOT NULL AND EXISTS (SELECT 1 FROM Pessoas p WHERE p.Id = c.VendedorPadraoId));
        INSERT INTO CarteiraClientes (Id, PessoaId, EmpresaId, TipoCarteiraId, VendedorId, InicioEm, FimEm, Exclusivo, Observacao, Ativo, CriadoEm)
        SELECT NEWID(), c.PessoaId, c.EmpresaId, '7a9e1c04-0000-0000-0000-000000000001', c.VendedorPadraoId,
               CAST(c.CriadoEm AS date), NULL, 0, N'Vendedor padrão existente antes da carteira de clientes', 1, SYSUTCDATETIME()
        FROM ContasCliente c
        WHERE c.VendedorPadraoId IS NOT NULL AND EXISTS (SELECT 1 FROM Pessoas p WHERE p.Id = c.VendedorPadraoId);
        IF @@ROWCOUNT <> @esperado
            THROW 50009, N'Migração da carteira de clientes: a contagem não conferiu. Nada foi alterado.', 1;
        """;

    /// <summary>
    /// Fase 9 — depois de criar HistoricoFiscal e EstabelecimentoCnaes: cada estabelecimento ganha o primeiro período
    /// fiscal (desde a data de criação, com a situação atual) e a tabela de CNAEs é montada a partir dos campos de texto
    /// (principal + secundários separados por vírgula). Confere as contagens e desfaz tudo se não conferirem.
    /// </summary>
    public const string HistoricoFiscalECnaes = """
        SET NOCOUNT ON;
        DECLARE @estabelecimentos int = (SELECT COUNT(*) FROM Estabelecimentos);
        INSERT INTO HistoricoFiscal (Id, PessoaId, EstabelecimentoId, InicioEm, FimEm, RegimeTributario, IndicadorIE,
                                     InscricaoEstadual, SituacaoReceita, ProdutorRural, CriadoEm)
        SELECT NEWID(), e.PessoaId, e.Id, CAST(e.CriadoEm AS date), NULL, e.RegimeTributario, e.IndicadorIE,
               e.InscricaoEstadual, e.SituacaoReceita, e.ProdutorRural, SYSUTCDATETIME()
        FROM Estabelecimentos e;
        IF @@ROWCOUNT <> @estabelecimentos
            THROW 50010, N'Migração do histórico fiscal: a contagem não conferiu. Nada foi alterado.', 1;

        ;WITH Codigos AS (
            SELECT e.Id AS EstabelecimentoId, e.PessoaId, e.CnaePrincipal AS Codigo, CAST(1 AS bit) AS Principal
            FROM Estabelecimentos e WHERE LEN(e.CnaePrincipal) = 7 AND e.CnaePrincipal NOT LIKE '%[^0-9]%'
            UNION ALL
            SELECT e.Id, e.PessoaId, LTRIM(RTRIM(s.value)), CAST(0 AS bit)
            FROM Estabelecimentos e CROSS APPLY STRING_SPLIT(e.CnaesSecundarios, ',') s
            WHERE e.CnaesSecundarios IS NOT NULL AND LEN(LTRIM(RTRIM(s.value))) = 7 AND LTRIM(RTRIM(s.value)) NOT LIKE '%[^0-9]%'
        ), Unicos AS (
            SELECT EstabelecimentoId, PessoaId, CAST(Codigo AS int) AS Codigo, Principal,
                   ROW_NUMBER() OVER (PARTITION BY EstabelecimentoId, CAST(Codigo AS int) ORDER BY Principal DESC) AS Ordem
            FROM Codigos
        )
        INSERT INTO EstabelecimentoCnaes (Id, PessoaId, EstabelecimentoId, Codigo, Principal, CriadoEm)
        SELECT NEWID(), PessoaId, EstabelecimentoId, Codigo, Principal, SYSUTCDATETIME() FROM Unicos WHERE Ordem = 1;
        """;
}
