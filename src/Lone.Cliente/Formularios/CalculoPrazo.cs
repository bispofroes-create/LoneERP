using System.Globalization;
using Lone.Cliente.ViewModels.Comum;

namespace Lone.Cliente.Formularios;

/// <summary>Um prazo pronto para escolher (ex.: "30 dias", "1 ano").</summary>
/// <param name="Dias">Dias corridos, contando o início; nulo quando é por ano.</param>
/// <param name="Anos">Anos pelo calendário (ex.: 1 ano: 03/10/2026 a 02/10/2027); nulo quando é por dias.</param>
/// <param name="Meses">Meses pelo calendário (ex.: 6 meses: 03/10/2026 a 02/04/2027); nulo quando é por dias ou anos.</param>
public sealed record PrazoPronto(string Nome, int? Dias = null, int? Anos = null, int? Meses = null)
{
    public override string ToString() => Nome;
}

/// <summary>Fim que cairia num sábado ou domingo e foi para a segunda-feira.</summary>
public sealed record AjusteFimDeSemana(DateOnly Original, DateOnly Ajustado)
{
    public int DiasSomados => Ajustado.DayNumber - Original.DayNumber;
}

/// <summary>
/// Conta do campo Prazo entre Início e Fim (pedido do usuário, 03/10/2026; docs/UX-ARQUITETURA.md):
/// <list type="bullet">
/// <item>contagem inclusiva, a mesma do Lone: o Fim é o último dia (30 dias a partir de 03/10/2026 terminam em 01/11/2026);</item>
/// <item>prazos prontos (7, 15, 30, 60, 90, 180 dias e 1 ano) ou os dias digitados;</item>
/// <item>"Não terminar em fim de semana": o Fim que cairia num sábado ou domingo vai para a segunda e o aviso diz quantos
/// dias foram somados. Feriados entram depois, com o calendário de feriados.</item>
/// </list>
/// Sem MAUI, para ser testada; o controle CampoPrazo (Lone.App) só mostra.
/// </summary>
public static class CalculoPrazo
{
    private static readonly CultureInfo Brasil = new("pt-BR");

    /// <summary>
    /// Prazos prontos de antes do cadastro "Prazos de período" (03/10/2026): usados enquanto a lista do cadastro não chega
    /// (ou se a API não responder). A lista do cadastro fica em <c>PrazosProntos</c> (Lone.Cliente.Api).
    /// </summary>
    public static readonly IReadOnlyList<PrazoPronto> Padrao =
    [
        new("7 dias", Dias: 7), new("15 dias", Dias: 15), new("30 dias", Dias: 30), new("60 dias", Dias: 60),
        new("90 dias", Dias: 90), new("180 dias", Dias: 180), new("1 ano", Anos: 1)
    ];

    /// <summary>Último dia do prazo, contando o início.</summary>
    public static DateOnly Fim(DateOnly inicio, int dias) => inicio.AddDays(Math.Max(1, dias) - 1);

    public static DateOnly Fim(DateOnly inicio, PrazoPronto prazo) =>
        prazo.Anos is { } anos ? inicio.AddYears(anos).AddDays(-1)
        : prazo.Meses is { } meses ? inicio.AddMonths(meses).AddDays(-1)
        : Fim(inicio, prazo.Dias ?? 1);

    /// <summary>Dias do período, contando o início e o fim; nulo se o fim vem antes do início.</summary>
    public static int? Dias(DateOnly inicio, DateOnly fim) => fim >= inicio ? fim.DayNumber - inicio.DayNumber + 1 : null;

    /// <summary>Sábado ou domingo vai para a segunda-feira; dia útil fica como está (nulo).</summary>
    public static AjusteFimDeSemana? ForaDoFimDeSemana(DateOnly fim) => fim.DayOfWeek switch
    {
        DayOfWeek.Saturday => new AjusteFimDeSemana(fim, fim.AddDays(2)),
        DayOfWeek.Sunday => new AjusteFimDeSemana(fim, fim.AddDays(1)),
        _ => null
    };

    /// <summary>"30 dias (03/10 a 01/11)".</summary>
    public static string Resumo(DateOnly inicio, DateOnly fim) =>
        Dias(inicio, fim) is { } n ? $"{Quantos(n)} ({Curta(inicio)} a {Curta(fim)})" : "O fim vem antes do início.";

    /// <summary>"+2 dias: o fim cairia no sábado 31/10 e foi para segunda 02/11."</summary>
    public static string Aviso(AjusteFimDeSemana ajuste) =>
        $"+{Quantos(ajuste.DiasSomados)}: o fim cairia no {NomeDia(ajuste.Original)} {Curta(ajuste.Original)} e foi para " +
        $"{NomeDia(ajuste.Ajustado)} {Curta(ajuste.Ajustado)}.";

    /// <summary>Dias digitados no campo Prazo: só número inteiro de 1 a 36500.</summary>
    public static int? LerDias(string? texto) =>
        int.TryParse((texto ?? string.Empty).Trim(), NumberStyles.None, Brasil, out var n) && n is >= 1 and <= 36500 ? n : null;

    public static string Texto(DateOnly data) => data.ToString("dd/MM/yyyy", Brasil);

    private static string Quantos(int n) => n == 1 ? "1 dia" : $"{n.ToString("N0", TextoTela.Brasil)} dias";

    private static string Curta(DateOnly d) => d.ToString("dd/MM", Brasil);

    private static string NomeDia(DateOnly d) => d.DayOfWeek switch
    {
        DayOfWeek.Saturday => "sábado",
        DayOfWeek.Sunday => "domingo",
        DayOfWeek.Monday => "segunda",
        DayOfWeek.Tuesday => "terça",
        DayOfWeek.Wednesday => "quarta",
        DayOfWeek.Thursday => "quinta",
        _ => "sexta"
    };
}
