using Lone.Domain.Enderecos.ConferenciaCep;

namespace Lone.Tests.Dominio;

/// <summary>
/// F1 do motor de endereçamento: faixa de numeração do CEP (início, fim, lado par/ímpar) lida do texto que acompanha o CEP,
/// e se um número está nela. Sem internet: só o texto recebido.
/// </summary>
public class FaixaNumeracaoTests
{
    [Theory]
    [InlineData("até 999", null, 999, LadoFaixa.Ambos)]
    [InlineData("até 999/1000", null, 1000, LadoFaixa.Ambos)]
    [InlineData("de 1000 ao fim", 1000, null, LadoFaixa.Ambos)]
    [InlineData("de 801/802 ao fim", 801, null, LadoFaixa.Ambos)]
    [InlineData("de 1 a 99 - lado ímpar", 1, 99, LadoFaixa.Impar)]
    [InlineData("de 2 a 98 - lado par", 2, 98, LadoFaixa.Par)]
    [InlineData("lado par", null, null, LadoFaixa.Par)]
    [InlineData("lado ímpar", null, null, LadoFaixa.Impar)]
    [InlineData("até 999 - lado ímpar", null, 999, LadoFaixa.Impar)]
    [InlineData("de 1001/1002 a 1499/1500", 1001, 1500, LadoFaixa.Ambos)]
    [InlineData("ATÉ 999", null, 999, LadoFaixa.Ambos)]
    [InlineData("  de 801 / 802  ao fim ", 801, null, LadoFaixa.Ambos)]
    public void Interpreta_os_textos_dos_Correios(string texto, int? inicio, int? fim, LadoFaixa lado)
    {
        var faixa = FaixaNumeracao.Interpretar(texto);

        Assert.NotNull(faixa);
        Assert.Equal(inicio, faixa.Inicio);
        Assert.Equal(fim, faixa.Fim);
        Assert.Equal(lado, faixa.Lado);
    }

    [Theory]
    [InlineData("960", 960)]
    [InlineData("1374 12 Andar", 1374)]
    [InlineData("37 3 Andar Conjunto 31 e 32", 37)]
    [InlineData("2313 2 Subsolo", 2313)]
    [InlineData("1230 Edifício Sede", 1230)]
    public void Cep_de_predio_vale_so_para_o_numero_do_predio(string texto, int numero)
    {
        var faixa = FaixaNumeracao.Interpretar(texto)!;

        Assert.Equal(FaixaNumeracao.Criar(numero, numero), faixa);
        Assert.Equal(PertinenciaFaixa.Dentro, faixa.Contem(numero));
        Assert.Equal(PertinenciaFaixa.Fora, faixa.Contem(numero + 2));
        Assert.Equal(PertinenciaFaixa.Fora, faixa.Contem(numero - 1));
        Assert.Equal(PertinenciaFaixa.Indeterminado, faixa.Contem("S/N")); // sem número: não elimina
    }

    [Theory]
    [InlineData("10 a 20")]
    [InlineData("999/1000")]
    [InlineData("10 ao fim")]
    [InlineData("10 até 20")]
    [InlineData("10 lado par")]
    public void Numero_seguido_de_marcador_de_faixa_nao_e_cep_de_predio(string texto) =>
        Assert.Null(FaixaNumeracao.Interpretar(texto));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sem_texto_e_a_rua_toda(string? texto) => Assert.Equal(FaixaNumeracao.Todas, FaixaNumeracao.Interpretar(texto));

    [Theory]
    [InlineData("apto 101")]
    [InlineData("de 500 a 100")]
    [InlineData("até 999/1002")]
    [InlineData("até")]
    [InlineData("de 10")]
    [InlineData("lado esquerdo")]
    [InlineData("lado par até 999")]
    [InlineData("até 0")]
    [InlineData("até 99999999999")]
    [InlineData("até 999 qualquer")]
    public void Texto_fora_dos_formatos_conhecidos_nao_e_interpretado(string texto) => Assert.Null(FaixaNumeracao.Interpretar(texto));

    [Theory]
    [InlineData(500, PertinenciaFaixa.Dentro)]
    [InlineData(100, PertinenciaFaixa.Dentro)]  // limite inferior
    [InlineData(999, PertinenciaFaixa.Dentro)]  // limite superior
    [InlineData(99, PertinenciaFaixa.Fora)]
    [InlineData(1000, PertinenciaFaixa.Fora)]
    public void Dentro_fora_e_limites_incluidos(int numero, PertinenciaFaixa esperado) =>
        Assert.Equal(esperado, FaixaNumeracao.Criar(100, 999).Contem(numero));

