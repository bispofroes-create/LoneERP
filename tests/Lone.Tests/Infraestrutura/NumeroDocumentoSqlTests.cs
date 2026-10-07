using System.Text;
using Lone.Domain.Documentos;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Infrastructure.Persistencia;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lone.Tests.Infraestrutura;

/// <summary>
/// P1-8A: a regra do número comparável escrita em SQL (backfill da migração e diagnóstico) é a MESMA do domínio, e o
/// diagnóstico é somente leitura e não mostra número inteiro. Banco temporário; pulado sem LONE_TESTES_SQLSERVER.
/// </summary>
public class NumeroDocumentoSqlTests
{
    private static readonly Guid Rg = TiposDocumentoSistema.Id(TipoDocumento.Rg);

    /// <summary>Corpo de prova: cada caractere de 1 a U+024F, casos internacionais e textos aleatórios (até 30 unidades UTF-16).</summary>
    private static List<string> Corpus()
    {
        var corpus = new List<string>();
        var todos = Enumerable.Range(1, 0x24F).Select(c => (char)c).ToArray();
        for (var i = 0; i < todos.Length; i += 30) corpus.Add(new string(todos.Skip(i).Take(30).ToArray()));
        corpus.AddRange([
            "", "12.345.678-9", " mg-12.345.678 ", "0012", "Ção-Ñ ü", "É", "ЖД12", "１２", "٣4", "ı i ß İ", "AB😀12", "#*()",
            "ǅǆ ǈ", "ﬁ ﬂ", "Ⅻ ⑫ ²³", "ＡＢｃ", "ŁŃŚŹŻ łńśźż", "ĞŞ ğş", "ΑΒΓ αβγ", "אב12", "中文12", "\t1\n2\r3", "A B​C"
        ]);
        var aleatorio = new Random(1808);
        int[][] faixas = [[0x20, 0x7E], [0xA0, 0xFF], [0x100, 0x17F], [0x300, 0x36F], [0x400, 0x4FF], [0xFF01, 0xFF5E]];
        for (var n = 0; n < 400; n++)
        {
            var texto = new StringBuilder();
            var tamanho = aleatorio.Next(0, 29);
            while (texto.Length < tamanho)
            {
                if (aleatorio.Next(20) == 0) { texto.Append("😀"); continue; }
                var faixa = faixas[aleatorio.Next(faixas.Length)];
                texto.Append((char)aleatorio.Next(faixa[0], faixa[1] + 1));
            }
            corpus.Add(texto.ToString());
        }
        return corpus;
    }

    private static async Task<Guid> PessoaAsync(SqlConnection sql)
    {
        var pessoa = Guid.NewGuid();
        await MigracaoCompletaTests.InserirAsync(sql, "Pessoas", new() { ["Id"] = pessoa, ["Nome"] = "P18 " + pessoa.ToString("N")[..6], ["Natureza"] = (byte)NaturezaPessoa.Fisica });
        return pessoa;
    }

    private static PessoaDocumento Documento(Guid pessoa, string numero, bool ativo = true, string? uf = null) => new()
    {
        Id = Guid.NewGuid(), PessoaId = pessoa, TipoDocumentoId = Rg, Tipo = TipoDocumento.Rg, Numero = numero, Uf = uf, Ativo = ativo,
        NumeroNormalizado = string.Empty // quem preenche aqui é o SQL da migração
    };

