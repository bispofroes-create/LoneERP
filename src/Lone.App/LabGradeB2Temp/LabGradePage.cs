// B2Temp — LABORATÓRIO TEMPORÁRIO DA P2-B2 (ETAPA 2). REMOVER AO FINAL DA ETAPA.
// Abre só com LONE_B2_LAB=1 (App.CreateWindow). Mede a GradeLista isolada, sem API, sem login, sem Pessoas.
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Lone.App.Controles.Grade;
using Lone.Cliente.Grade;
using Microsoft.Maui.Media;

namespace Lone.App.LabGradeB2Temp;

/// <summary>Linha sintética (parecida com a de Pessoas): nome, subtítulo e 8 células.</summary>
internal sealed class LinhaLab(int indice, string nome, string subtitulo, IReadOnlyList<CelulaGrade> celulas) : ILinhaGrade
{
    private bool _destacada, _selecionada;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int Indice { get; } = indice;
    public Guid Chave { get; } = Guid.NewGuid();
    public string Nome { get; } = nome;
    public string Subtitulo { get; } = subtitulo;
    public IReadOnlyList<CelulaGrade> Celulas { get; } = celulas;

    public bool Destacada
    {
        get => _destacada;
        set { if (_destacada == value) return; _destacada = value; PropertyChanged?.Invoke(this, new(nameof(Destacada))); }
    }

    public bool Selecionada
    {
        get => _selecionada;
        set { if (_selecionada == value) return; _selecionada = value; PropertyChanged?.Invoke(this, new(nameof(Selecionada))); }
    }
}

/// <summary>Parte fixa do laboratório (como a de Pessoas: avatar + nome + subtítulo), preenchida sem ligações.</summary>
internal sealed class ParteFixaLab : Grid
{
    private readonly Label _iniciais = new() { FontSize = 13, FontAttributes = FontAttributes.Bold, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
    private readonly Label _nome = new() { FontAttributes = FontAttributes.Bold, LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 1 };
    private readonly Label _sub = new() { FontSize = 13, LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 1 };

    public ParteFixaLab()
    {
        ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        ColumnSpacing = 8;
        Padding = new Thickness(12, 0, 4, 0);
        if (CoresGrade.Recurso("TextoTabela") is Style t) _nome.Style = t;
        if (CoresGrade.Recurso("TextoTabelaSecundario") is Style s) _sub.Style = s;
        var avatar = new Border { WidthRequest = 36, HeightRequest = 36, StrokeThickness = 0, VerticalOptions = LayoutOptions.Center, Content = _iniciais, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 18 } };
        avatar.SetAppThemeColor(BackgroundColorProperty, CoresGrade.Cor("Selecao"), CoresGrade.Cor("SelecaoEscuro"));
        this.Add(avatar, 0, 0);
        var textos = new VerticalStackLayout { VerticalOptions = LayoutOptions.Center, Spacing = 2 };
        textos.Add(_nome);
        textos.Add(_sub);
        this.Add(textos, 1, 0);
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        if (BindingContext is not LinhaLab l) return;
        _nome.Text = l.Nome;
        _sub.Text = l.Subtitulo;
        _iniciais.Text = l.Nome.Length > 0 ? l.Nome[..1] : "?";
        ToolTipProperties.SetText(_nome, l.Nome);
    }
}

public sealed class LabGradePage : ContentPage
{
    private static readonly string[] Cidades = ["Belo Horizonte", "Contagem", "Betim", "Uberlândia", "Juiz de Fora", "Montes Claros", "São Paulo", "Campinas"];
    private static readonly string[] Nomes = ["Mercearia Gregório", "Ana Paula Souza", "Distribuidora Horizonte", "Carlos Eduardo Lima", "Padaria Pão Nosso", "Fernanda Ribeiro", "Atacado Minas Sul", "João Pedro Alves"];

    private readonly ContentView _host = new();
    private readonly Label _status = new() { FontSize = 12 };
    private readonly string _log = Path.Combine(AppContext.BaseDirectory, "b2-log.txt");
    private readonly string _id = Environment.GetEnvironmentVariable("LONE_B2_TENTATIVA") ?? "manual";
    private readonly int _n = int.TryParse(Environment.GetEnvironmentVariable("LONE_B2_LINHAS"), out var n) ? n : 1000;
    private readonly string _janela = Environment.GetEnvironmentVariable("LONE_B2_JANELA") ?? "normal";
    private readonly string _ordem = Environment.GetEnvironmentVariable("LONE_B2_ORDEM") ?? "padrao,mfi";
    private readonly string _roteiro = Environment.GetEnvironmentVariable("LONE_B2_ROTEIRO") ?? "matriz";
    private readonly bool _manual = Environment.GetEnvironmentVariable("LONE_B2_MANUAL") == "1";
    private readonly Stopwatch _relogio = Stopwatch.StartNew();
    private bool _iniciou;

    public LabGradePage()
    {
        Title = "Laboratório GradeLista (P2-B2)";
        var raiz = new Grid { Padding = new Thickness(8) };
        raiz.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        raiz.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        raiz.Add(_status, 0, 0);
        raiz.Add(_host, 0, 1);
        Content = raiz;
        this.SetAppThemeColor(BackgroundColorProperty, CoresGrade.Cor("Superficie"), CoresGrade.Cor("SuperficieEscuro"));
        Loaded += async (_, _) =>
        {
            if (_iniciou) return;
            _iniciou = true;
            try
            {
                if (_manual) await ManualAsync();
                else if (_roteiro == "memoria") await MemoriaAsync();
                else if (_roteiro == "investigacao") await InvestigacaoAsync();
                else if (_roteiro == "controle") await ControleAsync();
                else if (_roteiro == "isolamento") await IsolamentoAsync();
                else if (_roteiro == "filtros") await FiltrosAsync();
                else await MatrizAsync();
            }
            catch (Exception ex)
            {
                Log($"ERRO;{ex.GetType().Name};{ex.Message.Replace(';', ',').Replace('\n', ' ')}");
            }
            if (!_manual)
            {
                Log("SOBREVIVEU;fim");
                await Task.Delay(300);
                Application.Current?.Quit();
            }
        };
    }

    // ---------------- Dados ----------------

    private static (ColunaGradeDef Fixa, IReadOnlyList<ColunaGradeDef> Colunas) Colunas(bool trocada = false)
    {
        // P2 aprovada para Pessoas + 3 colunas extras (peso 1, mínima do catálogo) para haver rolagem lateral real.
        var fixa = ColunaGradeDef.Fixa("nome", "NOME / RAZÃO SOCIAL", TipoCelula.Texto, 340, minima: 260);
        var doc = ColunaGradeDef.Fixa("documento", "CPF/CNPJ", TipoCelula.Texto, 180);
        var tipo = ColunaGradeDef.Fixa("tipo", "TIPO", TipoCelula.Selo, 80);
        var papeis = ColunaGradeDef.Proporcional("papeis", "PAPÉIS", TipoCelula.Pilulas, 2, 160);
        var cidade = ColunaGradeDef.Proporcional("cidade", "CIDADE", TipoCelula.Texto, 2, 160);
        var situacao = ColunaGradeDef.Fixa("situacao", "SITUAÇÃO", TipoCelula.Selo, 120);
        var email = ColunaGradeDef.Proporcional("email", "E-MAIL", TipoCelula.Texto, 1, 200);
        var telefone = ColunaGradeDef.Proporcional("telefone", "TELEFONE", TipoCelula.Texto, 1, 160);
        var cadastro = ColunaGradeDef.Proporcional("cadastro", "CADASTRADO EM", TipoCelula.Texto, 1, 140);
        ColunaGradeDef[] colunas = trocada
            ? new[] { doc, tipo, cidade, papeis, situacao, telefone, email, cadastro }
            : new[] { doc, tipo, papeis, cidade, situacao, email, telefone, cadastro };
        return (fixa, colunas);
    }

