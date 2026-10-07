using System.Text;
using Lone.Domain.Documentos;

namespace Lone.Infrastructure.Persistencia;

/// <summary>
/// P1-8 — SQL da migração dos documentos e o diagnóstico (somente leitura) que vem antes de qualquer bloqueio.
/// A forma comparável do número é a MESMA regra de <see cref="NumeroDocumento.Normalizar"/>, caractere a caractere:
/// letra acentuada pela mesma tabela, a..z → A..Z, fica só A..Z e 0..9. Sem função SQL permanente: a expressão é
/// montada aqui a partir das constantes do domínio (uma fonte só) e um teste confere a paridade C# × SQL.
/// </summary>
public static class SqlMigracaoDocumentos
{
    /// <summary>
    /// Expressão SQL (subconsulta) com o número comparável de <paramref name="coluna"/> (nvarchar até 30).
    /// Posições em unidades UTF-16 (COLLATE Latin1_General_BIN2, como o C# percorre a string); um caractere fora da
    /// tabela e fora de A–Z/0–9 sai.
    /// </summary>
    public static string ExpressaoNumeroNormalizado(string coluna)
    {
        var acentuadas = "N'" + NumeroDocumento.Acentuadas + "' COLLATE Latin1_General_BIN2";
        var semAcento = "N'" + NumeroDocumento.SemAcento + "'";
        var posicoes = string.Join(",", Enumerable.Range(1, NumeroDocumento.TamanhoMaximo).Select(i => $"({i})"));
        var original = $"SUBSTRING({coluna} COLLATE Latin1_General_BIN2, v.i, 1)";
        return $"""
            ISNULL((SELECT n.c AS [text()]
                FROM (SELECT v.i,
                             CASE WHEN UNICODE(m.c) BETWEEN 97 AND 122 THEN NCHAR(UNICODE(m.c) - 32) ELSE m.c END AS c
                      FROM (VALUES {posicoes}) v(i)
                      CROSS APPLY (SELECT {original} AS o) s
                      CROSS APPLY (SELECT CASE WHEN CHARINDEX(s.o COLLATE Latin1_General_BIN2, {acentuadas}) > 0
                                               THEN SUBSTRING({semAcento}, CHARINDEX(s.o COLLATE Latin1_General_BIN2, {acentuadas}), 1)
                                               ELSE s.o END AS c) m
                      WHERE v.i <= DATALENGTH({coluna}) / 2) n
                WHERE UNICODE(n.c) BETWEEN 48 AND 57 OR UNICODE(n.c) BETWEEN 65 AND 90
                ORDER BY n.i
                FOR XML PATH('')), '')
            """;
    }

    /// <summary>
    /// Depois de criar PessoaDocumentos.NumeroNormalizado (nasce vazio): preenche todos os documentos (ativos e inativos)
    /// e confere: nenhuma linha perdida e nada fora de A–Z/0–9. O número exibido (Numero) não muda.
    /// </summary>
    public static readonly string PreencherNumeroNormalizado = $"""
        SET NOCOUNT ON;
        DECLARE @total int = (SELECT COUNT(*) FROM PessoaDocumentos);
        UPDATE d SET NumeroNormalizado = {ExpressaoNumeroNormalizado("d.Numero")}
        FROM PessoaDocumentos d;
        IF (SELECT COUNT(*) FROM PessoaDocumentos) <> @total
           OR EXISTS (SELECT 1 FROM PessoaDocumentos
                      WHERE NumeroNormalizado COLLATE Latin1_General_BIN2 LIKE '%[^0-9A-Z]%'
                         OR LEN(NumeroNormalizado) > LEN(Numero))
            THROW 50090, N'Migração de documentos (P1-8): o número comparável não conferiu. Nada foi alterado.', 1;
        """;

    /// <summary>
    /// Depois da semente: os cinco tipos de sistema existem e nenhum documento ficou com chave de unicidade (a migração não
    /// liga bloqueio nenhum).
    /// </summary>
    public const string ConferirSementeESemBloqueio = """
        SET NOCOUNT ON;
        IF (SELECT COUNT(*) FROM TiposDocumento WHERE TipoSistema IS NOT NULL) <> 5
           OR EXISTS (SELECT 1 FROM TiposDocumento WHERE Unicidade IN (2, 3))
           OR EXISTS (SELECT 1 FROM PessoaDocumentos WHERE ChaveUnicidade IS NOT NULL)
            THROW 50091, N'Migração de documentos (P1-8): a semente dos tipos não conferiu. Nada foi alterado.', 1;
        """;

    /// <summary>
    /// Diagnóstico P1-8 — SOMENTE LEITURA (só uma tabela temporária #DocP18 no tempdb). Roda antes ou depois da migração:
    /// usa apenas colunas que já existiam e calcula o número comparável com a mesma regra. Números aparecem mascarados
    /// (só os 3 últimos caracteres). Resultados, nesta ordem:
    /// 1. contagens por tipo; 2. tipos de sistema com nome diferente do original; 3. repetidos na mesma pessoa (ativos);
    /// 4. repetidos entre pessoas no mesmo tipo (ativos); 5. repetidos entre pessoas no mesmo tipo e UF (ativos);
    /// 6. documentos ativos sem número comparável; 7. por tipo, ativos que não passariam nos formatos "só números" e
    /// "letras e números".
    /// </summary>
    public static readonly string Diagnostico = MontarDiagnostico();

