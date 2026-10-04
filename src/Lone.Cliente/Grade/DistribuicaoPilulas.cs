namespace Lone.Cliente.Grade;

/// <summary>Quantos selos vão na 1ª linha, quantos na 2ª e quantos ficam no "+N".</summary>
public sealed record Distribuicao(int NaLinha1, int NaLinha2, int Ocultos)
{
    public int Visiveis => NaLinha1 + NaLinha2;
}

/// <summary>
/// Coluna Papéis (decisão D-UX-01, 02/10/2026): nomes completos, sem abreviar; primeiro <b>uma linha</b>, depois
/// <b>duas</b> e, se ainda não couber, um <b>+N</b> no fim; nunca uma terceira linha nem linha mais alta. O que aparece
/// depende do espaço efetivo da célula (largura da coluna, densidade), não de um número fixo. Função pura: a célula
/// só desenha o resultado.
/// </summary>
public static class DistribuicaoPilulas
{
    /// <param name="larguras">Largura de cada selo, na ordem.</param>
    /// <param name="disponivel">Largura útil da célula (sem as margens).</param>
    /// <param name="espaco">Espaço entre os selos.</param>
    /// <param name="maxLinhas">1 ou 2 (a altura da linha decide).</param>
    /// <param name="larguraMais">Largura do selo "+N" para um N (o texto muda com o número).</param>
    public static Distribuicao Calcular(IReadOnlyList<double> larguras, double disponivel, double espaco, int maxLinhas,
        Func<int, double> larguraMais)
    {
        var n = larguras.Count;
        if (n == 0) return new Distribuicao(0, 0, 0);
        maxLinhas = Math.Clamp(maxLinhas, 1, 2);

        var linha1 = Cabem(larguras, 0, disponivel, espaco);
        if (linha1 == n) return new Distribuicao(n, 0, 0);
        if (maxLinhas == 1)
        {
            var k = ComMais(larguras, 0, linha1, n, disponivel, espaco, larguraMais);
            return new Distribuicao(k, 0, n - k);
        }

        linha1 = Math.Max(1, linha1); // um selo mais largo que a célula ainda aparece (recortado), não vira "+N" sozinho
        var linha2 = Cabem(larguras, linha1, disponivel, espaco);
        if (linha1 + linha2 == n) return new Distribuicao(linha1, linha2, 0);
        var k2 = ComMais(larguras, linha1, linha2, n, disponivel, espaco, larguraMais);
        return new Distribuicao(linha1, k2, n - linha1 - k2);
    }

    /// <summary>Quantos selos, a partir de <paramref name="inicio"/>, cabem numa linha.</summary>
    private static int Cabem(IReadOnlyList<double> larguras, int inicio, double disponivel, double espaco)
    {
        double usado = 0;
        var quantos = 0;
        for (var i = inicio; i < larguras.Count; i++)
        {
            var mais = (quantos == 0 ? 0 : espaco) + larguras[i];
            if (usado + mais > disponivel + 0.5) break;
            usado += mais;
            quantos++;
        }
        return quantos;
    }

    /// <summary>Tira selos do fim da linha até sobrar lugar para o "+N" dos que ficaram de fora.</summary>
    private static int ComMais(IReadOnlyList<double> larguras, int inicio, int cabem, int total, double disponivel, double espaco,
        Func<int, double> larguraMais)
    {
        for (var k = cabem; k >= 0; k--)
        {
            double usado = 0;
            for (var i = 0; i < k; i++) usado += (i == 0 ? 0 : espaco) + larguras[inicio + i];
            var ocultos = total - inicio - k;
            if (usado + (k == 0 ? 0 : espaco) + larguraMais(ocultos) <= disponivel + 0.5) return k;
        }
        return 0;
    }

    /// <summary>Dica do "+N": "Mais 2 papéis: Parceiro, Representante".</summary>
    public static string DicaOcultos(IReadOnlyList<string> nomes, int aPartirDe)
    {
        var ocultos = nomes.Skip(aPartirDe).ToList();
        return ocultos.Count == 1 ? $"Mais 1 papel: {ocultos[0]}" : $"Mais {ocultos.Count} papéis: {string.Join(", ", ocultos)}";
    }
}
