namespace Lone.App.Plataforma;

/// <summary>
/// Listas de escolha (Picker) iguais em todo o Lone. No Windows, o controle nativo (ComboBox do WinUI) às vezes só abria
/// clicando na setinha: um clique no meio do campo não abria a lista (achado no roteiro de telas da 2b-1b, lista
/// "Território" das operações territoriais). Aqui, qualquer toque no campo abre a lista — o evento é ouvido mesmo quando
/// o próprio controle já o tratou, e abrir o que já está aberto não muda nada.
/// </summary>
public static class AjusteListaEscolha
{
#if WINDOWS
    /// <summary>Cada controle nativo recebe o ouvinte uma vez só (o mapeamento pode rodar de novo no mesmo controle).</summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Microsoft.UI.Xaml.Controls.ComboBox, object> Ajustados = new();
#endif

    public static void Aplicar()
    {
        Microsoft.Maui.Handlers.PickerHandler.Mapper.AppendToMapping("LoneListaAbreNoCampoTodo", (handler, picker) =>
        {
#if WINDOWS
            var lista = handler.PlatformView;
            if (Ajustados.TryGetValue(lista, out _)) return;
            Ajustados.Add(lista, new object());
            // Estado no momento do toque: se a lista já estava aberta, o toque é para fechá-la (não reabre).
            var estavaAberta = false;
            lista.AddHandler(Microsoft.UI.Xaml.UIElement.PointerPressedEvent,
                new Microsoft.UI.Xaml.Input.PointerEventHandler((_, _) => estavaAberta = lista.IsDropDownOpen),
                handledEventsToo: true);
            lista.AddHandler(Microsoft.UI.Xaml.UIElement.TappedEvent,
                new Microsoft.UI.Xaml.Input.TappedEventHandler((_, _) =>
                {
                    if (lista.IsEnabled && !estavaAberta && !lista.IsDropDownOpen) lista.IsDropDownOpen = true;
                }),
                handledEventsToo: true);
#endif
        });
    }
}