    private static string MontarDiagnostico()
    {
        var nomes = string.Join(",\n        ", TiposDocumentoSistema.Todos.Select(t => $"('{t.Id}', N'{t.Nome}')"));
        var separadores = NumeroDocumento.Separadores.Replace("-", "") ; // o hífen vai no fim da classe do LIKE
        var classeAlfa = "[^0-9A-Za-z" + NumeroDocumento.Acentuadas + separadores + "-]";
        var classeDigitos = "[^0-9" + separadores + "-]";
        var sql = new StringBuilder();
        sql.Append($"""
            SET NOCOUNT ON;
            IF OBJECT_ID('tempdb..#DocP18') IS NOT NULL DROP TABLE #DocP18;
            SELECT d.Id, d.PessoaId, d.TipoDocumentoId, d.Ativo, d.Uf, d.Numero,
                   CAST({ExpressaoNumeroNormalizado("d.Numero")} AS varchar(30)) AS Comparavel
            INTO #DocP18
            FROM PessoaDocumentos d;

            -- 1. Contagens por tipo
            SELECT t.Id AS TipoId, t.Nome AS Tipo, t.TipoSistema, t.Ativo AS TipoAtivo,
                   SUM(CASE WHEN x.Ativo = 1 THEN 1 ELSE 0 END) AS DocumentosAtivos,
                   SUM(CASE WHEN x.Ativo = 0 THEN 1 ELSE 0 END) AS DocumentosInativos
            FROM TiposDocumento t LEFT JOIN #DocP18 x ON x.TipoDocumentoId = t.Id
            GROUP BY t.Id, t.Nome, t.TipoSistema, t.Ativo
            ORDER BY t.TipoSistema DESC, t.Nome;

            -- 2. Tipos de sistema com nome diferente do original (não são corrigidos automaticamente)
            SELECT t.Id AS TipoId, t.Nome AS NomeAtual, o.Nome AS NomeOriginal
            FROM TiposDocumento t
            JOIN (VALUES
                    {nomes}) o(Id, Nome) ON o.Id = t.Id
            WHERE t.Nome COLLATE Latin1_General_BIN2 <> o.Nome COLLATE Latin1_General_BIN2;

            -- 3. Repetidos na MESMA pessoa (ativos, mesmo tipo e mesmo número comparável)
            SELECT x.PessoaId, p.Codigo, t.Nome AS Tipo, N'•••' + RIGHT(x.Comparavel, 3) AS NumeroMascarado, COUNT(*) AS Quantidade
            FROM #DocP18 x JOIN TiposDocumento t ON t.Id = x.TipoDocumentoId JOIN Pessoas p ON p.Id = x.PessoaId
            WHERE x.Ativo = 1 AND x.Comparavel <> ''
            GROUP BY x.PessoaId, p.Codigo, t.Nome, x.Comparavel
            HAVING COUNT(*) > 1
            ORDER BY p.Codigo;

            -- 4. Repetidos entre PESSOAS diferentes, no mesmo tipo (ativos)
            SELECT t.Nome AS Tipo, N'•••' + RIGHT(x.Comparavel, 3) AS NumeroMascarado, COUNT(DISTINCT x.PessoaId) AS Pessoas
            FROM #DocP18 x JOIN TiposDocumento t ON t.Id = x.TipoDocumentoId
            WHERE x.Ativo = 1 AND x.Comparavel <> ''
            GROUP BY t.Nome, x.Comparavel
            HAVING COUNT(DISTINCT x.PessoaId) > 1
            ORDER BY t.Nome;

            -- 5. Repetidos entre PESSOAS diferentes, no mesmo tipo e na mesma UF (ativos com UF)
            SELECT t.Nome AS Tipo, x.Uf, N'•••' + RIGHT(x.Comparavel, 3) AS NumeroMascarado, COUNT(DISTINCT x.PessoaId) AS Pessoas
            FROM #DocP18 x JOIN TiposDocumento t ON t.Id = x.TipoDocumentoId
            WHERE x.Ativo = 1 AND x.Comparavel <> '' AND x.Uf IS NOT NULL
            GROUP BY t.Nome, x.Uf, x.Comparavel
            HAVING COUNT(DISTINCT x.PessoaId) > 1
            ORDER BY t.Nome, x.Uf;

            -- 6. Ativos sem número comparável (só símbolos ou letras fora de A–Z): ficam fora de qualquer unicidade
            SELECT t.Nome AS Tipo, COUNT(*) AS Quantidade
            FROM #DocP18 x JOIN TiposDocumento t ON t.Id = x.TipoDocumentoId
            WHERE x.Ativo = 1 AND x.Comparavel = ''
            GROUP BY t.Nome;

            -- 7. Por tipo, ativos que NÃO passariam nos formatos fechados (se um dia forem escolhidos)
            SELECT t.Nome AS Tipo,
                   SUM(CASE WHEN x.Comparavel = '' OR x.Numero COLLATE Latin1_General_BIN2 LIKE N'%{classeDigitos}%' THEN 1 ELSE 0 END) AS ForaDeSoNumeros,
                   SUM(CASE WHEN x.Comparavel = '' OR x.Numero COLLATE Latin1_General_BIN2 LIKE N'%{classeAlfa}%' THEN 1 ELSE 0 END) AS ForaDeLetrasENumeros
            FROM #DocP18 x JOIN TiposDocumento t ON t.Id = x.TipoDocumentoId
            WHERE x.Ativo = 1
            GROUP BY t.Nome
            ORDER BY t.Nome;

            DROP TABLE #DocP18;
            """);
        return sql.ToString();
    }
}
