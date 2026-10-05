using Lone.Domain.Enderecos;
using Lone.Domain.Enderecos.ConferenciaCep;

namespace Lone.Tests.Dominio;

/// <summary>
/// F1 do motor de endereçamento: normalização do logradouro só para comparar ("R. Barao" = "Rua Barão"). É a mesma da
/// duplicidade de endereços (nenhuma regra nova) e nunca muda o texto original.
/// </summary>
public class NormalizadorLogradouroTests
{
    [Theory]
    [InlineData("R. Barao", "Rua Barão")]
    [InlineData("R Barão", "RUA BARAO")]
    [InlineData("Av. Brasil", "Avenida Brasil")]
    [InlineData("AVN Brasil", "Avenida Brasil")]
    [InlineData("Tv. das Flores", "Travessa das Flores")]
    [InlineData("Trav. das Flores", "Travessa das Flores")]
    [InlineData("Pç. da Matriz", "Praça da Matriz")]
    [InlineData("Pca da Matriz", "Praça da Matriz")]
    [InlineData("Rod. Fernão Dias", "Rodovia Fernão Dias")]
    public void Abreviacao_do_tipo_no_comeco_equivale_ao_nome_completo(string abreviado, string completo)
    {
        Assert.True(NormalizadorLogradouro.Equivalentes(abreviado, completo));
        Assert.Equal(NormalizadorLogradouro.Normalizar(completo), NormalizadorLogradouro.Normalizar(abreviado));
    }

    [Theory]
    [InlineData("Rua São João", "RUA SAO JOAO")]
    [InlineData("Rua Conceição", "RUA CONCEICAO")]
    [InlineData("Praça Tiradentes", "PRACA TIRADENTES")]
    [InlineData("rua barão", "RUA BARAO")]
    public void Acento_e_maiuscula_nao_contam(string entrada, string esperado) =>
        Assert.Equal(esperado, NormalizadorLogradouro.Normalizar(entrada));

    [Theory]
    [InlineData("  Rua   Barão  ", "Rua Barão")]
    [InlineData("Rua Barão.", "Rua Barão")]
    [InlineData("Rua - Barão", "Rua Barão")]
    [InlineData("Rua Barão, ", "rua barao")]
    [InlineData("Rua\tBarão", "Rua Barão")]
    public void Espacos_pontuacao_e_caixa_sao_diferencas_irrelevantes(string a, string b) =>
        Assert.True(NormalizadorLogradouro.Equivalentes(a, b));

    [Theory]
    [InlineData("Rua Barão", "Rua Barão de Cocais")]
    [InlineData("Rua A", "Rua B")]
    [InlineData("Rua Brasil", "Avenida Brasil")]
    public void Logradouros_diferentes_nao_sao_equivalentes(string a, string b) =>
        Assert.False(NormalizadorLogradouro.Equivalentes(a, b));

    [Theory]
    [InlineData("Al. Santos", "Alameda Santos")]
    [InlineData("Est. Velha", "Estrada Velha")]
    [InlineData("PR 445", "Paraná 445")]
    public void Abreviacao_ambigua_nao_e_expandida_nunca_vira_falso_igual(string a, string b) =>
        Assert.False(NormalizadorLogradouro.Equivalentes(a, b));

    [Fact]
    public void Abreviacao_so_e_expandida_no_comeco()
    {
        Assert.Equal("RUA DR R SILVA", NormalizadorLogradouro.Normalizar("Rua Dr. R. Silva"));
        // "R" sozinho não é expandido (não há nome depois).
        Assert.Equal("R", NormalizadorLogradouro.Normalizar("R."));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".,-/")]
    public void Entrada_vazia_ou_sem_letra_vira_vazio_e_nunca_e_equivalente(string? entrada)
    {
        Assert.Equal(string.Empty, NormalizadorLogradouro.Normalizar(entrada));
        Assert.False(NormalizadorLogradouro.Equivalentes(entrada, entrada));
        Assert.False(NormalizadorLogradouro.Equivalentes(entrada, "Rua Barão"));
        Assert.False(NormalizadorLogradouro.Equivalentes("Rua Barão", entrada));
    }

    [Fact]
    public void Nao_altera_o_valor_original_e_e_a_mesma_normalizacao_da_duplicidade()
    {
        var original = "R. Barão";

        var normalizado = NormalizadorLogradouro.Normalizar(original);

        Assert.Equal("R. Barão", original);
        Assert.Equal("RUA BARAO", normalizado);
        Assert.Equal(DuplicidadeEndereco.Logradouro(original), normalizado);
    }

    [Fact]
    public void E_deterministica()
    {
        var resultados = Enumerable.Range(0, 50).Select(_ => NormalizadorLogradouro.Normalizar("Av. Pres. Tancredo Neves")).Distinct();
        Assert.Equal("AVENIDA PRES TANCREDO NEVES", Assert.Single(resultados));
    }
}
