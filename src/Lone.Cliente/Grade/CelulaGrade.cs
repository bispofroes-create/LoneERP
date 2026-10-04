namespace Lone.Cliente.Grade;

/// <summary>Um selo dentro de uma célula: o texto sempre aparece; o tom (cor) só ajuda.</summary>
public sealed record SeloGrade(string Texto, string Tom);

/// <summary>
/// Uma célula já formatada, ligada à sua coluna. Não guarda largura: a largura é da coluna
/// (<c>Coluna.LarguraEfetiva</c>). O tipo também vem da coluna, então a célula nunca diverge do molde da coluna.
/// </summary>
public sealed class CelulaGrade
{
    public const string SemValor = "—";

    private CelulaGrade(ColunaGradeDef coluna, string? texto, string? tom, IReadOnlyList<SeloGrade> selos)
    {
        Coluna = coluna;
        Texto = string.IsNullOrWhiteSpace(texto) ? SemValor : texto;
        Tom = tom;
        Selos = selos;
    }

    /// <summary>
    /// Texto simples. O <paramref name="tom"/> opcional marca um aviso no próprio texto (ex.: "Sem CPF", "Município a
    /// corrigir") sem trocar o molde da coluna: continua sendo um texto, só com a cor do tom.
    /// </summary>
    public static CelulaGrade DeTexto(ColunaGradeDef coluna, string? texto, string? tom = null) =>
        new(Exigir(coluna, TipoCelula.Texto), texto, tom, []);

    public static CelulaGrade DeSelo(ColunaGradeDef coluna, string? texto, string tom) =>
        new(Exigir(coluna, TipoCelula.Selo), texto, tom ?? throw new ArgumentNullException(nameof(tom)), []);

    /// <summary>Selos lado a lado; o texto junta os selos (dica e leitores de tela).</summary>
    public static CelulaGrade DePilulas(ColunaGradeDef coluna, IReadOnlyList<SeloGrade>? selos)
    {
        var lista = selos ?? [];
        return new(Exigir(coluna, TipoCelula.Pilulas), string.Join(", ", lista.Select(s => s.Texto)), null, lista);
    }

    /// <summary>A coluna desta célula (referência): largura e tipo vêm dela.</summary>
    public ColunaGradeDef Coluna { get; }

    public TipoCelula Tipo => Coluna.Tipo;

    public string Texto { get; }

    /// <summary>Tom ("Sucesso", "Aviso", "Neutro"...): obrigatório no selo; opcional no texto (aviso); nulo nas pílulas.</summary>
    public string? Tom { get; }

    /// <summary>Os selos (só no tipo <see cref="TipoCelula.Pilulas"/>; vazio nos outros).</summary>
    public IReadOnlyList<SeloGrade> Selos { get; }

    public bool Vazia => Tipo == TipoCelula.Pilulas ? Selos.Count == 0 : Texto == SemValor;

    private static ColunaGradeDef Exigir(ColunaGradeDef coluna, TipoCelula tipo)
    {
        ArgumentNullException.ThrowIfNull(coluna);
        if (coluna.Tipo != tipo)
            throw new ArgumentException($"A coluna \"{coluna.Chave}\" é do tipo {coluna.Tipo}, não {tipo}.", nameof(coluna));
        return coluna;
    }
}
