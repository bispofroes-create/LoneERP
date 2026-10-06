using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Integracoes;
using Lone.Contracts.Municipios;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enderecos.ConferenciaCep;

namespace Lone.Tests.Cliente;

/// <summary>
/// F2: a conferência na ficha. Mostrar o resultado não muda nenhum campo; vários candidatos ficam para escolha; usar a
/// sugestão muda só o CEP, como alteração não salva, e leva a marca de procedência no Salvar (que a API confere). "Conferir
/// CEP" nunca grava.
/// </summary>
public class ConferenciaCepFichaTests
{
    private static EnderecoFormulario Endereco()
    {
        var e = EnderecoFormulario.De(new EnderecoDto
        {
            Id = Guid.NewGuid(), Cep = "35790000", Logradouro = "R. Barao", Numero = "150", Bairro = "Centro", MunicipioId = 3120904,
            Cidade = "Curvelo", Uf = "MG"
        });
        return e;
    }

    private static CandidatoCepDto Candidato(string cep) =>
        new() { Cep = cep, Logradouro = "Rua Barão", Faixa = "até 999/1000", Bairro = "Centro", Cidade = "Curvelo", Uf = "MG", CodigoMunicipioIbge = "3120904" };

    private static DecisaoCepDto Decisao(ResultadoDecisaoCep resultado, params string[] candidatos) => new()
    {
        Resultado = resultado, CepInformado = "35790000", Fonte = CepFonte.ViaCep,
        CepSugerido = resultado == ResultadoDecisaoCep.UmCandidato ? candidatos[0] : null,
        Candidatos = candidatos.Select(Candidato).ToList(), Motivos = ["CEP informado não encontrado; foi localizado um único CEP compatível."]
    };

    [Fact]
    public void Pedido_de_conferencia_le_os_campos_sem_mudar_nada()
    {
        var e = Endereco();
        var antes = e.Localizacao();

        var pedido = e.ParaConferencia();

        Assert.Equal(("35790-000", "R. Barao", "150", "Centro", "Curvelo", "MG", "3120904"),
            (pedido.Cep, pedido.Logradouro, pedido.Numero, pedido.Bairro, pedido.Cidade, pedido.Uf, pedido.CodigoMunicipioIbge));
        Assert.Equal(antes, e.Localizacao());
    }

    [Fact]
    public void Mostrar_a_decisao_nao_altera_o_endereco()
    {
        var e = Endereco();
        var antes = e.Localizacao();

        e.MostrarConferencia(Decisao(ResultadoDecisaoCep.UmCandidato, "35790001"));

        Assert.Equal(antes, e.Localizacao());
        Assert.True(e.TemConferenciaCep);
        Assert.Equal("Usar a sugestão: 35790-001 · Rua Barão · até 999/1000 · Centro", Assert.Single(e.CandidatosCep).Texto);
        Assert.Null(e.ParaDto(0).SugestaoCepAplicada);
    }

    [Fact]
    public void Varios_candidatos_ficam_para_escolha_sem_escolher()
    {
        var e = Endereco();

        e.MostrarConferencia(Decisao(ResultadoDecisaoCep.VariosCandidatos, "35790001", "35790002"));

        Assert.Equal(2, e.CandidatosCep.Count);
        Assert.All(e.CandidatosCep, c => Assert.StartsWith("Usar 3579", c.Texto));
        Assert.Equal("35790-000", e.Cep);
    }

    [Fact]
    public void Aviso_de_lista_incompleta_aparece_junto_do_resultado()
    {
        var e = Endereco();
        var decisao = Decisao(ResultadoDecisaoCep.VariosCandidatos, "35790001", "35790002");
        decisao.Avisos = ["A lista de candidatos pode estar incompleta."];

        e.MostrarConferencia(decisao);

        Assert.EndsWith("⚠ A lista de candidatos pode estar incompleta.", e.TextoConferenciaCep);
        Assert.Equal("35790-000", e.Cep);
    }

    [Theory]
    [InlineData(ResultadoDecisaoCep.Conferido)]
    [InlineData(ResultadoDecisaoCep.Divergente)]
    [InlineData(ResultadoDecisaoCep.NenhumCandidato)]
    [InlineData(ResultadoDecisaoCep.FonteIndisponivel)]
    public void Sem_candidato_nao_ha_o_que_usar(ResultadoDecisaoCep resultado)
    {
        var e = Endereco();

        e.MostrarConferencia(new DecisaoCepDto { Resultado = resultado, CepInformado = "35790000", Motivos = ["m"] });

        Assert.False(e.TemCandidatosCep);
        Assert.Equal("35790-000", e.Cep);
    }

    [Fact]
    public void Usar_a_sugestao_muda_so_o_cep_sem_salvar_e_leva_a_marca()
    {
        var e = Endereco();
        e.MostrarConferencia(Decisao(ResultadoDecisaoCep.UmCandidato, "35790001"));

        e.CandidatosCep.Single().UsarCommand.Execute(null);

        Assert.Equal("35790-001", e.Cep);
        Assert.Equal(("R. Barao", "150", "Centro"), (e.Logradouro, e.Numero, e.Bairro)); // o resto fica como o usuário digitou
        Assert.NotEqual(e.ComoGravado!.Cep, e.ParaComparacao().Cep);                    // alteração pendente (não salva)
        Assert.Contains("ainda não salvo", e.TextoConferenciaCep);
        var marca = e.ParaDto(0).SugestaoCepAplicada!;
        Assert.Equal(("35790000", "35790001", CepFonte.ViaCep), (marca.CepConferido, marca.CepSugerido, marca.Fonte));
    }

