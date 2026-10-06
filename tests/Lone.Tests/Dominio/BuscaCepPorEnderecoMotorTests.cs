using Lone.Domain.Enderecos.ConferenciaCep;

namespace Lone.Tests.Dominio;

/// <summary>
/// Checkpoint D, domínio: <see cref="MotorCep.BuscarPorEndereco"/> — o usuário não sabe o CEP. É o mesmo filtro dos casos 4
/// a 6 (sem duplicar regra): busca candidatos, nunca descobre "o CEP certo". Nenhum é escolhido, sugerido como melhor ou
/// inventado; número indeterminado não elimina; cada candidato explica por que ficou (componentes, nunca divergentes).
/// </summary>
public class BuscaCepPorEnderecoMotorTests
{
    private static EnderecoConferenciaCep Endereco(string? numero = "150", string? bairro = "Centro", string? cep = null,
                                                   string? logradouro = "R. Barao") =>
        new(cep, logradouro, numero, bairro, "Curvelo", "MG", "3120904");

    private static RegistroCep Registro(string cep, string? complemento = "até 999/1000", string? bairro = "Centro",
                                        string logradouro = "Rua Barão", string? unidade = null) =>
        new(cep, logradouro, complemento, bairro, "Curvelo", "MG", "3120904", unidade);

    private static DecisaoCep Buscar(EnderecoConferenciaCep endereco, params RegistroCep[] registros) =>
        MotorCep.BuscarPorEndereco(endereco, RespostaBuscaEndereco.Realizada(registros, CepFonte.ViaCep));

    private static SituacaoComponenteCep Numero(CandidatoCep c) => c.Componentes.Single(x => x.Componente == ComponenteCep.Numero).Situacao;

    // ---- Zero, um, vários ----

    [Fact]
    public void Zero_candidatos_nao_inventa_cep()
    {
        var d = Buscar(Endereco(), Registro("35790001", complemento: "de 1001/1002 ao fim"));

        Assert.Equal(ResultadoDecisaoCep.NenhumCandidato, d.Resultado);
        Assert.Empty(d.Candidatos);
        Assert.Null(d.CepSugerido);
        Assert.Equal("", d.CepInformado);
        Assert.Equal(["Não encontramos um CEP compatível com os dados informados."], d.Motivos);
    }

    [Fact]
    public void Busca_sem_nenhum_registro_tambem_e_nenhum_candidato()
    {
        var d = Buscar(Endereco());

        Assert.Equal(ResultadoDecisaoCep.NenhumCandidato, d.Resultado);
        Assert.Empty(d.Candidatos);
    }

    [Fact]
    public void Um_candidato_e_so_um_candidato_nao_uma_sugestao_nem_certeza()
    {
        var d = Buscar(Endereco(), Registro("35790001"));

        Assert.Equal(ResultadoDecisaoCep.UmCandidato, d.Resultado);
        Assert.Equal("35790001", Assert.Single(d.Candidatos).Cep);
        Assert.Null(d.CepSugerido);                                     // nada é "o sugerido": o usuário escolhe
        Assert.Contains("Confira antes de usar", Assert.Single(d.Motivos));
    }

    [Fact]
    public void Varios_candidatos_vem_todos_em_ordem_de_cep_sem_escolha()
    {
        var d = Buscar(Endereco(), Registro("35790007", complemento: null), Registro("35790001"), Registro("35790003"));

        Assert.Equal(ResultadoDecisaoCep.VariosCandidatos, d.Resultado);
        Assert.Equal(["35790001", "35790003", "35790007"], d.Candidatos.Select(c => c.Cep));
        Assert.Null(d.CepSugerido);
    }

    [Fact]
    public void A_ordem_da_resposta_nao_muda_a_decisao()
    {
        var registros = new[] { Registro("35790007"), Registro("35790001"), Registro("35790003") };

        var a = Buscar(Endereco(), registros);
        var b = Buscar(Endereco(), registros.Reverse().ToArray());

        Assert.Equal(a.Candidatos, b.Candidatos);
        Assert.Equal(a.Motivos, b.Motivos);
    }

