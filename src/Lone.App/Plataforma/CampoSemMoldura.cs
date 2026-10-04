namespace Lone.App.Plataforma;

/// <summary>
/// Campo (Entry, Picker ou SearchBar) que fica dentro de uma borda desenhada pelo Lone: linha de filtro da grade, pesquisa
/// do topo de Pessoas, busca de colunas, busca de campos do painel de filtros, pesquisa do menu.
/// No Windows, o controle nativo (TextBox, ComboBox ou AutoSuggestBox do WinUI) tem moldura, fundo e a linha de foco
/// próprios; dentro da nossa borda isso aparecia como uma segunda moldura (as "linhas" nas laterais, 03/10/2026). Aqui o
/// controle nativo fica sem moldura e sem fundo em todos os estados (normal, ponteiro em cima, foco, desligado), a borda
/// de fora é a única e fica com a cor primária enquanto o campo está em uso.
/// Em XAML: <c>plat:CampoSemMoldura.Ligado="True"</c> no campo; em código: <see cref="Aplicar"/>.
/// Padrão para as próximas telas: docs/UX-ARQUITETURA.md.
/// </summary>
public static class CampoSemMoldura
{
    public static readonly BindableProperty LigadoProperty = BindableProperty.CreateAttached(
        "Ligado", typeof(bool), typeof(CampoSemMoldura), false,
        propertyChanged: (campo, _, novo) => { if (novo is true && campo is View v) Aplicar(v); });

    public static bool GetLigado(BindableObject campo) => (bool)campo.GetValue(LigadoProperty);

    public static void SetLigado(BindableObject campo, bool valor) => campo.SetValue(LigadoProperty, valor);

    public static void Aplicar(View campo)
    {
        campo.HandlerChanged += (_, _) =>
        {
            LigarFoco(campo);
#if WINDOWS
            switch (campo.Handler?.PlatformView)
            {
                case Microsoft.UI.Xaml.Controls.TextBox texto:
                    Limpar(texto);
                    break;
                case Microsoft.UI.Xaml.Controls.ComboBox lista:
                    Limpar(lista);
                    lista.UseSystemFocusVisuals = false; // a borda de fora já mostra o foco
                    break;
                case Microsoft.UI.Xaml.Controls.AutoSuggestBox busca:
                    // A pesquisa (SearchBar) tem uma caixa de texto dentro do modelo: limpa as duas.
                    Limpar(busca);
                    void LimparInternas() { foreach (var interna in Internas<Microsoft.UI.Xaml.Controls.TextBox>(busca)) Limpar(interna); }
                    busca.Loaded += (_, _) => LimparInternas();
                    if (busca.IsLoaded) LimparInternas();
                    break;
            }
#endif
        };
    }

    /// <summary>Borda de fora com a cor primária enquanto o campo está em uso (uma vez por borda).</summary>
    private static void LigarFoco(View campo)
    {
        Element? pai = campo.Parent;
        while (pai is not null and not Border) pai = pai.Parent;
        if (pai is not Border borda || borda.Triggers.OfType<DataTrigger>().Any(t => t.Binding is Binding { Source: var s } && ReferenceEquals(s, campo)))
            return;
        if (Application.Current?.Resources.TryGetValue("Primaria", out var clara) != true || clara is not Color corClara ||
            !Application.Current.Resources.TryGetValue("PrimariaEscuro", out var escura) || escura is not Color corEscura)
            return;
        var emUso = new DataTrigger(typeof(Border)) { Binding = new Binding(nameof(VisualElement.IsFocused), source: campo), Value = true };
        emUso.Setters.Add(new Setter
        {
            Property = Border.StrokeProperty,
            Value = Application.Current.RequestedTheme == AppTheme.Dark ? corEscura : corClara
        });
        // No começo da lista: um aviso da própria borda (ex.: valor inválido em vermelho) continua valendo com o foco.
        borda.Triggers.Insert(0, emUso);
    }

#if WINDOWS
    private static readonly string[] Prefixos = ["TextControl", "ComboBox", "AutoSuggestBox"];
    private static readonly string[] Estados = ["", "PointerOver", "Pressed", "Focused", "Unfocused", "Disabled"];

    private static void Limpar(Microsoft.UI.Xaml.Controls.Control controle)
    {
        var transparente = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
        var zero = new Microsoft.UI.Xaml.Thickness(0);
        // Recursos locais valem para os estados visuais do modelo do controle (que procuram estas chaves por nome).
        foreach (var prefixo in Prefixos)
            foreach (var estado in Estados)
            {
                controle.Resources[$"{prefixo}Background{estado}"] = transparente;
                controle.Resources[$"{prefixo}BorderBrush{estado}"] = transparente;
            }
        foreach (var chave in new[] { "TextControlBorderThemeThickness", "TextControlBorderThemeThicknessFocused", "ComboBoxBorderThemeThickness" })
            controle.Resources[chave] = zero;
        controle.BorderThickness = zero;
        controle.Background = transparente;
        controle.BorderBrush = transparente;
    }

    private static IEnumerable<T> Internas<T>(Microsoft.UI.Xaml.DependencyObject raiz) where T : Microsoft.UI.Xaml.DependencyObject
    {
        for (var i = 0; i < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(raiz); i++)
        {
            var filho = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(raiz, i);
            if (filho is T achado) yield return achado;
            foreach (var neto in Internas<T>(filho)) yield return neto;
        }
    }
#endif
}