    [Fact]
    public void Mudar_o_cep_depois_de_usar_a_sugestao_tira_a_marca()
    {
        var e = Endereco();
        e.MostrarConferencia(Decisao(ResultadoDecisaoCep.UmCandidato, "35790001"));
        e.CandidatosCep.Single().UsarCommand.Execute(null);

        e.Cep = "35790-005";

        Assert.Null(e.ParaDto(0).SugestaoCepAplicada);
        Assert.False(e.TemConferenciaCep);
    }

    [Fact]
    public void Conferir_cep_so_chama_a_conferencia()
    {
        var e = Endereco();
        var chamadas = 0;
        e.AoConferirCep = _ => { chamadas++; return Task.CompletedTask; };

        e.ConferirCepCommand.Execute(null);

        Assert.Equal(1, chamadas);
        Assert.Equal("35790-000", e.Cep);
    }

    // ---- Fase 1 (L-1 / T-1): a decisão vale só para os dados com que foi feita ----

    /// <summary>Cada alteração de um dado que participa da conferência (CEP, logradouro, número, "Sem número", bairro, município).</summary>
    public static TheoryData<string> AlteracoesQueInvalidam() => new()
    {
        "cep", "logradouro", "numero", "sem-numero", "bairro", "municipio-escolhido", "municipio-uf", "municipio-texto", "exterior"
    };

    private static void Alterar(EnderecoFormulario e, string campo)
    {
        switch (campo)
        {
            case "cep": e.Cep = "35790-005"; break;
            case "logradouro": e.Logradouro = "R. Barao do Rio Branco"; break;
            case "numero": e.Numero = "2000"; break;
            case "sem-numero": e.SemNumero = true; break;
            case "bairro": e.Bairro = "Centro Historico"; break;
            case "municipio-escolhido": e.Municipio.Escolher(new MunicipioDto { Id = 3118007, Nome = "Corinto", Uf = "MG" }); break;
            case "municipio-uf": e.Municipio.Uf = "SP"; break;
            case "municipio-texto": e.Municipio.Texto = "Curv"; break;
            case "exterior": e.NoExterior = true; break;
            default: throw new ArgumentOutOfRangeException(nameof(campo));
        }
    }

    [Theory]
    [MemberData(nameof(AlteracoesQueInvalidam))]
    public void Mudar_um_dado_conferido_tira_a_decisao_da_tela(string campo)
    {
        var e = Endereco();
        e.MostrarConferencia(new DecisaoCepDto
        {
            Resultado = ResultadoDecisaoCep.Conferido, CepInformado = "35790000", Fonte = CepFonte.ViaCep,
            Motivos = ["CEP conferido: corresponde ao endereço."]
        });
        Assert.True(e.ConferenciaCepOk);

        Alterar(e, campo);

        Assert.False(e.TemConferenciaCep);                        // não fica "conferido" com outro endereço
        Assert.Equal(GravidadeConferenciaCep.Nenhuma, e.GravidadeConferenciaCep);
        Assert.Equal(string.Empty, e.IconeConferenciaCep);
    }

    [Theory]
    [MemberData(nameof(AlteracoesQueInvalidam))]
    public void Candidato_de_decisao_velha_nao_pode_mais_ser_usado(string campo)
    {
        var e = Endereco();
        e.MostrarConferencia(Decisao(ResultadoDecisaoCep.UmCandidato, "35790001"));
        var botaoVelho = e.CandidatosCep.Single();

        Alterar(e, campo);
        var cepDepois = e.Cep;
        botaoVelho.UsarCommand.Execute(null);                    // um clique atrasado no botão que já saiu da tela

        Assert.False(e.TemCandidatosCep);
        Assert.Equal(cepDepois, e.Cep);                          // nada é aplicado
        Assert.Null(e.ParaDto(0).SugestaoCepAplicada);
    }

    [Fact]
    public void Botao_de_uma_conferencia_anterior_nao_aplica_depois_de_conferir_de_novo()
    {
        var e = Endereco();
        e.MostrarConferencia(Decisao(ResultadoDecisaoCep.UmCandidato, "35790001"));
        var botaoVelho = e.CandidatosCep.Single();
        e.Numero = "151";
        e.MostrarConferencia(Decisao(ResultadoDecisaoCep.UmCandidato, "35790002")); // nova conferência, outra sugestão

        botaoVelho.UsarCommand.Execute(null);

        Assert.Equal("35790-000", e.Cep);
        Assert.Null(e.ParaDto(0).SugestaoCepAplicada);
        Assert.Equal("35790002", Assert.Single(e.CandidatosCep).Candidato.Cep);
    }

