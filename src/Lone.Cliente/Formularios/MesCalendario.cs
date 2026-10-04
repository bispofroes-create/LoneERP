using System.Globalization;

namespace Lone.Cliente.Formularios;

/// <summary>Um dia na grade do calendário dos campos de data.</summary>
/// <param name="DoMes">Falso para os dias do mês anterior ou seguinte que completam a grade (aparecem apagados).</param>
public sealed record DiaCalendario(DateOnly Data, bool DoMes, bool Hoje, bool Escolhido);

/// <summary>
/// Conta do calendário que abre pelo ícone 📅 dos campos de data (03/10/2026, docs/UX-ARQUITETURA.md): a grade do mês
/// sempre com 6 semanas (42 dias), começando no domingo, como no calendário brasileiro; assim o calendário não muda de
/// altura ao trocar de mês.
/// </summary>
public static class MesCalendario
{
    public const int DiasNaGrade = 42;

    public static readonly string[] IniciaisDaSemana = ["D", "S", "T", "Q", "Q", "S", "S"];

    private static readonly CultureInfo Brasil = new("pt-BR");

    public static IReadOnlyList<DiaCalendario> Dias(int ano, int mes, DateOnly hoje, DateOnly? escolhido)
    {
        var primeiro = new DateOnly(ano, mes, 1);
        var inicio = primeiro.AddDays(-(int)primeiro.DayOfWeek);
        var dias = new List<DiaCalendario>(DiasNaGrade);
        for (var i = 0; i < DiasNaGrade; i++)
        {
            var data = inicio.AddDays(i);
            dias.Add(new DiaCalendario(data, data.Month == mes, data == hoje, data == escolhido));
        }
        return dias;
    }

    /// <summary>"Outubro de 2026".</summary>
    public static string Titulo(int ano, int mes)
    {
        var nome = Brasil.DateTimeFormat.GetMonthName(mes);
        return $"{char.ToUpper(nome[0], Brasil)}{nome[1..]} de {ano}";
    }

    /// <summary>Texto que o campo recebe ao escolher um dia.</summary>
    public static string Texto(DateOnly data) => data.ToString("dd/MM/yyyy", Brasil);
}
