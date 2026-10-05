using Lone.Domain.Entidades;
using Lone.Domain.Enderecos;
using Lone.Domain.Enderecos.ConferenciaCep;
using Lone.Domain.Enums;

namespace Lone.Tests.Dominio;

/// <summary>
/// F1 do motor de endereçamento: a decisão do CEP em seis casos (conferido, divergente, não encontrado, um, vários e
/// nenhum candidato) e a fonte indisponível. Só domínio: os dados chegam prontos, sem provedor, sem internet e sem banco.
/// O motor nunca altera o endereço, nunca escolhe entre candidatos e nunca inventa CEP.
/// </summary>
public class MotorCepTests
{
    private const string CepCurvelo = "35790000";

    private static EnderecoConferenciaCep Endereco(string? cep = "35790-000", string? logradouro = "R. Barao", string? numero = "150",
                                                   string? bairro = "Centro", string? cidade = "Curvelo", string? uf = "MG",
                                                   string? ibge = "3120904") =>
        new(cep, logradouro, numero, bairro, cidade, uf, ibge);

    private static RegistroCep Registro(string cep = CepCurvelo, string? logradouro = "Rua Barão", string? complemento = "até 999/1000",
                                        string? bairro = "Centro", string? cidade = "Curvelo", string? uf = "MG",
                                        string? ibge = "3120904") =>
        new(cep, logradouro, complemento, bairro, cidade, uf, ibge);

    private static RespostaConsultaCep Encontrado(RegistroCep? registro = null) =>
        RespostaConsultaCep.Encontrado(registro ?? Registro(), CepFonte.ViaCep);

    private static readonly RespostaConsultaCep Inexistente = RespostaConsultaCep.NaoEncontrado(CepFonte.ViaCep);

    private static RespostaBuscaEndereco Busca(params RegistroCep[] registros) => RespostaBuscaEndereco.Realizada(registros, CepFonte.ViaCep);

    // ---- Caso 1: conferido ----

    [Fact]
    public void Caso1_cep_que_corresponde_ao_endereco_e_conferido()
    {
        var decisao = MotorCep.Decidir(Endereco(), Encontrado());

        Assert.Equal(ResultadoDecisaoCep.Conferido, decisao.Resultado);
        Assert.Equal(CepSituacao.Conferido, decisao.Situacao);
        Assert.Equal(CepFonte.ViaCep, decisao.Fonte);
        Assert.Equal(CepCurvelo, decisao.CepInformado);
        Assert.Null(decisao.CepSugerido);
        Assert.Empty(decisao.Candidatos);
        Assert.False(decisao.DeveBuscarPorEndereco);
        Assert.Contains("CEP conferido: corresponde ao endereço.", decisao.Motivos);
    }

    [Fact]
    public void Caso1_cep_geral_da_cidade_sem_logradouro_e_conferido_sem_olhar_faixa()
    {
        var geral = Registro(logradouro: null, complemento: null, bairro: null);

        var decisao = MotorCep.Decidir(Endereco(numero: "99999"), Encontrado(geral));

        Assert.Equal(ResultadoDecisaoCep.Conferido, decisao.Resultado);
    }

    [Fact]
    public void Caso1_municipio_comparado_pelo_nome_quando_falta_o_codigo_do_ibge()
    {
        var decisao = MotorCep.Decidir(Endereco(ibge: null, cidade: "CURVELO"), Encontrado(Registro(ibge: null)));

        Assert.Equal(ResultadoDecisaoCep.Conferido, decisao.Resultado);
    }

    [Fact]
    public void Caso1_numero_sem_parte_legivel_nao_diverge_mas_fica_registrado()
    {
        var decisao = MotorCep.Decidir(Endereco(numero: "S/N"), Encontrado());

        Assert.Equal(ResultadoDecisaoCep.Conferido, decisao.Resultado);
        Assert.Contains(decisao.Motivos, m => m.Contains("não tem número legível", StringComparison.Ordinal));
    }

    [Fact]
    public void Caso1_faixa_nao_interpretada_nao_diverge_mas_fica_registrada()
    {
        var decisao = MotorCep.Decidir(Endereco(), Encontrado(Registro(complemento: "apto 101")));

        Assert.Equal(ResultadoDecisaoCep.Conferido, decisao.Resultado);
        Assert.Contains(decisao.Motivos, m => m.Contains("não foi interpretada", StringComparison.Ordinal));
    }

    // ---- Caso 2: divergente ----

