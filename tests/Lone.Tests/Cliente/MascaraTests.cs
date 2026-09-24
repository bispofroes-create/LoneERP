using Lone.Cliente.ViewModels.Comum;

namespace Lone.Tests.Cliente;

public class MascaraTests
{
    [Theory]
    [InlineData(TipoMascara.Data, "15031990", "15/03/1990")]
    [InlineData(TipoMascara.Data, "150", "15/0")]
    [InlineData(TipoMascara.Data, "15/", "15")] // Backspace depois da barra: a barra sai junto
    [InlineData(TipoMascara.Data, "15/03/19901", "15/03/1990")]
    [InlineData(TipoMascara.Cpf, "52998224725", "529.982.247-25")]
    [InlineData(TipoMascara.Cpf, "5299", "529.9")]
    [InlineData(TipoMascara.Cep, "01310100", "01310-100")]
    [InlineData(TipoMascara.Cnpj, "11222333000181", "11.222.333/0001-81")]
    [InlineData(TipoMascara.Cnpj, "12abc34501de35", "12.ABC.345/01DE-35")] // CNPJ alfanumérico
    [InlineData(TipoMascara.Cnpj, "12ABC34501DEXY", "12.ABC.345/01DE")]    // verificadores só aceitam números
    [InlineData(TipoMascara.Telefone, "1133334444", "(11) 3333-4444")]
    [InlineData(TipoMascara.Telefone, "11988887777", "(11) 98888-7777")]
    [InlineData(TipoMascara.Telefone, "1", "(1")]
    [InlineData(TipoMascara.Telefone, "+1 555 0100", "+1 555 0100")] // exterior: como digitado
    [InlineData(TipoMascara.Telefone, "0800 123 4567", "0800 123 4567")] // 0800: como digitado
    [InlineData(TipoMascara.Telefone, "55 11 98888-7777", "55 11 98888-7777")] // mais de 11 dígitos: como digitado
    [InlineData(TipoMascara.Nenhuma, "livre 123", "livre 123")]
    public void Formata_enquanto_digita(TipoMascara tipo, string digitado, string esperado)
    {
        Assert.Equal(esperado, Mascara.Aplicar(tipo, digitado));
    }

    [Fact]
    public void Aplicar_de_novo_nao_muda_o_texto_ja_formatado()
    {
        foreach (var (tipo, texto) in new[] { (TipoMascara.Data, "15/03/1990"), (TipoMascara.Telefone, "(11) 98888-7777"), (TipoMascara.Cnpj, "12.ABC.345/01DE-35") })
            Assert.Equal(texto, Mascara.Aplicar(tipo, texto));
    }
}
