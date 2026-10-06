using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Integracoes;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enderecos.ConferenciaCep;

namespace Lone.Tests.Cliente;

/// <summary>
/// Checkpoint G, ficha: "Consultar outra fonte" é discreto e só aparece com uma conferência pelo CEP na tela; mostrar a
/// segunda opinião não muda campo nenhum nem a conferência; resposta de outro CEP é descartada; nada é aplicado.
/// </summary>
public class SegundaOpiniaoFichaTests
{
    private static EnderecoFormulario Endereco() => EnderecoFormulario.De(new EnderecoDto
    {
        Id = Guid.NewGuid(), Cep = "35790000", Logradouro = "R. Barao", Numero = "150", Bairro = "Centro", MunicipioId = 3120904,
        Cidade = "Curvelo", Uf = "MG"
    });

    private static DecisaoCepDto Conferido() => new()
    {
        Resultado = ResultadoDecisaoCep.Conferido, CepInformado = "35790000", Fonte = CepFonte.ViaCep,
        Motivos = ["CEP conferido: corresponde ao endereço."],
        Componentes = [new ComponenteCepDto { Componente = ComponenteCep.Cep, Situacao = SituacaoComponenteCep.Confirmado, Motivo = "existe" }]
    };

    private static SegundaOpiniaoCepDto Divergem() => new()
    {
        Cep = "35790000", Resultado = ResultadoSegundaOpiniaoCep.Divergem, FontePrincipal = CepFonte.ViaCep, SegundaFonte = CepFonte.BrasilApi,
        Mensagem = "⚠ As fontes consultadas apresentam informações diferentes (município). O Lone não escolhe qual está certa: a conferência continua sendo a feita com ViaCEP. Confira o endereço com o cliente ou com os Correios.",
        Componentes =
        [
            new ComparacaoComponenteFontesDto
            {
                Componente = ComponenteComparacaoFontes.Municipio, Situacao = SituacaoComparacaoFontes.Divergem, ValorPrincipal = "Curvelo",
                ValorSegunda = "Corinto", Motivo = "Município: ViaCEP informa \"Curvelo\"; BrasilAPI informa \"Corinto\"."
            }
        ]
    };

    [Fact]
    public void Link_so_aparece_com_conferencia_pelo_cep_na_tela()
    {
        var e = Endereco();
        Assert.False(e.PodeConsultarOutraFonte);

        e.MostrarConferencia(Conferido());

        Assert.True(e.PodeConsultarOutraFonte);
    }

    [Fact]
    public void Fonte_indisponivel_nao_oferece_segunda_opiniao()
    {
        var e = Endereco();

        e.MostrarConferencia(new DecisaoCepDto { Resultado = ResultadoDecisaoCep.FonteIndisponivel, CepInformado = "35790000", Motivos = ["fora do ar"] });

        Assert.False(e.PodeConsultarOutraFonte);
    }

    [Fact]
    public void Mostrar_a_divergencia_nao_muda_nenhum_campo_nem_a_conferencia()
    {
        var e = Endereco();
        e.MostrarConferencia(Conferido());
        var localizacao = e.Localizacao();
        var antes = (e.TextoConferenciaCep, e.GravidadeConferenciaCep);

        Assert.True(e.MostrarSegundaOpiniao(Divergem(), "35790000"));

        Assert.Equal(localizacao, e.Localizacao());          // nenhum campo do endereço muda
        Assert.Equal(antes, (e.TextoConferenciaCep, e.GravidadeConferenciaCep)); // nem a conferência mostrada
        Assert.True(e.TemSegundaOpiniao);
        Assert.True(e.SegundaOpiniaoDivergente);
        Assert.False(e.PodeConsultarOutraFonte);                 // já mostrada
        Assert.Equal("≠", Assert.Single(e.ComparacaoFontes).Icone);
        Assert.Empty(e.CandidatosCep);                            // nada para "usar"
        Assert.Null(e.ParaDto(0).SugestaoCepAplicada);
    }

    [Fact]
    public void Resposta_de_outro_cep_e_descartada()
    {
        var e = Endereco();
        e.MostrarConferencia(Conferido());
        e.Cep = "35790-001"; // mudou durante a consulta

        Assert.False(e.MostrarSegundaOpiniao(Divergem(), "35790000"));
        Assert.False(e.TemSegundaOpiniao);
    }

    [Fact]
    public void Nova_conferencia_ou_mudanca_no_endereco_tira_a_segunda_opiniao()
    {
        var e = Endereco();
        e.MostrarConferencia(Conferido());
        e.MostrarSegundaOpiniao(Divergem(), "35790000");

        e.Numero = "151"; // dado conferível mudou: a conferência (e a segunda opinião) saem da tela

        Assert.False(e.TemSegundaOpiniao);
        Assert.Empty(e.ComparacaoFontes);
    }
}