    [Fact]
    public void Caso2_cep_de_outro_logradouro_e_divergente()
    {
        var decisao = MotorCep.Decidir(Endereco(), Encontrado(Registro(logradouro: "Rua Padre Corrêa")));

        Assert.Equal(ResultadoDecisaoCep.Divergente, decisao.Resultado);
        Assert.Equal(CepSituacao.Divergente, decisao.Situacao);
        Assert.Contains("O CEP informado corresponde a outro logradouro (Rua Padre Corrêa).", decisao.Motivos);
        Assert.Equal("Rua Padre Corrêa", decisao.RegistroConsultado!.Logradouro);
    }

    [Theory]
    [InlineData("1001")]
    [InlineData("2000")]
    public void Caso2_numero_fora_da_faixa_e_divergente(string numero)
    {
        var decisao = MotorCep.Decidir(Endereco(numero: numero), Encontrado());

        Assert.Equal(ResultadoDecisaoCep.Divergente, decisao.Resultado);
        Assert.Contains("O número informado é incompatível com a faixa do CEP (até 999/1000).", decisao.Motivos);
    }

    [Fact]
    public void Caso2_numero_do_outro_lado_da_rua_e_divergente()
    {
        var decisao = MotorCep.Decidir(Endereco(numero: "150"), Encontrado(Registro(complemento: "lado ímpar")));

        Assert.Equal(ResultadoDecisaoCep.Divergente, decisao.Resultado);
    }

    [Fact]
    public void Caso2_cep_de_outro_municipio_ou_uf_e_divergente()
    {
        var outroMunicipio = MotorCep.Decidir(Endereco(), Encontrado(Registro(cidade: "Corinto", ibge: "3119401")));
        var outraUf = MotorCep.Decidir(Endereco(), Encontrado(Registro(uf: "BA")));

        Assert.Equal(ResultadoDecisaoCep.Divergente, outroMunicipio.Resultado);
        Assert.Contains("O CEP informado é de outro município (Corinto).", outroMunicipio.Motivos);
        Assert.Equal(ResultadoDecisaoCep.Divergente, outraUf.Resultado);
        Assert.Contains("O CEP informado é de outra UF (BA).", outraUf.Motivos);
    }

    [Fact]
    public void Caso2_o_bairro_nao_entra_na_conferencia_do_cep_informado()
    {
        var decisao = MotorCep.Decidir(Endereco(bairro: "Centro"), Encontrado(Registro(bairro: "Bela Vista")));

        Assert.Equal(ResultadoDecisaoCep.Conferido, decisao.Resultado);
    }

    // ---- Caso 3: não encontrado ----

    [Fact]
    public void Caso3_cep_inexistente_sem_busca_pede_busca_pelo_endereco()
    {
        var decisao = MotorCep.Decidir(Endereco(), Inexistente);

        Assert.Equal(ResultadoDecisaoCep.NaoEncontrado, decisao.Resultado);
        Assert.Equal(CepSituacao.NaoEncontrado, decisao.Situacao);
        Assert.True(decisao.DeveBuscarPorEndereco);
        Assert.Null(decisao.CepSugerido);
        Assert.Empty(decisao.Candidatos);
        Assert.Contains("CEP informado não encontrado.", decisao.Motivos);
    }

    [Fact]
    public void Caso3_cep_inexistente_com_busca_indisponivel_continua_nao_encontrado_sem_candidato()
    {
        var decisao = MotorCep.Decidir(Endereco(), Inexistente, RespostaBuscaEndereco.Indisponivel(CepFonte.ViaCep));

        Assert.Equal(ResultadoDecisaoCep.NaoEncontrado, decisao.Resultado);
        Assert.True(decisao.DeveBuscarPorEndereco);
        Assert.Null(decisao.CepSugerido);
        Assert.Empty(decisao.Candidatos);
    }

    // ---- Caso 4: um candidato ----

    [Fact]
    public void Caso4_um_unico_candidato_compativel_vira_sugestao_pendente_de_decisao()
    {
        var candidato = Registro(cep: "35790001");
        var busca = Busca(candidato,
            Registro(cep: "35790002", logradouro: "Rua Barão", complemento: "de 1001/1002 ao fim"),  // fora da faixa
            Registro(cep: "35790003", logradouro: "Rua Outra"));                                      // outro logradouro

        var decisao = MotorCep.Decidir(Endereco(), Inexistente, busca);

        Assert.Equal(ResultadoDecisaoCep.UmCandidato, decisao.Resultado);
        Assert.Equal(CepSituacao.PendenteDeDecisao, decisao.Situacao);
        Assert.Equal("35790001", decisao.CepSugerido);
        Assert.Equal([candidato], decisao.Candidatos);
        Assert.Equal(CepFonte.ViaCep, decisao.Fonte);
        Assert.Contains("CEP informado não encontrado; foi localizado um único CEP compatível.", decisao.Motivos);
        Assert.Contains("Sugestão: 35790-000 → 35790-001.", decisao.Motivos);
        Assert.Equal(CepCurvelo, decisao.CepInformado);
    }

