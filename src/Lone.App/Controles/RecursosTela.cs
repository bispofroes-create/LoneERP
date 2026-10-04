namespace Lone.App.Controles;

/// <summary>
/// Estilos e cores do Lone para os controles feitos em código (BarraFicha, ListaCadastro, FichaCadastro): lidos dos
/// recursos do aplicativo (Estilos.xaml e Cores.xaml), com a cor do tema claro e do escuro.
/// </summary>
internal static class RecursosTela
{
    public static void Estilo(VisualElement elemento, string chave)
    {
        if (Application.Current?.Resources.TryGetValue(chave, out var estilo) == true && estilo is Style s) elemento.Style = s;
    }

    /// <summary>Cor do tema claro/escuro pelas chaves de Cores.xaml (a escura é a chave + "Escuro", salvo indicada).</summary>
    public static void Cor(BindableObject alvo, BindableProperty propriedade, string clara, string? escura = null)
    {
        var recursos = Application.Current?.Resources;
        if (recursos is null || !recursos.TryGetValue(clara, out var c) || c is not Color corClara) return;
        var corEscura = recursos.TryGetValue(escura ?? clara + "Escuro", out var e) && e is Color ce ? ce : corClara;
        alvo.SetAppThemeColor(propriedade, corClara, corEscura);
    }

    /// <summary>Margem das páginas (a mesma dos dois lados): 16 no celular, 32 × 24 no computador.</summary>
    public static Thickness MargemPagina => DeviceInfo.Idiom == DeviceIdiom.Phone ? new Thickness(16) : new Thickness(32, 24);
}
