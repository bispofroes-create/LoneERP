using Lone.Domain.Documentos;
using Lone.Domain.Enums;

namespace Lone.Tests.Dominio;

/// <summary>P1-8: o número comparável do documento (contrato único), os formatos fechados e a chave de unicidade.</summary>
public class NumeroDocumentoTests
{
    [Theory]
    [InlineData("12.345.678-9", "123456789")]           // pontos e hífen
    [InlineData("12 345 678 9", "123456789")]           // espaços
    [InlineData("123/456\\789", "123456789")]           // barras
    [InlineData("MG-12.345.678", "MG12345678")]         // letras ficam
    [InlineData("ab-c1", "ABC1")]                       // caixa
    [InlineData("0012", "0012")]                        // zeros à esquerda ficam
    [InlineData("000", "000")]
    [InlineData("Ção-Ñ ü", "CAONU")]                    // acentos pela tabela
    [InlineData("ÿý", "YY")]
    [InlineData("É", "E")]                       // acento combinado (decomposto) sai
    [InlineData("ЖД12", "12")]                          // letras fora do latim básico saem
    [InlineData("１２", "")]                            // dígitos de largura total saem
    [InlineData("٣4", "4")]                             // dígitos de outra escrita saem
    [InlineData("ı i ß", "I")]                          // i sem ponto e ß saem (sem regra de cultura)
    [InlineData("AB😀12", "AB12")]                      // fora do plano básico sai
    [InlineData("#*()", "")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Normaliza_so_para_comparar(string? numero, string esperado) =>
        Assert.Equal(esperado, NumeroDocumento.Normalizar(numero));

    [Fact]
    public void Formas_diferentes_do_mesmo_numero_ficam_iguais()
    {
        var formas = new[] { "12.345.678-9", "12345678-9", "123456789", " 12 345 678 9 ", "12/345/678/9" };
        Assert.Single(formas.Select(NumeroDocumento.Normalizar).Distinct());
        Assert.NotEqual(NumeroDocumento.Normalizar("0123"), NumeroDocumento.Normalizar("123"));
    }

    [Fact]
    public void Tabela_de_acentos_tem_os_dois_lados_do_mesmo_tamanho_e_so_letras_basicas()
    {
        Assert.Equal(NumeroDocumento.Acentuadas.Length, NumeroDocumento.SemAcento.Length);
        Assert.All(NumeroDocumento.SemAcento, c => Assert.InRange(c, 'A', 'Z'));
        Assert.Equal(NumeroDocumento.Acentuadas.Length, NumeroDocumento.Acentuadas.Distinct().Count());
    }

    [Fact]
    public void Resultado_e_sempre_ascii_e_nunca_maior_que_o_digitado()
    {
        var aleatorio = new Random(18);
        for (var n = 0; n < 2000; n++)
        {
            var texto = new string(Enumerable.Range(0, aleatorio.Next(0, 31)).Select(_ => (char)aleatorio.Next(1, 0x3000)).ToArray());
            var comparavel = NumeroDocumento.Normalizar(texto);
            Assert.True(comparavel.Length <= texto.Length);
            Assert.All(comparavel, c => Assert.True(c is >= 'A' and <= 'Z' or >= '0' and <= '9'));
            Assert.Equal(comparavel, NumeroDocumento.Normalizar(comparavel)); // idempotente
        }
    }

    [Theory]
    [InlineData("qualquer #coisa*", FormatoNumeroDocumento.Livre, true)]
    [InlineData("", FormatoNumeroDocumento.Livre, true)]
    [InlineData("12.345.678-9", FormatoNumeroDocumento.SomenteDigitos, true)]
    [InlineData("12 345/678", FormatoNumeroDocumento.SomenteDigitos, true)]
    [InlineData("12.345.678-X", FormatoNumeroDocumento.SomenteDigitos, false)]
    [InlineData("12#3", FormatoNumeroDocumento.SomenteDigitos, false)]
    [InlineData("-.-", FormatoNumeroDocumento.SomenteDigitos, false)]
    [InlineData("MG-12.345.678", FormatoNumeroDocumento.Alfanumerico, true)]
    [InlineData("Ção 12", FormatoNumeroDocumento.Alfanumerico, true)]
    [InlineData("AB#12", FormatoNumeroDocumento.Alfanumerico, false)]
    [InlineData("ЖД12", FormatoNumeroDocumento.Alfanumerico, false)]
    [InlineData("", FormatoNumeroDocumento.Alfanumerico, false)]
    public void Formatos_fechados(string numero, FormatoNumeroDocumento formato, bool aceito) =>
        Assert.Equal(aceito, NumeroDocumento.ConferirFormato(numero, formato, null, null) is null);

    [Fact]
    public void Tamanhos_contam_so_o_numero_comparavel()
    {
        Assert.Null(NumeroDocumento.ConferirFormato("12.345.678-9", FormatoNumeroDocumento.SomenteDigitos, 9, 9));
        Assert.Contains("deve ter 9", NumeroDocumento.ConferirFormato("1234567890", FormatoNumeroDocumento.SomenteDigitos, 9, 9));
        Assert.Contains("pelo menos 5", NumeroDocumento.ConferirFormato("1.2.3", FormatoNumeroDocumento.Livre, 5, null));
        Assert.Contains("no máximo 3", NumeroDocumento.ConferirFormato("1-2-3-4", FormatoNumeroDocumento.Livre, null, 3));
        Assert.Null(NumeroDocumento.ConferirFormato("1-2-3", FormatoNumeroDocumento.Livre, 1, 3));
    }

    [Fact]
    public void Chave_de_unicidade_so_nos_modos_que_bloqueiam_e_nunca_com_parte_ausente()
    {
        var tipo = new Guid("7a9e1c03-0000-0000-0000-000000000002");
        Assert.Null(NumeroDocumento.ChaveUnicidade(UnicidadeDocumento.Nenhuma, tipo, "SP", "123"));
        Assert.Null(NumeroDocumento.ChaveUnicidade(UnicidadeDocumento.Aviso, tipo, "SP", "123"));
        Assert.Equal("7a9e1c03000000000000000000000002|123", NumeroDocumento.ChaveUnicidade(UnicidadeDocumento.PorTipo, tipo, "SP", "123"));
        Assert.Equal("7a9e1c03000000000000000000000002|SP|123", NumeroDocumento.ChaveUnicidade(UnicidadeDocumento.PorTipoEUf, tipo, "sp", "123"));
        Assert.Null(NumeroDocumento.ChaveUnicidade(UnicidadeDocumento.PorTipoEUf, tipo, null, "123"));   // sem UF: fora
        Assert.Null(NumeroDocumento.ChaveUnicidade(UnicidadeDocumento.PorTipo, tipo, null, ""));          // sem número comparável: fora
        Assert.Null(NumeroDocumento.ChaveUnicidade(UnicidadeDocumento.PorTipo, Guid.Empty, null, "123"));

        var maior = NumeroDocumento.ChaveUnicidade(UnicidadeDocumento.PorTipoEUf, tipo, "SP", new string('9', NumeroDocumento.TamanhoMaximo))!;
        Assert.True(maior.Length <= NumeroDocumento.TamanhoChave);
    }
}