    [Fact]
    public void O_cep_digitado_e_ignorado_na_busca_sem_cep()
    {
        var d = Buscar(Endereco(cep: "35790001"), Registro("35790001"));

        Assert.Equal("35790001", Assert.Single(d.Candidatos).Cep); // não é excluído como na conferência (não há CEP conferido)
    }

    // ---- Número: prédio, faixa, lado, indeterminado ----

    [Fact]
    public void Predio_especifico_so_e_candidato_para_o_numero_do_predio()
    {
        var registros = new[] { Registro("35790001"), Registro("35790911", complemento: "960", unidade: "Edifício Central") };

        var no150 = Buscar(Endereco(numero: "150"), registros);
        var no960 = Buscar(Endereco(numero: "960"), registros);

        Assert.Equal(["35790001"], no150.Candidatos.Select(c => c.Cep));
        Assert.Equal(["35790001", "35790911"], no960.Candidatos.Select(c => c.Cep));
        var predio = no960.CandidatosAvaliados.Single(c => c.Registro.Cep == "35790911");
        Assert.Equal(SituacaoComponenteCep.Confirmado, Numero(predio));
        Assert.Contains("prédio", predio.Componentes.Single(x => x.Componente == ComponenteCep.Numero).Motivo);
        Assert.Equal("Edifício Central", predio.Registro.Unidade);       // unidade só acompanha; não decide nada
    }

    [Theory]
    [InlineData("de 612 a 1510 - lado par", "1000", true)]
    [InlineData("de 612 a 1510 - lado par", "1001", false)]
    [InlineData("até 609 - lado ímpar", "609", true)]
    [InlineData("até 609 - lado ímpar", "608", false)]
    [InlineData("até 999/1000", "150", true)]
    [InlineData("até 999/1000", "1002", false)]
    public void Faixa_e_lado_par_ou_impar_mantem_ou_tiram_o_candidato(string faixa, string numero, bool fica)
    {
        var d = Buscar(Endereco(numero: numero), Registro("35790001", complemento: faixa));

        Assert.Equal(fica, d.Candidatos.Count == 1);
        if (fica) Assert.Equal(SituacaoComponenteCep.Confirmado, Numero(d.CandidatosAvaliados[0]));
    }

    [Theory]
    [InlineData("S/N")]
    [InlineData("KM 23")]
    public void Numero_indeterminado_nao_elimina_mas_explica_que_nao_foi_validado(string numero)
    {
        var d = Buscar(Endereco(numero: numero), Registro("35790001"), Registro("35790911", complemento: "960"));

        Assert.Equal(2, d.Candidatos.Count);
        Assert.All(d.CandidatosAvaliados, c => Assert.Equal(SituacaoComponenteCep.NaoValidavel, Numero(c)));
    }

    [Fact]
    public void Sem_numero_tambem_nao_elimina()
    {
        var d = Buscar(Endereco(numero: null), Registro("35790001"));

        Assert.Equal(SituacaoComponenteCep.NaoInformado, Numero(Assert.Single(d.CandidatosAvaliados)));
    }

    [Fact]
    public void Candidato_sem_faixa_fica_e_diz_que_a_fonte_nao_informa()
    {
        var d = Buscar(Endereco(), Registro("35790001", complemento: null));

        Assert.Equal(SituacaoComponenteCep.NaoInformado, Numero(Assert.Single(d.CandidatosAvaliados)));
    }

    // ---- Bairro (filtro existente, sem flexibilizar) ----

    [Fact]
    public void Bairro_compativel_fica_e_bairro_diferente_sai()
    {
        var d = Buscar(Endereco(bairro: "centro"), Registro("35790001", bairro: "CENTRO"), Registro("35790002", bairro: "Centro Histórico"));

        Assert.Equal(["35790001"], d.Candidatos.Select(c => c.Cep));
    }