    [Fact]
    public void Voltar_ao_valor_conferido_nao_ressuscita_a_decisao()
    {
        var e = Endereco();
        e.MostrarConferencia(Decisao(ResultadoDecisaoCep.UmCandidato, "35790001"));

        e.Numero = "2000";
        e.Numero = "150";

        Assert.False(e.TemConferenciaCep);
        Assert.False(e.TemCandidatosCep);
    }

    [Fact]
    public void Dados_que_nao_participam_da_conferencia_nao_a_invalidam()
    {
        var e = Endereco();
        e.MostrarConferencia(Decisao(ResultadoDecisaoCep.UmCandidato, "35790001"));

        e.Complemento = "Sala 2";
        e.Descricao = "Depósito";
        e.Observacoes = "Fundos";

        Assert.True(e.TemConferenciaCep);
        Assert.Single(e.CandidatosCep);
    }

    [Fact]
    public void Usar_a_sugestao_nao_se_invalida_nem_dispara_a_consulta_ao_digitar()
    {
        var e = Endereco();
        var consultas = 0;
        e.AoBuscarCep = _ => { consultas++; return Task.CompletedTask; };
        e.MostrarConferencia(Decisao(ResultadoDecisaoCep.UmCandidato, "35790001"));

        e.CandidatosCep.Single().UsarCommand.Execute(null);

        Assert.Equal(0, consultas);                              // a troca do CEP pela sugestão não consulta de novo
        Assert.Equal("35790-001", e.Cep);
        Assert.Contains("aplicado (ainda não salvo)", e.TextoConferenciaCep);
        Assert.NotNull(e.ParaDto(0).SugestaoCepAplicada);
        Assert.Equal(("R. Barao", "150", "Centro"), (e.Logradouro, e.Numero, e.Bairro));
    }

    [Fact]
    public void Mudar_outro_dado_depois_de_usar_a_sugestao_tira_o_texto_mas_a_marca_segue_com_o_cep_sugerido()
    {
        var e = Endereco();
        e.MostrarConferencia(Decisao(ResultadoDecisaoCep.UmCandidato, "35790001"));
        e.CandidatosCep.Single().UsarCommand.Execute(null);

        e.Numero = "151";

        Assert.False(e.TemConferenciaCep);
        // A marca diz só de onde veio o CEP (a API confere antes de registrar); vale enquanto o CEP for o sugerido.
        Assert.Equal("35790001", e.ParaDto(0).SugestaoCepAplicada!.CepSugerido);

        e.Cep = "35790-000";
        Assert.Null(e.ParaDto(0).SugestaoCepAplicada);
    }

    [Fact]
    public void Buscar_cep_que_preenche_o_endereco_tambem_invalida_a_conferencia()
    {
        var e = Endereco();
        e.MostrarConferencia(Decisao(ResultadoDecisaoCep.UmCandidato, "35790001"));

        e.AplicarCep(new DadosCep { Cep = "35790000", Logradouro = "Rua Barão", Bairro = "Centro", Cidade = "Curvelo", Uf = "MG", CodigoMunicipioIbge = "3120904" });

        Assert.False(e.TemConferenciaCep);
        Assert.False(e.TemCandidatosCep);
    }

    [Fact]
    public void Sem_numero_e_numero_se_ajustam_sem_erro_e_invalidam_uma_vez()
    {
        var e = Endereco();
        e.MostrarConferencia(Decisao(ResultadoDecisaoCep.UmCandidato, "35790001"));

        e.Numero = "sn"; // vira "S/N" e marca "Sem número" (eventos encadeados)

        Assert.True(e.SemNumero);
        Assert.Equal("S/N", e.Numero);
        Assert.False(e.TemConferenciaCep);
    }

    [Fact]
    public void Resposta_de_conferencia_feita_com_outros_dados_nao_e_mostrada()
    {
        var e = Endereco();
        var pedido = e.ParaConferencia();
        e.Numero = "2000"; // o usuário mexeu enquanto a conferência estava em andamento

        var mostrou = e.MostrarConferencia(Decisao(ResultadoDecisaoCep.UmCandidato, "35790001"), pedido);

        Assert.False(mostrou);
        Assert.False(e.TemConferenciaCep);
        Assert.False(e.TemCandidatosCep);
    }

    [Fact]
    public void Resposta_com_os_mesmos_dados_e_mostrada()
    {
        var e = Endereco();
        var pedido = e.ParaConferencia();

        Assert.True(e.MostrarConferencia(Decisao(ResultadoDecisaoCep.UmCandidato, "35790001"), pedido));
        Assert.True(e.TemCandidatosCep);
    }

    // ---- Gravidade visual (só apresentação) ----

    [Theory]
    [InlineData(ResultadoDecisaoCep.Conferido, GravidadeConferenciaCep.Ok, "✓")]
    [InlineData(ResultadoDecisaoCep.UmCandidato, GravidadeConferenciaCep.Atencao, "⚠")]
    [InlineData(ResultadoDecisaoCep.VariosCandidatos, GravidadeConferenciaCep.Atencao, "⚠")]
    [InlineData(ResultadoDecisaoCep.FonteIndisponivel, GravidadeConferenciaCep.Atencao, "⚠")] // indisponível ≠ inexistente
    [InlineData(ResultadoDecisaoCep.Divergente, GravidadeConferenciaCep.Alerta, "✗")]
    [InlineData(ResultadoDecisaoCep.NaoEncontrado, GravidadeConferenciaCep.Alerta, "✗")]
    [InlineData(ResultadoDecisaoCep.NenhumCandidato, GravidadeConferenciaCep.Alerta, "✗")]
    public void Cada_resultado_tem_icone_e_gravidade(ResultadoDecisaoCep resultado, GravidadeConferenciaCep gravidade, string icone)
    {
        var e = Endereco();

        e.MostrarConferencia(new DecisaoCepDto { Resultado = resultado, CepInformado = "35790000", Motivos = ["m"] });

        Assert.Equal((gravidade, icone), (e.GravidadeConferenciaCep, e.IconeConferenciaCep));
        Assert.Equal("35790-000", e.Cep); // gravidade não muda nada no endereço
    }

