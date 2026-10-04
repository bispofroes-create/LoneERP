namespace Lone.App.Plataforma;

/// <summary>
/// Voltar pelo teclado e pelo mouse, como em qualquer programa do Windows: Alt+←, a tecla "Voltar" de teclados multimídia
/// e o botão lateral "voltar" do mouse. Tudo chama o mesmo Voltar do motor de navegação (o botão ← da barra de título e o
/// voltar do celular também). Alt+← não conflita com edição de texto (as caixas usam Ctrl+← para andar por palavras).
/// Ctrl+N = Novo na tela de cadastro aberta (padrão do botão Novo, 03/10/2026).
/// Ligado à janela enquanto o sistema está aberto e solto ao sair (<see cref="Dispose"/>).
/// </summary>
public sealed class AtalhosNavegacao : IDisposable
{
#if WINDOWS
    private Microsoft.UI.Xaml.UIElement? _raiz;
    private Microsoft.UI.Xaml.Input.KeyEventHandler? _tecla;
    private Microsoft.UI.Xaml.Input.PointerEventHandler? _ponteiro;
#endif

    private AtalhosNavegacao() { }

    public static AtalhosNavegacao Ligar(Window janela, Func<Task> voltar, Action novo)
    {
        var atalhos = new AtalhosNavegacao();
#if WINDOWS
        if (janela.Handler?.PlatformView is not Microsoft.UI.Xaml.Window nativa || nativa.Content is not { } raiz) return atalhos;
        atalhos._raiz = raiz;
        atalhos._tecla = (_, e) =>
        {
            var ctrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control)
                .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
            if (ctrl && e.Key == Windows.System.VirtualKey.N && !e.KeyStatus.IsMenuKeyDown)
            {
                e.Handled = true;
                novo();
                return;
            }
            var altMaisEsquerda = e.Key == Windows.System.VirtualKey.Left && e.KeyStatus.IsMenuKeyDown;
            if (!altMaisEsquerda && e.Key != Windows.System.VirtualKey.GoBack) return;
            e.Handled = true;
            _ = voltar();
        };
        atalhos._ponteiro = (_, e) =>
        {
            if (!e.GetCurrentPoint(raiz).Properties.IsXButton1Pressed) return;
            e.Handled = true;
            _ = voltar();
        };
        // Mesmo quando um campo já tratou a tecla/clique (handledEventsToo): o atalho vale em qualquer ponto da tela.
        raiz.AddHandler(Microsoft.UI.Xaml.UIElement.KeyDownEvent, atalhos._tecla, true);
        raiz.AddHandler(Microsoft.UI.Xaml.UIElement.PointerPressedEvent, atalhos._ponteiro, true);
#endif
        return atalhos;
    }

    public void Dispose()
    {
#if WINDOWS
        if (_raiz is null) return;
        if (_tecla is not null) _raiz.RemoveHandler(Microsoft.UI.Xaml.UIElement.KeyDownEvent, _tecla);
        if (_ponteiro is not null) _raiz.RemoveHandler(Microsoft.UI.Xaml.UIElement.PointerPressedEvent, _ponteiro);
        _raiz = null;
#endif
    }
}
