using Microsoft.Maui.Layouts;

namespace Lone.App.Controles;

/// <summary>
/// Campos das fichas em FlexLayout: o normal é dois por linha (base 50%). Com largura de sobra, os campos de meia linha
/// passam a ocupar um terço (três por linha), em vez de ficarem esticados. Campos de linha inteira (100%) e itens de
/// base automática (selos, botões) não mudam; no celular a base já é 100%, então nada muda. Ligado a todos os
/// FlexLayout pelo estilo implícito (Estilos.xaml). Só aparência.
/// </summary>
public static class ColunasAdaptaveis
{
    /// <summary>A partir desta largura do FlexLayout (em pontos), três colunas.</summary>
    public const double LarguraTresColunas = 1000;

    public static readonly BindableProperty AtivoProperty = BindableProperty.CreateAttached(
        "Ativo", typeof(bool), typeof(ColunasAdaptaveis), false, propertyChanged: AoMudarAtivo);

    public static bool GetAtivo(BindableObject objeto) => (bool)objeto.GetValue(AtivoProperty);
    public static void SetAtivo(BindableObject objeto, bool valor) => objeto.SetValue(AtivoProperty, valor);

    /// <summary>A base que o campo tinha antes do ajuste (vinda do estilo ou do XAML), guardada na primeira vez.</summary>
    private static readonly BindableProperty BaseOriginalProperty = BindableProperty.CreateAttached(
        "BaseOriginal", typeof(object), typeof(ColunasAdaptaveis), null);

    /// <summary>Colunas já aplicadas neste FlexLayout (0 = ainda não): só mexe nos filhos quando o número muda.</summary>
    private static readonly BindableProperty ColunasProperty = BindableProperty.CreateAttached(
        "Colunas", typeof(int), typeof(ColunasAdaptaveis), 0);

    private static readonly FlexBasis MeiaLinha = new(0.5f, isRelative: true);

    private static void AoMudarAtivo(BindableObject objeto, object antigo, object novo)
    {
        if (objeto is not FlexLayout layout) return;
        layout.SizeChanged -= AoMudarTamanho;
        if ((bool)novo) layout.SizeChanged += AoMudarTamanho;
    }

    private static void AoMudarTamanho(object? sender, EventArgs e)
    {
        if (sender is not FlexLayout layout || layout.Width <= 0) return;
        var colunas = layout.Width >= LarguraTresColunas ? 3 : 2;
        if ((int)layout.GetValue(ColunasProperty) == colunas) return;
        layout.SetValue(ColunasProperty, colunas);

        foreach (var filho in layout.Children.OfType<BindableObject>())
        {
            if (filho.GetValue(BaseOriginalProperty) is not FlexBasis original)
            {
                original = FlexLayout.GetBasis(filho);
                filho.SetValue(BaseOriginalProperty, original);
            }
            // Só os campos de meia linha ("50%"): 100%, automático e outras bases ficam como estão.
            // (IsRelative não é público no FlexBasis; a igualdade do struct compara valor e tipo da base.)
            if (!original.Equals(MeiaLinha)) continue;
            FlexLayout.SetBasis(filho, new FlexBasis(colunas == 3 ? 1f / 3 : 0.5f, isRelative: true));
        }
    }
}