    [Fact]
    public void Todo_resultado_do_motor_tem_gravidade()
    {
        foreach (var r in Enum.GetValues<ResultadoDecisaoCep>())
            Assert.NotEqual(GravidadeConferenciaCep.Nenhuma, EnderecoFormulario.GravidadeDe(r));
    }

    // ---- Ordenação só na apresentação (L-9): ordenar não é escolher ----

    [Fact]
    public void Varios_candidatos_aparecem_do_mais_especifico_ao_mais_geral_sem_escolher()
    {
        var e = Endereco(); // número 150
        var decisao = Decisao(ResultadoDecisaoCep.VariosCandidatos, "35790001", "35790002", "35790003", "35790004");
        decisao.Candidatos[0].Faixa = null;                      // CEP geral do logradouro
        decisao.Candidatos[1].Faixa = "de 101 a 199";            // faixa com o número
        decisao.Candidatos[2].Faixa = "150";                     // prédio com o número exato
        decisao.Candidatos[3].Faixa = "texto que não é faixa";   // não interpretada

        e.MostrarConferencia(decisao);

        Assert.Equal(["35790003", "35790002", "35790001", "35790004"], e.CandidatosCep.Select(c => c.Candidato.Cep));
        Assert.All(e.CandidatosCep, c => Assert.StartsWith("Usar 3579", c.Texto)); // nenhum vira "a sugestão"
        Assert.Equal("35790-000", e.Cep);
        Assert.Null(e.ParaDto(0).SugestaoCepAplicada);
        Assert.Equal(["35790001", "35790002", "35790003", "35790004"], decisao.Candidatos.Select(c => c.Cep)); // a decisão não muda
    }

    [Fact]
    public void Sem_numero_legivel_a_ordem_e_a_do_motor()
    {
        var candidatos = new[] { "35790001", "35790002" }.Select(Candidato).ToList();
        candidatos[1].Faixa = "150";

        var ordem = EnderecoFormulario.OrdenarParaExibicao(candidatos, "S/N");

        Assert.Equal(["35790001", "35790002"], ordem.Select(c => c.Cep));
    }

    // ---- Textos ----

    [Fact]
    public void Cep_inexistente_fala_da_consulta_de_cep_e_nao_dos_correios()
    {
        var e = new EnderecoFormulario { Cep = "99999-999" };

        e.MarcarCepInexistente();

        Assert.Equal("⚠ CEP 99999-999 não encontrado na consulta de CEP. Confira o número.", e.AvisoCep);
        Assert.DoesNotContain("Correios", e.ValidarCep("Endereço 1"));
    }

    // ---- Checkpoint C: componentes na ficha ----

    private static DecisaoCepDto ComComponentes(ResultadoDecisaoCep resultado, params SituacaoComponenteCep[] situacoes) => new()
    {
        Resultado = resultado, CepInformado = "35790000", Fonte = CepFonte.ViaCep, Motivos = ["CEP conferido: corresponde ao endereço."],
        Componentes = situacoes.Select((s, i) => new ComponenteCepDto
        {
            Componente = (ComponenteCep)(i + 1), Situacao = s, Motivo = $"motivo {i + 1}."
        }).ToList()
    };

    private const SituacaoComponenteCep Ok = SituacaoComponenteCep.Confirmado;

    [Fact]
    public void Conferido_com_componente_nao_validavel_e_atencao_e_nao_erro()
    {
        var e = Endereco();

        e.MostrarConferencia(ComComponentes(ResultadoDecisaoCep.Conferido, Ok, Ok, Ok, Ok, SituacaoComponenteCep.NaoValidavel));

        Assert.Equal((GravidadeConferenciaCep.Atencao, "⚠"), (e.GravidadeConferenciaCep, e.IconeConferenciaCep));
        Assert.False(e.ConferenciaCepAlerta);
    }

    [Theory]
    [InlineData(SituacaoComponenteCep.Confirmado)]
    [InlineData(SituacaoComponenteCep.NaoInformado)] // a fonte não traz faixa (rua inteira): sem ressalva
    public void Conferido_sem_componente_nao_validavel_fica_ok(SituacaoComponenteCep numero)
    {
        var e = Endereco();

        e.MostrarConferencia(ComComponentes(ResultadoDecisaoCep.Conferido, Ok, Ok, Ok, Ok, numero));

        Assert.Equal(GravidadeConferenciaCep.Ok, e.GravidadeConferenciaCep);
    }

