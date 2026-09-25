using System.Text;
using Lone.Domain.Entidades;

namespace Lone.Domain.Profissoes;

/// <summary>
/// Lê o arquivo oficial "CBO2002 - Ocupacao.csv" do Ministério do Trabalho (colunas CODIGO;TITULO, separadas por
/// ponto e vírgula, em geral em ISO-8859-1). Aceita também UTF-8, aspas, código com hífen ("2410-05") e cabeçalho.
/// Linhas que não dá para entender são contadas e ignoradas (a importação decide se o arquivo serve).
/// </summary>
public static class LeitorCbo
{
    public sealed record Resultado(IReadOnlyList<OcupacaoCbo> Ocupacoes, int LinhasIgnoradas);

    public static Resultado Ler(byte[] arquivo)
    {
        var texto = Decodificar(arquivo);
        var ocupacoes = new Dictionary<int, OcupacaoCbo>();
        var ignoradas = 0;

        foreach (var linhaBruta in texto.Split('\n'))
        {
            var linha = linhaBruta.Trim().TrimEnd('\r');
            if (linha.Length == 0) continue;

            var partes = linha.Split(';', 2);
            if (partes.Length < 2)
            {
                ignoradas++;
                continue;
            }

            var codigo = new string(Limpar(partes[0]).Where(char.IsAsciiDigit).ToArray());
            var titulo = RegrasProfissao.Texto(Limpar(partes[1]));
            if (codigo.Length != 6 || titulo is null)
            {
                // O cabeçalho ("CODIGO;TITULO") cai aqui sem contar como problema.
                if (!Limpar(partes[0]).Equals("CODIGO", StringComparison.OrdinalIgnoreCase)) ignoradas++;
                continue;
            }

            var id = int.Parse(codigo, System.Globalization.CultureInfo.InvariantCulture);
            if (titulo.Length > OcupacaoCbo.TamanhoMaximoTitulo) titulo = titulo[..OcupacaoCbo.TamanhoMaximoTitulo];
            ocupacoes.TryAdd(id, new OcupacaoCbo { Id = id, Titulo = titulo, Ativo = true }); // repetido: vale o primeiro
        }

        return new Resultado(ocupacoes.Values.OrderBy(o => o.Id).ToList(), ignoradas);
    }

    /// <summary>UTF-8 se o arquivo for UTF-8 válido; senão ISO-8859-1 (o formato do arquivo oficial).</summary>
    private static string Decodificar(byte[] arquivo)
    {
        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(arquivo).TrimStart('﻿');
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(arquivo);
        }
    }

    private static string Limpar(string campo) => campo.Trim().Trim('"').Trim();
}
