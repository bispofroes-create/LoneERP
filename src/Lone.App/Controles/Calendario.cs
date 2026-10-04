using Lone.Cliente.Formularios;

namespace Lone.App.Controles;

/// <summary>
/// Calendário pequeno que abre pelo ícone 📅 dos campos de data (03/10/2026, docs/UX-ARQUITETURA.md): mês e ano com
/// navegação, hoje destacado, o dia escolhido em cor primária, "Hoje" e "Limpar". Próprio do Lone (o DatePicker do MAUI
/// não aceita data vazia) e feito em código, sem depender de recursos dinâmicos: ele é mostrado num popup, fora da
/// árvore da página. A conta da grade fica em <see cref="MesCalendario"/>.
/// </summary>
public sealed class Calendario : ContentView
{
    private const double LadoDia = 36;

    private readonly Label _titulo = new() { FontSize = 15, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.Center };
    private readonly Button[] _dias = new Button[MesCalendario.DiasNaGrade];
    private readonly DateOnly _hoje = DateOnly.FromDateTime(DateTime.Today);
    private readonly DateOnly? _escolhido;
    private readonly bool _escuro = Application.Current?.RequestedTheme == AppTheme.Dark;
    private int _ano;
    private int _mes;

    /// <param name="escolhido">Data que já está no campo (o calendário abre no mês dela; sem data, no mês atual).</param>
    /// <param name="podeLimpar">Mostra "Limpar" (campo de data opcional ou já preenchido).</param>
    public Calendario(DateOnly? escolhido, bool podeLimpar)
    {
        _escolhido = escolhido;
        var inicio = escolhido ?? _hoje;
        _ano = inicio.Year;
        _mes = inicio.Month;

        var cabecalho = new Grid { ColumnSpacing = 2 };
        Colunas(cabecalho, GridLength.Auto, GridLength.Auto, GridLength.Star, GridLength.Auto, GridLength.Auto);
        cabecalho.Add(Navegar("«", "Ano anterior", () => Mudar(-12)), 0);
        cabecalho.Add(Navegar("‹", "Mês anterior", () => Mudar(-1)), 1);
        cabecalho.Add(_titulo, 2);
        cabecalho.Add(Navegar("›", "Próximo mês", () => Mudar(1)), 3);
        cabecalho.Add(Navegar("»", "Próximo ano", () => Mudar(12)), 4);

        var grade = new Grid { ColumnSpacing = 2, RowSpacing = 2 };
        for (var c = 0; c < 7; c++) grade.ColumnDefinitions.Add(new ColumnDefinition(LadoDia));
        for (var l = 0; l < 7; l++) grade.RowDefinitions.Add(new RowDefinition(l == 0 ? 24 : LadoDia - 4));
        for (var c = 0; c < 7; c++)
            grade.Add(new Label
            {
                Text = MesCalendario.IniciaisDaSemana[c], FontSize = 12, TextColor = Cor("TextoSecundario"),
                HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center
            }, c, 0);
        for (var i = 0; i < _dias.Length; i++)
        {
            var dia = new Button
            {
                FontSize = 13, Padding = 0, CornerRadius = 4, BorderWidth = 0, MinimumHeightRequest = 0, MinimumWidthRequest = 0,
                WidthRequest = LadoDia, HeightRequest = LadoDia - 4, BackgroundColor = Colors.Transparent
            };
            dia.Clicked += (s, _) => { if (((Button)s!).CommandParameter is DateOnly d) Escolheu?.Invoke(this, d); };
            _dias[i] = dia;
            grade.Add(dia, i % 7, i / 7 + 1);
        }

        var rodape = new Grid();
        Colunas(rodape, GridLength.Auto, GridLength.Star, GridLength.Auto);
        rodape.Add(Acao("Hoje", () => Escolheu?.Invoke(this, _hoje)), 0);
        if (podeLimpar) rodape.Add(Acao("Limpar", () => Escolheu?.Invoke(this, null)), 2);

        Content = new VerticalStackLayout { Spacing = 8, Padding = 4, Children = { cabecalho, grade, rodape } };
        Montar();
    }

    /// <summary>Dia escolhido (nulo = "Limpar").</summary>
    public event EventHandler<DateOnly?>? Escolheu;

    private void Mudar(int meses)
    {
        var novo = new DateOnly(_ano, _mes, 1).AddMonths(meses);
        _ano = novo.Year;
        _mes = novo.Month;
        Montar();
    }

    private void Montar()
    {
        _titulo.Text = MesCalendario.Titulo(_ano, _mes);
        _titulo.TextColor = Cor("Texto");
        var dias = MesCalendario.Dias(_ano, _mes, _hoje, _escolhido);
        for (var i = 0; i < dias.Count; i++)
        {
            var (dia, botao) = (dias[i], _dias[i]);
            botao.Text = dia.Data.Day.ToString();
            botao.CommandParameter = dia.Data;
            SemanticProperties.SetDescription(botao, MesCalendario.Texto(dia.Data));
            botao.FontAttributes = dia.Hoje || dia.Escolhido ? FontAttributes.Bold : FontAttributes.None;
            botao.BackgroundColor = dia.Escolhido ? Cor("Primaria") : Colors.Transparent;
            botao.TextColor = dia.Escolhido ? (_escuro ? Cor("Fundo") : Colors.White)
                : dia.Hoje ? Cor("Primaria")
                : dia.DoMes ? Cor("Texto") : Cor("TextoSecundario").WithAlpha(0.6f);
            // Hoje: contorno na cor primária (quando não é também o dia escolhido).
            botao.BorderWidth = dia.Hoje && !dia.Escolhido ? 1 : 0;
            botao.BorderColor = Cor("Primaria");
        }
    }

    private Button Navegar(string texto, string descricao, Action acao)
    {
        var botao = Acao(texto, acao);
        botao.FontSize = 16;
        botao.WidthRequest = 32;
        SemanticProperties.SetDescription(botao, descricao);
        ToolTipProperties.SetText(botao, descricao);
        return botao;
    }

    private Button Acao(string texto, Action acao)
    {
        var botao = new Button
        {
            Text = texto, FontSize = 13, Padding = new Thickness(8, 0), MinimumHeightRequest = 0, MinimumWidthRequest = 0,
            HeightRequest = 32, BackgroundColor = Colors.Transparent, TextColor = Cor("Primaria"), BorderWidth = 0
        };
        botao.Clicked += (_, _) => acao();
        return botao;
    }

    /// <summary>Cor do tema atual lida direto dos recursos do aplicativo (o popup não herda os recursos da página).</summary>
    private Color Cor(string chave)
    {
        var recursos = Application.Current?.Resources;
        if (recursos is not null && (_escuro && recursos.TryGetValue(chave + "Escuro", out var e) && e is Color ce)) return ce;
        return recursos is not null && recursos.TryGetValue(chave, out var c) && c is Color cc ? cc : Colors.Gray;
    }

    private static void Colunas(Grid grade, params GridLength[] larguras)
    {
        foreach (var largura in larguras) grade.ColumnDefinitions.Add(new ColumnDefinition(largura));
    }
}
