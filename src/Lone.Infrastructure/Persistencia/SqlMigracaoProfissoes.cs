namespace Lone.Infrastructure.Persistencia;

/// <summary>
/// SQL da migração que troca a profissão em texto livre (Pessoas.Profissao) pelo cadastro de profissões.
/// Nada é perdido: a coluna Profissao continua com o texto original; cada texto distinto (sem diferenciar maiúsculas
/// nem acentos) vira uma profissão, com a grafia mais usada, e cada pessoa é ligada à sua. Duplicidades de sentido
/// ("Adv." e "Advogado") são juntadas depois, pela ação "Mesclar" da tela de profissões.
/// Se a contagem depois não conferir com a de antes, a migração inteira é desfeita.
/// </summary>
public static class SqlMigracaoProfissoes
{
    /// <summary>No fim do Up, depois de criadas as tabelas, a coluna Pessoas.ProfissaoId e a chave estrangeira.</summary>
    public const string CriarProfissoesELigarPessoas = """
        SET NOCOUNT ON;

        DECLARE @ComTexto int = (SELECT COUNT(*) FROM Pessoas WHERE Profissao IS NOT NULL AND LTRIM(RTRIM(Profissao)) <> N'');
        DECLARE @Operacao uniqueidentifier = NEWID();
        DECLARE @Agora datetime2 = SYSUTCDATETIME();

        CREATE TABLE #Profissoes (
            Id uniqueidentifier NOT NULL DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
            Nome nvarchar(80) COLLATE Latin1_General_CI_AI NOT NULL UNIQUE,
            Usos int NOT NULL);

        WITH Grafias AS (
            SELECT LTRIM(RTRIM(Profissao)) COLLATE Latin1_General_CS_AS AS Nome, COUNT(*) AS Usos
            FROM Pessoas
            WHERE Profissao IS NOT NULL AND LTRIM(RTRIM(Profissao)) <> N''
            GROUP BY LTRIM(RTRIM(Profissao)) COLLATE Latin1_General_CS_AS
        ), Ordenadas AS (
            SELECT Nome,
                   SUM(Usos) OVER (PARTITION BY Nome COLLATE Latin1_General_CI_AI) AS Total,
                   ROW_NUMBER() OVER (PARTITION BY Nome COLLATE Latin1_General_CI_AI ORDER BY Usos DESC, Nome) AS N
            FROM Grafias
        )
        INSERT INTO #Profissoes (Nome, Usos)
        SELECT Nome, Total FROM Ordenadas WHERE N = 1;

        INSERT INTO Profissoes (Id, Nome, Descricao, OcupacaoCboId, Ativo, CriadoEm, AtualizadoEm)
        SELECT Id, Nome, NULL, NULL, 1, @Agora, NULL FROM #Profissoes;

        INSERT INTO Auditoria (DataHora, Usuario, OperacaoId, Origem, Entidade, RegistroId, RaizEntidade, RaizId, Acao, Campo, ValorAnterior, ValorNovo, Descricao)
        SELECT @Agora, N'sistema', @Operacao, 3, 'Profissao', CONVERT(varchar(40), Id), 'Profissao', Id, 3, NULL, NULL, NULL,
               CONCAT(N'Profissão ''', Nome, N''' criada pela migração a partir das profissões já digitadas nos cadastros (', Usos, N' uso(s)).')
        FROM #Profissoes;

        -- Cada pessoa ligada à sua profissão (o texto original continua na coluna Profissao).
        UPDATE p SET ProfissaoId = n.Id
        FROM Pessoas p
        JOIN #Profissoes n ON n.Nome = LTRIM(RTRIM(p.Profissao)) COLLATE Latin1_General_CI_AI
        WHERE p.Profissao IS NOT NULL AND LTRIM(RTRIM(p.Profissao)) <> N'';

        -- Conferência: toda pessoa com texto ficou ligada. Senão, desfaz a migração inteira.
        IF (SELECT COUNT(*) FROM Pessoas WHERE ProfissaoId IS NOT NULL) <> @ComTexto
            THROW 50003, N'Migração de profissões: a contagem depois não confere com a de antes. Nada foi alterado.', 1;

        DROP TABLE #Profissoes;
        """;
}