    [Fact]
    public void Fonte_sem_bairro_nao_elimina_e_bairro_e_opcional()
    {
        Assert.Single(Buscar(Endereco(), Registro("35790001", bairro: null)).Candidatos);
        Assert.Single(Buscar(Endereco(bairro: null), Registro("35790001", bairro: "Centro")).Candidatos);
    }

    // ---- Fonte indisponível ----

    [Fact]
    public void Fonte_indisponivel_nunca_vira_nenhum_candidato()
    {
        var d = MotorCep.BuscarPorEndereco(Endereco(), RespostaBuscaEndereco.Indisponivel(CepFonte.ViaCep));

        Assert.Equal(ResultadoDecisaoCep.FonteIndisponivel, d.Resultado);
        Assert.Empty(d.Candidatos);
        Assert.Contains("Não foi possível consultar a fonte de CEP agora", Assert.Single(d.Motivos));
    }

    // ---- Componentes por candidato (mesma avaliação do filtro) ----

    [Fact]
    public void Candidato_totalmente_compativel_tem_os_quatro_componentes_confirmados()
    {
        var c = Assert.Single(Buscar(Endereco(), Registro("35790001")).CandidatosAvaliados);

        Assert.Equal([ComponenteCep.Uf, ComponenteCep.Municipio, ComponenteCep.Logradouro, ComponenteCep.Numero],
            c.Componentes.Select(x => x.Componente));
        Assert.All(c.Componentes, x => Assert.Equal(SituacaoComponenteCep.Confirmado, x.Situacao));
    }

    [Fact]
    public void Candidato_com_divergencia_real_nao_aparece_e_os_que_ficam_nunca_tem_componente_divergente()
    {
        var d = Buscar(Endereco(),
            Registro("35790001"),                                     // fica
            Registro("35790002", logradouro: "Rua Padre Corrêa"),     // outro logradouro: sai
            Registro("35790003", complemento: "de 1001/1002 ao fim"), // número fora: sai
            Registro("35790004") with { Uf = "BA" },                  // outra UF: sai
            Registro("35790005") with { CodigoMunicipioIbge = "3119401" }, // outro município: sai
            Registro("35790006", bairro: "Bela Vista"),               // outro bairro: sai
            Registro("35790007", complemento: "lote 5"),              // faixa não lida: fica (não validável)
            Registro("123"));                                         // CEP inválido: sai

        Assert.Equal(["35790001", "35790007"], d.Candidatos.Select(c => c.Cep));
        Assert.All(d.CandidatosAvaliados, c => Assert.DoesNotContain(c.Componentes, x => x.Situacao == SituacaoComponenteCep.Divergente));
        Assert.Equal(SituacaoComponenteCep.NaoValidavel, Numero(d.CandidatosAvaliados[1]));
    }

    [Fact]
    public void Candidatos_avaliados_sao_os_mesmos_candidatos_na_mesma_ordem()
    {
        var d = Buscar(Endereco(), Registro("35790003"), Registro("35790001"));

        Assert.Equal(d.Candidatos, d.CandidatosAvaliados.Select(c => c.Registro));
    }

    [Fact]
    public void Na_conferencia_os_candidatos_tambem_ganham_componentes_sem_mudar_a_decisao()
    {
        var d = MotorCep.Decidir(Endereco(cep: "35790000"), RespostaConsultaCep.NaoEncontrado(CepFonte.ViaCep),
            RespostaBuscaEndereco.Realizada([Registro("35790001"), Registro("35790000")], CepFonte.ViaCep));

        Assert.Equal(ResultadoDecisaoCep.UmCandidato, d.Resultado);   // o próprio CEP informado continua fora
        Assert.Equal("35790001", d.CepSugerido);
        Assert.Equal(4, Assert.Single(d.CandidatosAvaliados).Componentes.Count);
    }
}
