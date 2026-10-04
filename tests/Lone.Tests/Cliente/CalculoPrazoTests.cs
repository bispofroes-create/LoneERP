using Lone.Cliente.Formularios;

namespace Lone.Tests.Cliente;

/// <summary>Campo Prazo entre Início e Fim (03/10/2026): contagem inclusiva, prazos prontos e fim fora do fim de semana.</summary>
public class CalculoPrazoTests
{
    private static readonly DateOnly Inicio = new(2026, 10, 3);

    [Fact]
    public void Contagem_inclusiva_como_no_Lone()
    {
        Assert.Equal(new DateOnly(2026, 11, 1), CalculoPrazo.Fim(Inicio, 30));
        Assert.Equal(Inicio, CalculoPrazo.Fim(Inicio, 1));
        Assert.Equal(30, CalculoPrazo.Dias(Inicio, new DateOnly(2026, 11, 1)));
        Assert.Null(CalculoPrazo.Dias(Inicio, new DateOnly(2026, 10, 2)));
        Assert.Equal("30 dias (03/10 a 01/11)", CalculoPrazo.Resumo(Inicio, new DateOnly(2026, 11, 1)));
    }

    [Fact]
    public void Um_ano_e_pelo_calendario()
    {
        var umAno = CalculoPrazo.Padrao.Single(p => p.Nome == "1 ano");
        Assert.Equal(new DateOnly(2027, 10, 2), CalculoPrazo.Fim(Inicio, umAno));
        Assert.Equal(new DateOnly(2026, 10, 9), CalculoPrazo.Fim(Inicio, CalculoPrazo.Padrao[0])); // 7 dias
    }

    [Fact]
    public void Fim_no_sabado_ou_domingo_vai_para_segunda()
    {
        var sabado = new DateOnly(2026, 10, 31);
        var ajuste = CalculoPrazo.ForaDoFimDeSemana(sabado)!;
        Assert.Equal(new DateOnly(2026, 11, 2), ajuste.Ajustado);
        Assert.Equal(2, ajuste.DiasSomados);
        Assert.Equal("+2 dias: o fim cairia no sábado 31/10 e foi para segunda 02/11.", CalculoPrazo.Aviso(ajuste));

        var domingo = CalculoPrazo.ForaDoFimDeSemana(new DateOnly(2026, 11, 1))!;
        Assert.Equal(1, domingo.DiasSomados);
        Assert.Null(CalculoPrazo.ForaDoFimDeSemana(new DateOnly(2026, 10, 30))); // sexta
    }

    [Theory]
    [InlineData("45", 45)]
    [InlineData(" 7 ", 7)]
    [InlineData("0", null)]
    [InlineData("-3", null)]
    [InlineData("abc", null)]
    [InlineData("", null)]
    public void Dias_digitados(string texto, int? esperado) => Assert.Equal(esperado, CalculoPrazo.LerDias(texto));
}
