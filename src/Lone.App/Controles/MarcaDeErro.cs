using Lone.Cliente.ViewModels.Comum;

namespace Lone.App.Controles;

/// <summary>
/// As marcas de um campo, iguais em todos os controles:
/// <list type="bullet">
/// <item>Erro (Lone Contextual, Fase 1): borda na cor de erro no controle de entrada e "⚠ mensagem" logo abaixo (ícone + texto:
/// não depende só da cor). Quem decide se há erro é o resumo de erros.</item>
/// <item>Destaque de alteração: fundo azul-claro (cor de informação: não é erro nem pendência) e "● Veio da Receita às 19:40 ·
/// antes: (vazio)" embaixo. Quem decide é o "Destacar alterações" da ficha.</item>
/// </list>
/// Aqui só se desenha. O controle põe <see cref="Mensagens"/> logo abaixo da entrada.
/// </summary>
internal sealed class MarcaDeErro
{
    private readonly View[] _entradas;
    private bool _comErro;
    private bool _destacado;

    public MarcaDeErro(params View[] entradas)
    {
        _entradas = entradas;
        Rotulo = new Label { IsVisible = false, FontSize = 12, LineBreakMode = LineBreakMode.WordWrap, Margin = new Thickness(0, 2, 0, 0) };
        if (Recurso("Erro") is { } erro) Rotulo.TextColor = erro;
        Destaque = new Label { IsVisible = false, FontSize = 12, LineBreakMode = LineBreakMode.WordWrap, Margin = new Thickness(0, 2, 0, 0) };
        Destaque.SetAppThemeColor(Label.TextColorProperty, Recurso("Informacao") ?? Colors.SteelBlue, Recurso("PrimariaEscuro") ?? Colors.LightSkyBlue);
        Mensagens = new VerticalStackLayout { Spacing = 0, Children = { Rotulo, Destaque } };
        foreach (var entrada in entradas)
            entrada.HandlerChanged += (_, _) => Pintar(entrada);
    }

    /// <summary>O "⚠ mensagem".</summary>
    public Label Rotulo { get; }

    /// <summary>O "● Veio da Receita... · antes: ...".</summary>
    public Label Destaque { get; }

    /// <summary>As duas linhas (erro e destaque), para o controle pôr embaixo da entrada.</summary>
    public View Mensagens { get; }

    public void Mostrar(string? mensagem)
    {
        _comErro = mensagem is not null;
        Rotulo.Text = mensagem is null ? string.Empty : "⚠ " + mensagem;
        Rotulo.IsVisible = _comErro;
        foreach (var entrada in _entradas) Pintar(entrada);
    }

    public void MostrarDestaque(DestaqueCampo? destaque)
    {
        _destacado = destaque is not null;
        Destaque.Text = destaque?.Texto ?? string.Empty;
        Destaque.IsVisible = _destacado;
        foreach (var entrada in _entradas)
        {
            if (destaque is null) entrada.ClearValue(ToolTipProperties.TextProperty);
            else ToolTipProperties.SetText(entrada, destaque.Texto);
            Pintar(entrada);
        }
    }

    private static Color? Recurso(string chave) =>
        Application.Current?.Resources.TryGetValue(chave, out var valor) == true && valor is Color cor ? cor : null;

    private void Pintar(View entrada)
    {
#if WINDOWS
        if (entrada.Handler?.PlatformView is not Microsoft.UI.Xaml.Controls.Control nativo) return;
        string[] bordas =
        [
            "TextControlBorderBrush", "TextControlBorderBrushPointerOver", "TextControlBorderBrushFocused",
            "ComboBoxBorderBrush", "ComboBoxBorderBrushPointerOver", "ComboBoxBorderBrushFocused", "ComboBoxBorderBrushPressed"
        ];
        string[] fundos =
        [
            "TextControlBackground", "TextControlBackgroundPointerOver", "TextControlBackgroundFocused",
            "ComboBoxBackground", "ComboBoxBackgroundPointerOver", "ComboBoxBackgroundFocused", "ComboBoxBackgroundPressed"
        ];
        if (_comErro)
        {
            var pincel = Pincel(Rotulo.TextColor ?? Colors.DarkRed);
            // Recursos locais valem para os estados visuais do modelo (normal, ponteiro em cima, foco).
            foreach (var chave in bordas) nativo.Resources[chave] = pincel;
            nativo.BorderBrush = pincel;
            nativo.BorderThickness = new Microsoft.UI.Xaml.Thickness(2);
        }
        else
        {
            foreach (var chave in bordas) nativo.Resources.Remove(chave);
            nativo.ClearValue(Microsoft.UI.Xaml.Controls.Control.BorderBrushProperty);
            nativo.ClearValue(Microsoft.UI.Xaml.Controls.Control.BorderThicknessProperty);
        }
        if (_destacado)
        {
            var escuro = Application.Current?.RequestedTheme == AppTheme.Dark;
            var pincel = Pincel(Recurso(escuro ? "InformacaoFundoEscuro" : "InformacaoFundo") ?? Colors.AliceBlue);
            foreach (var chave in fundos) nativo.Resources[chave] = pincel;
            nativo.Background = pincel;
        }
        else
        {
            foreach (var chave in fundos) nativo.Resources.Remove(chave);
            nativo.ClearValue(Microsoft.UI.Xaml.Controls.Control.BackgroundProperty);
        }
#endif
    }

#if WINDOWS
    private static Microsoft.UI.Xaml.Media.SolidColorBrush Pincel(Color cor) =>
        new(Microsoft.UI.ColorHelper.FromArgb(255, (byte)(cor.Red * 255), (byte)(cor.Green * 255), (byte)(cor.Blue * 255)));
#endif
}
