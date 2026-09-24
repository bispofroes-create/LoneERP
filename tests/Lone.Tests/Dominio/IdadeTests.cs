using Lone.Domain.Comum;

namespace Lone.Tests.Dominio;

public class IdadeTests
{
    [Theory]
    [InlineData("1990-03-15", "2026-03-14", 35)]
    [InlineData("1990-03-15", "2026-03-15", 36)]
    [InlineData("2000-02-29", "2026-02-28", 25)] // nascido em 29/02: completa ano em 01/03 nos anos comuns
    [InlineData("2000-02-29", "2026-03-01", 26)]
    [InlineData("2026-01-10", "2026-09-24", 0)]
    public void Conta_anos_completos(string nascimento, string data, int esperado)
    {
        Assert.Equal(esperado, Idade.Em(DateOnly.Parse(nascimento), DateOnly.Parse(data)));
    }
}
