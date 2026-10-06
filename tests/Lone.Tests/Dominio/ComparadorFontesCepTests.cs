using Lone.Domain.Enderecos.ConferenciaCep;

namespace Lone.Tests.Dominio;

/// <summary>
/// Checkpoint G, domínio: a comparação entre duas fontes que responderam à MESMA pergunta (consulta pelo CEP). Só dados que
/// as duas informam; ausência não é divergência; indisponibilidade não é conflito; normalização existente (sem
/// aproximação); divergência nunca escolhe fonte.
/// </summary>
public class ComparadorFontesCepTests
{
    private static readonly RegistroCep ViaCep = new("35790000", "Rua Barão", "até 999/1000", "Centro", "Curvelo", "MG", "3120904");

    /// <summary>Como a BrasilAPI responde: sem código IBGE e sem faixa.</summary>
    private static readonly RegistroCep BrasilApi = new("35790000", "Rua Barão", null, "Centro", "Curvelo", "MG");

    private static ComparacaoFontesCep Comparar(RegistroCep a, RegistroCep b) =>
        ComparadorFontesCep.Comparar("35790000", RespostaConsultaCep.Encontrado(a, CepFonte.ViaCep), RespostaConsultaCep.Encontrado(b, CepFonte.BrasilApi));

    private static SituacaoComparacaoFontes Situacao(ComparacaoFontesCep c, ComponenteComparacaoFontes componente) =>
        c.Componentes.Single(x => x.Componente == componente).Situacao;

    [Fact]
    public void Duas_fontes_concordam_nos_dados_principais()
    {
        var c = Comparar(ViaCep, BrasilApi);

        Assert.Equal(ResultadoSegundaOpiniaoCep.Concordam, c.Resultado);
        Assert.Equal((CepFonte?)CepFonte.ViaCep, c.FontePrincipal);
        Assert.Equal((CepFonte?)CepFonte.BrasilApi, c.SegundaFonte);
        Assert.StartsWith("✓ ViaCEP e BrasilAPI concordam nos dados principais.", c.Mensagem);
        Assert.Equal(SituacaoComparacaoFontes.Concordam, Situacao(c, ComponenteComparacaoFontes.Uf));
        Assert.Equal(SituacaoComparacaoFontes.Concordam, Situacao(c, ComponenteComparacaoFontes.Municipio));
        Assert.Equal(SituacaoComparacaoFontes.Concordam, Situacao(c, ComponenteComparacaoFontes.Logradouro));
    }

    [Fact]
    public void Uma_fonte_sem_o_campo_e_nao_comparavel_nunca_divergencia()
    {
        var c = Comparar(ViaCep, BrasilApi); // BrasilAPI: sem IBGE e sem faixa

        Assert.Equal(SituacaoComparacaoFontes.NaoComparavel, Situacao(c, ComponenteComparacaoFontes.CodigoIbge));
        Assert.Equal(SituacaoComparacaoFontes.NaoComparavel, Situacao(c, ComponenteComparacaoFontes.Faixa));
        Assert.Contains("BrasilAPI não informa", c.Componentes.Single(x => x.Componente == ComponenteComparacaoFontes.CodigoIbge).Motivo);
        Assert.Equal(ResultadoSegundaOpiniaoCep.Concordam, c.Resultado);
        Assert.Contains("Alguns dados não puderam ser comparados", c.Mensagem);
    }

    [Fact]
    public void Uf_divergente()
    {
        var c = Comparar(ViaCep, BrasilApi with { Uf = "BA" });

        Assert.Equal(ResultadoSegundaOpiniaoCep.Divergem, c.Resultado);
        Assert.Equal(SituacaoComparacaoFontes.Divergem, Situacao(c, ComponenteComparacaoFontes.Uf));
        Assert.StartsWith("⚠ As fontes consultadas apresentam informações diferentes (UF).", c.Mensagem);
    }

    [Fact]
    public void Municipio_divergente()
    {
        var c = Comparar(ViaCep, BrasilApi with { Cidade = "Corinto" });

        Assert.Equal(ResultadoSegundaOpiniaoCep.Divergem, c.Resultado);
        Assert.Equal(SituacaoComparacaoFontes.Divergem, Situacao(c, ComponenteComparacaoFontes.Municipio));
        Assert.Contains("ViaCEP informa \"Curvelo\"; BrasilAPI informa \"Corinto\"", c.Componentes.Single(x => x.Componente == ComponenteComparacaoFontes.Municipio).Motivo);
    }

    [Fact]
    public void Codigo_ibge_divergente_quando_as_duas_informam()
    {
        var c = Comparar(ViaCep, ViaCep with { CodigoMunicipioIbge = "3120805" });

        Assert.Equal(ResultadoSegundaOpiniaoCep.Divergem, c.Resultado);
        Assert.Equal(SituacaoComparacaoFontes.Divergem, Situacao(c, ComponenteComparacaoFontes.CodigoIbge));
        Assert.Equal(SituacaoComparacaoFontes.Concordam, Situacao(c, ComponenteComparacaoFontes.Municipio)); // nome igual, código não
    }

