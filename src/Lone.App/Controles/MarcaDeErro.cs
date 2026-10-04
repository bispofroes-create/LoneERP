namespace Lone.App.Controles;

/// <summary>
/// A marca de erro de um campo (Lone Contextual, Fase 1), igual em todos os controles: borda na cor de erro no controle de
/// entrada e "⚠ mensagem" logo abaixo (ícone + texto: não depende só da cor). Diferente de aviso (amarelo) e informação
/// (azul). Quem decide se há erro é o resumo de erros; aqui só se desenha.
/// </summary>
internal sealed class MarcaDeErro
{
    private readonly View[] _entradas;
    private bool _comErro;

    public MarcaDeErro(params View[] entradas)
    {
        _entradas = entradas;
        Rotulo = new Label { IsVisible = false, FontSize = 12, LineBreakMode = LineBreakMode.WordWrap, Margin = new Thickness(0, 2, 0, 0) };
        if (Application.Current?.Resources.TryGetValue("Erro", out var cor) == true && cor is Color erro) Rotulo.TextColor = erro;
        foreach (var entrada in entradas)
            entrada.HandlerChanged += (_, _) => Pintar(entrada);
    }

    /// <summary>O "⚠ mensagem" (o controle põe embaixo da entrada).</summary>
    public Label Rotulo { get; }

    public void Mostrar(string? mensagem)
    {
        _comErro = mensagem is not null;
        Rotulo.Text = mensagem is null ? string.Empty : "⚠ " + mensagem;
        Rotulo.IsVisible = _comErro;
        foreach (var entrada in _entradas) Pintar(entrada);
    }

    private void Pintar(View entrada)
    {
#if WINDOWS
        if (entrada.Handler?.PlatformView is not Microsoft.UI.Xaml.Controls.Control nativo) return;
        string[] chaves =
        [
            "TextControlBorderBrush", "TextControlBorderBrushPointerOver", "TextControlBorderBrushFocused",
            "ComboBoxBorderBrush", "ComboBoxBorderBrushPointerOver", "ComboBoxBorderBrushFocused", "ComboBoxBorderBrushPressed"
        ];
        if (_comErro)
        {
            var cor = Rotulo.TextColor ?? Colors.DarkRed;
            var pincel = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(
                255, (byte)(cor.Red * 255), (byte)(cor.Green * 255), (byte)(cor.Blue * 255)));
            // Recursos locais valem para os estados visuais do modelo (normal, ponteiro em cima, foco).
            foreach (var chave in chaves) nativo.Resources[chave] = pincel;
            nativo.BorderBrush = pincel;
            nativo.BorderThickness = new Microsoft.UI.Xaml.Thickness(2);
        }
        else
        {
            foreach (var chave in chaves) nativo.Resources.Remove(chave);
            nativo.ClearValue(Microsoft.UI.Xaml.Controls.Control.BorderBrushProperty);
            nativo.ClearValue(Microsoft.UI.Xaml.Controls.Control.BorderThicknessProperty);
        }
#endif
    }
}
