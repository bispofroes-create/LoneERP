#if DEBUG
using System.ComponentModel;
using System.Globalization;
using Lone.Cliente.ViewModels.Pessoas;

namespace Lone.App.LabGradeB2Temp;

/// <summary>
/// B2Temp — diagnóstico 3.0 (P2-B2, Etapa 3): por que o botão "Colunas" some ao trocar a escala com Pessoas aberta.
/// INSTRUMENTAÇÃO TEMPORÁRIA, só em Debug, só observa (não muda estado nem layout). Remover junto com o laboratório.
/// Grava em _entrega/p2-b2/diag-30-botao-colunas.txt (procura a pasta _entrega subindo a partir do executável).
/// </summary>
internal static class Diag30
{
    private static readonly object Trava = new();
    private static string? _arquivo;
    private static int _seq;

    private static string F(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);

    private static string Arquivo()
    {
        if (_arquivo is not null) return _arquivo;
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "_entrega"))) dir = dir.Parent;
        var pasta = dir is null ? Path.GetTempPath() : Path.Combine(dir.FullName, "_entrega", "p2-b2");
        Directory.CreateDirectory(pasta);
        return _arquivo = Path.Combine(pasta, "diag-30-botao-colunas.txt");
    }

    public static void Log(string texto)
    {
        try
        {
            lock (Trava)
                File.AppendAllText(Arquivo(), $"{++_seq};{DateTime.Now:HH:mm:ss.fff};{texto}{Environment.NewLine}");
        }
        catch { /* diagnóstico nunca derruba a tela */ }
    }

    /// <summary>Quem mudou o valor: as primeiras chamadas do código do Lone na pilha.</summary>
    private static string Origem()
    {
        var quadros = Environment.StackTrace.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.StartsWith("at Lone.", StringComparison.Ordinal) && !l.Contains("Diag30", StringComparison.Ordinal))
            .Select(l => l.Split(" in ")[0].Replace("at ", string.Empty))
            .Take(5);
        return string.Join(" <- ", quadros);
    }

    private static string Estado(PessoasViewModel vm, Button botao) =>
        $"mostrarColunas={vm.Grade.MostrarColunas};previaVisivel={vm.Previa.Visivel};previaCabe={vm.Previa.Cabe};" +
        $"filtrosAbertos={vm.Filtros.Aberto};densidade={F(DeviceDisplay.MainDisplayInfo.Density)};" +
        $"botao.IsVisible={botao.IsVisible};botao.W={F(botao.Width)};botao.X={F(botao.X)};barra.W={F((botao.Parent as VisualElement)?.Width ?? -1)}";

    private static int _excecoes;
    private static bool _ligouExcecoes;

    /// <summary>
    /// Exceções de layout/COM no momento em que chegam ao código gerenciado (antes de derrubar o app): tipo, mensagem e
    /// a pilha completa da thread — mostra qual chamada do MAUI/da grade estava em curso. No máximo 30 por sessão.
    /// </summary>
    private static void LigarExcecoes()
    {
        if (_ligouExcecoes) return;
        _ligouExcecoes = true;
        AppDomain.CurrentDomain.FirstChanceException += (_, e) =>
        {
            var ex = e.Exception;
            var nome = ex.GetType().Name;
            if (!(nome.Contains("LayoutCycle") || ex is System.Runtime.InteropServices.COMException)) return;
            if (Interlocked.Increment(ref _excecoes) > 30) return;
            var pilha = string.Join(" | ", Environment.StackTrace.Split('\n')
                .Select(l => l.Trim()).Where(l => l.StartsWith("at ", StringComparison.Ordinal) && !l.Contains("Diag30"))
                .Select(l => l.Split(" in ")[0].Replace("at ", string.Empty)).Take(30));
            Log($"EXCECAO;{nome};0x{ex.HResult:X8};{ex.Message.Replace('\n', ' ').Replace(';', ',')};pilha={pilha}");
        };
    }

    private static int _tamanhos;

    /// <summary>
    /// Ciclo de layout = algum elemento muda de tamanho sem parar até o WinUI desistir. Registra cada mudança de tamanho
    /// dos elementos da tela (fora das linhas da lista): o fim do registro, antes da queda, mostra qual elemento oscilava.
    /// No máximo 20.000 linhas por sessão.
    /// </summary>
    private static void VigiarTamanhos(Element raiz)
    {
        var vistos = new HashSet<Element>();
        void Percorrer(Element e, string caminho, int nivel)
        {
            if (nivel > 40 || !vistos.Add(e)) return;
            var nome = string.IsNullOrEmpty(e.StyleId) ? e.GetType().Name : $"{e.GetType().Name}({e.StyleId})";
            var aqui = $"{caminho}/{nome}";
            if (e is VisualElement v)
                v.SizeChanged += (_, _) =>
                {
                    if (Interlocked.Increment(ref _tamanhos) <= 20000)
                        Log($"tam;{F(v.Width)}x{F(v.Height)};{aqui}");
                };
            foreach (var filho in ((IVisualTreeElement)e).GetVisualChildren().OfType<Element>())
                Percorrer(filho, nivel < 3 ? aqui : $".../{nome}", nivel + 1);
        }
        Percorrer(raiz, string.Empty, 0);
        // Elementos criados depois (linhas novas da lista, etiquetas de filtro, campos do cabeçalho) também entram.
        raiz.DescendantAdded += (_, a) =>
        {
            if (a.Element is VisualElement novo && vistos.Add(novo))
            {
                var nome = string.IsNullOrEmpty(novo.StyleId) ? novo.GetType().Name : $"{novo.GetType().Name}({novo.StyleId})";
                var pai = novo.Parent?.GetType().Name ?? "?";
                novo.SizeChanged += (_, _) =>
                {
                    if (Interlocked.Increment(ref _tamanhos) <= 20000)
                        Log($"tam;{F(novo.Width)}x{F(novo.Height)};+/{pai}/{nome}");
                };
            }
        };
        Log($"tam;vigiando={vistos.Count}");
    }

    public static void Ligar(ContentPage pagina, PessoasViewModel vm, Button botao, Layout lista, Func<double> painel)
    {
        LigarExcecoes();
        Log($"ligado;{Estado(vm, botao)}");
        // Inscrito depois do SizeChanged da própria página: registra o estado já recalculado pelo AjustarLarguras.
        pagina.SizeChanged += (_, _) =>
        {
            var p = painel();
            var larguraLista = pagina.Width - lista.Padding.HorizontalThickness - p;
            Log($"pagina.SizeChanged;W={F(pagina.Width)};H={F(pagina.Height)};padding={F(lista.Padding.HorizontalThickness)};" +
                $"painel={F(p)};larguraListaPassada={F(larguraLista)};{Estado(vm, botao)}");
        };
        vm.Grade.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(GradePessoas.MostrarColunas)) return;
            Log($"MostrarColunas.mudou;valor={vm.Grade.MostrarColunas};paginaW={F(pagina.Width)};origem={Origem()}");
        };
        vm.Previa.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(PreviaPessoa.Visivel) or nameof(PreviaPessoa.Cabe))
                Log($"Previa.{e.PropertyName}.mudou;visivel={vm.Previa.Visivel};cabe={vm.Previa.Cabe};paginaW={F(pagina.Width)}");
        };
        botao.SizeChanged += (_, _) => Log($"botao.SizeChanged;{Estado(vm, botao)}");
        botao.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(VisualElement.IsVisible)) Log($"botao.IsVisible.mudou;{Estado(vm, botao)}");
        };
        DeviceDisplay.MainDisplayInfoChanged += (_, e) =>
            Log($"escala.MainDisplayInfoChanged;densidade={F(e.DisplayInfo.Density)};paginaW={F(pagina.Width)};{Estado(vm, botao)}");
        pagina.Loaded += (_, _) =>
        {
            // Depois da primeira lista: a tela já tem cabeçalho, filtros e etiquetas montados.
            pagina.Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(4), () => VigiarTamanhos(pagina));
#if WINDOWS
            if (pagina.Window?.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativa && nativa.Content?.XamlRoot is { } raiz)
            {
                Log($"xamlroot.inicial;escala={F(raiz.RasterizationScale)};tamanho={F(raiz.Size.Width)}x{F(raiz.Size.Height)}");
                raiz.Changed += (r, _) =>
                    Log($"xamlroot.mudou;escala={F(r.RasterizationScale)};tamanho={F(r.Size.Width)}x{F(r.Size.Height)};paginaW={F(pagina.Width)};{Estado(vm, botao)}");
            }
            else Log("xamlroot.indisponivel");
#endif
        };
    }
}
#endif