    [Fact]
    public void Caso4_candidato_de_outro_bairro_ou_municipio_e_descartado()
    {
        var busca = Busca(Registro(cep: "35790001"), Registro(cep: "35790004", bairro: "Bela Vista"),
            Registro(cep: "39400000", cidade: "Montes Claros", ibge: "3143302"));

        var decisao = MotorCep.Decidir(Endereco(), Inexistente, busca);

        Assert.Equal(ResultadoDecisaoCep.UmCandidato, decisao.Resultado);
        Assert.Equal("35790001", decisao.CepSugerido);
    }

    [Fact]
    public void Caso4_cep_repetido_na_resposta_conta_uma_vez()
    {
        var decisao = MotorCep.Decidir(Endereco(), Inexistente, Busca(Registro(cep: "35790-001"), Registro(cep: "35790001")));

        Assert.Equal(ResultadoDecisaoCep.UmCandidato, decisao.Resultado);
        Assert.Single(decisao.Candidatos);
    }

    // ---- Caso 5: vários candidatos ----

    [Fact]
    public void Caso5_varios_candidatos_vao_para_escolha_em_ordem_de_cep()
    {
        var a = Registro(cep: "35790009", complemento: null);
        var b = Registro(cep: "35790001", complemento: "até 999/1000");

        var decisao = MotorCep.Decidir(Endereco(), Inexistente, Busca(a, b));

        Assert.Equal(ResultadoDecisaoCep.VariosCandidatos, decisao.Resultado);
        Assert.Equal(CepSituacao.MultiplosCandidatos, decisao.Situacao);
        Assert.Equal(["35790001", "35790009"], decisao.Candidatos.Select(c => c.Cep));
        Assert.Contains("CEP informado não encontrado; foram localizados 2 CEPs compatíveis. Escolha um.", decisao.Motivos);
    }

    [Fact]
    public void Caso5_sem_numero_legivel_nenhuma_faixa_elimina_candidato()
    {
        var decisao = MotorCep.Decidir(Endereco(numero: "S/N"), Inexistente,
            Busca(Registro(cep: "35790001", complemento: "até 999/1000"), Registro(cep: "35790002", complemento: "de 1001/1002 ao fim")));

        Assert.Equal(ResultadoDecisaoCep.VariosCandidatos, decisao.Resultado);
        Assert.Equal(2, decisao.Candidatos.Count);
    }

    // ---- Caso 6: nenhum candidato ----

    [Fact]
    public void Caso6_busca_sem_resultado_e_nao_localizado()
    {
        var decisao = MotorCep.Decidir(Endereco(), Inexistente, Busca());

        Assert.Equal(ResultadoDecisaoCep.NenhumCandidato, decisao.Resultado);
        Assert.Equal(CepSituacao.NaoEncontrado, decisao.Situacao);
        Assert.Contains("CEP não localizado: nenhum CEP compatível com o endereço foi encontrado.", decisao.Motivos);
    }

    [Fact]
    public void Caso6_busca_so_com_incompativeis_e_nao_localizado()
    {
        var busca = Busca(Registro(cep: "35790002", complemento: "de 1001/1002 ao fim"), Registro(cep: "35790003", logradouro: "Rua Outra"),
            Registro(cep: "123", logradouro: "Rua Barão"), Registro(cep: CepCurvelo));

        var decisao = MotorCep.Decidir(Endereco(), Inexistente, busca);

        Assert.Equal(ResultadoDecisaoCep.NenhumCandidato, decisao.Resultado);
        Assert.Empty(decisao.Candidatos);
    }

    // ---- Fonte indisponível ----

