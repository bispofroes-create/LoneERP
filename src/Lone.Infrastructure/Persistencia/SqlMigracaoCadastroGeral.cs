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
}
