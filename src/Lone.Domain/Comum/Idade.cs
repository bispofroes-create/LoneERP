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

    /// <summary>"42 anos", "1 ano", "Menos de 1 ano", "Data no futuro"; sem data = vazio (nunca guarda idade antiga).</summary>
    public static string Texto(DateOnly? nascimento, DateOnly hoje)
    {
        if (nascimento is not { } n) return string.Empty;
        if (n > hoje) return "Data no futuro";
        var anos = Em(n, hoje);
        return anos switch { 0 => "Menos de 1 ano", 1 => "1 ano", _ => $"{anos} anos" };
    }
}
