namespace Lone.Cliente.Grade;

/// <summary>
/// O que a grade mostra, entregue de uma vez (troca atômica): as colunas e as linhas juntas. Assim a célula <c>i</c> de
/// cada linha sempre corresponde à coluna <c>i</c>, sem estado intermediário desalinhado. A coerência é conferida aqui.
/// A parte fixa (nome) não é coluna de célula: a grade a recebe à parte.
/// </summary>
public sealed class ConteudoGrade
{
    public static ConteudoGrade Vazio { get; } = new([], []);

    public ConteudoGrade(IReadOnlyList<ColunaGradeDef> colunas, IReadOnlyList<ILinhaGrade> linhas)
    {
        ArgumentNullException.ThrowIfNull(colunas);
        ArgumentNullException.ThrowIfNull(linhas);
        for (var i = 0; i < linhas.Count; i++)
        {
            var celulas = linhas[i].Celulas;
            if (celulas.Count != colunas.Count)
                throw new ArgumentException($"A linha {i} tem {celulas.Count} células para {colunas.Count} colunas.", nameof(linhas));
            for (var j = 0; j < celulas.Count; j++)
                if (!ReferenceEquals(celulas[j].Coluna, colunas[j]))
                    throw new ArgumentException($"A célula {j} da linha {i} é da coluna \"{celulas[j].Coluna.Chave}\", não de \"{colunas[j].Chave}\".", nameof(linhas));
        }
        Colunas = colunas;
        Linhas = linhas;
    }

    public IReadOnlyList<ColunaGradeDef> Colunas { get; }
    public IReadOnlyList<ILinhaGrade> Linhas { get; }

    /// <summary>As mesmas colunas (mesmas instâncias, mesma ordem): só as linhas mudaram e a grade não remonta o molde.</summary>
    public bool MesmasColunas(ConteudoGrade? outro) =>
        outro is not null && (ReferenceEquals(outro.Colunas, Colunas) || outro.Colunas.SequenceEqual(Colunas));
}
