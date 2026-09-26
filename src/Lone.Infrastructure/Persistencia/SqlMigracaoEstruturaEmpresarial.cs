namespace Lone.Infrastructure.Persistencia;

/// <summary>
/// SQL de dados da migração "EstruturaEmpresarial" (grupo empresarial, relacionamentos ativos e condição de pagamento do
/// fornecedor). Vai no FIM do Up, depois de criadas as colunas novas:
/// <c>migrationBuilder.Sql(SqlMigracaoEstruturaEmpresarial.Dados);</c>
/// Nada é apagado: o texto antigo da condição de pagamento continua na coluna de sempre.
/// </summary>
public static class SqlMigracaoEstruturaEmpresarial
{
    public const string CheckGrupoSoPessoaJuridica = "CK_Pessoas_GrupoEmpresarialSoPJ";
    public const string IndiceRelacionamentoAberto = "IX_PessoaRelacionamentos_Aberto";

    public const string Dados = """
        SET NOCOUNT ON;

        -- 1. Relacionamentos já gravados ficam ativos (a coluna nova nasce com 0).
        UPDATE PessoaRelacionamentos SET Ativo = 1;

        -- 2. Condição de pagamento em texto: liga à condição do cadastro de MESMO nome (sem diferenciar maiúsculas e
        --    acentos; espaços nas pontas não contam), só quando há exatamente uma. O texto é preservado. O que não
        --    converter continua só no texto e a ficha mostra "não convertida".
        UPDATE f
           SET CondicaoPagamentoId = (SELECT c.Id FROM CondicoesPagamento c
                                       WHERE c.Nome = LTRIM(RTRIM(f.CondicaoPagamento)) COLLATE Latin1_General_CI_AI)
          FROM ContasFornecedor f
         WHERE f.CondicaoPagamentoId IS NULL
           AND LTRIM(RTRIM(ISNULL(f.CondicaoPagamento, N''))) <> N''
           AND (SELECT COUNT(*) FROM CondicoesPagamento c
                 WHERE c.Nome = LTRIM(RTRIM(f.CondicaoPagamento)) COLLATE Latin1_General_CI_AI) = 1;

        -- Cliente: o mesmo critério, só onde a conta ainda não tem condição escolhida.
        UPDATE cc
           SET CondicaoPagamentoId = (SELECT c.Id FROM CondicoesPagamento c
                                       WHERE c.Nome = LTRIM(RTRIM(cc.CondicaoPagamento)) COLLATE Latin1_General_CI_AI)
          FROM ContasCliente cc
         WHERE cc.CondicaoPagamentoId IS NULL
           AND LTRIM(RTRIM(ISNULL(cc.CondicaoPagamento, N''))) <> N''
           AND (SELECT COUNT(*) FROM CondicoesPagamento c
                 WHERE c.Nome = LTRIM(RTRIM(cc.CondicaoPagamento)) COLLATE Latin1_General_CI_AI) = 1;

        -- Conferências (qualquer falha desfaz a migração inteira).
        IF EXISTS (SELECT 1 FROM PessoaRelacionamentos WHERE Ativo = 0)
            THROW 50040, N'Migração EstruturaEmpresarial: relacionamentos não ficaram ativos. Nada foi alterado.', 1;
        IF EXISTS (SELECT 1 FROM Pessoas WHERE GrupoEmpresarialId IS NOT NULL AND Natureza <> 1)
            THROW 50041, N'Migração EstruturaEmpresarial: pessoa que não é jurídica num grupo empresarial. Nada foi alterado.', 1;

        DECLARE @naoConvertidas int =
            (SELECT COUNT(*) FROM ContasFornecedor WHERE CondicaoPagamentoId IS NULL AND LTRIM(RTRIM(ISNULL(CondicaoPagamento, N''))) <> N'') +
            (SELECT COUNT(*) FROM ContasCliente WHERE CondicaoPagamentoId IS NULL AND LTRIM(RTRIM(ISNULL(CondicaoPagamento, N''))) <> N'');
        PRINT CONCAT(N'Condições de pagamento em texto que não viraram referência (continuam no texto): ', @naoConvertidas);
        """;
}
