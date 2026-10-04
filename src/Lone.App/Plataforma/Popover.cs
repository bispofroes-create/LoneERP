namespace Lone.App.Plataforma;

/// <summary>
/// Janela pequena presa a um controle (popover), usada pelo calendário dos campos de data (03/10/2026). No Windows é o
/// Flyout do WinUI com o conteúdo MAUI dentro: fecha ao clicar fora ou com Esc, como nos ERPs de referência.
/// Nas outras plataformas ainda não há popover (o ícone do calendário não aparece; digitar continua valendo).
/// </summary>
public static class Popover
{
    public static bool Disponivel =>
#if WINDOWS
        true;
#else
        false;
#endif

    /// <summary>Mostra <paramref name="conteudo"/> abaixo de <paramref name="ancora"/>; devolve a ação que fecha.</summary>
    public static Action? Mostrar(View ancora, View conteudo)
    {
#if WINDOWS
        if (ancora.Handler?.MauiContext is not { } contexto || ancora.Handler.PlatformView is not Microsoft.UI.Xaml.FrameworkElement alvo)
            return null;
        var flyout = new Microsoft.UI.Xaml.Controls.Flyout
        {
            Content = Microsoft.Maui.Platform.ElementExtensions.ToPlatform(conteudo, contexto),
            Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.BottomEdgeAlignedRight
        };
        flyout.ShowAt(alvo);
        return flyout.Hide;
#else
        return null;
#endif
    }
}
