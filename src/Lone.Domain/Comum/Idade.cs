namespace Lone.Domain.Comum;

/// <summary>
/// Idade em anos completos numa data. Quem nasceu em 29/02 completa ano em 01/03 nos anos comuns
/// (Código Civil, art. 132, § 3º: sem dia correspondente, vale o dia seguinte).
/// </summary>
public static class Idade
{
    public static int Em(DateOnly nascimento, DateOnly data)
    {
        var anos = data.Year - nascimento.Year;
        if (data.Month < nascimento.Month || (data.Month == nascimento.Month && data.Day < nascimento.Day))
            anos--;
        return anos;
    }
}
