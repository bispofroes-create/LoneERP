using Lone.Application.Consultas;
using Lone.Contracts.Pessoas;
using Lone.Domain.Validacao;

namespace Lone.Tests.Aplicacao;

public class ConsultaPessoasTests
{
    [Theory]
    [InlineData("Ana", "Ana")]
    [InlineData("=SOMA(A1)", "'=SOMA(A1)")]      // fórmula vira texto no Excel
    [InlineData("-10", "'-10")]
    [InlineData("a;b", "\"a;b\"")]
    [InlineData("diz \"oi\"", "\"diz \"\"oi\"\"\"")]
    [InlineData("x\"; y", "\"x\"\"; y\"")]
    public void Celula_do_csv_e_segura(string valor, string esperado) =>
        Assert.Equal(esperado, ConsultaPessoasAppService.Celula(valor));

    [Fact]
    public void Criterios_sao_limpos()
    {
        var c = ConsultaPessoasAppService.Normalizar(new CriteriosPessoas { Texto = "  ana ", Uf = " sp", Cnae = "47.11-3" });
        Assert.Equal("ana", c.Texto);
        Assert.Equal("SP", c.Uf);
        Assert.Equal("47113", c.Cnae);
    }

    [Theory]
    [InlineData("SPX", null, null)]
    [InlineData(null, 0, null)]
    [InlineData(null, null, "12345678")]
    public void Criterios_invalidos_sao_recusados(string? uf, int? semInteracao, string? cnae) =>
        Assert.Throws<ValidacaoException>(() => ConsultaPessoasAppService.Normalizar(
            new CriteriosPessoas { Uf = uf, SemInteracaoDias = semInteracao, Cnae = cnae }));

    [Fact]
    public void Criterio_gravado_ilegivel_vira_vazio() =>
        Assert.Null(ConsultaPessoasAppService.LerCriterios("{isto não é json").Texto);
}
