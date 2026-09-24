using Lone.Application.Pessoas;

namespace Lone.Tests.Aplicacao;

public class FaixasEtariasTests
{
    [Fact]
    public void Distribui_por_idade_e_separa_quem_nao_informou_a_data()
    {
        var hoje = new DateOnly(2026, 9, 24);
        DateOnly? Nascido(int ano, int mes = 1, int dia = 1) => new DateOnly(ano, mes, dia);

        var faixas = FaixasEtarias.Contar(
        [
            Nascido(2010),              // 16
            Nascido(2008, 9, 24),       // 18 hoje
            Nascido(2008, 9, 25),       // ainda 17
            Nascido(1990), Nascido(1995), // 36 e 31
            Nascido(1960),              // 66
            null,
            Nascido(2030)               // data no futuro conta como não informada
        ], hoje);

        var porNome = faixas.ToDictionary(f => f.Faixa, f => f.Quantidade);
        Assert.Equal(2, porNome["Até 17 anos"]);
        Assert.Equal(1, porNome["18 a 24 anos"]);
        Assert.Equal(1, porNome["25 a 34 anos"]);
        Assert.Equal(1, porNome["35 a 44 anos"]);
        Assert.Equal(0, porNome["45 a 59 anos"]);
        Assert.Equal(1, porNome["60 anos ou mais"]);
        Assert.Equal(2, porNome[FaixasEtarias.SemData]);
        Assert.Equal(FaixasEtarias.SemData, faixas[^1].Faixa);
    }
}
