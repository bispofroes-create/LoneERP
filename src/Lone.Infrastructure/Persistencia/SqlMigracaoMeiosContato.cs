namespace Lone.Infrastructure.Persistencia;

/// <summary>
/// SQL da migração dos telefones/e-mails: os existentes ficam ativos, e o antigo tipo "WhatsApp" vira celular com a
/// marcação WhatsApp (mesmo número, nada se perde). Conferido: não pode sobrar registro do tipo WhatsApp.
/// </summary>
public static class SqlMigracaoMeiosContato
{
    /// <summary>No fim do Up (as colunas novas já existem).</summary>
    public const string AtivarEConverterWhatsApp = """
        SET NOCOUNT ON;

        UPDATE PessoaMeiosContato SET Ativo = 1;

        UPDATE PessoaMeiosContato SET Tipo = 1, WhatsApp = 1 WHERE Tipo = 2;

        IF EXISTS (SELECT 1 FROM PessoaMeiosContato WHERE Tipo = 2 OR Ativo = 0)
            THROW 50005, N'Migração de telefones/e-mails: a conversão não conferiu. Nada foi alterado.', 1;
        """;
}