    private static List<ILinhaGrade> Gerar(int n, IReadOnlyList<ColunaGradeDef> colunas, int semente)
    {
        var r = new Random(semente);
        var linhas = new List<ILinhaGrade>(n);
        for (var i = 0; i < n; i++)
        {
            var pj = r.Next(3) == 0;
            var nome = $"{Nomes[r.Next(Nomes.Length)]} {i + 1}";
            var celulas = new CelulaGrade[colunas.Count];
            for (var j = 0; j < colunas.Count; j++)
            {
                var c = colunas[j];
                celulas[j] = c.Chave switch
                {
                    "documento" => CelulaGrade.DeTexto(c, pj ? $"{r.Next(10, 99)}.{r.Next(100, 999)}.{r.Next(100, 999)}/0001-{r.Next(10, 99)}" : $"{r.Next(100, 999)}.{r.Next(100, 999)}.{r.Next(100, 999)}-{r.Next(10, 99)}"),
                    "tipo" => CelulaGrade.DeSelo(c, pj ? "PJ" : "PF", "Neutro"),
                    "papeis" => CelulaGrade.DePilulas(c, r.Next(4) switch
                    {
                        0 => [],
                        1 => [new SeloGrade("Cliente", "Informacao")],
                        2 => [new SeloGrade("Cliente", "Informacao"), new SeloGrade("Fornecedor", "Aviso")],
                        _ => [new SeloGrade("Fornecedor", "Aviso"), new SeloGrade("Colaborador", "Neutro"), new SeloGrade("Empresa do grupo", "Grupo")]
                    }),
                    "cidade" => CelulaGrade.DeTexto(c, Cidades[r.Next(Cidades.Length)] + " / MG"),
                    "situacao" => r.Next(5) == 0 ? CelulaGrade.DeSelo(c, "Inativa", "Aviso") : CelulaGrade.DeSelo(c, "Ativa", "Sucesso"),
                    "email" => CelulaGrade.DeTexto(c, r.Next(5) == 0 ? null : $"contato{i}@exemplo.com.br"),
                    "telefone" => CelulaGrade.DeTexto(c, r.Next(4) == 0 ? null : $"(31) 9{r.Next(1000, 9999)}-{r.Next(1000, 9999)}"),
                    _ => CelulaGrade.DeTexto(c, new DateTime(2020, 1, 1).AddDays(r.Next(2000)).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)),
                };
            }
            linhas.Add(new LinhaLab(i, nome, $"Cód. {1000 + i} · {(pj ? "Pessoa jurídica" : "Pessoa física")}", celulas));
        }
        return linhas;
    }

    private GradeLista NovaGrade(ColunaGradeDef fixa)
    {
        var grade = new GradeLista
        {
            ColunaFixa = fixa,
            MoldeParteFixa = new DataTemplate(() => new ParteFixaLab()),
        };
        grade.ComandoTocar = new Command<ILinhaGrade>(l => { foreach (var x in grade.Conteudo.Linhas) ((LinhaLab)x).Selecionada = ReferenceEquals(x, l); _status.Text = $"Tocou: {((LinhaLab)l).Nome}"; });
        grade.ComandoTocarDuasVezes = new Command<ILinhaGrade>(l => _status.Text = $"Toque duplo: {((LinhaLab)l).Nome}");
        grade.ComandoPonteiroEntrou = new Command<ILinhaGrade>(l => ((LinhaLab)l).Destacada = true);
        grade.ComandoPonteiroSaiu = new Command<ILinhaGrade>(l => ((LinhaLab)l).Destacada = false);
        // Etapa 3 (3.2): a seta segue a mesma regra de Pessoas (crescente → decrescente → padrão).
        grade.ComandoOrdenar = new Command<ColunaGradeDef>(c =>
        {
            if (grade.ColunaOrdenada != c.Chave) { grade.ColunaOrdenada = c.Chave; grade.OrdemDecrescente = false; }
            else if (!grade.OrdemDecrescente) grade.OrdemDecrescente = true;
            else { grade.ColunaOrdenada = null; grade.OrdemDecrescente = false; }
            _status.Text = $"Ordenar: {c.Titulo} ({grade.ColunaOrdenada ?? "padrão"}{(grade.OrdemDecrescente ? " ▼" : "")})";
        });
        // Campo de filtro por coluna (a tela decide pela chave); a grade só posiciona, desloca e revela no foco.
        grade.CriarFiltro = c =>
        {
            var campo = new Entry { Placeholder = $"Filtrar {c.Titulo.ToLowerInvariant()}", FontSize = 13 };
            // Registro: a coluna do campo com foco e o deslocamento logo depois (a grade rola até ela).
            campo.Focused += async (_, _) =>
            {
                await Task.Delay(400);
                Log($"manual;filtro-foco;coluna={c.Chave};deslocamento={F(grade.DeslocamentoLateral)};max={F(grade.MaximoLateral)};{Alinhamento(grade)}");
            };
            return campo;
        };
        return grade;
    }

    // ---------------- Registro ----------------

    private void Log(string texto)
    {
        var linha = $"{_id};{DateTime.Now:HH:mm:ss.fff};{texto}";
        try { File.AppendAllText(_log, linha + Environment.NewLine); } catch { /* laboratório */ }
        Debug.WriteLine(linha);
    }

    private static string F(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);

    private static double Densidade => DeviceDisplay.MainDisplayInfo.Density;

    private static (long Gerenciada, long Ws) Memoria()
    {
        for (var i = 0; i < 3; i++) { GC.Collect(); GC.WaitForPendingFinalizers(); }
        GC.Collect();
        var ger = GC.GetTotalMemory(true) / (1024 * 1024);
        using var p = Process.GetCurrentProcess();
        p.Refresh();
        return (ger, p.WorkingSet64 / (1024 * 1024));
    }

    /// <summary>Espera a grade parar de criar/registrar linhas (o motor terminou de desenhar). Devolve o ms da última mudança.</summary>
    private long _primeira = -1;

    private async Task<long> EstavelAsync(GradeLista grade, Stopwatch desde, int limiteMs = 15000)
    {
        var ultimaMudanca = desde.ElapsedMilliseconds;
        var assinatura = (-1, -1, -1);
        var inicial = (grade.B2Vivas.Count, grade.B2Criadas, grade.B2Vinculos);
        _primeira = -1;
        while (desde.ElapsedMilliseconds < limiteMs)
        {
            await Task.Delay(16);
            var agora = (grade.B2Vivas.Count, grade.B2Criadas, grade.B2Vinculos);
            // "primeira" = primeira mudança visível do motor depois da ordem (linhas criadas, recicladas ou registradas).
            if (_primeira < 0 && agora != inicial) _primeira = desde.ElapsedMilliseconds;
            if (agora != assinatura) { assinatura = agora; ultimaMudanca = desde.ElapsedMilliseconds; continue; }
            if (grade.B2Vivas.Count > 0 && desde.ElapsedMilliseconds - ultimaMudanca >= 200) return ultimaMudanca;
        }
        return -1;
    }

    /// <summary>Altura real da linha no Windows (as medidas do MAUI ficam em -1 dentro do motor).</summary>
    private static double AlturaNativa(LinhaGradeView v)
    {
#if WINDOWS
        if (v.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement fe) return fe.ActualHeight;
#endif
        return -1;
    }

    /// <summary>Linhas vivas x linhas realmente dentro da área visível da lista (posição nativa).</summary>
    private static string Nativas(GradeLista grade)
    {
#if WINDOWS
        if (grade.B2Vista.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement lista)
        {
            int naTela = 0, comAltura = 0;
            double min = double.MaxValue, max = 0;
            foreach (var v in grade.B2Vivas)
            {
                if (v.Handler?.PlatformView is not Microsoft.UI.Xaml.FrameworkElement fe || fe.ActualHeight <= 0) continue;
                comAltura++;
                min = Math.Min(min, fe.ActualHeight);
                max = Math.Max(max, fe.ActualHeight);
                try
                {
                    var y = fe.TransformToVisual(lista).TransformPoint(new global::Windows.Foundation.Point(0, 0)).Y;
                    if (y + fe.ActualHeight > 0 && y < lista.ActualHeight) naTela++;
                }
                catch { /* elemento fora da árvore (reserva do motor) */ }
            }
            return $"vivas={grade.B2Vivas.Count};comAltura={comAltura};naTela={naTela};alturaNativa={(comAltura > 0 ? F(min) + "-" + F(max) : "-")};listaNativa={F(lista.ActualHeight)}";
        }
#endif
        return "nativo=indisponivel";
    }

    /// <summary>Linhas que cabem na área da lista (altura da grade menos o cabeçalho).</summary>
    private static int Visiveis(GradeLista grade) => (int)Math.Ceiling((grade.Height - GradeLista.AlturaCabecalho) / grade.AlturaLinha);

    private static bool Materializada(GradeLista grade, int indice) => grade.B2Vivas.Any(v => (v.Linha as LinhaLab)?.Indice == indice);

    // ---------------- Janela ----------------

    private async Task AjustarJanelaAsync(string janela, int larguraPx = 1440, int alturaPx = 753)
    {
#if WINDOWS
        if (Window?.Handler?.PlatformView is Microsoft.UI.Xaml.Window w)
        {
            var presenter = w.AppWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
            if (janela == "max") presenter?.Maximize();
            else
            {
                if (presenter?.State == Microsoft.UI.Windowing.OverlappedPresenterState.Maximized) presenter.Restore();
                w.AppWindow.Resize(new global::Windows.Graphics.SizeInt32(larguraPx, alturaPx));
            }
        }
#endif
        await Task.Delay(700);
    }

    // ---------------- Roteiros ----------------

    private async Task MatrizAsync()
    {
        await AjustarJanelaAsync(_janela);
        Log($"inicio;build=B2;densidade={F(Densidade)};linhas={_n};janela={_janela};ordem={_ordem};pagina={F(Width)}x{F(Height)}");
        foreach (var modo in _ordem.Split(','))
            await RoteiroAsync(modo);
    }

    private async Task RoteiroAsync(string m)
    {
        var (fixa, colunas) = Colunas();
        var grade = NovaGrade(fixa);
        var linhas = Gerar(_n, colunas, 1);

        // 1. Abrir
        var sw = Stopwatch.StartNew();
        grade.Conteudo = new ConteudoGrade(colunas, linhas);
        _host.Content = grade;
        var abrir = await EstavelAsync(grade, sw);
        var mem = Memoria();
        var visiveis = Visiveis(grade);
        Log($"{m};diag;visiveisEstimadas={visiveis};nativo={grade.B2Nativo};{Nativas(grade)}");
        Log($"{m};etapa;abrir;ok;primeira={_primeira};ms={abrir};vivas={grade.B2Vivas.Count};criadas={grade.B2Criadas};grade={F(grade.Width)}x{F(grade.Height)};conteudo={F(grade.LarguraConteudo)};fixa={F(grade.LarguraParteFixa)};lateral={(grade.LarguraConteudo > grade.Width + 1)};ger={mem.Gerenciada};ws={mem.Ws}");
        var maxVivas = grade.B2Vivas.Count;
        Log($"{m};diag-pos-abrir;{Nativas(grade)};barraVerticalVisivel={BarraVisivel(grade)};maxLateral={F(grade.MaximoLateral)}");

        // 2. Rolagem até o fim e volta (3x): prova direta = a linha do último/primeiro registro existe na tela
        for (var v = 1; v <= 3; v++)
        {
            sw.Restart();
            grade.B2RolarAte(_n - 1, ScrollToPosition.End);
            var t = await EstavelAsync(grade, sw);
            maxVivas = Math.Max(maxVivas, grade.B2Vivas.Count);
            Log($"{m};rolagem;fim;{v};ok={grade.PrimeiroVisivel >= _n - visiveis};materializada={Materializada(grade, _n - 1)};ms={t};primeiro={grade.PrimeiroVisivel};ultimoInformado={grade.B2UltimoVisivel};vivas={grade.B2Vivas.Count}");
            sw.Restart();
            var okGrade = await grade.IrParaAsync(_n - 1);
            Log($"{m};rolagem;fim-pela-grade;{v};ok={okGrade && grade.PrimeiroVisivel >= _n - visiveis};confirmou={okGrade};ms={sw.ElapsedMilliseconds};primeiro={grade.PrimeiroVisivel};tentativas={grade.B2UltimasTentativas}");
            sw.Restart();
            grade.B2RolarAte(0, ScrollToPosition.Start);
            t = await EstavelAsync(grade, sw);
            Log($"{m};rolagem;inicio;{v};ok={grade.PrimeiroVisivel == 0};materializada={Materializada(grade, 0)};ms={t};primeiro={grade.PrimeiroVisivel};vivas={grade.B2Vivas.Count}");
        }

        // 3. Âncora lógica: leva o alvo ao topo e confirma pelo primeiro visível
        foreach (var alvo in new[] { _n / 2, Math.Max(0, _n - 3), Math.Min(7, _n - 1), _n / 4 })
        {
            sw.Restart();
            var ok = await grade.IrParaAsync(alvo);
            var t = sw.ElapsedMilliseconds;
            await Task.Delay(150);
            var menorViva = grade.B2Vivas.Select(x => (x.Linha as LinhaLab)?.Indice ?? int.MaxValue).DefaultIfEmpty(-1).Min();
            Log($"{m};ancora;alvo={alvo};ok={ok};primeiro={grade.PrimeiroVisivel};tentativas={grade.B2UltimasTentativas};ms={t};alvoMaterializado={Materializada(grade, alvo)};menorViva={menorViva}");
        }
        await grade.IrParaAsync(0);

        // 4. Rolagem lateral + coluna fixa (alinhamento antes e depois de rolar a lista com a tabela deslocada)
        var maxLateral = grade.MaximoLateral;
        if (_id.Contains("#1-")) await CapturarAsync("inicio");
        Log($"{m};lateral;largura;conteudo={F(grade.LarguraConteudo)};grade={F(grade.Width)};maxPt={F(maxLateral)};maxPx={F(maxLateral * Densidade)}");
        if (maxLateral > 1)
        {
            for (var v = 1; v <= 2; v++)
            {
                await grade.RolarLateralAsync(maxLateral);
                await Task.Delay(250);
                if (v == 1 && _id.Contains("#1-")) await CapturarAsync("lateral-fim");
                Log($"{m};lateral;fim;{v};{Alinhamento(grade)};barraVerticalVisivel={BarraVisivel(grade)}");
                sw.Restart();
                grade.B2RolarAte(_n / 2, ScrollToPosition.Start);
                await EstavelAsync(grade, sw);
                Log($"{m};lateral;rolou-lista;{v};{Alinhamento(grade)};novasCriadas={grade.B2Criadas}");
                await grade.RolarLateralAsync(0);
                await Task.Delay(250);
                Log($"{m};lateral;inicio;{v};{Alinhamento(grade)};barraVerticalVisivel={BarraVisivel(grade)}");
                grade.B2RolarAte(0, ScrollToPosition.Start);
                await Task.Delay(250);
            }
        }

        // 5. Troca atômica das linhas (como pesquisar/limpar): 10 vezes, modelos novos a cada vez
        for (var k = 1; k <= 10; k++)
        {
            var forma = "reset";
            var antes = (grade.B2Criadas, grade.B2Reciclagens, grade.B2TrocasItens);
            var novas = Gerar(_n, colunas, 100 + k);
            sw.Restart();
            grade.Conteudo = new ConteudoGrade(colunas, novas);
            var t = await EstavelAsync(grade, sw);
            Log($"{m};etapa;troca-linhas;{k};ok;forma={forma};primeira={_primeira};ms={t};criadasDelta={grade.B2Criadas - antes.Item1};reciclagensDelta={grade.B2Reciclagens - antes.Item2};trocasItensDelta={grade.B2TrocasItens - antes.Item3};vinculos={grade.B2Vinculos};vivas={grade.B2Vivas.Count}");
        }

        // 6. Troca de colunas (ordem diferente) junto com as linhas: 2 vezes
        for (var k = 1; k <= 2; k++)
        {
            var (_, outras) = Colunas(trocada: k == 1);
            var novas = Gerar(_n, outras, 200 + k);
            var antes = grade.B2Criadas;
            sw.Restart();
            grade.Conteudo = new ConteudoGrade(outras, novas);
            var t = await EstavelAsync(grade, sw);
            var coerente = grade.B2Vivas.All(x => x.Linha is null || x.Linha.Celulas.Count == outras.Count);
            Log($"{m};etapa;troca-colunas;{k};ok;ms={t};criadasDelta={grade.B2Criadas - antes};vivas={grade.B2Vivas.Count};coerente={coerente}");
            colunas = outras;
        }

        // 7. Densidade: só a altura da grade (sem trocar a lista, sem novos modelos)
        foreach (var altura in new[] { 44.0, 56.0 })
        {
            var antes = (grade.B2Criadas, grade.B2TrocasItens, Lista: grade.Conteudo.Linhas);
            sw.Restart();
            grade.AlturaLinha = altura;
            var t = await EstavelAsync(grade, sw);
            await Task.Delay(200);
            var alturas = grade.B2Vivas.Select(AlturaNativa).Where(h => h > 0).ToList();
            var alturaOk = alturas.Count > 0 && alturas.All(h => Math.Abs(h - altura) < 1.5);
            Log($"{m};densidade;altura={F(altura)};ok={alturaOk};ms={t};alturasVivas={(alturas.Count > 0 ? F(alturas.Min()) + "-" + F(alturas.Max()) : "-")};criadasDelta={grade.B2Criadas - antes.Item1};trocasItensDelta={grade.B2TrocasItens - antes.Item2};mesmosModelos={ReferenceEquals(antes.Lista, grade.Conteudo.Linhas)};vivas={grade.B2Vivas.Count}");
        }

        // 8. Redimensionar a janela (só na janela normal): larguras recalculadas sem trocar a lista
        if (_janela == "normal")
        {
            foreach (var largura in new[] { 1100, 1700, 1440 })
            {
                var antes = (grade.B2Criadas, grade.B2TrocasItens, grade.B2Recalculos);
                await AjustarJanelaAsync("normal", largura);
                var esperado = CalculadoraLarguras.Calcular([fixa, .. colunas], grade.Width).Total;
                var alinhadas = grade.B2Vivas.All(x => Math.Abs(x.ColumnDefinitions[0].Width.Value - grade.LarguraParteFixa) < 0.5);
                Log($"{m};redimensionar;px={largura};grade={F(grade.Width)};conteudo={F(grade.LarguraConteudo)};esperado={F(esperado)};ok={Math.Abs(esperado - grade.LarguraConteudo) < 1.5};recalculosDelta={grade.B2Recalculos - antes.Item3};criadasDelta={grade.B2Criadas - antes.Item1};trocasItensDelta={grade.B2TrocasItens - antes.Item2};fixaNasVivas={alinhadas}");
            }
        }

        // 9. Seleção e destaque vindos do modelo (sem seleção nativa do motor)
        var viva = grade.B2Vivas.FirstOrDefault(x => x.Linha is not null);
        var outra = grade.B2Vivas.FirstOrDefault(x => x.Linha is not null && !ReferenceEquals(x, viva));
        if (viva?.Linha is LinhaLab sel && outra?.Linha is LinhaLab dest)
        {
            sel.Selecionada = true;
            dest.Destacada = true;
            await Task.Delay(100);
            var corSel = CoresGrade.DoTema("Selecao");
            var corDest = CoresGrade.DoTema("SuperficieRealce");
            Log($"{m};interacao;selecionada={Igual(viva.CorDoEstado, corSel)};destacada={Igual(outra.CorDoEstado, corDest)}");
            sel.Selecionada = false;
            dest.Destacada = false;
            await Task.Delay(100);
            Log($"{m};interacao;limpou={Igual(viva.CorDoEstado, Colors.Transparent) && Igual(outra.CorDoEstado, Colors.Transparent)}");
        }

        // 10. Fechar a grade e conferir que ela é liberada (sem retenção)
        viva = null;
        outra = null;
        var memAntes = Memoria();
        var final = (grade.B2Criadas, grade.B2Reciclagens);
        Log($"{m};antes-de-fechar;registradas={grade.B2Vivas.Count};vivasReais={grade.B2VivasReais};criadas={grade.B2Criadas};painel={grade.B2LinhasNoPainel};limite={grade.B2LimitePainel};renovacoes={grade.B2Renovacoes};ger={memAntes.Gerenciada};ws={memAntes.Ws}");
        maxVivas = Math.Max(maxVivas, grade.B2Vivas.Count);
        var referencia = new WeakReference(grade);
        _host.Content = null;
        grade = null!;
        await Task.Delay(400);
        var memFinal = Memoria();
        Log($"{m};SAIU;criadas={final.Item1};reciclagens={final.Item2};maxVivas={maxVivas};liberada={!referencia.IsAlive};ger={memFinal.Gerenciada};ws={memFinal.Ws}");
    }

    /// <summary>Faixa das células deslocada exatamente −x em todas as linhas vivas e no cabeçalho; parte fixa parada.</summary>
    private static string Alinhamento(GradeLista grade)
    {
        var x = grade.DeslocamentoLateral;
        var vivas = grade.B2Vivas.ToList();
        var ok = vivas.Count(v => Math.Abs(v.B2Faixa.TranslationX + x) < 0.5 && v.Fixa.TranslationX == 0);
        var cab = Math.Abs(grade.B2FaixaTitulos.TranslationX + x) < 0.5;
        return $"deslocamento={F(x)};alinhadas={ok}/{vivas.Count};cabecalho={cab}";
    }

    /// <summary>
    /// A barra vertical é do motor: visível se a lista nativa não passa da borda direita da grade (medida nativa, em
    /// pontos de tela).
    /// </summary>
    private static bool BarraVisivel(GradeLista grade)
    {
#if WINDOWS
        if (grade.B2Vista.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement lista && grade.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement g)
        {
            var x = lista.TransformToVisual(g).TransformPoint(new global::Windows.Foundation.Point(0, 0)).X;
            return x >= -0.5 && x + lista.ActualWidth <= g.ActualWidth + 1;
        }
#endif
        return false;
    }

    private static bool Igual(Color? a, Color? b) =>
        a is not null && b is not null && Math.Abs(a.Red - b.Red) < 0.01 && Math.Abs(a.Green - b.Green) < 0.01 && Math.Abs(a.Blue - b.Blue) < 0.01 && Math.Abs(a.Alpha - b.Alpha) < 0.01;

    /// <summary>
    /// Memória com a configuração final (G8): 50 trocas e 20 rolagens fim/início numa grade aberta, com coleta controlada
    /// e diagnóstico do painel a cada 10; espera de 10 s; fechar e conferir a liberação (com a área vazia e depois de
    /// pôr outro conteúdo); 20 aberturas/fechamentos.
    /// </summary>
    private async Task MemoriaAsync()
    {
        await AjustarJanelaAsync(_janela);
        Log($"inicio;build=B2;densidade={F(Densidade)};linhas={_n};janela={_janela};roteiro=memoria");
        var (fixa, colunas) = Colunas();
        var grade = NovaGrade(fixa);
        grade.Conteudo = new ConteudoGrade(colunas, Gerar(_n, colunas, 0));
        _host.Content = grade;
        await EstavelAsync(grade, Stopwatch.StartNew());
        await ColetaControladaAsync();
        Log($"memoria;troca;0;renovacoes={grade.B2Renovacoes};painel={grade.B2LinhasNoPainel};limite={grade.B2LimitePainel};{Diagnostico(grade)}");
        var tempos = new List<long>();
        for (var k = 1; k <= 50; k++)
        {
            var sw = Stopwatch.StartNew();
            grade.Conteudo = new ConteudoGrade(colunas, Gerar(_n, colunas, 2000 + k));
            tempos.Add(await EstavelAsync(grade, sw));
            if (k % 10 != 0) continue;
            await ColetaControladaAsync();
            Log($"memoria;troca;{k};ms={string.Join('/', tempos.Skip(k - 10))};renovacoes={grade.B2Renovacoes};painel={grade.B2LinhasNoPainel};limite={grade.B2LimitePainel};{Diagnostico(grade)}");
        }
        for (var k = 1; k <= 20; k++)
        {
            grade.B2RolarAte(_n - 1, ScrollToPosition.End);
            await EstavelAsync(grade, Stopwatch.StartNew());
            grade.B2RolarAte(0, ScrollToPosition.Start);
            await EstavelAsync(grade, Stopwatch.StartNew());
            if (k % 10 != 0) continue;
            await ColetaControladaAsync();
            Log($"memoria;rolagem;{k};renovacoes={grade.B2Renovacoes};painel={grade.B2LinhasNoPainel};limite={grade.B2LimitePainel};{Diagnostico(grade)}");
        }
        // Depois das rolagens, mais 10 trocas: a renovação também solta o que as rolagens acumularam?
        for (var k = 1; k <= 10; k++)
        {
            grade.Conteudo = new ConteudoGrade(colunas, Gerar(_n, colunas, 3000 + k));
            await EstavelAsync(grade, Stopwatch.StartNew());
        }
        await ColetaControladaAsync();
        Log($"memoria;troca-apos-rolagem;10;renovacoes={grade.B2Renovacoes};painel={grade.B2LinhasNoPainel};limite={grade.B2LimitePainel};{Diagnostico(grade)}");
        await Task.Delay(10000);
        await ColetaControladaAsync();
        Log($"memoria;espera;10s;renovacoes={grade.B2Renovacoes};painel={grade.B2LinhasNoPainel};limite={grade.B2LimitePainel};{Diagnostico(grade)}");

        var referencia = new WeakReference(grade);
        _host.Content = null;
        grade = null!;
        foreach (var segundos in new[] { 1, 3, 10 })
        {
            await Task.Delay(segundos * 1000);
            await ColetaControladaAsync();
            var (ger, ws) = MemoriaSemColeta();
            Log($"memoria;fechada-area-vazia;{segundos}s;liberada={!referencia.IsAlive};ger={ger};ws={ws}");
        }
        _host.Content = new Label { Text = "outro conteúdo" };
        await Task.Delay(1000);
        await ColetaControladaAsync();
        var (ger2, ws2) = MemoriaSemColeta();
        Log($"memoria;fechada-outro-conteudo;1s;liberada={!referencia.IsAlive};ger={ger2};ws={ws2}");
        _host.Content = null;

        var referencias = new List<WeakReference>();
        for (var c = 1; c <= 20; c++)
        {
            var g = NovaGrade(fixa);
            g.Conteudo = new ConteudoGrade(colunas, Gerar(_n, colunas, c));
            _host.Content = g;
            await EstavelAsync(g, Stopwatch.StartNew());
            g.B2RolarAte(_n / 2, ScrollToPosition.Start);
            await Task.Delay(300);
            _host.Content = null;
            referencias.Add(new WeakReference(g));
            g = null!;
            await Task.Delay(200);
            if (c % 5 != 0) continue;
            await ColetaControladaAsync();
            var (ger, ws) = MemoriaSemColeta();
            Log($"memoria;abrir-fechar;{c};liberadas={referencias.Count(r => !r.IsAlive)}/{referencias.Count};ger={ger};ws={ws}");
        }
        _host.Content = new Label { Text = "fim" };
        await Task.Delay(1000);
        await ColetaControladaAsync();
        var (gerF, wsF) = MemoriaSemColeta();
        Log($"memoria;final;liberadas={referencias.Count(r => !r.IsAlive)}/{referencias.Count};ger={gerF};ws={wsF}");
    }

    // ---------------- Capturas ----------------

    private async Task CapturarAsync(string rotulo)
    {
        try
        {
            await Task.Delay(300);
            if (!Screenshot.Default.IsCaptureSupported) { Log("captura;nao-suportada"); return; }
            var foto = await Screenshot.Default.CaptureAsync();
            var nome = $"b2-visual-{_n}-{_janela}-{F(Densidade)}-{rotulo}.png";
            await using var origem = await foto.OpenReadAsync(ScreenshotFormat.Png);
            await using var destino = File.Create(Path.Combine(AppContext.BaseDirectory, nome));
            await origem.CopyToAsync(destino);
            Log($"captura;{nome}");
        }
        catch (Exception ex) { Log($"captura;falhou;{ex.GetType().Name}"); }
    }

    // ---------------- Investigação da memória (G8) ----------------

    /// <summary>Coleta controlada: 3 rodadas de coleta completa com pausa, para o Windows soltar as referências nativas.</summary>
    private static async Task ColetaControladaAsync()
    {
        for (var i = 0; i < 3; i++)
        {
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect();
            await Task.Delay(300);
        }
    }

    /// <summary>
    /// Onde estão as linhas visuais que continuam na memória: com controle nativo? carregadas? na árvore da lista?
    /// dentro do painel (reserva do ItemsStackPanel)? inscritas no modelo? ligadas a um modelo da lista atual?
    /// </summary>
    private static string Diagnostico(GradeLista grade)
    {
        var todas = grade.B2TodasVivas();
        var atuais = new HashSet<object>(grade.Conteudo.Linhas, ReferenceEqualityComparer.Instance);
        int comHandler = 0, carregadas = 0, naArvore = 0, noPainel = 0, inscritas = 0, ligadasAtual = 0, filhosPainel = -1;
#if WINDOWS
        var lista = grade.B2Vista.Handler?.PlatformView as Microsoft.UI.Xaml.Controls.ListViewBase;
        var painel = lista?.ItemsPanelRoot;
        filhosPainel = painel?.Children.Count ?? -1;
#endif
        foreach (var v in todas)
        {
            if (v.Handler is not null) comHandler++;
            if (v.IsLoaded) carregadas++;
            if (v.B2Inscrita) inscritas++;
            if (v.Linha is not null && atuais.Contains(v.Linha)) ligadasAtual++;
#if WINDOWS
            if (v.Handler?.PlatformView is Microsoft.UI.Xaml.DependencyObject no)
            {
                var pai = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(no);
                var dentroDoPainel = false;
                var naLista = false;
                while (pai is not null)
                {
                    if (painel is not null && ReferenceEquals(pai, painel)) dentroDoPainel = true;
                    if (lista is not null && ReferenceEquals(pai, lista)) { naLista = true; break; }
                    pai = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(pai);
                }
                if (naLista) naArvore++;
                if (dentroDoPainel) noPainel++;
            }
#endif
        }
        var (ger, ws) = MemoriaSemColeta();
        return $"vivasReais={todas.Count};registradas={grade.B2Vivas.Count};criadas={grade.B2Criadas};comHandler={comHandler};carregadas={carregadas};naArvore={naArvore};noPainel={noPainel};filhosPainel={filhosPainel};inscritas={inscritas};ligadasListaAtual={ligadasAtual};ger={ger};ws={ws}";
    }

    private static (long Gerenciada, long Ws) MemoriaSemColeta()
    {
        using var p = Process.GetCurrentProcess();
        p.Refresh();
        return (GC.GetTotalMemory(false) / (1024 * 1024), p.WorkingSet64 / (1024 * 1024));
    }

    /// <summary>
    /// G8: três variantes, uma de cada vez, na mesma janela: base; sem inscrição da linha no modelo; renovando a fonte
    /// do motor a cada 10 trocas. Cada uma: 50 trocas + 20 rolagens fim/início, coleta controlada a cada 10, espera
    /// longa (10 s) e fechamento com verificação da liberação. No fim, 20 aberturas/fechamentos com coleta controlada.
    /// </summary>
    private async Task InvestigacaoAsync()
    {
        await AjustarJanelaAsync(_janela);
        Log($"inicio;build=B2;densidade={F(Densidade)};linhas={_n};janela={_janela};roteiro=investigacao");
        var (fixa, colunas) = Colunas();
        foreach (var variante in new[] { "base", "sem-inscricao", "renovar-10" })
        {
            var grade = NovaGrade(fixa);
            grade.B2SemInscricao = variante == "sem-inscricao";
            grade.B2RenovarACada = variante == "renovar-10" ? 10 : 0;
            grade.Conteudo = new ConteudoGrade(colunas, Gerar(_n, colunas, 0));
            _host.Content = grade;
            await EstavelAsync(grade, Stopwatch.StartNew());
            await ColetaControladaAsync();
            Log($"{variante};investigacao;troca;0;{Diagnostico(grade)}");
            var tempos = new List<long>();
            for (var k = 1; k <= 50; k++)
            {
                var sw = Stopwatch.StartNew();
                grade.Conteudo = new ConteudoGrade(colunas, Gerar(_n, colunas, 1000 + k));
                tempos.Add(await EstavelAsync(grade, sw));
                if (k % 10 != 0) continue;
                await ColetaControladaAsync();
                Log($"{variante};investigacao;troca;{k};ms={string.Join('/', tempos.Skip(k - 10))};{Diagnostico(grade)}");
            }
            for (var k = 1; k <= 20; k++)
            {
                grade.B2RolarAte(_n - 1, ScrollToPosition.End);
                await EstavelAsync(grade, Stopwatch.StartNew());
                grade.B2RolarAte(0, ScrollToPosition.Start);
                await EstavelAsync(grade, Stopwatch.StartNew());
                if (k % 10 != 0) continue;
                await ColetaControladaAsync();
                Log($"{variante};investigacao;rolagem;{k};{Diagnostico(grade)}");
            }
            for (var s = 1; s <= 5; s++)
            {
                await Task.Delay(2000);
                await ColetaControladaAsync();
                Log($"{variante};investigacao;espera;{s * 2}s;{Diagnostico(grade)}");
            }
            var referencia = new WeakReference(grade);
            _host.Content = null;
            grade = null!;
            foreach (var segundos in new[] { 1, 3, 10 })
            {
                await Task.Delay(segundos * 1000);
                await ColetaControladaAsync();
                var (ger, ws) = MemoriaSemColeta();
                Log($"{variante};investigacao;fechada;{segundos}s;liberada={!referencia.IsAlive};ger={ger};ws={ws}");
            }
        }

        var referencias = new List<WeakReference>();
        for (var c = 1; c <= 20; c++)
        {
            var g = NovaGrade(fixa);
            g.Conteudo = new ConteudoGrade(colunas, Gerar(_n, colunas, c));
            _host.Content = g;
            await EstavelAsync(g, Stopwatch.StartNew());
            g.B2RolarAte(_n / 2, ScrollToPosition.Start);
            await Task.Delay(300);
            _host.Content = null;
            referencias.Add(new WeakReference(g));
            g = null!;
            await Task.Delay(200);
            if (c % 5 != 0) continue;
            await ColetaControladaAsync();
            var (ger, ws) = MemoriaSemColeta();
            Log($"fechar;investigacao;abrir-fechar;{c};liberadas={referencias.Count(r => !r.IsAlive)}/{referencias.Count};ger={ger};ws={ws}");
        }
        foreach (var segundos in new[] { 3, 10 })
        {
            await Task.Delay(segundos * 1000);
            await ColetaControladaAsync();
            var (ger, ws) = MemoriaSemColeta();
            Log($"fechar;investigacao;espera;{segundos}s;liberadas={referencias.Count(r => !r.IsAlive)}/{referencias.Count};ger={ger};ws={ws}");
        }
    }

    // ---------------- Isolamento do acúmulo do painel (MAUI padrão × WinUI puro) ----------------

    /// <summary>Coleção que troca tudo e avisa um único Reset (a mesma semântica da fonte da GradeLista).</summary>
    private sealed class ColecaoReset : ObservableCollection<string>
    {
        public void Trocar(IEnumerable<string> itens)
        {
            Items.Clear();
            foreach (var item in itens) Items.Add(item);
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }

    private static IEnumerable<string> Textos(int n, int semente) => Enumerable.Range(0, n).Select(i => $"Registro {semente}-{i}");

    /// <summary>
    /// Mesmo roteiro da investigação (50 trocas por Reset + 20 idas e voltas da rolagem), sem a GradeLista:
    /// (1) CollectionView padrão do MAUI com um Label por linha (reserva 1 e reserva padrão do Windows);
    /// (2) ListView puro do WinUI, sem MAUI, numa janela nativa. Conta os filhos do painel nativo com coleta controlada.
    /// </summary>
    /// <summary>
    /// Etapa 3 — queda com cliques seguidos num filtro de escolha (Pessoas). Reproduz o que a tela faz a cada escolha:
    /// o Picker do cabeçalho muda, a lista é trocada (às vezes vazia) e, acima da grade, a etiqueta do filtro e o
    /// indicador de carregamento aparecem e somem (a altura da grade muda). Três variantes, do mais isolado ao mais
    /// parecido com a tela; o registro diz em qual a queda acontece (a última "variante;inicio" sem "ok").
    /// </summary>
    private async Task FiltrosAsync()
    {
        await AjustarJanelaAsync(_janela);
        Log($"inicio;build=B2;densidade={F(Densidade)};linhas={_n};janela={_janela};roteiro=filtros");
        var (fixa, colunas) = Colunas();
        foreach (var (nome, pagina, linhaFiltro) in new[] { ("so-grade", false, true), ("grade-e-pagina", true, true), ("pagina-sem-linha-de-filtro", true, false) })
        {
            Log($"filtros;variante;{nome};inicio");
            var grade = NovaGrade(fixa);
            Picker? escolha = null;
            grade.CriarFiltro = c =>
            {
                if (c.Chave != "situacao") return new Entry { Placeholder = "contém…", FontSize = 13 };
                escolha = new Picker { FontSize = 13, ItemsSource = new List<string> { "Todos", "Ativo", "Inativo" }, SelectedIndex = 0 };
                return new Border { Content = escolha };
            };
            grade.MostrarLinhaFiltro = linhaFiltro;
            grade.Conteudo = new ConteudoGrade(colunas, Gerar(50, colunas, 1));
            var etiqueta = new Label { Text = "Situação: Ativo  ✕", Padding = new Thickness(12, 6), IsVisible = false };
            var ocupado = new ActivityIndicator { WidthRequest = 18, HeightRequest = 18, IsVisible = false };
            var tela = new Grid();
            tela.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            tela.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            tela.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            tela.Add(etiqueta, 0, 0);
            tela.Add(ocupado, 0, 1);
            tela.Add(grade, 0, 2);
            _host.Content = tela;
            await Task.Delay(1500);
            escolha?.Focus();
            for (var i = 1; i <= 300; i++)
            {
                var valor = i % 3;
                if (escolha is not null && linhaFiltro) escolha.SelectedIndex = valor;
                if (pagina) { ocupado.IsVisible = ocupado.IsRunning = true; etiqueta.IsVisible = valor != 0; }
                await Task.Delay(15);
                grade.Conteudo = new ConteudoGrade(colunas, valor == 2 ? new List<ILinhaGrade>() : Gerar(50, colunas, i));
                if (pagina) ocupado.IsVisible = ocupado.IsRunning = false;
                await Task.Delay(i % 10 == 0 ? 120 : 25);
                if (i % 50 == 0) Log($"filtros;variante;{nome};passo={i}");
            }
            Log($"filtros;variante;{nome};ok");
            _host.Content = new Label { Text = "próxima" };
            await Task.Delay(800);
        }
    }

    private async Task IsolamentoAsync()
    {
        await AjustarJanelaAsync(_janela);
        Log($"inicio;build=B2;densidade={F(Densidade)};linhas={_n};janela={_janela};roteiro=isolamento");
#if WINDOWS
        foreach (var reserva in new double?[] { 1, null })
        {
            var nome = reserva is null ? "maui-padrao-reserva4" : "maui-padrao-reserva1";
            var colecao = new ColecaoReset();
            colecao.Trocar(Textos(_n, 0));
            var cv = new CollectionView
            {
                SelectionMode = SelectionMode.None,
                ItemsSource = colecao,
                ItemTemplate = new DataTemplate(() =>
                {
                    var rotulo = new Label { HeightRequest = 56, VerticalTextAlignment = TextAlignment.Center, Padding = new Thickness(12, 0) };
                    rotulo.SetBinding(Label.TextProperty, ".");
                    return rotulo;
                })
            };
            _host.Content = cv;
            await Task.Delay(1500);
            int Filhos()
            {
                if (cv.Handler?.PlatformView is not Microsoft.UI.Xaml.Controls.ListViewBase lv) return -1;
                if (reserva is { } r && lv.ItemsPanelRoot is Microsoft.UI.Xaml.Controls.ItemsStackPanel p && p.CacheLength != r) p.CacheLength = r;
                return lv.ItemsPanelRoot?.Children.Count ?? -1;
            }
            Filhos();
            await Task.Delay(800);
            await ColetaControladaAsync();
            Log($"{nome};isolamento;troca;0;painel={Filhos()};{MemoriaTexto()}");
            for (var k = 1; k <= 50; k++)
            {
                colecao.Trocar(Textos(_n, k));
                await Task.Delay(300);
                if (k % 10 != 0) continue;
                await ColetaControladaAsync();
                Log($"{nome};isolamento;troca;{k};painel={Filhos()};{MemoriaTexto()}");
            }
            for (var k = 1; k <= 20; k++)
            {
                cv.ScrollTo(_n - 1, -1, ScrollToPosition.End, false);
                await Task.Delay(300);
                cv.ScrollTo(0, -1, ScrollToPosition.Start, false);
                await Task.Delay(300);
                if (k % 10 != 0) continue;
                await ColetaControladaAsync();
                Log($"{nome};isolamento;rolagem;{k};painel={Filhos()};{MemoriaTexto()}");
            }
            _host.Content = new Label { Text = "próximo" };
            await Task.Delay(500);
        }

        // ListView puro do WinUI (sem MAUI), na mesma semântica de Reset.
        {
            var colecao = new ColecaoReset();
            colecao.Trocar(Textos(_n, 0));
            var lista = new Microsoft.UI.Xaml.Controls.ListView { ItemsSource = colecao, SelectionMode = Microsoft.UI.Xaml.Controls.ListViewSelectionMode.None };
            var janela = new Microsoft.UI.Xaml.Window { Title = "Lone - ListView WinUI puro (P2-B2)", Content = lista };
            janela.AppWindow.Resize(new global::Windows.Graphics.SizeInt32(1440, 753));
            janela.Activate();
            await Task.Delay(1500);
            int Filhos() => lista.ItemsPanelRoot?.Children.Count ?? -1;
            if (lista.ItemsPanelRoot is Microsoft.UI.Xaml.Controls.ItemsStackPanel painel) painel.CacheLength = 1;
            await Task.Delay(800);
            await ColetaControladaAsync();
            Log($"winui-puro-reserva1;isolamento;troca;0;painel={Filhos()};painelTipo={lista.ItemsPanelRoot?.GetType().Name};{MemoriaTexto()}");
            for (var k = 1; k <= 50; k++)
            {
                colecao.Trocar(Textos(_n, k));
                await Task.Delay(300);
                if (k % 10 != 0) continue;
                await ColetaControladaAsync();
                Log($"winui-puro-reserva1;isolamento;troca;{k};painel={Filhos()};{MemoriaTexto()}");
            }
            for (var k = 1; k <= 20; k++)
            {
                lista.ScrollIntoView(colecao[^1], Microsoft.UI.Xaml.Controls.ScrollIntoViewAlignment.Leading);
                await Task.Delay(300);
                lista.ScrollIntoView(colecao[0], Microsoft.UI.Xaml.Controls.ScrollIntoViewAlignment.Leading);
                await Task.Delay(300);
                if (k % 10 != 0) continue;
                await ColetaControladaAsync();
                Log($"winui-puro-reserva1;isolamento;rolagem;{k};painel={Filhos()};{MemoriaTexto()}");
            }
            janela.Close();
            await Task.Delay(500);
        }
#else
        Log("isolamento;so-windows");
#endif
    }

    private static string MemoriaTexto()
    {
        var (ger, ws) = MemoriaSemColeta();
        return $"ger={ger};ws={ws}";
    }

    // ---------------- Controle do G1 (sem grade) ----------------

    /// <summary>Mesma janela e mesmo tempo, sem GradeLista: só um texto atualizado, para comparar a frequência de quedas.</summary>
    private async Task ControleAsync()
    {
        await AjustarJanelaAsync(_janela);
        Log($"inicio;build=B2;densidade={F(Densidade)};linhas=0;janela={_janela};roteiro=controle");
        var texto = new Label { FontSize = 20 };
        _host.Content = texto;
        var sw = Stopwatch.StartNew();
        var i = 0;
        while (sw.Elapsed < TimeSpan.FromSeconds(30))
        {
            texto.Text = $"Controle sem grade — {++i}";
            await Task.Delay(100);
        }
        Log($"controle;etapa;fim;ok;atualizacoes={i}");
    }

    /// <summary>
    /// Uso manual: 1.000 linhas e uma barra de ações (densidade, trocar lista, trocar colunas, âncora, modo cartão).
    /// Registra cada ação, cada recálculo de largura e cada mudança de escala do Windows.
    /// </summary>
    private async Task ManualAsync()
    {
        await AjustarJanelaAsync(_janela);
        var (fixa, colunas) = Colunas();
        var grade = NovaGrade(fixa);
        grade.Conteudo = new ConteudoGrade(colunas, Gerar(_n, colunas, 1));
        var trocadas = false;
        var semente = 1;
        // Quebra em várias linhas quando a janela é estreita (nenhum botão fica fora da tela).
        var acoes = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, Padding = new Thickness(0, 0, 0, 8) };
        void Acao(string texto, Action fazer)
        {
            var botao = new Button { Text = texto, FontSize = 13, Padding = new Thickness(10, 4), Margin = new Thickness(0, 0, 8, 4) };
            botao.Clicked += (_, _) => { fazer(); Log($"manual;acao;{texto}"); };
            acoes.Add(botao);
        }
        Acao("Linha de filtro", () => grade.MostrarLinhaFiltro = !grade.MostrarLinhaFiltro);
        Acao("Densidade", () => grade.AlturaLinha = grade.AlturaLinha > 50 ? 44 : 56);
        Acao("Trocar lista", () => grade.Conteudo = new ConteudoGrade(grade.Conteudo.Colunas, Gerar(_n, grade.Conteudo.Colunas, ++semente)));
        Acao("Trocar colunas", () =>
        {
            trocadas = !trocadas;
            var (_, outras) = Colunas(trocadas);
            grade.Conteudo = new ConteudoGrade(outras, Gerar(_n, outras, ++semente));
        });
        Acao("Âncora: meio", () => _ = grade.IrParaAsync(_n / 2));
        Acao("Âncora: fim", () => _ = grade.IrParaAsync(_n - 1));
        Acao("Início", () => _ = grade.IrParaAsync(0));
        Acao("Lateral: fim", () => _ = grade.RolarLateralAsync(grade.MaximoLateral));
        Acao("Lateral: início", () => _ = grade.RolarLateralAsync(0));
        Acao("Modo cartão", () => grade.MostrarColunas = !grade.MostrarColunas);
        var tela = new Grid();
        tela.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        tela.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        tela.Add(acoes, 0, 0);
        tela.Add(grade, 0, 1);
        _host.Content = tela;
        Log($"inicio;build=B2;densidade={F(Densidade)};linhas={_n};janela={_janela};roteiro=manual");
        _status.Text = "Manual: clique (seleciona), duplo clique, mouse em cima (destaque), título (ordenar), roda, Shift + roda, barra lateral. Troque a escala do Windows com o app aberto e redimensione a janela.";
        DeviceDisplay.MainDisplayInfoChanged += (_, e) => Log($"manual;escala-mudou;densidade={F(e.DisplayInfo.Density)}");
#if WINDOWS
        // Aviso nativo do WinUI quando a escala ou o tamanho da raiz mudam (sugestão a verificar: XamlRoot.Changed).
        if (Window?.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativa && nativa.Content?.XamlRoot is { } raizXaml)
        {
            Log($"manual;xamlroot;inicial;escala={F(raizXaml.RasterizationScale)};tamanho={F(raizXaml.Size.Width)}x{F(raizXaml.Size.Height)}");
            raizXaml.Changed += (r, _) => Log($"manual;xamlroot;mudou;escala={F(r.RasterizationScale)};tamanho={F(r.Size.Width)}x{F(r.Size.Height)};densidadeMaui={F(Densidade)};grade={F(grade.Width)}");
        }
        else Log("manual;xamlroot;indisponivel");
#endif
        var recalculos = -1;
        var deslocamento = -1.0;
        var painel = -1;
        var aberta = true;
        Unloaded += (_, _) => aberta = false;
        if (Window is { } janela) janela.Destroying += (_, _) => aberta = false;
        while (aberta)
        {
            await Task.Delay(500);
            if (!aberta || !IsLoaded || !grade.IsLoaded) break; // a janela fechou: não tocar mais no controle nativo
            if (grade.B2LinhasNoPainel != painel)
            {
                painel = grade.B2LinhasNoPainel;
                Log($"manual;painel;linhas={painel};limite={grade.B2LimitePainel};renovacoes={grade.B2Renovacoes};primeiro={grade.PrimeiroVisivel};criadas={grade.B2Criadas}");
            }
            if (grade.DeslocamentoLateral != deslocamento)
            {
                deslocamento = grade.DeslocamentoLateral;
                Log($"manual;lateral;deslocamento={F(deslocamento)};max={F(grade.MaximoLateral)};{Alinhamento(grade)};barraVerticalVisivel={BarraVisivel(grade)}");
            }
            if (grade.B2Recalculos == recalculos) continue;
            recalculos = grade.B2Recalculos;
            Log($"manual;recalculo;{recalculos};densidade={F(Densidade)};grade={F(grade.Width)};conteudo={F(grade.LarguraConteudo)};fixa={F(grade.LarguraParteFixa)};lateral={grade.MaximoLateral > 0.5};max={F(grade.MaximoLateral)};barraVerticalVisivel={BarraVisivel(grade)};vivas={grade.B2Vivas.Count};criadas={grade.B2Criadas}");
        }
    }
}
