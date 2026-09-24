namespace Lone.Infrastructure.Persistencia;

/// <summary>
/// SQL usado pela migração que troca a naturalidade em texto pelo município do IBGE. As colunas antigas
/// (NaturalidadeCidade/NaturalidadeUf) são removidas, mas o texto de cada pessoa é guardado antes e vira uma
/// pendência de município; a conciliação automática liga ao município certo o que conseguir identificar.
/// Os endereços não perdem nada (cidade, UF e código IBGE continuam como cópia) e são conciliados do mesmo jeito.
/// </summary>
public static class SqlMigracaoMunicipios
{
    /// <summary>No início da migração: copia os textos para uma tabela provisória (antes de as colunas saírem).</summary>
    public const string GuardarNaturalidade = """
        IF COL_LENGTH('Pessoas', 'NaturalidadeCidade') IS NOT NULL AND OBJECT_ID('_MigracaoNaturalidade') IS NULL
            EXEC('SELECT Id AS PessoaId, LEFT(LTRIM(RTRIM(NaturalidadeCidade)), 100) AS Texto, NaturalidadeUf AS Uf
                  INTO _MigracaoNaturalidade
                  FROM Pessoas
                  WHERE NaturalidadeCidade IS NOT NULL AND LTRIM(RTRIM(NaturalidadeCidade)) <> ''''');
        """;

    /// <summary>No fim da migração: cada texto guardado vira pendência (a conciliação resolve o que der).</summary>
    public const string CriarPendenciasDeNaturalidade = """
        IF OBJECT_ID('_MigracaoNaturalidade') IS NOT NULL
        BEGIN
            EXEC('INSERT INTO PendenciasMunicipio (Id, PessoaId, Origem, RegistroId, TextoOriginal, UfOriginal, CriadaEm)
                  SELECT NEWID(), PessoaId, 0, PessoaId, Texto, Uf, SYSUTCDATETIME() FROM _MigracaoNaturalidade');
            DROP TABLE _MigracaoNaturalidade;
        END
        """;
}
