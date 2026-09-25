namespace Lone.Infrastructure.Persistencia;

/// <summary>
/// SQL da migração que troca as etiquetas em texto livre (PessoaEtiquetas.Texto) pelo cadastro de etiquetas.
/// Nada é perdido: a coluna Texto continua com o texto original; cada texto distinto (sem diferenciar maiúsculas
/// nem acentos) vira uma etiqueta, com a grafia mais usada; cada pessoa é ligada à sua. Se uma pessoa tinha a
/// mesma etiqueta escrita de dois jeitos ("VIP" e "Víp"), fica uma ligação só e o fato vai para o histórico dela.
/// Se a contagem depois não conferir com a de antes, a migração inteira é desfeita.
/// </summary>
public static class SqlMigracaoEtiquetas
{
    /// <summary>Depois de criar Etiquetas e a coluna PessoaEtiquetas.EtiquetaId (ainda nula), antes dos índices e da chave estrangeira.</summary>
    public const string CriarEtiquetasELigarPessoas = """
        SET NOCOUNT ON;

        DECLARE @Antes int = (SELECT COUNT(*) FROM PessoaEtiquetas);
        DECLARE @Operacao uniqueidentifier = NEWID();
        DECLARE @Agora datetime2 = SYSUTCDATETIME();

        IF EXISTS (SELECT 1 FROM PessoaEtiquetas WHERE Texto IS NULL OR LTRIM(RTRIM(Texto)) = N'')
            THROW 50001, N'Migração de etiquetas: há etiqueta de pessoa sem texto. Nada foi alterado.', 1;

        -- Uma etiqueta por texto (sem diferenciar maiúsculas nem acentos), com a grafia mais usada.
        CREATE TABLE #Etiquetas (
            Id uniqueidentifier NOT NULL DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
            Nome nvarchar(40) COLLATE Latin1_General_CI_AI NOT NULL UNIQUE,
            Usos int NOT NULL);

        WITH Grafias AS (
            SELECT LTRIM(RTRIM(Texto)) COLLATE Latin1_General_CS_AS AS Nome, COUNT(*) AS Usos
            FROM PessoaEtiquetas
            GROUP BY LTRIM(RTRIM(Texto)) COLLATE Latin1_General_CS_AS
        ), Ordenadas AS (
            SELECT Nome,
                   SUM(Usos) OVER (PARTITION BY Nome COLLATE Latin1_General_CI_AI) AS Total,
                   ROW_NUMBER() OVER (PARTITION BY Nome COLLATE Latin1_General_CI_AI ORDER BY Usos DESC, Nome) AS N
            FROM Grafias
        )
        INSERT INTO #Etiquetas (Nome, Usos)
        SELECT Nome, Total FROM Ordenadas WHERE N = 1;

        INSERT INTO Etiquetas (Id, Nome, Descricao, Ativo, CriadoEm, AtualizadoEm)
        SELECT Id, Nome, NULL, 1, @Agora, NULL FROM #Etiquetas;

        INSERT INTO Auditoria (DataHora, Usuario, OperacaoId, Origem, Entidade, RegistroId, RaizEntidade, RaizId, Acao, Campo, ValorAnterior, ValorNovo, Descricao)
        SELECT @Agora, N'sistema', @Operacao, 3, 'Etiqueta', CONVERT(varchar(40), Id), 'Etiqueta', Id, 3, NULL, NULL, NULL,
               CONCAT(N'Etiqueta ''', Nome, N''' criada pela migração a partir das etiquetas já digitadas nos cadastros (', Usos, N' uso(s)).')
        FROM #Etiquetas;

        -- Cada pessoa ligada à sua etiqueta (o texto original continua na coluna Texto).
        UPDATE pe SET EtiquetaId = e.Id
        FROM PessoaEtiquetas pe
        JOIN #Etiquetas e ON e.Nome = LTRIM(RTRIM(pe.Texto)) COLLATE Latin1_General_CI_AI;

        -- A mesma etiqueta escrita de dois jeitos na mesma pessoa: fica a mais antiga; a outra vai para o histórico.
        WITH Repetidas AS (
            SELECT Id, PessoaId, Texto, EtiquetaId,
                   ROW_NUMBER() OVER (PARTITION BY PessoaId, EtiquetaId ORDER BY CriadoEm, Id) AS N
            FROM PessoaEtiquetas
        )
        SELECT Id, PessoaId, Texto, EtiquetaId INTO #Repetidas FROM Repetidas WHERE N > 1;

        INSERT INTO Auditoria (DataHora, Usuario, OperacaoId, Origem, Entidade, RegistroId, RaizEntidade, RaizId, Acao, Campo, ValorAnterior, ValorNovo, Descricao)
        SELECT @Agora, N'sistema', @Operacao, 3, 'PessoaEtiqueta', CONVERT(varchar(40), r.Id), 'Pessoa', r.PessoaId, 3, NULL, NULL, NULL,
               CONCAT(N'Migração das etiquetas: "', r.Texto, N'" era a mesma etiqueta que "', e.Nome,
                      N'" (só mudavam maiúsculas ou acentos) e ficou uma só.')
        FROM #Repetidas r
        JOIN #Etiquetas e ON e.Id = r.EtiquetaId;

        DELETE pe FROM PessoaEtiquetas pe JOIN #Repetidas r ON r.Id = pe.Id;

        -- Conferência: tudo ligado e nada sumiu (ligadas + unidas = antes). Senão, desfaz a migração inteira.
        DECLARE @Ligadas int = (SELECT COUNT(*) FROM PessoaEtiquetas WHERE EtiquetaId IS NOT NULL);
        DECLARE @Unidas int = (SELECT COUNT(*) FROM #Repetidas);
        IF EXISTS (SELECT 1 FROM PessoaEtiquetas WHERE EtiquetaId IS NULL) OR @Ligadas + @Unidas <> @Antes
            THROW 50002, N'Migração de etiquetas: a contagem depois não confere com a de antes. Nada foi alterado.', 1;

        DROP TABLE #Repetidas;
        DROP TABLE #Etiquetas;
        """;

    /// <summary>No Down, antes de a coluna EtiquetaId sair: garante o texto em todas as ligações (inclusive as feitas depois).</summary>
    public const string DevolverTexto = """
        UPDATE pe SET Texto = e.Nome
        FROM PessoaEtiquetas pe
        JOIN Etiquetas e ON e.Id = pe.EtiquetaId
        WHERE pe.Texto IS NULL;
        """;
}
