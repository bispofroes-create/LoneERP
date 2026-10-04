namespace Lone.Cliente.Formularios;

/// <summary>Como os itens de uma linha se alinham na vertical (os nomes seguem o FlexLayout que o bloco substitui).</summary>
public enum AlinhamentoLinha
{
    /// <summary>Cada item ocupa a altura toda da linha (padrão).</summary>
    Esticar,
    Inicio,
    Centro,
    Fim
}

/// <summary>
/// Um item do bloco: visível ou não, e a largura pedida. <see cref="Colunas"/> = colunas da grade (1 ou 2; a grade tem
/// 1, 2 ou 3 colunas conforme a largura); <see cref="Fracao"/> = parte da largura do bloco (0,5 = 1 coluna, 1 = linha
/// inteira); <see cref="LarguraFixa"/> = pontos; nenhum dos três = largura natural do item.
/// <see cref="Peso"/> (barra de filtros, 03/10/2026): o item começa com <see cref="LarguraFixa"/> (mínima) e, fechada a
/// linha, divide com os outros itens com peso a sobra da linha, na proporção do peso — a linha fica sempre cheia.
/// </summary>
public readonly record struct ItemBloco(bool Visivel, double? Fracao = null, double? LarguraFixa = null, int? Colunas = null, double? Peso = null);

/// <summary>Posição calculada de um item (vale a área inteira do item, margem incluída). Item escondido: tudo zero.</summary>
public readonly record struct PosicaoItem(double X, double Y, double Largura, double Altura);

/// <summary>
/// Cálculo do bloco de campos das fichas (BlocoCampos, Lone.App), sem MAUI para poder ser testado.
/// Substitui o FlexLayout do MAUI, que repartia a altura do bloco igualmente entre as linhas e cortava os campos mais
/// altos (diagnóstico de 03/10/2026). Regras:
/// <list type="bullet">
/// <item>itens lado a lado na ordem; quando o próximo não cabe, começa outra linha;</item>
/// <item>grade do Lone (03/10/2026, referência SAP Fiori): 1 coluna até <see cref="LarguraDuasColunas"/>, 2 colunas até
/// <see cref="LarguraTresColunas"/>, 3 colunas daí em diante; o campo ocupa 1 coluna, 2 colunas ou a linha inteira
/// (meia linha, 0,5, é 1 coluna);</item>
/// <item>cada linha tem a altura do seu item mais alto; a altura do bloco é a soma das linhas;</item>
/// <item>item escondido não ocupa espaço.</item>
/// </list>
/// </summary>
public static class CalculoBlocoCampos
{
    /// <summary>A partir desta largura do bloco (pontos), a grade tem 2 colunas.</summary>
    public const double LarguraDuasColunas = 600;

    /// <summary>A partir desta largura do bloco (pontos), a grade tem 3 colunas (como o SAP: 3 só em tela bem larga).</summary>
    public const double LarguraTresColunas = 1300;

    /// <summary>Folga de arredondamento ao decidir se um item ainda cabe na linha.</summary>
    private const double Folga = 0.5;

    /// <summary>Colunas da grade para esta largura do bloco: 1, 2 ou 3.</summary>
    public static int ColunasDaGrade(double largura) =>
        largura >= LarguraTresColunas ? 3 : largura >= LarguraDuasColunas ? 2 : 1;

    /// <summary>Largura que o item ocupa no bloco (ainda sem saber a altura).</summary>
    public static double LarguraDoItem(ItemBloco item, double largura, Func<double> larguraNatural)
    {
        var grade = ColunasDaGrade(largura);
        if (item.Colunas is { } n and > 0) return Math.Max(0, largura * Math.Min(n, grade) / grade);
        if (item.Fracao is { } f)
            return Math.Max(0, Math.Abs(f - 0.5) < 0.0001 ? largura / grade : largura * f); // meia linha = 1 coluna
        if (item.LarguraFixa is { } fixa) return Math.Min(Math.Max(0, fixa), largura);
        return Math.Min(Math.Max(0, larguraNatural()), largura);
    }

    /// <summary>
    /// Posições de todos os itens (mesma ordem de <paramref name="itens"/>) e a altura do bloco.
    /// <paramref name="larguraNatural"/>: largura do item sem limite (só para itens sem fração nem largura fixa).
    /// <paramref name="altura"/>: altura do item com a largura que ele vai ter.
    /// </summary>
    public static IReadOnlyList<PosicaoItem> Calcular(
        double largura,
        IReadOnlyList<ItemBloco> itens,
        Func<int, double> larguraNatural,
        Func<int, double, double> altura,
        AlinhamentoLinha alinhamento,
        out double alturaTotal)
    {
        var posicoes = new PosicaoItem[itens.Count];
        var linha = new List<(int Indice, double X, double Largura, double Altura)>();
        double y = 0, x = 0;

        void FecharLinha()
        {
            if (linha.Count == 0) return;
            // Itens com peso dividem a sobra da linha (a altura é medida de novo com a largura final).
            var pesos = linha.Sum(i => itens[i.Indice].Peso is > 0 ? itens[i.Indice].Peso!.Value : 0);
            var sobra = largura - linha.Sum(i => i.Largura);
            if (pesos > 0 && sobra > Folga)
            {
                double deslocamento = 0;
                for (var k = 0; k < linha.Count; k++)
                {
                    var (ind, xi, li, ai) = linha[k];
                    var extra = itens[ind].Peso is > 0 ? sobra * itens[ind].Peso!.Value / pesos : 0;
                    var nova = li + extra;
                    linha[k] = (ind, xi + deslocamento, nova, extra > 0 ? Math.Max(0, altura(ind, nova)) : ai);
                    deslocamento += extra;
                }
            }
            var alturaLinha = linha.Max(i => i.Altura);
            foreach (var (indice, xi, li, ai) in linha)
            {
                var (yi, hi) = alinhamento switch
                {
                    AlinhamentoLinha.Inicio => (y, ai),
                    AlinhamentoLinha.Centro => (y + (alturaLinha - ai) / 2, ai),
                    AlinhamentoLinha.Fim => (y + alturaLinha - ai, ai),
                    _ => (y, alturaLinha)
                };
                posicoes[indice] = new PosicaoItem(xi, yi, li, hi);
            }
            y += alturaLinha;
            x = 0;
            linha.Clear();
        }

        for (var i = 0; i < itens.Count; i++)
        {
            var item = itens[i];
            if (!item.Visivel) continue; // posição zerada: não ocupa espaço
            var indice = i;
            var li = LarguraDoItem(item, largura, () => larguraNatural(indice));
            if (linha.Count > 0 && x + li > largura + Folga) FecharLinha();
            linha.Add((i, x, li, Math.Max(0, altura(i, li))));
            x += li;
        }
        FecharLinha();
        alturaTotal = y;
        return posicoes;
    }
}
