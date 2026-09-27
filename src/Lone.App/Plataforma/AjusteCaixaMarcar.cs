namespace Lone.App.Plataforma;

/// <summary>
/// Caixas de marcar iguais em todo o Lone. No Windows, o controle nativo (WinUI) reserva 120 px de largura mínima para
/// um texto que no Lone fica fora dele (o texto é um Label ao lado, em <see cref="Controles.CaixaMarcar"/>) — sobrava um
/// vão grande entre a caixa e o texto. Aqui a largura mínima e o espaço interno são zerados para todas as caixas; o
/// espaço entre caixa e texto passa a ser só <see cref="Controles.CaixaMarcar.EspacoTexto"/>.
/// </summary>
public static class AjusteCaixaMarcar
{
    public static void Aplicar()
    {
        Microsoft.Maui.Handlers.CheckBoxHandler.Mapper.AppendToMapping("LoneCaixaMarcarCompacta", (handler, _) =>
        {
#if WINDOWS
            handler.PlatformView.MinWidth = 0;
            handler.PlatformView.Padding = new Microsoft.UI.Xaml.Thickness(0);
#endif
        });
    }
}