    [Theory]
    [InlineData("até 999", 1, PertinenciaFaixa.Dentro)]
    [InlineData("até 999", 999, PertinenciaFaixa.Dentro)]
    [InlineData("até 999", 1000, PertinenciaFaixa.Fora)]
    [InlineData("de 1000 ao fim", 1000, PertinenciaFaixa.Dentro)]
    [InlineData("de 1000 ao fim", 999999, PertinenciaFaixa.Dentro)]
    [InlineData("de 1000 ao fim", 999, PertinenciaFaixa.Fora)]
    public void Faixa_aberta_no_comeco_ou_no_fim(string texto, int numero, PertinenciaFaixa esperado) =>
        Assert.Equal(esperado, FaixaNumeracao.Interpretar(texto)!.Contem(numero));

    [Theory]
    [InlineData(999, PertinenciaFaixa.Dentro)]
    [InlineData(1000, PertinenciaFaixa.Dentro)]
    [InlineData(1001, PertinenciaFaixa.Fora)]
    public void Ate_999_barra_1000_vale_ate_999_impar_e_1000_par(int numero, PertinenciaFaixa esperado) =>
        Assert.Equal(esperado, FaixaNumeracao.Interpretar("até 999/1000")!.Contem(numero));

    [Theory]
    [InlineData(800, PertinenciaFaixa.Fora)]
    [InlineData(801, PertinenciaFaixa.Dentro)]
    [InlineData(802, PertinenciaFaixa.Dentro)]
    [InlineData(5000, PertinenciaFaixa.Dentro)]
    public void De_801_barra_802_ao_fim(int numero, PertinenciaFaixa esperado) =>
        Assert.Equal(esperado, FaixaNumeracao.Interpretar("de 801/802 ao fim")!.Contem(numero));

    [Theory]
    [InlineData(2, PertinenciaFaixa.Dentro)]
    [InlineData(1000, PertinenciaFaixa.Dentro)]
    [InlineData(1, PertinenciaFaixa.Fora)]
    [InlineData(999, PertinenciaFaixa.Fora)]
    public void Lado_par(int numero, PertinenciaFaixa esperado) =>
        Assert.Equal(esperado, FaixaNumeracao.Interpretar("lado par")!.Contem(numero));

    [Theory]
    [InlineData(1, PertinenciaFaixa.Dentro)]
    [InlineData(99, PertinenciaFaixa.Dentro)]
    [InlineData(2, PertinenciaFaixa.Fora)]
    [InlineData(98, PertinenciaFaixa.Fora)]
    [InlineData(101, PertinenciaFaixa.Fora)]
    public void Lado_impar(int numero, PertinenciaFaixa esperado) =>
        Assert.Equal(esperado, FaixaNumeracao.Interpretar("de 1 a 99 - lado ímpar")!.Contem(numero));

    [Fact]
    public void Paridade_incompativel_fica_fora_mesmo_dentro_dos_limites()
    {
        var par = FaixaNumeracao.Criar(2, 100, LadoFaixa.Par);
        var impar = FaixaNumeracao.Criar(1, 99, LadoFaixa.Impar);

        Assert.Equal(PertinenciaFaixa.Fora, par.Contem(51));
        Assert.Equal(PertinenciaFaixa.Fora, impar.Contem(50));
        Assert.Equal(PertinenciaFaixa.Dentro, par.Contem(50));
        Assert.Equal(PertinenciaFaixa.Dentro, impar.Contem(51));
    }

    [Theory]
    [InlineData("100A", PertinenciaFaixa.Dentro)]
    [InlineData("100 fundos", PertinenciaFaixa.Dentro)]
    [InlineData("1000B", PertinenciaFaixa.Fora)]
    [InlineData(" 250 ", PertinenciaFaixa.Dentro)]
    public void Numero_com_letra_usa_a_parte_numerica(string numero, PertinenciaFaixa esperado) =>
        Assert.Equal(esperado, FaixaNumeracao.Interpretar("até 999")!.Contem(numero));

    [Theory]
    [InlineData("S/N")]
    [InlineData("SN")]
    [InlineData("sem número")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("KM 23")]
    [InlineData("Lote 5")]
    [InlineData("0")]
    public void Sem_numero_legivel_e_indeterminado(string? numero)
    {
        Assert.Equal(PertinenciaFaixa.Indeterminado, FaixaNumeracao.Interpretar("até 999")!.Contem(numero));
        Assert.Null(FaixaNumeracao.ParteNumerica(numero));
    }

    [Fact]
    public void Criar_recusa_faixa_invalida()
    {
        Assert.Throws<ArgumentException>(() => FaixaNumeracao.Criar(500, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => FaixaNumeracao.Criar(0, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => FaixaNumeracao.Criar(1, -5));
        Assert.Throws<ArgumentOutOfRangeException>(() => FaixaNumeracao.Criar(1, 5, (LadoFaixa)9));
    }

    [Fact]
    public void E_deterministica()
    {
        var faixas = Enumerable.Range(0, 20).Select(_ => FaixaNumeracao.Interpretar("de 801/802 ao fim")).Distinct().ToList();
        Assert.Single(faixas);
        Assert.Equal(FaixaNumeracao.Criar(801, null), faixas[0]);
    }
}
