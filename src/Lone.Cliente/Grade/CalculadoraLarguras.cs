namespace Lone.Cliente.Grade;

/// <summary>Larguras calculadas (na ordem das colunas), a soma e se a grade passa da largura disponível.</summary>
public sealed record ResultadoLarguras(IReadOnlyList<double> Larguras, double Total, bool RolagemLateral);

/// <summary>
/// A única regra de largura das colunas (função pura, em pontos de tela; a mesma em 100, 125 e 150%):
/// <list type="number">
/// <item><b>Cabe</b> (fixas + mínimas das proporcionais ≤ disponível): o espaço livre é dividido entre as proporcionais
/// pelo peso;</item>
/// <item><b>Atingiu a máxima:</b> a coluna para nela e o resto é redividido entre as outras proporcionais;</item>
/// <item><b>Sobrou espaço</b> sem proporcional para receber: vai para a última coluna;</item>
/// <item><b>Não cabe:</b> as fixas com mínima menor encolhem (na proporção do que cada uma pode ceder) só o necessário;
/// se nem assim couber, todas ficam na mínima e aparece a rolagem lateral.</item>
/// </list>
/// Quem chama passa todas as colunas desenhadas, inclusive a parte fixa (nome), e a largura disponível da grade.
/// </summary>
public static class CalculadoraLarguras
{
    private const double Folga = 0.001;

    public static ResultadoLarguras Calcular(IReadOnlyList<ColunaGradeDef> colunas, double disponivel)
    {
        ArgumentNullException.ThrowIfNull(colunas);
        var n = colunas.Count;
        if (n == 0) return new ResultadoLarguras([], 0, false);
        if (!double.IsFinite(disponivel) || disponivel < 0) disponivel = 0;

        var larguras = new double[n];
        double necessario = 0, cedivel = 0;
        for (var i = 0; i < n; i++)
        {
            var c = colunas[i];
            larguras[i] = c.Modo == ModoLargura.Fixa ? c.Largura : c.Minima;
            necessario += larguras[i];
            if (c.Modo == ModoLargura.Fixa) cedivel += c.Largura - c.Minima;
        }

        if (necessario > disponivel + Folga)
            Apertar(colunas, larguras, necessario - disponivel, cedivel);
        else
            Distribuir(colunas, larguras, disponivel - necessario);

        var total = larguras.Sum();
        return new ResultadoLarguras(larguras, total, total > disponivel + Folga);
    }

    /// <summary>
    /// Grava as larguras calculadas nas colunas (<see cref="ColunaGradeDef.LarguraEfetiva"/>). Só avisa as colunas
    /// cuja largura mudou. Devolve se alguma mudou.
    /// </summary>
    public static bool Aplicar(IReadOnlyList<ColunaGradeDef> colunas, ResultadoLarguras resultado)
    {
        ArgumentNullException.ThrowIfNull(colunas);
        ArgumentNullException.ThrowIfNull(resultado);
        if (colunas.Count != resultado.Larguras.Count)
            throw new ArgumentException("O cálculo é de outro conjunto de colunas.", nameof(resultado));

        var mudou = false;
        for (var i = 0; i < colunas.Count; i++)
        {
            if (Math.Abs(colunas[i].LarguraEfetiva - resultado.Larguras[i]) <= Folga) continue;
            colunas[i].LarguraEfetiva = resultado.Larguras[i];
            mudou = true;
        }
        return mudou;
    }

    /// <summary>Não cabe: as fixas que podem encolher cedem juntas, até a mínima.</summary>
    private static void Apertar(IReadOnlyList<ColunaGradeDef> colunas, double[] larguras, double falta, double cedivel)
    {
        if (cedivel <= 0) return;
        var fator = Math.Min(1, falta / cedivel);
        for (var i = 0; i < colunas.Count; i++)
        {
            var c = colunas[i];
            if (c.Modo == ModoLargura.Fixa) larguras[i] = c.Largura - (c.Largura - c.Minima) * fator;
        }
    }

    /// <summary>Cabe: o livre vai para as proporcionais pelo peso (respeitando a máxima); o que sobrar, para a última.</summary>
    private static void Distribuir(IReadOnlyList<ColunaGradeDef> colunas, double[] larguras, double livre)
    {
        var ativas = Enumerable.Range(0, colunas.Count).Where(i => colunas[i].Modo == ModoLargura.Proporcional).ToList();
        while (livre > Folga && ativas.Count > 0)
        {
            var pesoTotal = ativas.Sum(i => colunas[i].Peso);
            // Quem passaria da máxima para nela; o resto é redividido na volta seguinte entre as que sobraram.
            var noLimite = ativas.Where(i => colunas[i].Maxima is { } max && larguras[i] + livre * colunas[i].Peso / pesoTotal > max).ToList();
            if (noLimite.Count == 0)
            {
                foreach (var i in ativas) larguras[i] += livre * colunas[i].Peso / pesoTotal;
                livre = 0;
                break;
            }
            foreach (var i in noLimite)
            {
                livre -= colunas[i].Maxima!.Value - larguras[i];
                larguras[i] = colunas[i].Maxima!.Value;
                ativas.Remove(i);
            }
        }
        if (livre > Folga) larguras[^1] += livre;
    }
}