    [Fact]
    public void Fonte_indisponivel_nao_conclui_nada_sobre_o_endereco()
    {
        var decisao = MotorCep.Decidir(Endereco(), RespostaConsultaCep.Indisponivel(CepFonte.ViaCep), Busca(Registro(cep: "35790001")));

        Assert.Equal(ResultadoDecisaoCep.FonteIndisponivel, decisao.Resultado);
        Assert.Equal(CepSituacao.FonteIndisponivel, decisao.Situacao);
        Assert.NotEqual(CepSituacao.Divergente, decisao.Situacao);
        Assert.NotEqual(CepSituacao.NaoEncontrado, decisao.Situacao);
        Assert.Null(decisao.CepSugerido);
        Assert.Empty(decisao.Candidatos);
        Assert.False(decisao.DeveBuscarPorEndereco);
        Assert.Equal(["Não foi possível conferir o CEP agora (serviço indisponível). Você pode salvar e conferir depois."], decisao.Motivos);
    }

    // ---- Segurança ----

    [Fact]
    public void Divergente_nao_corrige_nem_sugere()
    {
        var decisao = MotorCep.Decidir(Endereco(numero: "1500"), Encontrado(), Busca(Registro(cep: "35790002", complemento: "de 1001/1002 ao fim")));

        Assert.Equal(ResultadoDecisaoCep.Divergente, decisao.Resultado);
        Assert.Null(decisao.CepSugerido);
        Assert.Empty(decisao.Candidatos);
        Assert.Equal(CepCurvelo, decisao.CepInformado);
    }

    [Fact]
    public void Varios_candidatos_nao_escolhem_nenhum()
    {
        var decisao = MotorCep.Decidir(Endereco(), Inexistente,
            Busca(Registro(cep: "35790001"), Registro(cep: "35790005"), Registro(cep: "35790007")));

        Assert.Equal(ResultadoDecisaoCep.VariosCandidatos, decisao.Resultado);
        Assert.Null(decisao.CepSugerido);
        Assert.Equal(3, decisao.Candidatos.Count);
    }

    [Fact]
    public void Nenhum_candidato_nao_inventa_cep()
    {
        var decisao = MotorCep.Decidir(Endereco(), Inexistente, Busca(Registro(cep: "35790003", logradouro: "Rua Outra")));

        Assert.Null(decisao.CepSugerido);
        Assert.Empty(decisao.Candidatos);
        Assert.Null(decisao.RegistroConsultado);
        Assert.Equal(CepCurvelo, decisao.CepInformado);
    }

    [Fact]
    public void Nenhum_resultado_devolve_situacao_corrigido()
    {
        var decisoes = new[]
        {
            MotorCep.Decidir(Endereco(), Encontrado()),
            MotorCep.Decidir(Endereco(), Encontrado(Registro(logradouro: "Rua Outra"))),
            MotorCep.Decidir(Endereco(), Inexistente),
            MotorCep.Decidir(Endereco(), Inexistente, Busca(Registro(cep: "35790001"))),
            MotorCep.Decidir(Endereco(), Inexistente, Busca(Registro(cep: "35790001"), Registro(cep: "35790005"))),
            MotorCep.Decidir(Endereco(), Inexistente, Busca()),
            MotorCep.Decidir(Endereco(), RespostaConsultaCep.Indisponivel())
        };

        Assert.Equal(Enum.GetValues<ResultadoDecisaoCep>().OrderBy(r => r), decisoes.Select(d => d.Resultado).OrderBy(r => r));
        Assert.DoesNotContain(decisoes, d => d.Situacao == CepSituacao.Corrigido);
    }

    [Fact]
    public void O_motor_nao_altera_a_entidade_de_endereco()
    {
        var entidade = new PessoaEndereco
        {
            Id = Guid.NewGuid(), Cep = "35790000", Logradouro = "R. Barao", Numero = "150", Bairro = "Centro", MunicipioId = 3120904,
            Cidade = "Curvelo", Uf = "MG", CodigoMunicipioIbge = "3120904", CodigoPais = PessoaEndereco.CodigoPaisBrasil, Pais = "Brasil",
            Finalidades = FinalidadeEndereco.Fiscal | FinalidadeEndereco.Entrega, Ordem = 0, Ativo = true
        };
        var antes = DuplicidadeEndereco.Chave(entidade);
        var endereco = EnderecoConferenciaCep.De(entidade);

        MotorCep.Decidir(endereco, Inexistente, Busca(Registro(cep: "35790001")));
        MotorCep.Decidir(endereco, Encontrado(Registro(logradouro: "Rua Outra")));
        MotorCep.Decidir(endereco, Inexistente, Busca(Registro(cep: "35790001"), Registro(cep: "35790005")));

        Assert.Equal(antes, DuplicidadeEndereco.Chave(entidade));
        Assert.Equal("35790000", entidade.Cep);
        Assert.Equal("MG", entidade.Uf);
        Assert.Equal("Curvelo", entidade.Cidade);
        Assert.Equal(3120904, entidade.MunicipioId);
        Assert.Equal("Centro", entidade.Bairro);
        Assert.Equal("R. Barao", entidade.Logradouro);
        Assert.Equal(FinalidadeEndereco.Fiscal | FinalidadeEndereco.Entrega, entidade.Finalidades);
        Assert.Equal(0, entidade.Ordem);
        Assert.True(entidade.Ativo);
        Assert.Equal("35790000", endereco.Cep);
    }

