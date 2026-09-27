using System.Collections.ObjectModel;

namespace Lone.Cliente.ViewModels.Comum;

/// <summary>
/// Atualiza uma lista da tela no lugar, mexendo só no que mudou (remove, insere ou move itens). Trocar a lista inteira
/// ou limpar e refazer recria os elementos visuais — no Windows, fazer isso enquanto a tela está sendo montada derrubava
/// o aplicativo (COMException no Measure, ficha de PJ, 26/09/2026).
/// </summary>
public static class ColecaoSincronizada
{
    /// <summary>Deixa <paramref name="destino"/> com os mesmos itens de <paramref name="desejados"/>, na mesma ordem.</summary>
    public static void Sincronizar<T>(ObservableCollection<T> destino, IReadOnlyList<T> desejados) where T : class
    {
        for (var i = destino.Count - 1; i >= 0; i--)
            if (!desejados.Contains(destino[i])) destino.RemoveAt(i);
        for (var i = 0; i < desejados.Count; i++)
        {
            if (i < destino.Count && ReferenceEquals(destino[i], desejados[i])) continue;
            var atual = destino.IndexOf(desejados[i]);
            if (atual >= 0) destino.Move(atual, i);
            else destino.Insert(i, desejados[i]);
        }
    }
}