    [Fact]
    public void Divergente_continua_alerta_pelo_resultado()
    {
        var e = Endereco();

        e.MostrarConferencia(ComComponentes(ResultadoDecisaoCep.Divergente, Ok, Ok, Ok, SituacaoComponenteCep.Divergente, Ok));

        Assert.Equal(GravidadeConferenciaCep.Alerta, e.GravidadeConferenciaCep);
    }

    [Fact]
    public void Detalhes_por_componente_ficam_recolhidos_e_abrem_no_link()
    {
        var e = Endereco();
        e.MostrarConferencia(ComComponentes(ResultadoDecisaoCep.Conferido, Ok, Ok, Ok, Ok, SituacaoComponenteCep.NaoValidavel));

        Assert.True(e.TemComponentesCep);
        Assert.False(e.MostrarDetalhesCep);
        Assert.Equal("Ver detalhes", e.TextoBotaoDetalhesCep);

        e.AlternarDetalhesCepCommand.Execute(null);

        Assert.True(e.MostrarDetalhesCep);
        Assert.Equal("Ocultar detalhes", e.TextoBotaoDetalhesCep);
        Assert.Equal(["CEP: confirmado. motivo 1.", "UF: confirmado. motivo 2.", "Município: confirmado. motivo 3.",
                      "Logradouro: confirmado. motivo 4.", "Número: não foi possível validar. motivo 5."],
            e.ComponentesCep.Select(c => c.Texto));
    }

    [Theory]
    [InlineData(SituacaoComponenteCep.Confirmado, "✓", true, false, false)]
    [InlineData(SituacaoComponenteCep.Divergente, "✗", false, false, true)]
    [InlineData(SituacaoComponenteCep.NaoValidavel, "⚠", false, true, false)]
    [InlineData(SituacaoComponenteCep.NaoInformado, "–", false, false, false)] // ausência de evidência não é vermelha
    public void Cada_estado_tem_icone_e_cor_proprios(SituacaoComponenteCep situacao, string icone, bool ok, bool atencao, bool alerta)
    {
        var linha = new LinhaComponenteCep(new ComponenteCepDto { Componente = ComponenteCep.Numero, Situacao = situacao, Motivo = "m" });

        Assert.Equal((icone, ok, atencao, alerta), (linha.Icone, linha.EhOk, linha.EhAtencao, linha.EhAlerta));
    }

    [Fact]
    public void Mudar_o_endereco_tira_tambem_os_componentes()
    {
        var e = Endereco();
        e.MostrarConferencia(ComComponentes(ResultadoDecisaoCep.Conferido, Ok, Ok, Ok, Ok, Ok));
        e.AlternarDetalhesCepCommand.Execute(null);

        e.Numero = "151";

        Assert.False(e.TemComponentesCep);
        Assert.False(e.MostrarDetalhesCep);
    }

    [Fact]
    public void Resposta_antiga_sem_componentes_funciona_como_antes()
    {
        var e = Endereco();

        e.MostrarConferencia(new DecisaoCepDto { Resultado = ResultadoDecisaoCep.Conferido, CepInformado = "35790000", Motivos = ["m"] });

        Assert.False(e.TemComponentesCep);
        Assert.Equal(GravidadeConferenciaCep.Ok, e.GravidadeConferenciaCep);
    }

    // ---- Checkpoint D: "Encontrar CEP" (busca pelo endereço sem CEP) na ficha ----

    private static EnderecoFormulario EnderecoSemCep()
    {
        var e = Endereco();
        e.Cep = string.Empty;
        return e;
    }

    private static DecisaoCepDto Busca(ResultadoDecisaoCep resultado, params string[] candidatos) => new()
    {
        Resultado = resultado, CepInformado = "", Fonte = CepFonte.ViaCep,
        Candidatos = candidatos.Select(Candidato).ToList(), Motivos = ["Encontramos um CEP compatível com os dados informados. Confira antes de usar."]
    };

    [Fact]
    public void Pedido_de_busca_le_os_campos_sem_cep_e_sem_mudar_nada()
    {
        var e = Endereco();
        var antes = e.Localizacao();

        var pedido = e.ParaBuscaPorEndereco();

        Assert.Equal(("R. Barao", "150", "Centro", "Curvelo", "MG", "3120904"),
            (pedido.Logradouro, pedido.Numero, pedido.Bairro, pedido.Cidade, pedido.Uf, pedido.CodigoMunicipioIbge));
        Assert.Equal(antes, e.Localizacao());
    }

    [Fact]
    public void Encontrar_cep_so_chama_a_busca()
    {
        var e = EnderecoSemCep();
        var buscas = 0;
        e.AoBuscarCepPorEndereco = _ => { buscas++; return Task.CompletedTask; };

        e.BuscarCepPorEnderecoCommand.Execute(null);

        Assert.Equal(1, buscas);
        Assert.Equal("", e.Cep);
    }

    [Fact]
    public void Um_candidato_da_busca_nao_e_aplicado_nem_chamado_de_sugestao()
    {
        var e = EnderecoSemCep();

        e.MostrarConferencia(Busca(ResultadoDecisaoCep.UmCandidato, "35790001"));

        Assert.Equal("", e.Cep);
        Assert.True(e.ResultadoDaBuscaPorEndereco);
        Assert.StartsWith("Usar 35790-001", Assert.Single(e.CandidatosCep).Texto); // não "Usar a sugestão"
        Assert.Null(e.ParaDto(0).SugestaoCepAplicada);
    }