    [Fact]
    public void A_copia_do_endereco_le_a_entidade_e_recusa_exterior()
    {
        var brasil = new PessoaEndereco { Cep = "35790000", Logradouro = "Rua A", MunicipioId = 3120904, Cidade = "Curvelo", Uf = "MG" };
        var exterior = new PessoaEndereco { Cep = null, Logradouro = "Main St", Cidade = "Miami", Uf = "EX", CodigoPais = "2496", Pais = "Estados Unidos" };

        Assert.Equal(new EnderecoConferenciaCep("35790000", "Rua A", null, null, "Curvelo", "MG", "3120904"), EnderecoConferenciaCep.De(brasil));
        Assert.Throws<ArgumentException>(() => EnderecoConferenciaCep.De(exterior));
    }

    // ---- Contrato de entrada ----

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("3579")]
    [InlineData("00000000")]
    public void Cep_invalido_no_endereco_e_erro_de_uso(string? cep) =>
        Assert.Throws<ArgumentException>(() => MotorCep.Decidir(Endereco(cep: cep), Encontrado()));

    [Fact]
    public void Consulta_de_outro_cep_e_erro_de_uso() =>
        Assert.Throws<ArgumentException>(() => MotorCep.Decidir(Endereco(), Encontrado(Registro(cep: "35790001"))));

    [Fact]
    public void Entradas_nulas_sao_erro_de_uso()
    {
        Assert.Throws<ArgumentNullException>(() => MotorCep.Decidir(null!, Encontrado()));
        Assert.Throws<ArgumentNullException>(() => MotorCep.Decidir(Endereco(), null!));
        Assert.Throws<ArgumentNullException>(() => RespostaConsultaCep.Encontrado(null!, CepFonte.ViaCep));
        Assert.Throws<ArgumentNullException>(() => RespostaBuscaEndereco.Realizada(null!, CepFonte.ViaCep));
    }

    // ---- Fonte ≠ situação ----

    [Fact]
    public void Fonte_e_situacao_sao_independentes()
    {
        var receita = MotorCep.Decidir(Endereco(), RespostaConsultaCep.Encontrado(Registro(), CepFonte.Receita));
        var brasilApi = MotorCep.Decidir(Endereco(), RespostaConsultaCep.Encontrado(Registro(), CepFonte.BrasilApi));

        Assert.Equal(receita.Situacao, brasilApi.Situacao);
        Assert.Equal(CepFonte.Receita, receita.Fonte);
        Assert.Equal(CepFonte.BrasilApi, brasilApi.Fonte);
    }

    // ---- Determinismo ----

    [Fact]
    public void Mesmas_entradas_mesma_decisao_e_a_ordem_da_busca_nao_importa()
    {
        var registros = new[]
        {
            Registro(cep: "35790007"), Registro(cep: "35790001"), Registro(cep: "35790005", complemento: "de 1001/1002 ao fim"),
            Registro(cep: "35790003")
        };

        var decisoes = Enumerable.Range(0, 10)
            .Select(i => MotorCep.Decidir(Endereco(), Inexistente,
                Busca(i % 2 == 0 ? registros : Enumerable.Reverse(registros).ToArray())))
            .ToList();

        Assert.All(decisoes, d =>
        {
            Assert.Equal(ResultadoDecisaoCep.VariosCandidatos, d.Resultado);
            Assert.Equal(["35790001", "35790003", "35790007"], d.Candidatos.Select(c => c.Cep));
            Assert.Equal(decisoes[0].Motivos, d.Motivos);
        });

        var conferidos = Enumerable.Range(0, 10).Select(_ => MotorCep.Decidir(Endereco(), Encontrado())).ToList();
        Assert.All(conferidos, d =>
        {
            Assert.Equal(ResultadoDecisaoCep.Conferido, d.Resultado);
            Assert.Equal(conferidos[0].Motivos, d.Motivos);
        });
    }
}
