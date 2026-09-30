namespace Lone.App.Plataforma;

/// <summary>
/// Menu lateral no Windows: o botão que abre o menu recolhido é o ☰ da barra de título do Lone (AppShell). O painel
/// nativo do WinUI (NavigationView) mostra o seu próprio botão no canto esquerdo quando o menu recolhe, sobreposto à barra
/// de título (quadrado escuro com o menu fechado, branco com ele aberto) — dois botões para a mesma coisa. Aqui o nativo
/// fica escondido, sempre (o próprio controle tenta mostrá-lo de novo a cada troca de modo).
/// </summary>
public static class AjusteMenuLateral
{
#if WINDOWS
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Microsoft.UI.Xaml.Controls.NavigationView, object> Ajustados = new();
#endif

    public static void Aplicar()
    {
#if WINDOWS
        // Só o Windows tem o ShellHandler (no Android o Shell usa outro renderizador, com o ☰ próprio da barra).
        Microsoft.Maui.Controls.Handlers.ShellHandler.Mapper.AppendToMapping("LoneMenuSemBotaoNativo", (handler, shell) =>
        {
            if (handler.PlatformView is not Microsoft.UI.Xaml.Controls.NavigationView painel) return;
            painel.IsPaneToggleButtonVisible = false;
            if (Ajustados.TryGetValue(painel, out _)) return;
            Ajustados.Add(painel, new object());
            painel.RegisterPropertyChangedCallback(Microsoft.UI.Xaml.Controls.NavigationView.IsPaneToggleButtonVisibleProperty,
                (_, _) =>
                {
                    if (painel.IsPaneToggleButtonVisible) painel.IsPaneToggleButtonVisible = false;
                });
        });
#endif
    }
}
