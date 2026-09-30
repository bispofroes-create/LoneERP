namespace Lone.Cliente.Navegacao;

/// <summary>
/// Quando o menu lateral fica fixo ao lado e quando recolhe (botão ☰ na barra de título). Regra pura, testável; quem
/// aplica é o AppShell (Windows). Mesmo limite do painel de navegação do Windows/Fluent (NavigationView: 1008). O menu
/// do Lone não tem ícones, então não há o modo intermediário "só ícones".
/// </summary>
public static class MenuLateral
{
    /// <summary>A partir desta largura da janela o menu fica fixo ao lado.</summary>
    public const double LarguraMenuFixo = 1008;

    /// <summary>Janela mais estreita que <see cref="LarguraMenuFixo"/>: menu recolhido (antes da 1ª medida, não recolhe).</summary>
    public static bool Recolhido(double larguraJanela) => larguraJanela > 0 && larguraJanela < LarguraMenuFixo;
}
