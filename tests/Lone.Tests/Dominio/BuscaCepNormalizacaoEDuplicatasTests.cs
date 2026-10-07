using Lone.Domain.Enderecos.ConferenciaCep;

namespace Lone.Tests.Dominio;

/// <summary>
/// Busca de CEP por endereço (D1–D5), domínio: a normalização da comparação de logradouro (acentos, caixa, espaços,
/// pontuação e caracteres inesperados não contam; nada de aproximação: outro nome é outro logradouro), a deduplicação
/// determinística (o mesmo CEP repetido pela fonte aparece uma vez; CEPs diferentes nunca são escondidos) e os registros
/// hostis (UF inválida, CEP inválido, textos enormes) que nunca viram candidato inventado.
/// </summary>
public class BuscaCepNormalizacaoEDuplicatasTests
{
    private static EnderecoConferenciaCep Endereco(string logradouro = "R. Barao", string? numero = "150") =>
        new(null, logradouro, numero, "Centro", "Curvelo", "MG", "3120904");

    private static RegistroCep Registro(string cep, string logradouro = "Rua Barão", string? complemento = "até 999/1000",
                                        string? uf = "MG", string? bairro = "Centro") =>
        new(cep, logradouro, complemento, bairro, "Curvelo", uf, "3120904");

    private static DecisaoCep Buscar(EnderecoConferenciaCep endereco, params RegistroCep[] registros) =>
        MotorCep.BuscarPorEndereco(endereco, RespostaBuscaEndereco.Realizada(registros, CepFonte.ViaCep));

    // ---- Normalização ----

    [Theory]
    [InlineData("Rua Barão")]          // com acento
    [InlineData("Rua Barao")]          // sem acento
    [InlineData("RUA BARÃO")]          // caixa alta
    [InlineData("rua barão")]          // caixa baixa
    [InlineData("  Rua   Barão  ")]    // espaços
    [InlineData("Rua Barão.")]         // pontuação
    [InlineData("R. Barão")]           // abreviação inequívoca do tipo
    [InlineData("Rua - Barão #")]      // caracteres inesperados
    public void Variacoes_de_escrita_do_mesmo_logradouro_encontram_o_candidato(string digitado)
    {
        var d = Buscar(Endereco(digitado), Registro("35790001"));

        Assert.Equal(ResultadoDecisaoCep.UmCandidato, d.Resultado);
        Assert.Equal("35790001", Assert.Single(d.Candidatos).Cep);
    }

    [Theory]
    [InlineData("Rua Barata")]         // parecido não é igual: nada de aproximação
    [InlineData("Rua Barão de Cocais")]
    [InlineData("Avenida Barão")]      // outro tipo de logradouro
    [InlineData("Rua Barãoo")]
    public void Logradouro_diferente_nao_vira_candidato_por_semelhanca(string digitado)
    {
        var d = Buscar(Endereco(digitado), Registro("35790001"));

        Assert.Equal(ResultadoDecisaoCep.NenhumCandidato, d.Resultado);
        Assert.Empty(d.Candidatos);
    }

    // ---- Duplicatas ----

    [Fact]
    public void O_mesmo_cep_repetido_aparece_uma_vez_so()
    {
        var d = Buscar(Endereco(), Registro("35790001"), Registro("35790-001"), Registro("35790001"));

        Assert.Equal(ResultadoDecisaoCep.UmCandidato, d.Resultado);
        Assert.Equal("35790001", Assert.Single(d.Candidatos).Cep);
    }

    [Fact]
    public void Ceps_diferentes_nunca_sao_escondidos_pela_deduplicacao()
    {
        var d = Buscar(Endereco(), Registro("35790002"), Registro("35790001"), Registro("35790002"), Registro("35790003"));

        Assert.Equal(ResultadoDecisaoCep.VariosCandidatos, d.Resultado);
        Assert.Equal(["35790001", "35790002", "35790003"], d.Candidatos.Select(c => c.Cep));
    }

    [Fact]
    public void Deduplicacao_e_deterministica_qualquer_que_seja_a_ordem_da_resposta()
    {
        var a = Registro("35790001", complemento: "até 999/1000");
        var b = Registro("35790001", complemento: null);

        var ab = Buscar(Endereco(), a, b);
        var ba = Buscar(Endereco(), b, a);

        Assert.Equal(Assert.Single(ab.Candidatos), Assert.Single(ba.Candidatos));
        Assert.Equal("até 999/1000", Assert.Single(ab.Candidatos).Complemento); // sempre o mesmo, pela ordem dos dados
    }

    // ---- Registros hostis ----

    [Theory]
    [InlineData("XX")]
    [InlineData("SP")]
    public void Registro_de_outra_uf_ou_uf_invalida_nao_vira_candidato(string uf)
    {
        var d = Buscar(Endereco(), Registro("35790001", uf: uf));

        Assert.Equal(ResultadoDecisaoCep.NenhumCandidato, d.Resultado);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("00000000")]
    [InlineData("ABCDEFGH")]
    public void Registro_com_cep_invalido_nao_vira_candidato(string cep)
    {
        var d = Buscar(Endereco(), Registro(cep), Registro("35790001"));

        Assert.Equal("35790001", Assert.Single(d.Candidatos).Cep);
    }

    [Fact]
    public void Textos_enormes_nao_derrubam_o_filtro_nem_viram_candidato()
    {
        var enorme = new string('A', 100_000);

        var d = Buscar(Endereco(enorme), Registro("35790001", logradouro: enorme + "B", complemento: enorme, bairro: enorme));

        Assert.Equal(ResultadoDecisaoCep.NenhumCandidato, d.Resultado);
    }
}
