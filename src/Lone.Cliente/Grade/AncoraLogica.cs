namespace Lone.Cliente.Grade;

/// <summary>
/// Âncora da rolagem por posição lógica (índice do primeiro registro visível), não por pixels: sobrevive à reciclagem
/// do visual, à troca de densidade e de escala. Funções puras usadas pelo adaptador de rolagem da grade.
/// </summary>
public static class AncoraLogica
{
    /// <summary>O índice guardado, dentro da lista atual ([0, quantidade − 1]). Lista vazia: nulo (nada a restaurar).</summary>
    public static int? Limitar(int indice, int quantidade) =>
        quantidade <= 0 ? null : Math.Clamp(indice, 0, quantidade - 1);

    /// <summary>
    /// A restauração chegou: o primeiro visível é o alvo. Perto do fim da lista o alvo não consegue subir até o topo
    /// (não há linhas abaixo para preencher a tela); com a lista no fim, parar antes do alvo também conta como certo.
    /// </summary>
    public static bool Confirmada(int alvo, int primeiroVisivel, bool listaNoFim) =>
        primeiroVisivel == alvo || (listaNoFim && primeiroVisivel >= 0 && primeiroVisivel < alvo);

    /// <summary>
    /// Reforço da restauração (decisão P3): depois de restaurar pelo índice, a linha marcada (aberta ou na prévia) que
    /// ficou fora da vista vai ao topo. Visível (ou sem linha marcada, ou vista desconhecida): nada a fazer.
    /// </summary>
    public static int? Reforco(int? marcada, int primeiroVisivel, int ultimoVisivel) =>
        marcada is { } m && primeiroVisivel >= 0 && ultimoVisivel >= primeiroVisivel && (m < primeiroVisivel || m > ultimoVisivel)
            ? m
            : null;

    /// <summary>Posição do registro (aberto ou da prévia) na lista atual; nulo se não estiver nela.</summary>
    public static int? IndiceDaChave(IReadOnlyList<ILinhaGrade> linhas, Guid? chave)
    {
        ArgumentNullException.ThrowIfNull(linhas);
        if (chave is not { } procurada) return null;
        for (var i = 0; i < linhas.Count; i++)
            if (linhas[i].Chave == procurada) return i;
        return null;
    }
}