    [Fact]
    public void Varios_candidatos_da_busca_ficam_todos_sem_escolha()
    {
        var e = EnderecoSemCep();

        e.MostrarConferencia(Busca(ResultadoDecisaoCep.VariosCandidatos, "35790001", "35790002", "35790003"));

        Assert.Equal(3, e.CandidatosCep.Count);
        Assert.Equal("", e.Cep);
    }

    [Fact]
    public void Usar_um_candidato_da_busca_aplica_so_ele_e_leva_a_marca_da_busca()
    {
        var e = EnderecoSemCep();
        e.MostrarConferencia(Busca(ResultadoDecisaoCep.VariosCandidatos, "35790001", "35790002"));

        e.CandidatosCep.Single(c => c.Candidato.Cep == "35790002").UsarCommand.Execute(null);

        Assert.Equal("35790-002", e.Cep);
        Assert.Equal(("R. Barao", "150", "Centro"), (e.Logradouro, e.Numero, e.Bairro));
        Assert.Contains("aplicado a partir da busca pelo endereço (ainda não salvo)", e.TextoConferenciaCep);
        var marca = e.ParaDto(0).SugestaoCepAplicada!;
        Assert.Equal(("", "35790002", CepFonte.ViaCep), (marca.CepConferido, marca.CepSugerido, marca.Fonte));
    }

    [Fact]
    public void Busca_obsoleta_nao_aparece_para_o_endereco_novo()
    {
        var e = EnderecoSemCep();
        var dados = e.ParaConferencia();      // a busca começou com o endereço A
        e.Logradouro = "Rua Outra";            // o usuário mudou para o endereço B

        var mostrou = e.MostrarConferencia(Busca(ResultadoDecisaoCep.UmCandidato, "35790001"), dados);

        Assert.False(mostrou);
        Assert.False(e.TemCandidatosCep);
        Assert.False(e.TemConferenciaCep);
    }

    [Theory]
    [InlineData("uf")]
    [InlineData("municipio")]
    [InlineData("logradouro")]
    [InlineData("bairro")]
    [InlineData("numero")]
    [InlineData("sem-numero")]
    public void Mudar_um_dado_da_busca_invalida_os_candidatos_e_o_botao_antigo(string campo)
    {
        var e = EnderecoSemCep();
        e.MostrarConferencia(Busca(ResultadoDecisaoCep.UmCandidato, "35790001"));
        var botaoVelho = e.CandidatosCep.Single();

        switch (campo)
        {
            case "uf": e.Municipio.Uf = "SP"; break;
            case "municipio": e.Municipio.Escolher(new MunicipioDto { Id = 3118007, Nome = "Corinto", Uf = "MG" }); break;
            case "logradouro": e.Logradouro = "Rua Outra"; break;
            case "bairro": e.Bairro = "Bela Vista"; break;
            case "numero": e.Numero = "151"; break;
            case "sem-numero": e.SemNumero = true; break;
        }
        botaoVelho.UsarCommand.Execute(null);

        Assert.False(e.TemCandidatosCep);
        Assert.Equal("", e.Cep);                                     // o botão antigo não aplica nada
        Assert.Null(e.ParaDto(0).SugestaoCepAplicada);
    }

    [Fact]
    public void Complemento_descricao_e_observacoes_nao_invalidam_a_busca()
    {
        var e = EnderecoSemCep();
        e.MostrarConferencia(Busca(ResultadoDecisaoCep.UmCandidato, "35790001"));

        e.Complemento = "Apto 12";
        e.Descricao = "Casa";
        e.Observacoes = "Portão azul";

        Assert.True(e.TemCandidatosCep);
    }

    [Fact]
    public void Nenhum_candidato_na_busca_e_atencao_e_nao_erro()
    {
        var e = EnderecoSemCep();

        e.MostrarConferencia(Busca(ResultadoDecisaoCep.NenhumCandidato));

        Assert.Equal(GravidadeConferenciaCep.Atencao, e.GravidadeConferenciaCep); // não achar não prova que o CEP não exista
        Assert.False(e.TemCandidatosCep);
        Assert.Equal("", e.Cep);
    }

    [Fact]
    public void Fonte_indisponivel_na_busca_e_atencao_e_nao_nenhum_candidato()
    {
        var e = EnderecoSemCep();

        e.MostrarConferencia(new DecisaoCepDto
        {
            Resultado = ResultadoDecisaoCep.FonteIndisponivel, CepInformado = "",
            Motivos = ["Não foi possível consultar a fonte de CEP agora. Tente de novo mais tarde; o cadastro pode seguir normalmente."]
        });

        Assert.Equal(GravidadeConferenciaCep.Atencao, e.GravidadeConferenciaCep);
        Assert.Contains("Não foi possível consultar a fonte de CEP agora", e.TextoConferenciaCep);
        Assert.DoesNotContain("Não encontramos", e.TextoConferenciaCep);
    }