    [Theory]
    [InlineData("R. Barao")]
    [InlineData("RUA BARÃO")]
    [InlineData("  Rua   Barão ")]
    public void Logradouro_equivalente_pela_normalizacao_existente(string outro)
    {
        var c = Comparar(ViaCep, BrasilApi with { Logradouro = outro });

        Assert.Equal(SituacaoComparacaoFontes.Concordam, Situacao(c, ComponenteComparacaoFontes.Logradouro));
        Assert.Equal(ResultadoSegundaOpiniaoCep.Concordam, c.Resultado);
    }

    [Theory]
    [InlineData("Rua Barão de Cocais")] // sem aproximação: parecido não é igual
    [InlineData("Avenida Barão")]
    public void Logradouro_realmente_divergente(string outro)
    {
        var c = Comparar(ViaCep, BrasilApi with { Logradouro = outro });

        Assert.Equal(SituacaoComparacaoFontes.Divergem, Situacao(c, ComponenteComparacaoFontes.Logradouro));
        Assert.Equal(ResultadoSegundaOpiniaoCep.Divergem, c.Resultado);
    }

    [Fact]
    public void So_uma_fonte_encontrou_o_cep_e_divergencia_de_existencia()
    {
        var c = ComparadorFontesCep.Comparar("35790000", RespostaConsultaCep.Encontrado(ViaCep, CepFonte.ViaCep),
            RespostaConsultaCep.NaoEncontrado(CepFonte.BrasilApi));

        Assert.Equal(ResultadoSegundaOpiniaoCep.Divergem, c.Resultado);
        Assert.Equal("ViaCEP encontrou o CEP; BrasilAPI não.", Assert.Single(c.Componentes).Motivo);
    }

    [Fact]
    public void As_duas_sem_o_cep_concordam_que_nao_existe()
    {
        var c = ComparadorFontesCep.Comparar("35790999", RespostaConsultaCep.NaoEncontrado(CepFonte.ViaCep),
            RespostaConsultaCep.NaoEncontrado(CepFonte.BrasilApi));

        Assert.Equal(ResultadoSegundaOpiniaoCep.Concordam, c.Resultado);
        Assert.Contains("não foi encontrado em nenhuma das duas", c.Mensagem);
    }

    [Fact]
    public void Segunda_fonte_indisponivel_nao_e_conflito()
    {
        var c = ComparadorFontesCep.Comparar("35790000", RespostaConsultaCep.Encontrado(ViaCep, CepFonte.ViaCep),
            RespostaConsultaCep.Indisponivel(CepFonte.BrasilApi));

        Assert.Equal(ResultadoSegundaOpiniaoCep.SegundaFonteIndisponivel, c.Resultado);
        Assert.Empty(c.Componentes);
        Assert.Equal("Não foi possível consultar BrasilAPI agora. Isso não é conflito: a conferência com ViaCEP continua valendo.", c.Mensagem);
    }

    [Fact]
    public void Fonte_principal_indisponivel_nao_compara_nada()
    {
        var c = ComparadorFontesCep.Comparar("35790000", RespostaConsultaCep.Indisponivel(), null);

        Assert.Equal(ResultadoSegundaOpiniaoCep.FontePrincipalIndisponivel, c.Resultado);
        Assert.Empty(c.Componentes);
    }

    [Fact]
    public void Sem_outra_fonte_configurada()
    {
        var c = ComparadorFontesCep.Comparar("35790000", RespostaConsultaCep.Encontrado(ViaCep, CepFonte.ViaCep), null);

        Assert.Equal(ResultadoSegundaOpiniaoCep.SemOutraFonte, c.Resultado);
    }

    [Fact]
    public void Sem_dado_principal_em_comum_e_inconclusiva()
    {
        var soCep = new RegistroCep("35790000", null, null, null, null, null);

        var c = Comparar(soCep, soCep with { Bairro = "Centro" });

        Assert.Equal(ResultadoSegundaOpiniaoCep.Inconclusiva, c.Resultado);
    }

    [Fact]
    public void Bairro_divergente_aparece_mas_e_so_da_comparacao_entre_fontes()
    {
        var c = Comparar(ViaCep, BrasilApi with { Bairro = "Vila Nova" });

        Assert.Equal(SituacaoComparacaoFontes.Divergem, Situacao(c, ComponenteComparacaoFontes.Bairro));
        Assert.Equal(ResultadoSegundaOpiniaoCep.Divergem, c.Resultado);
    }

    [Fact]
    public void Divergencia_nunca_escolhe_fonte_nem_pontua()
    {
        var c = Comparar(ViaCep, BrasilApi with { Uf = "BA", Cidade = "Salvador" });

        Assert.Contains("O Lone não escolhe qual está certa", c.Mensagem);
        Assert.Contains("a conferência continua sendo a feita com ViaCEP", c.Mensagem);
        foreach (var proibido in new[] { "venc", "%", "pontua", "score", "maioria", "mais confiável", "provável" })
        {
            Assert.DoesNotContain(proibido, c.Mensagem, StringComparison.OrdinalIgnoreCase);
            Assert.All(c.Componentes, x => Assert.DoesNotContain(proibido, x.Motivo, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void Os_valores_vem_como_cada_fonte_informou()
    {
        var c = Comparar(ViaCep, BrasilApi with { Logradouro = "R. Barao" });

        var l = c.Componentes.Single(x => x.Componente == ComponenteComparacaoFontes.Logradouro);
        Assert.Equal(("Rua Barão", "R. Barao"), (l.ValorPrincipal, l.ValorSegunda)); // nada é reescrito
    }
}
