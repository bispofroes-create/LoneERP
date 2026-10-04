using CommunityToolkit.Mvvm.ComponentModel;

namespace Lone.Cliente.Grade;

/// <summary>Como a célula é desenhada: um molde enxuto por tipo, sem alternativas ocultas.</summary>
public enum TipoCelula
{
    /// <summary>Um texto (1 Label).</summary>
    Texto,

    /// <summary>Um selo colorido com texto (situação, tipo).</summary>
    Selo,

    /// <summary>Vários selos lado a lado (papéis).</summary>
    Pilulas
}

/// <summary>Como a coluna ocupa a largura da grade.</summary>
public enum ModoLargura
{
    /// <summary>Largura própria; só encolhe (até a mínima) quando a grade não cabe na janela.</summary>
    Fixa,

    /// <summary>Começa na mínima e recebe parte do espaço livre pelo peso, até a máxima (se houver).</summary>
    Proporcional
}

/// <summary>
/// Uma coluna da grade (infraestrutura, sem conhecer nenhuma tela): chave, título, tipo de célula e regra de largura.
/// A largura pertence à coluna: <see cref="LarguraEfetiva"/> é calculada pela <see cref="CalculadoraLarguras"/> e as
/// células e o cabeçalho ligam nela, sem reconstruir as linhas quando a janela muda.
/// </summary>
public sealed class ColunaGradeDef : ObservableObject
{
    private double _larguraEfetiva;

    private ColunaGradeDef(string chave, string titulo, TipoCelula tipo, bool ordenavel, ModoLargura modo,
        double largura, double peso, double minima, double? maxima)
    {
        if (string.IsNullOrWhiteSpace(chave)) throw new ArgumentException("A coluna precisa de uma chave.", nameof(chave));
        if (!Positivo(minima)) throw new ArgumentOutOfRangeException(nameof(minima), minima, "A mínima precisa ser maior que zero.");
        if (maxima is { } max && (!double.IsFinite(max) || max < minima))
            throw new ArgumentOutOfRangeException(nameof(maxima), maxima, "A máxima não pode ser menor que a mínima.");

        Chave = chave;
        Titulo = titulo ?? string.Empty;
        Tipo = tipo;
        Ordenavel = ordenavel;
        Modo = modo;
        Largura = largura;
        Peso = peso;
        Minima = minima;
        Maxima = maxima;
        _larguraEfetiva = modo == ModoLargura.Fixa ? largura : minima;
    }

    /// <summary>Coluna de largura própria. Sem mínima, nunca encolhe (mínima = largura).</summary>
    public static ColunaGradeDef Fixa(string chave, string titulo, TipoCelula tipo, double largura, double? minima = null,
        bool ordenavel = true)
    {
        if (!Positivo(largura)) throw new ArgumentOutOfRangeException(nameof(largura), largura, "A largura precisa ser maior que zero.");
        var min = minima ?? largura;
        if (min > largura) throw new ArgumentOutOfRangeException(nameof(minima), minima, "A mínima não pode passar da largura.");
        return new ColunaGradeDef(chave, titulo, tipo, ordenavel, ModoLargura.Fixa, largura, 0, min, null);
    }

    /// <summary>Coluna que divide o espaço livre com as outras proporcionais, pelo peso.</summary>
    public static ColunaGradeDef Proporcional(string chave, string titulo, TipoCelula tipo, double peso, double minima,
        double? maxima = null, bool ordenavel = true)
    {
        if (!Positivo(peso)) throw new ArgumentOutOfRangeException(nameof(peso), peso, "O peso precisa ser maior que zero.");
        return new ColunaGradeDef(chave, titulo, tipo, ordenavel, ModoLargura.Proporcional, minima, peso, minima, maxima);
    }

    public string Chave { get; }
    public string Titulo { get; }
    public TipoCelula Tipo { get; }
    public bool Ordenavel { get; }
    public ModoLargura Modo { get; }

    /// <summary>Fixa: a largura própria. Proporcional: igual à mínima (o resto vem do peso).</summary>
    public double Largura { get; }

    /// <summary>Proporcional: a parte do espaço livre que cabe a esta coluna. Fixa: 0.</summary>
    public double Peso { get; }

    public double Minima { get; }

    /// <summary>Limite da proporcional (nulo = sem limite).</summary>
    public double? Maxima { get; }

    /// <summary>A largura desenhada agora (pontos de tela). Só a calculadora muda.</summary>
    public double LarguraEfetiva
    {
        get => _larguraEfetiva;
        internal set => SetProperty(ref _larguraEfetiva, value);
    }

    private static bool Positivo(double valor) => double.IsFinite(valor) && valor > 0;
}
