#if DEBUG
using System.Globalization;
using System.Text;

namespace Lone.App.LabGradeB2Temp;

/// <summary>
/// B2Temp — diagnóstico dos campos cortados nas fichas (03/10/2026). INSTRUMENTAÇÃO TEMPORÁRIA, só em Debug, só observa
/// (não muda estado nem layout). Remover junto com o laboratório.
/// A cada 1,5 s, com a ficha aberta, mede todo FlexLayout visível da página: cada filho (tipo, visível, base, altura
/// pedida × recebida) e, nos campos de texto/escolha, o controle do MAUI e o nativo do Windows. Só grava quando mudou.
/// Arquivo: _entrega/p2-b2/diag-campos.txt.
/// </summary>
internal static class DiagCampos
{
    private static string? _arquivo;
    private static int _seq;

    private static string F(double v) => double.IsInfinity(v) ? "inf" : v.ToString("0.#", CultureInfo.InvariantCulture);

    private static void Log(string texto)
    {
        try
        {
            if (_arquivo is null)
            {
                var dir = new DirectoryInfo(AppContext.BaseDirectory);
                while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "_entrega"))) dir = dir.Parent;
                var pasta = dir is null ? Path.GetTempPath() : Path.Combine(dir.FullName, "_entrega", "p2-b2");
                Directory.CreateDirectory(pasta);
                _arquivo = Path.Combine(pasta, "diag-campos.txt");
            }
            File.AppendAllText(_arquivo, $"--- {++_seq} {DateTime.Now:HH:mm:ss.fff}{Environment.NewLine}{texto}{Environment.NewLine}");
        }
        catch { /* diagnóstico nunca derruba a tela */ }
    }

    public static void Ligar(ContentPage pagina, Func<bool> fichaAberta)
    {
        var ultimo = string.Empty;
        bool? aberta = null;
        Log("ligado");
        pagina.Dispatcher.StartTimer(TimeSpan.FromMilliseconds(1500), () =>
        {
            try
            {
                var agora = fichaAberta();
                if (agora != aberta) { aberta = agora; Log($"fichaAberta={agora}"); }
                if (!agora) return true;
                var texto = Medir(pagina);
                if (texto != ultimo) { ultimo = texto; Log(texto); }
            }
            catch (Exception ex) { Log("erro;" + ex.Message); }
            return true;
        });
    }

    private static string Medir(ContentPage pagina)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"pagina W={F(pagina.Width)} H={F(pagina.Height)}");
        var n = 0;
        foreach (var flex in Descendentes(pagina).OfType<Layout>().Where(l => l is FlexLayout or Controles.BlocoCampos))
        {
            if (!VisivelDeVerdade(flex) || flex.Width <= 0) continue;
            n++;
            sb.AppendLine($"{(flex is FlexLayout ? "FLEX" : "BLOCO")}#{n} W={F(flex.Width)} H={F(flex.Height)} filhos={flex.Children.Count}");
            var i = 0;
            foreach (var filho in flex.Children.OfType<View>())
            {
                i++;
                var nome = filho.GetType().Name + (filho is Controles.Campo c ? $"({c.Rotulo})" : filho is Label l ? $"({Curto(l.Text)})" : "");
                sb.AppendLine($"  [{i}] {nome} vis={filho.IsVisible} base={FlexLayout.GetBasis(filho)} " +
                              $"X={F(filho.X)} Y={F(filho.Y)} W={F(filho.Width)} H={F(filho.Height)} desired={F(filho.DesiredSize.Height)} " +
                              $"margem={filho.Margin.Top},{filho.Margin.Bottom}");
                foreach (var campo in Descendentes(filho).OfType<View>().Where(v => v is Entry or Picker))
                    sb.AppendLine("      " + Controle(campo));
            }
        }
        return sb.ToString();
    }

    private static string Controle(View v)
    {
        var s = $"{v.GetType().Name} Y={F(v.Y)} H={F(v.Height)} desired={F(v.DesiredSize.Height)} minReq={F(v.MinimumHeightRequest)} pai.H={F((v.Parent as VisualElement)?.Height ?? -1)}";
#if WINDOWS
        if (v.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.Control nativo)
            s += $" | nativo={nativo.GetType().Name} actualH={F(nativo.ActualHeight)} desiredH={F(nativo.DesiredSize.Height)} minH={F(nativo.MinHeight)} " +
                 $"altura={F(nativo.Height)} padding={nativo.Padding.Top},{nativo.Padding.Bottom} borda={nativo.BorderThickness.Top},{nativo.BorderThickness.Bottom} fonte={F(nativo.FontSize)}";
#endif
        return s;
    }

    private static string Curto(string? t) => t is null ? "" : t.Length <= 25 ? t : t[..25] + "…";

    private static bool VisivelDeVerdade(Element e)
    {
        for (Element? x = e; x is not null; x = x.Parent)
            if (x is VisualElement { IsVisible: false }) return false;
        return true;
    }

    private static IEnumerable<Element> Descendentes(Element raiz)
    {
        foreach (var filho in ((IVisualTreeElement)raiz).GetVisualChildren().OfType<Element>())
        {
            yield return filho;
            foreach (var neto in Descendentes(filho)) yield return neto;
        }
    }
}
#endif