    [Fact]
    public void Aviso_de_lista_limitada_aparece_junto_dos_candidatos()
    {
        var e = EnderecoSemCep();
        var busca = Busca(ResultadoDecisaoCep.VariosCandidatos, "35790001", "35790002");
        busca.Avisos = ["A fonte retornou o limite de resultados: a lista pode estar incompleta."];

        e.MostrarConferencia(busca);

        Assert.EndsWith("⚠ A fonte retornou o limite de resultados: a lista pode estar incompleta.", e.TextoConferenciaCep);
    }

    [Fact]
    public void Candidato_mostra_unidade_municipio_e_componentes_sem_nota_nem_recomendacao()
    {
        var candidato = Candidato("35790911");
        candidato.Faixa = "960";
        candidato.Unidade = "Edifício Central";
        candidato.Componentes =
        [
            new() { Componente = ComponenteCep.Uf, Situacao = SituacaoComponenteCep.Confirmado, Motivo = "m" },
            new() { Componente = ComponenteCep.Municipio, Situacao = SituacaoComponenteCep.Confirmado, Motivo = "m" },
            new() { Componente = ComponenteCep.Logradouro, Situacao = SituacaoComponenteCep.Confirmado, Motivo = "m" },
            new() { Componente = ComponenteCep.Numero, Situacao = SituacaoComponenteCep.NaoValidavel, Motivo = "m" }
        ];

        var opcao = new OpcaoCandidatoCep(candidato, unico: false, _ => { });

        Assert.Equal("Usar 35790-911 · Rua Barão · 960 · Edifício Central · Centro", opcao.Texto);
        Assert.Equal("Curvelo/MG · UF ✓ · Município ✓ · Logradouro ✓ · Número ⚠ não foi possível validar", opcao.Detalhe);
        Assert.DoesNotContain("%", opcao.Detalhe);
        Assert.DoesNotContain("recomend", opcao.Texto + opcao.Detalhe, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Na_tela_cep_geral_vem_antes_dos_que_nao_dao_para_conferir()
    {
        var candidatos = new[] { "35790001", "35790002", "35790003" }.Select(Candidato).ToList();
        candidatos[0].Faixa = "lote 5";  // não interpretada
        candidatos[1].Faixa = null;      // CEP sem faixa (rua toda)
        candidatos[2].Faixa = "até 999/1000";

        var ordem = EnderecoFormulario.OrdenarParaExibicao(candidatos, "150");

        Assert.Equal(["35790003", "35790002", "35790001"], ordem.Select(c => c.Cep));
    }

    [Fact]
    public void Salvar_nao_chama_nenhuma_consulta_externa()
    {
        var e = EnderecoSemCep();
        var chamadas = 0;
        e.AoBuscarCep = _ => { chamadas++; return Task.CompletedTask; };
        e.AoConferirCep = _ => { chamadas++; return Task.CompletedTask; };
        e.AoBuscarCepPorEndereco = _ => { chamadas++; return Task.CompletedTask; };
        e.MostrarConferencia(Busca(ResultadoDecisaoCep.UmCandidato, "35790001"));

        _ = e.ParaDto(0);
        _ = e.ValidarCep("Endereço 1");

        Assert.Equal(0, chamadas);
    }

    // ---- F3: conferência gravada na ficha, pedido "conferido na ficha" e offline ----

    private static EnderecoFormulario Gravado(CepSituacao situacao, CepFonte? fonte = CepFonte.ViaCep) => EnderecoFormulario.De(new EnderecoDto
    {
        Id = Guid.NewGuid(), Cep = "35790000", Logradouro = "R. Barao", Numero = "150", Bairro = "Centro", MunicipioId = 3120904,
        Cidade = "Curvelo", Uf = "MG", CepSituacao = situacao, CepFonte = situacao == CepSituacao.NaoConferido ? null : fonte,
        CepConferidoEm = situacao == CepSituacao.NaoConferido ? null : new DateTime(2026, 10, 5, 20, 30, 0, DateTimeKind.Utc)
    });

    [Fact]
    public void Endereco_conferido_mostra_a_linha_discreta_com_fonte_e_data()
    {
        var e = Gravado(CepSituacao.Conferido);

        Assert.True(e.TemEstadoCepGravado);
        // R-E3 (F6): a data é da conferência do Lone; a fonte vem à parte (não parece hora de resposta da fonte).
        Assert.Equal($"✓ CEP conferido em {EnderecoFormulario.DataLocal(new DateTime(2026, 10, 5, 20, 30, 0, DateTimeKind.Utc))} (fonte: ViaCEP)",
            e.TextoEstadoCepGravado);
        Assert.True(e.EstadoCepGravadoOk);
    }

    [Theory]
    [InlineData(CepSituacao.Divergente, " (fonte: ViaCEP): o CEP não correspondia ao endereço")]
    [InlineData(CepSituacao.NaoEncontrado, " (fonte: ViaCEP): CEP não encontrado")]
    public void Situacao_antiga_aparece_como_atencao_e_nao_como_erro(CepSituacao situacao, string texto)
    {
        var e = Gravado(situacao);

        Assert.StartsWith("Última conferência em ", e.TextoEstadoCepGravado); // R-E3 (F6): data da conferência, fonte à parte
        Assert.EndsWith(texto, e.TextoEstadoCepGravado);
        Assert.True(e.EstadoCepGravadoAtencao);
        Assert.False(e.EstadoCepGravadoOk);
    }

    [Fact]
    public void Endereco_antigo_sem_conferencia_diz_que_ainda_nao_foi_conferido()
    {
        var e = Gravado(CepSituacao.NaoConferido);

        Assert.Equal("CEP ainda não conferido", e.TextoEstadoCepGravado);
        Assert.False(e.EstadoCepGravadoOk || e.EstadoCepGravadoAtencao);
    }

    [Theory]
    [InlineData("cep")]
    [InlineData("numero")]
    [InlineData("sem-numero")]
    [InlineData("bairro")]
    [InlineData("logradouro")]
    [InlineData("municipio")]
    public void Mudar_dado_conferido_tira_a_linha_gravada(string campo)
    {
        var e = Gravado(CepSituacao.Conferido);

        switch (campo)
        {
            case "cep": e.Cep = "35790-005"; break;
            case "numero": e.Numero = "151"; break;
            case "sem-numero": e.SemNumero = true; break;
            case "bairro": e.Bairro = "Bela Vista"; break;
            case "logradouro": e.Logradouro = "Rua Outra"; break;
            case "municipio": e.Municipio.Escolher(new MunicipioDto { Id = 3118007, Nome = "Corinto", Uf = "MG" }); break;
        }

        Assert.False(e.TemEstadoCepGravado);                       // não parece atual para outros dados
        Assert.Equal(CepSituacao.Conferido, e.ParaDto(0).CepSituacao); // só leitura: quem decide é a API no Salvar
    }

    [Fact]
    public void Complemento_nao_tira_a_linha_gravada()
    {
        var e = Gravado(CepSituacao.Conferido);

        e.Complemento = "Sala 3";
        e.Observacoes = "fundos";

        Assert.True(e.TemEstadoCepGravado);
    }

    [Fact]
    public void Resultado_novo_na_tela_esconde_a_linha_gravada()
    {
        var e = Gravado(CepSituacao.Conferido);

        e.MostrarConferencia(Decisao(ResultadoDecisaoCep.UmCandidato, "35790001"));

        Assert.False(e.TemEstadoCepGravado);
    }

    [Fact]
    public void Salvar_pede_a_conferencia_so_quando_ela_vale_para_os_dados_atuais()
    {
        var e = Endereco();
        Assert.False(e.ParaDto(0).ConferenciaCepNaFicha);           // sem conferência

        e.MostrarConferencia(new DecisaoCepDto { Resultado = ResultadoDecisaoCep.Conferido, CepInformado = "35790000", Fonte = CepFonte.ViaCep, Motivos = ["m"] });
        Assert.True(e.ParaDto(0).ConferenciaCepNaFicha);

        e.Numero = "151";                                            // conferiu, mudou o número, salvou
        Assert.False(e.ParaDto(0).ConferenciaCepNaFicha);
    }

    [Fact]
    public void Salvar_nao_pede_conferencia_depois_de_usar_a_sugestao_nem_da_busca_sem_cep()
    {
        var sugestao = Endereco();
        sugestao.MostrarConferencia(Decisao(ResultadoDecisaoCep.UmCandidato, "35790001"));
        sugestao.CandidatosCep.Single().UsarCommand.Execute(null);   // o CEP novo não foi conferido (só sugerido)

        var busca = EnderecoSemCep();
        busca.MostrarConferencia(Busca(ResultadoDecisaoCep.UmCandidato, "35790001"));
        busca.CandidatosCep.Single().UsarCommand.Execute(null);

        Assert.False(sugestao.ParaDto(0).ConferenciaCepNaFicha);
        Assert.NotNull(sugestao.ParaDto(0).SugestaoCepAplicada);       // procedência (DM3) continua, é outro fato
        Assert.False(busca.ParaDto(0).ConferenciaCepNaFicha);
    }

    [Fact]
    public void Offline_mostra_a_ultima_informacao_como_anterior_e_nunca_como_conferencia_atual()
    {
        var e = Endereco();
        var decisao = new DecisaoCepDto
        {
            Resultado = ResultadoDecisaoCep.FonteIndisponivel, CepInformado = "35790000",
            Motivos = ["Não foi possível conferir o CEP agora (serviço indisponível). Você pode salvar e conferir depois."],
            InformacaoAnterior = new InformacaoAnteriorCepDto
            {
                Fonte = CepFonte.ViaCep, ConsultadoEm = new DateTime(2026, 10, 5, 20, 30, 0, DateTimeKind.Utc),
                Registro = Candidato("35790000"), Resultado = ResultadoDecisaoCep.Conferido, Motivos = ["CEP conferido: corresponde ao endereço."]
            }
        };

        e.MostrarConferencia(decisao);

        Assert.Equal(GravidadeConferenciaCep.Atencao, e.GravidadeConferenciaCep);       // nunca ✓ por informação antiga
        Assert.Contains("Última informação disponível: CEP encontrado via ViaCEP em ", e.TextoConferenciaCep);
        Assert.Contains("Não foi possível atualizar agora.", e.TextoConferenciaCep);
        Assert.DoesNotContain("✓", e.TextoConferenciaCep);
    }
}