    [FatoSqlServer]
    public async Task Sql_da_migracao_calcula_exatamente_o_mesmo_numero_comparavel_do_dominio()
    {
        await using var banco = await BancoDeTeste.CriarAsync(comProtecoes: false);
        await using var sql = new SqlConnection(banco.Conexao);
        await sql.OpenAsync();
        var pessoa = await PessoaAsync(sql);
        var corpus = Corpus();
        Assert.All(corpus, t => Assert.True(t.Length <= NumeroDocumento.TamanhoMaximo, t));

        await using (var db = banco.Contexto())
        {
            db.PessoaDocumentos.AddRange(corpus.Select(t => Documento(pessoa, t)));
            await db.SaveChangesAsync();
        }

        await using (var db = banco.Contexto())
            await db.Database.ExecuteSqlRawAsync(SqlMigracaoDocumentos.PreencherNumeroNormalizado);

        await using var leitura = banco.Contexto();
        var gravados = await leitura.PessoaDocumentos.AsNoTracking().Where(d => d.PessoaId == pessoa).ToListAsync();
        Assert.Equal(corpus.Count, gravados.Count);
        var diferentes = gravados
            .Where(d => d.NumeroNormalizado != NumeroDocumento.Normalizar(d.Numero))
            .Select(d => $"[{string.Join(" ", d.Numero.Select(c => ((int)c).ToString("X4")))}] SQL={d.NumeroNormalizado} C#={NumeroDocumento.Normalizar(d.Numero)}")
            .ToList();
        Assert.True(diferentes.Count == 0, string.Join(Environment.NewLine, diferentes.Take(20)));
        Assert.Contains(gravados, d => d.Numero == "12.345.678-9" && d.NumeroNormalizado == "123456789");
    }

    [FatoSqlServer]
    public async Task Diagnostico_e_somente_leitura_acha_repetidos_e_nomes_divergentes_e_mascara_os_numeros()
    {
        await using var banco = await BancoDeTeste.CriarAsync(comProtecoes: false);
        await using var sql = new SqlConnection(banco.Conexao);
        await sql.OpenAsync();
        var a = await PessoaAsync(sql);
        var b = await PessoaAsync(sql);
        await using (var db = banco.Contexto())
        {
            db.PessoaDocumentos.AddRange(
                Documento(a, "55.443.322-1", uf: "SP"), Documento(a, "554433221", uf: "SP"),   // repetido na mesma pessoa
                Documento(b, "55443322-1", uf: "SP"),                                          // e em outra pessoa, mesma UF
                Documento(b, "55443322-1", ativo: false),                                      // inativo não conta
                Documento(b, "#-#"));                                                          // sem número comparável
            await db.SaveChangesAsync();
        }
        await new SqlCommand($"UPDATE TiposDocumento SET Nome = N'Registro Geral' WHERE Id = '{Rg}'", sql).ExecuteNonQueryAsync();
        const string Soma = "SELECT CHECKSUM_AGG(CHECKSUM(Id, Numero, NumeroNormalizado, Ativo, Uf)) FROM PessoaDocumentos";
        var antes = await new SqlCommand(Soma, sql).ExecuteScalarAsync();

        var resultados = new List<List<object?[]>>();
        await using (var comando = new SqlCommand(SqlMigracaoDocumentos.Diagnostico, sql))
        await using (var leitor = await comando.ExecuteReaderAsync())
        {
            do
            {
                var linhas = new List<object?[]>();
                while (await leitor.ReadAsync())
                {
                    var valores = new object?[leitor.FieldCount];
                    leitor.GetValues(valores!);
                    linhas.Add(valores);
                }
                resultados.Add(linhas);
            } while (await leitor.NextResultAsync());
        }

        Assert.Equal(7, resultados.Count);
        Assert.Equal("Registro Geral", Assert.Single(resultados[1])[1]);                        // 2. nome divergente
        Assert.Equal(2, Convert.ToInt32(Assert.Single(resultados[2])[4]));                       // 3. mesma pessoa: 2
        Assert.Equal(2, Convert.ToInt32(Assert.Single(resultados[3])[2]));                       // 4. entre pessoas: 2
        Assert.Equal(2, Convert.ToInt32(Assert.Single(resultados[4])[3]));                       // 5. mesma UF: 2
        Assert.Equal(1, Convert.ToInt32(Assert.Single(resultados[5])[1]));                       // 6. sem número comparável
        var celulas = resultados.SelectMany(r => r).SelectMany(l => l).OfType<string>().ToList();
        Assert.DoesNotContain(celulas, c => c.Contains("554433221") || c.Contains("55.443") || c.Contains("443322"));
        Assert.Contains(celulas, c => c == "•••221");

        Assert.Equal(antes, await new SqlCommand(Soma, sql).ExecuteScalarAsync()); // nada mudou
    }
}
