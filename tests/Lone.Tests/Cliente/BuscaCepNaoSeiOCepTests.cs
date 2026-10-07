using System.Net;
using Lone.Cliente.ViewModels;
using Lone.Cliente.ViewModels.Comum;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Comum;
using Lone.Contracts.Integracoes;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enderecos.ConferenciaCep;
using Lone.Domain.Enums;
using Lone.Domain.Pessoas;

namespace Lone.Tests.Cliente;

/// <summary>
/// Busca de CEP por endereço, decisões D1–D5 (06/10/2026), na tela de pessoas com o servidor falso:
/// <list type="bullet">
/// <item>"Não sei o CEP" começa a busca; faltou UF, município ou logradouro: nada é consultado, a mensagem diz o que falta e
/// a ficha leva ao campo;</item>
/// <item>durante a busca: "Procurando CEPs…", a tela continua usável (sem o ocupado geral), sem disparo duplicado, e
/// "Cancelar" interrompe sem mudar o endereço nem deixar resultado;</item>
/// <item>um ou vários candidatos ficam para escolha; nenhum, tempo esgotado, resposta inválida e indisponibilidade são
/// tratados sem erro técnico;</item>
/// <item>D1: "Usar" preenche o CEP e chama a conferência oficial do Motor de CEP; número, complemento, logradouro e bairro
/// ficam como estavam;</item>
/// <item>D2: o CEP continua obrigatório; D4: nada de busca por texto livre.</item>
/// </list>
/// </summary>
public class BuscaCepNaoSeiOCepTests
{
    private static readonly string CaminhoBusca = "/" + Rotas.Consultas.BuscarCepPorEndereco;
    private static readonly string CaminhoConferir = "/" + Rotas.Consultas.ConferirCep;

    private static async Task<(PessoasViewModel Tela, AmbienteCliente Ambiente, EnderecoFormulario Endereco, List<DestinoCampo> Focos)>
        FichaAsync(NaturezaPessoa natureza = NaturezaPessoa.Fisica)
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao());
        var (tela, _) = await PessoasViewModelTests.AbrirTelaComAsync(ambiente);
        await tela.NovoCommand.ExecuteAsync(null);
        var focos = new List<DestinoCampo>();
        tela.Validacao.FocoPedido += focos.Add;
        var f = tela.Formulario!;
        // PJ lê as opções da estrutura empresarial ao trocar a natureza: respondida aqui, fora da fila da busca.
        ambiente.Servidor.ResponderEm("/" + Rotas.Pessoas.OpcoesEstrutura, HttpStatusCode.OK, new EstruturaEmpresarialOpcoesDto(), vezes: 5);
        f.Natureza = Opcao.De(OpcoesPessoa.Naturezas, natureza);
        f.Nome = "Ana";
        var e = f.Enderecos[0];
        e.Municipio.Definir(3120904, "Curvelo", "MG");
        e.Logradouro = "R. Barao";
        e.Numero = "150";
        e.Complemento = "Apto 2";
        e.Bairro = "Centro";
        return (tela, ambiente, e, focos);
    }

    private static CandidatoCepDto Candidato(string cep) =>
        new() { Cep = cep, Logradouro = "Rua Barão", Faixa = "até 999/1000", Bairro = "Centro", Cidade = "Curvelo", Uf = "MG", CodigoMunicipioIbge = "3120904" };

    private static DecisaoCepDto Busca(ResultadoDecisaoCep resultado, params string[] ceps) => new()
    {
        Resultado = resultado, CepInformado = "", Fonte = CepFonte.ViaCep, Candidatos = ceps.Select(Candidato).ToList(),
        Motivos = [resultado == ResultadoDecisaoCep.NenhumCandidato
            ? "Não encontramos CEP compatível com os dados informados."
            : "Encontramos CEPs compatíveis com os dados informados. Confira antes de usar."]
    };

    private static DecisaoCepDto Conferencia(ResultadoDecisaoCep resultado, string cep, string motivo) => new()
    {
        Resultado = resultado, CepInformado = cep, Fonte = CepFonte.ViaCep, Motivos = [motivo],
        RegistroConsultado = Candidato(cep)
    };

    private static int Buscas(AmbienteCliente a) => a.Servidor.Recebidas.Count(r => r.Caminho == CaminhoBusca);

    private static (string, string, string, string, string?, int?, string) Endereco(EnderecoFormulario e) =>
        (e.Logradouro, e.Numero, e.Complemento, e.Bairro, e.Municipio.Uf, e.Municipio.MunicipioId, e.Cep);

    private static async Task EsperarAsync(Func<bool> condicao)
    {
        for (var i = 0; i < 500 && !condicao(); i++) await Task.Delay(10);
        Assert.True(condicao(), "A condição esperada não aconteceu a tempo.");
    }

    /// <summary>Começa a busca com o servidor segurando a resposta e espera a chamada chegar.</summary>
    private static async Task<Task> BuscaEmAndamentoAsync(AmbienteCliente ambiente, EnderecoFormulario e)
    {
        var antes = Buscas(ambiente);
        ambiente.Servidor.Segurar = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var busca = e.BuscarCepPorEnderecoCommand.ExecuteAsync(null);
        await EsperarAsync(() => Buscas(ambiente) == antes + 1);
        return busca;
    }

    // ---- 1. Início ----

    [Fact]
    public async Task Nao_sei_o_cep_inicia_a_busca_pelo_endereco_sem_cep()
    {
        var (tela, ambiente, e, focos) = await FichaAsync();
        ambiente.Servidor.Responder(HttpStatusCode.OK, Busca(ResultadoDecisaoCep.VariosCandidatos, "35790001", "35790002"));

        await e.BuscarCepPorEnderecoCommand.ExecuteAsync(null);

        var chamada = ambiente.Servidor.Recebidas[^1];
        Assert.Equal((HttpMethod.Post, CaminhoBusca), (chamada.Metodo, chamada.Caminho));
        Assert.Contains("\"logradouro\":\"R. Barao\"", chamada.Corpo, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"codigoMunicipioIbge\":\"3120904\"", chamada.Corpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"cep\"", chamada.Corpo, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, e.CandidatosCep.Count);
        Assert.Empty(focos);
        Assert.False(e.ProcurandoCep);
    }

    // ---- 2 a 4. Faltou dado: nada é consultado, a mensagem diz o que falta e a ficha leva ao campo ----

    [Theory]
    [InlineData("uf", CamposFichaPessoa.Municipio, "escolha a UF")]
    [InlineData("municipio", CamposFichaPessoa.Municipio, "escolha o município na lista")]
    [InlineData("logradouro", CamposFichaPessoa.Logradouro, "informe o logradouro")]
    [InlineData("logradouro-curto", CamposFichaPessoa.Logradouro, "informe o logradouro")]
    public async Task Faltou_dado_nao_consulta_avisa_e_leva_ao_campo(string falta, string campo, string mensagem)
    {
        var (tela, ambiente, e, focos) = await FichaAsync();
        switch (falta)
        {
            case "uf": e.Municipio.Definir(null, null, null); break;
            case "municipio": e.Municipio.Definir(null, null, "MG"); break;
            case "logradouro": e.Logradouro = ""; break;
            case "logradouro-curto": e.Logradouro = "R."; break;
        }
        var chamadas = ambiente.Servidor.Recebidas.Count;
        var antes = Endereco(e);

        await e.BuscarCepPorEnderecoCommand.ExecuteAsync(null);

        Assert.Equal(chamadas, ambiente.Servidor.Recebidas.Count); // nenhuma requisição
        Assert.Equal(TipoMensagem.Aviso, tela.TipoMensagem);
        Assert.Contains(mensagem, tela.Mensagem);
        Assert.Equal(new DestinoCampo(campo, e.Id), Assert.Single(focos));
        Assert.Equal(antes, Endereco(e));
        Assert.False(e.ProcurandoCep);
        Assert.False(e.TemConferenciaCep);
    }

    [Fact]
    public async Task Ordem_do_que_falta_segue_a_tela_uf_antes_do_logradouro()
    {
        var (tela, _, e, focos) = await FichaAsync();
        e.Municipio.Definir(null, null, null);
        e.Logradouro = "";

        await e.BuscarCepPorEnderecoCommand.ExecuteAsync(null);

        Assert.Equal(CamposFichaPessoa.Municipio, Assert.Single(focos).Campo);
        Assert.Contains("escolha a UF", tela.Mensagem);
    }

    [Fact]
    public async Task Numero_e_bairro_nao_sao_exigidos_para_buscar()
    {
        var (_, ambiente, e, focos) = await FichaAsync();
        e.Numero = "";
        e.Bairro = "";
        ambiente.Servidor.Responder(HttpStatusCode.OK, Busca(ResultadoDecisaoCep.UmCandidato, "35790001"));

        await e.BuscarCepPorEnderecoCommand.ExecuteAsync(null);

        Assert.Equal(1, Buscas(ambiente));
        Assert.Empty(focos);
    }

    // ---- 5 e 6. "Procurando CEPs…" sem travar a tela, sem disparo duplicado ----

    [Fact]
    public async Task Durante_a_busca_mostra_procurando_e_a_tela_continua_usavel()
    {
        var (tela, ambiente, e, _) = await FichaAsync();
        ambiente.Servidor.Responder(HttpStatusCode.OK, Busca(ResultadoDecisaoCep.UmCandidato, "35790001"));

        var busca = await BuscaEmAndamentoAsync(ambiente, e);

        Assert.True(e.ProcurandoCep);
        Assert.False(e.SemBuscaCepEmAndamento);               // o link some; no lugar, "Procurando CEPs…" e "Cancelar"
        Assert.True(e.CancelarBuscaCepCommand.CanExecute(null));
        Assert.False(tela.Ocupado);                           // sem o indicador de ocupado da tela inteira
        Assert.True(tela.Livre);
        tela.Formulario!.Nome = "Ana Maria";                   // a ficha continua editável
        Assert.Equal("Ana Maria", tela.Formulario.Nome);

        ambiente.Servidor.Segurar!.SetResult();
        await busca.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(e.ProcurandoCep);
        Assert.True(e.SemBuscaCepEmAndamento);
        Assert.Single(e.CandidatosCep);
    }

    [Fact]
    public async Task Clicar_de_novo_durante_a_busca_nao_dispara_outra()
    {
        var (_, ambiente, e, _) = await FichaAsync();
        ambiente.Servidor.Responder(HttpStatusCode.OK, Busca(ResultadoDecisaoCep.UmCandidato, "35790001"));

        var busca = await BuscaEmAndamentoAsync(ambiente, e);

        Assert.False(e.BuscarCepPorEnderecoCommand.CanExecute(null)); // o próprio botão fica indisponível
        await e.AoBuscarCepPorEndereco!(e).WaitAsync(TimeSpan.FromSeconds(5)); // e o pedido repetido volta sem consultar
        Assert.Equal(1, Buscas(ambiente));

        ambiente.Servidor.Segurar!.SetResult();
        await busca.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, Buscas(ambiente));
    }

    // ---- 7 a 9. Cancelar ----

    [Fact]
    public async Task Cancelar_interrompe_a_busca_sem_mudar_o_endereco_nem_deixar_resultado()
    {
        var (tela, ambiente, e, focos) = await FichaAsync();
        var antes = Endereco(e);
        var busca = await BuscaEmAndamentoAsync(ambiente, e); // sem resposta: só termina se o cancelamento funcionar

        e.CancelarBuscaCepCommand.Execute(null);
        await busca.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(e.ProcurandoCep);
        Assert.Equal(antes, Endereco(e));                     // endereço intacto, CEP vazio
        Assert.Equal("", e.Cep);
        Assert.False(e.TemConferenciaCep);
        Assert.False(e.TemCandidatosCep);
        Assert.Null(e.ParaDto(0).SugestaoCepAplicada);
        Assert.False(tela.TemMensagem);                       // nenhum erro técnico
        Assert.Empty(focos);
    }

    [Fact]
    public async Task Cancelar_nao_deixa_o_resultado_anterior_na_tela()
    {
        var (tela, ambiente, e, _) = await FichaAsync();
        ambiente.Servidor.Responder(HttpStatusCode.OK, Busca(ResultadoDecisaoCep.VariosCandidatos, "35790001", "35790002"));
        await e.BuscarCepPorEnderecoCommand.ExecuteAsync(null);
        Assert.Equal(2, e.CandidatosCep.Count);

        var busca = await BuscaEmAndamentoAsync(ambiente, e);
        Assert.False(e.TemCandidatosCep);                     // a busca nova já tira o resultado velho
        Assert.False(e.TemConferenciaCep);
        e.CancelarBuscaCepCommand.Execute(null);
        await busca.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(e.TemCandidatosCep);
        Assert.False(e.TemConferenciaCep);
        Assert.Equal("", e.Cep);
        Assert.False(tela.TemMensagem);
    }

    [Fact]
    public async Task Depois_de_cancelar_pode_buscar_de_novo_e_digitar_o_cep_a_mao()
    {
        var (_, ambiente, e, _) = await FichaAsync();
        var busca = await BuscaEmAndamentoAsync(ambiente, e);
        e.CancelarBuscaCepCommand.Execute(null);
        await busca.WaitAsync(TimeSpan.FromSeconds(5));
        ambiente.Servidor.Segurar = null;

        ambiente.Servidor.Responder(HttpStatusCode.OK, Busca(ResultadoDecisaoCep.UmCandidato, "35790001"));
        await e.BuscarCepPorEnderecoCommand.ExecuteAsync(null);
        Assert.Single(e.CandidatosCep);

        // 21. CEP manual: a consulta automática dos 8 dígitos continua a de sempre.
        ambiente.Servidor.Responder(HttpStatusCode.OK, new DadosCep
        {
            Cep = "35790000", Logradouro = "Rua Barão", Bairro = "Centro", Cidade = "Curvelo", Uf = "MG", CodigoMunicipioIbge = "3120904"
        });
        e.Cep = "35790-000";
        await EsperarAsync(() => ambiente.Servidor.Recebidas[^1].Caminho == "/" + Rotas.Consultas.Cep("35790000"));
        Assert.Equal("35790-000", e.Cep);
        Assert.False(e.TemCandidatosCep);                     // o CEP digitado tira os candidatos da busca
    }

    // ---- 10 a 12. Resultados ----

    [Fact]
    public async Task Um_resultado_fica_para_selecionar_sem_ser_aplicado()
    {
        var (_, ambiente, e, _) = await FichaAsync();
        ambiente.Servidor.Responder(HttpStatusCode.OK, Busca(ResultadoDecisaoCep.UmCandidato, "35790001"));

        await e.BuscarCepPorEnderecoCommand.ExecuteAsync(null);

        var opcao = Assert.Single(e.CandidatosCep);
        Assert.StartsWith("Usar 35790-001 · Rua Barão · até 999/1000 · Centro", opcao.Texto); // CEP, logradouro, complemento, bairro
        Assert.StartsWith("Curvelo/MG", opcao.Detalhe);                                      // município e UF
        Assert.Contains("Fonte: ViaCEP.", e.TextoConferenciaCep);                           // a fonte fica dita
        Assert.Equal("", e.Cep);                                                             // nada aplicado sem escolher
        Assert.True(opcao.UsarCommand.CanExecute(null));
    }

    [Fact]
    public async Task Varios_resultados_exigem_escolha_e_nenhum_e_destacado()
    {
        var (_, ambiente, e, _) = await FichaAsync();
        ambiente.Servidor.Responder(HttpStatusCode.OK, Busca(ResultadoDecisaoCep.VariosCandidatos, "35790001", "35790002", "35790003"));

        await e.BuscarCepPorEnderecoCommand.ExecuteAsync(null);

        Assert.Equal(3, e.CandidatosCep.Count);
        Assert.All(e.CandidatosCep, c => Assert.StartsWith("Usar 3579", c.Texto)); // nenhum "Usar a sugestão"
        Assert.Equal("", e.Cep);
        var textos = e.TextoConferenciaCep + string.Concat(e.CandidatosCep.Select(c => c.Texto + c.Detalhe));
        Assert.DoesNotContain("%", textos);                                          // sem porcentagem nem "100% correto"
        Assert.DoesNotContain("recomend", textos, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("score", textos, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Nenhum_resultado_e_atencao_nao_inventa_cep_e_nao_libera_salvar_sem_cep()
    {
        var (tela, ambiente, e, _) = await FichaAsync();
        ambiente.Servidor.Responder(HttpStatusCode.OK, Busca(ResultadoDecisaoCep.NenhumCandidato));

        await e.BuscarCepPorEnderecoCommand.ExecuteAsync(null);

        Assert.Equal(GravidadeConferenciaCep.Atencao, e.GravidadeConferenciaCep);
        Assert.Contains("Não encontramos CEP compatível", e.TextoConferenciaCep);
        Assert.False(e.TemCandidatosCep);
        Assert.Equal("", e.Cep);
        Assert.False(tela.TemMensagem);

        // 28. D2: o CEP continua obrigatório; não existe caminho para salvar sem ele.
        Assert.Contains(tela.Formulario!.ValidarLocalmenteComCampos(), x => x.Campo == CamposFichaPessoa.Cep && x.Item == e.Id);
    }

    // ---- 13 a 15. Tempo esgotado, resposta inválida, indisponibilidade ----

    [Fact]
    public async Task Tempo_esgotado_e_tratado_com_mensagem_amigavel()
    {
        var (tela, ambiente, e, _) = await FichaAsync();
        var antes = Endereco(e);
        ambiente.Servidor.TempoEsgotado();

        await e.BuscarCepPorEnderecoCommand.ExecuteAsync(null);

        Assert.Equal(PessoasViewModel.MensagemBuscaCepIndisponivel, tela.Mensagem);
        Assert.Equal(TipoMensagem.Aviso, tela.TipoMensagem);
        Assert.Equal(antes, Endereco(e));
        Assert.False(e.ProcurandoCep);
        Assert.False(e.TemCandidatosCep);
    }

    [Theory]
    [InlineData("{ isto não é json")]
    [InlineData("<html>erro</html>")]
    [InlineData("null")]
    public async Task Resposta_invalida_e_tratada_sem_erro_tecnico(string corpo)
    {
        var (tela, ambiente, e, _) = await FichaAsync();
        var antes = Endereco(e);
        ambiente.Servidor.RespostaInvalida(corpo);

        await e.BuscarCepPorEnderecoCommand.ExecuteAsync(null);

        Assert.Equal(PessoasViewModel.MensagemBuscaCepIndisponivel, tela.Mensagem);
        Assert.DoesNotContain("inesperado", tela.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(antes, Endereco(e));
        Assert.False(e.ProcurandoCep);
    }

    [Fact]
    public async Task Erro_do_servidor_e_tratado_com_mensagem_amigavel()
    {
        var (tela, ambiente, e, _) = await FichaAsync();
        ambiente.Servidor.Responder(HttpStatusCode.BadGateway);

        await e.BuscarCepPorEnderecoCommand.ExecuteAsync(null);

        Assert.Equal(PessoasViewModel.MensagemBuscaCepIndisponivel, tela.Mensagem);
        Assert.DoesNotContain("HTTP", tela.Mensagem);
    }

    [Fact]
    public async Task Servidor_fora_do_ar_e_tratado_com_mensagem_amigavel()
    {
        var (tela, ambiente, e, _) = await FichaAsync();
        var antes = Endereco(e);
        ambiente.Servidor.ForaDoAr();

        await e.BuscarCepPorEnderecoCommand.ExecuteAsync(null);

        Assert.Equal("Não foi possível pesquisar CEPs neste momento. Você pode tentar novamente ou informar o CEP manualmente.", tela.Mensagem);
        Assert.Equal(antes, Endereco(e));
        Assert.False(e.ProcurandoCep);
    }

    [Fact]
    public async Task Fonte_de_cep_indisponivel_e_atencao_com_o_caminho_manual()
    {
        var (tela, ambiente, e, _) = await FichaAsync();
        ambiente.Servidor.Responder(HttpStatusCode.OK, new DecisaoCepDto
        {
            Resultado = ResultadoDecisaoCep.FonteIndisponivel, CepInformado = "", Fonte = CepFonte.ViaCep,
            Motivos = [MotorCep.MensagemBuscaIndisponivel]
        });

        await e.BuscarCepPorEnderecoCommand.ExecuteAsync(null);

        Assert.Equal(GravidadeConferenciaCep.Atencao, e.GravidadeConferenciaCep);
        Assert.Contains("informar o CEP manualmente", e.TextoConferenciaCep);
        Assert.DoesNotContain("Fonte: ViaCEP", e.TextoConferenciaCep); // a fonte que não respondeu não aparece como origem
        Assert.False(e.TemCandidatosCep);
        Assert.Equal("", e.Cep);
        Assert.False(tela.TemMensagem);
    }

    [Fact]
    public void Indisponibilidade_da_fonte_na_busca_nao_diz_que_o_cadastro_segue_sem_cep()
    {
        var d = MotorCep.BuscarPorEndereco(new EnderecoConferenciaCep(null, "R. Barao", "150", "Centro", "Curvelo", "MG", "3120904"),
            RespostaBuscaEndereco.Indisponivel(CepFonte.ViaCep));

        Assert.Equal(MotorCep.MensagemBuscaIndisponivel, Assert.Single(d.Motivos));
        Assert.DoesNotContain("pode seguir", MotorCep.MensagemBuscaIndisponivel);
    }

    // ---- 16 a 20, 24. D1: "Usar" preenche o CEP e passa pela conferência oficial ----

    [Fact]
    public async Task Usar_preenche_o_cep_chama_a_conferencia_oficial_e_preserva_o_resto()
    {
        var (tela, ambiente, e, _) = await FichaAsync();
        ambiente.Servidor.Responder(HttpStatusCode.OK, Busca(ResultadoDecisaoCep.VariosCandidatos, "35790001", "35790002"));
        await e.BuscarCepPorEnderecoCommand.ExecuteAsync(null);
        ambiente.Servidor.ResponderEm(CaminhoConferir, HttpStatusCode.OK,
            Conferencia(ResultadoDecisaoCep.Conferido, "35790002", "O CEP corresponde ao endereço informado."));

        e.CandidatosCep.Single(c => c.Candidato.Cep == "35790002").UsarCommand.Execute(null);
        Assert.Equal("35790-002", e.Cep);                                  // 16: preenchido na hora
        await e.ConferenciaAposUsar.WaitAsync(TimeSpan.FromSeconds(5));

        var chamada = ambiente.Servidor.Recebidas[^1];                     // 17: a conferência oficial, uma vez
        Assert.Equal((HttpMethod.Post, CaminhoConferir), (chamada.Metodo, chamada.Caminho));
        Assert.Contains("35790-002", chamada.Corpo);
        Assert.Equal(1, ambiente.Servidor.Recebidas.Count(r => r.Caminho == CaminhoConferir));
        Assert.Contains("O CEP corresponde ao endereço informado.", e.TextoConferenciaCep);
        Assert.Equal(GravidadeConferenciaCep.Ok, e.GravidadeConferenciaCep);
        Assert.True(e.ConferenciaCepVigente);                              // o Salvar pede para gravar esta conferência
        // 19, 20: número, complemento, logradouro e bairro como o usuário digitou (o candidato não sobrescreve nada).
        Assert.Equal(("R. Barao", "150", "Apto 2", "Centro", "MG", 3120904),
            (e.Logradouro, e.Numero, e.Complemento, e.Bairro, e.Municipio.Uf, e.Municipio.MunicipioId));
        var marca = e.ParaDto(0).SugestaoCepAplicada!;                      // a procedência da busca continua indo no Salvar
        Assert.Equal(("", "35790002", CepFonte.ViaCep), (marca.CepConferido, marca.CepSugerido, marca.Fonte));
        Assert.False(tela.TemMensagem);

        // 24. A segunda opinião continua disponível sobre essa conferência.
        Assert.True(e.PodeConsultarOutraFonte);
    }

    [Fact]
    public async Task Divergencia_depois_de_usar_segue_as_regras_atuais()
    {
        var (_, ambiente, e, _) = await FichaAsync();
        ambiente.Servidor.Responder(HttpStatusCode.OK, Busca(ResultadoDecisaoCep.UmCandidato, "35790001"));
        await e.BuscarCepPorEnderecoCommand.ExecuteAsync(null);
        ambiente.Servidor.ResponderEm(CaminhoConferir, HttpStatusCode.OK,
            Conferencia(ResultadoDecisaoCep.Divergente, "35790001", "O número informado está fora da faixa deste CEP."));

        e.CandidatosCep.Single().UsarCommand.Execute(null);
        await e.ConferenciaAposUsar.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(EnderecoFormulario.GravidadeDe(ResultadoDecisaoCep.Divergente), e.GravidadeConferenciaCep);
        Assert.Contains("fora da faixa", e.TextoConferenciaCep);
        Assert.Equal("35790-001", e.Cep);                                  // nada é trocado sozinho
        Assert.Equal(("150", "Apto 2"), (e.Numero, e.Complemento));
        Assert.True(e.ConferenciaCepVigente);                              // vai para o Salvar como está, a API decide
    }

    [Fact]
    public async Task Conferencia_indisponivel_depois_de_usar_mantem_o_cep_escolhido()
    {
        var (tela, ambiente, e, _) = await FichaAsync();
        ambiente.Servidor.Responder(HttpStatusCode.OK, Busca(ResultadoDecisaoCep.UmCandidato, "35790001"));
        await e.BuscarCepPorEnderecoCommand.ExecuteAsync(null);
        ambiente.Servidor.ResponderEm(CaminhoConferir, HttpStatusCode.OK, new DecisaoCepDto
        {
            Resultado = ResultadoDecisaoCep.FonteIndisponivel, CepInformado = "35790001",
            Motivos = ["Não foi possível consultar a fonte de CEP agora."]
        });

        e.CandidatosCep.Single().UsarCommand.Execute(null);
        await e.ConferenciaAposUsar.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("35790-001", e.Cep);
        Assert.Equal(GravidadeConferenciaCep.Atencao, e.GravidadeConferenciaCep);
        Assert.NotNull(e.ParaDto(0).SugestaoCepAplicada);                  // a escolha continua; a API confere no Salvar
    }

    [Fact]
    public void Usar_a_sugestao_da_conferencia_nao_dispara_outra_conferencia()
    {
        // D1 vale para o CEP escolhido na busca sem CEP. A sugestão da conferência (caso 4) já veio da conferência e segue
        // como antes: aplicada como alteração não salva, sem nova conferência automática.
        var e = EnderecoFormulario.De(new EnderecoDto
        {
            Id = Guid.NewGuid(), Cep = "35790000", Logradouro = "R. Barao", Numero = "150", Bairro = "Centro", MunicipioId = 3120904,
            Cidade = "Curvelo", Uf = "MG"
        });
        var conferencias = 0;
        e.AoConferirCep = _ => { conferencias++; return Task.CompletedTask; };
        e.MostrarConferencia(new DecisaoCepDto
        {
            Resultado = ResultadoDecisaoCep.UmCandidato, CepInformado = "35790000", Fonte = CepFonte.ViaCep, CepSugerido = "35790001",
            Candidatos = [Candidato("35790001")], Motivos = ["CEP informado não encontrado; foi localizado um único CEP compatível."]
        });

        Assert.Single(e.CandidatosCep).UsarCommand.Execute(null);

        Assert.Equal("35790-001", e.Cep);
        Assert.Equal(0, conferencias);
    }

    // ---- 26. PF, PJ e estrangeiro ----

    [Theory]
    [InlineData(NaturezaPessoa.Fisica)]
    [InlineData(NaturezaPessoa.Juridica)]
    [InlineData(NaturezaPessoa.Estrangeiro)]
    public async Task Busca_funciona_igual_para_pf_pj_e_estrangeiro_com_endereco_no_brasil(NaturezaPessoa natureza)
    {
        var (_, ambiente, e, _) = await FichaAsync(natureza);
        ambiente.Servidor.Responder(HttpStatusCode.OK, Busca(ResultadoDecisaoCep.UmCandidato, "35790001"));

        await e.BuscarCepPorEnderecoCommand.ExecuteAsync(null);

        Assert.Equal(1, Buscas(ambiente));
        Assert.Single(e.CandidatosCep);
    }

    [Fact]
    public async Task Endereco_no_exterior_nao_busca_cep_brasileiro()
    {
        var (tela, ambiente, e, focos) = await FichaAsync(NaturezaPessoa.Estrangeiro);
        e.NoExterior = true;
        var chamadas = ambiente.Servidor.Recebidas.Count;

        await e.BuscarCepPorEnderecoCommand.ExecuteAsync(null);

        Assert.Equal(chamadas, ambiente.Servidor.Recebidas.Count);
        Assert.Contains("só para endereço no Brasil", tela.Mensagem);
        Assert.Single(focos);
    }

    // ---- 27. D4: sem busca por texto livre ----

    [Fact]
    public void O_pedido_de_busca_tem_so_os_campos_estruturados()
    {
        var campos = typeof(BuscarCepPorEnderecoRequisicao).GetProperties().Select(p => p.Name).OrderBy(n => n);

        Assert.Equal(["Bairro", "Cidade", "CodigoMunicipioIbge", "Logradouro", "Numero", "Uf"], campos);
    }

    // ---- Revisão noturna: concorrência, cancelamento, respostas e normalização ----

    [Fact]
    public async Task Trocar_de_ficha_durante_a_busca_cancela_e_nada_chega_a_ficha_nova()
    {
        var (tela, ambiente, e, _) = await FichaAsync();
        var antes = Endereco(e);
        var busca = await BuscaEmAndamentoAsync(ambiente, e);
        ambiente.Servidor.Segurar = null; // só a busca fica presa; o resto responde normalmente

        await tela.NovoCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromSeconds(5)); // outra ficha (confirma descartar)
        await busca.WaitAsync(TimeSpan.FromSeconds(5));                                 // a busca da ficha anterior terminou

        Assert.NotSame(e, tela.Formulario!.Enderecos[0]);
        Assert.False(e.ProcurandoCep);
        Assert.Equal(antes, Endereco(e));
        Assert.False(e.TemCandidatosCep);
        Assert.False(tela.Formulario.Enderecos[0].ProcurandoCep);
        Assert.False(tela.Formulario.Enderecos[0].TemConferenciaCep);
        Assert.False(tela.TemMensagem);                // nem erro, nem "indisponível" na ficha nova
        Assert.Equal(1, Buscas(ambiente));
    }

    [Fact]
    public async Task Resposta_que_chega_depois_de_editar_o_endereco_e_descartada()
    {
        var (tela, ambiente, e, _) = await FichaAsync();
        ambiente.Servidor.Responder(HttpStatusCode.OK, Busca(ResultadoDecisaoCep.UmCandidato, "35790001"));
        var busca = await BuscaEmAndamentoAsync(ambiente, e);

        e.Logradouro = "Rua Outra";                   // o usuário mudou o endereço enquanto a busca andava
        ambiente.Servidor.Segurar!.SetResult();
        await busca.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(e.TemCandidatosCep);             // a resposta era do endereço anterior
        Assert.Equal("", e.Cep);
        Assert.Equal("Rua Outra", e.Logradouro);
        Assert.Contains("O endereço mudou durante a busca", tela.Mensagem);
        Assert.False(e.ProcurandoCep);
    }

    [Fact]
    public async Task Resposta_que_chega_depois_de_cancelar_nao_aparece()
    {
        var (tela, ambiente, e, _) = await FichaAsync();
        ambiente.Servidor.Responder(HttpStatusCode.OK, Busca(ResultadoDecisaoCep.UmCandidato, "35790001"));
        var busca = await BuscaEmAndamentoAsync(ambiente, e);

        e.CancelarBuscaCepCommand.Execute(null);
        ambiente.Servidor.Segurar!.TrySetResult();    // a resposta "chega" logo depois do cancelamento
        await busca.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(e.TemCandidatosCep);
        Assert.False(e.TemConferenciaCep);
        Assert.Equal("", e.Cep);
        Assert.False(tela.TemMensagem);
        e.CancelarBuscaCepCommand.Execute(null);      // cancelar de novo, sem busca: nada acontece (nem exceção)
        Assert.False(e.ProcurandoCep);
    }

    [Fact]
    public async Task Clique_repetido_em_usar_aplica_e_confere_uma_vez_so()
    {
        var (_, ambiente, e, _) = await FichaAsync();
        ambiente.Servidor.Responder(HttpStatusCode.OK, Busca(ResultadoDecisaoCep.VariosCandidatos, "35790001", "35790002"));
        await e.BuscarCepPorEnderecoCommand.ExecuteAsync(null);
        ambiente.Servidor.ResponderEm(CaminhoConferir, HttpStatusCode.OK,
            Conferencia(ResultadoDecisaoCep.Conferido, "35790002", "O CEP corresponde ao endereço informado."));
        var usar = e.CandidatosCep.Single(c => c.Candidato.Cep == "35790002").UsarCommand;

        usar.Execute(null);
        usar.Execute(null);                           // clique duplo no mesmo botão
        await e.ConferenciaAposUsar.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, ambiente.Servidor.Recebidas.Count(r => r.Caminho == CaminhoConferir));
        Assert.Equal("35790-002", e.Cep);
        Assert.Contains("O CEP corresponde ao endereço informado.", e.TextoConferenciaCep); // a conferência aguardada é a real
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Status_inesperado_da_api_vira_mensagem_amigavel(HttpStatusCode status)
    {
        var (tela, ambiente, e, _) = await FichaAsync();
        var antes = Endereco(e);
        ambiente.Servidor.Responder(status);

        await e.BuscarCepPorEnderecoCommand.ExecuteAsync(null);

        Assert.Equal(PessoasViewModel.MensagemBuscaCepIndisponivel, tela.Mensagem);
        Assert.Equal(antes, Endereco(e));
        Assert.False(e.ProcurandoCep);
    }

    [Fact]
    public async Task Dados_recusados_pela_api_mostram_a_mensagem_da_regra()
    {
        var (tela, ambiente, e, _) = await FichaAsync();
        ambiente.Servidor.Problema(HttpStatusCode.BadRequest, ErrosApi.Validacao, "Informe UF, cidade e logradouro para buscar o CEP.");

        await e.BuscarCepPorEnderecoCommand.ExecuteAsync(null);

        Assert.Equal("Informe UF, cidade e logradouro para buscar o CEP.", tela.Mensagem); // a regra, não "indisponível"
        Assert.Equal(TipoMensagem.Erro, tela.TipoMensagem);
        Assert.False(e.ProcurandoCep);
    }

    [Fact]
    public async Task Candidato_com_campos_ausentes_mostra_so_o_que_existe()
    {
        var (_, ambiente, e, _) = await FichaAsync();
        var busca = Busca(ResultadoDecisaoCep.UmCandidato);
        busca.Candidatos = [new CandidatoCepDto { Cep = "35790001", Cidade = "Curvelo", Uf = "MG" }];
        ambiente.Servidor.Responder(HttpStatusCode.OK, busca);

        await e.BuscarCepPorEnderecoCommand.ExecuteAsync(null);

        var opcao = Assert.Single(e.CandidatosCep);
        Assert.StartsWith("Usar 35790-001", opcao.Texto);
        Assert.DoesNotContain("null", opcao.Texto + opcao.Detalhe, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("·  ·", opcao.Texto);
        Assert.Equal(("R. Barao", "150", "Apto 2", "Centro"), (e.Logradouro, e.Numero, e.Complemento, e.Bairro));
    }

    [Theory]
    [InlineData("Rua Barão")]
    [InlineData("rua barao")]
    [InlineData("RUA BARÃO")]
    [InlineData("  R.   Barão  ")]
    [InlineData("Av. JK")]
    [InlineData("Rua 7")]
    public async Task Logradouro_escrito_de_varios_jeitos_pode_ser_buscado(string logradouro)
    {
        var (_, ambiente, e, focos) = await FichaAsync();
        e.Logradouro = logradouro;
        ambiente.Servidor.Responder(HttpStatusCode.OK, Busca(ResultadoDecisaoCep.NenhumCandidato));

        await e.BuscarCepPorEnderecoCommand.ExecuteAsync(null);

        Assert.Equal(1, Buscas(ambiente));
        Assert.Empty(focos);
        Assert.Equal(logradouro, e.Logradouro);       // a busca nunca reescreve o que foi digitado
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("R.")]
    [InlineData("--")]
    [InlineData("Ab")]
    public async Task Logradouro_sem_letras_suficientes_nao_e_buscado(string logradouro)
    {
        var (_, ambiente, e, focos) = await FichaAsync();
        e.Logradouro = logradouro;

        await e.BuscarCepPorEnderecoCommand.ExecuteAsync(null);

        Assert.Equal(0, Buscas(ambiente));
        Assert.Equal(CamposFichaPessoa.Logradouro, Assert.Single(focos).Campo);
    }

    // ---- Orientação local junto do "Não sei o CEP" (janela de fechamento, 07/10/2026) ----

    [Theory]
    [InlineData("uf", CamposFichaPessoa.Municipio, "Para encontrar o CEP pelo endereço, escolha a UF.")]
    [InlineData("municipio", CamposFichaPessoa.Municipio, "Para encontrar o CEP pelo endereço, escolha o município na lista.")]
    [InlineData("logradouro", CamposFichaPessoa.Logradouro, "Para encontrar o CEP pelo endereço, informe o logradouro (pelo menos 3 letras).")]
    [InlineData("logradouro-curto", CamposFichaPessoa.Logradouro, "Para encontrar o CEP pelo endereço, informe o logradouro (pelo menos 3 letras).")]
    public async Task Faltou_dado_mostra_a_orientacao_junto_da_acao_alem_da_faixa_e_do_foco(string falta, string campo, string mensagem)
    {
        var (tela, ambiente, e, focos) = await FichaAsync();
        switch (falta)
        {
            case "uf": e.Municipio.Definir(null, null, null); break;
            case "municipio": e.Municipio.Definir(null, null, "MG"); break;
            case "logradouro": e.Logradouro = ""; break;
            case "logradouro-curto": e.Logradouro = "Ab"; break;
        }
        var chamadas = ambiente.Servidor.Recebidas.Count;

        await e.BuscarCepPorEnderecoCommand.ExecuteAsync(null);

        Assert.True(e.TemOrientacaoBuscaCep);
        Assert.Equal(mensagem, e.OrientacaoBuscaCep);                  // a orientação local é a mesma regra e o mesmo texto
        Assert.Equal(mensagem, tela.Mensagem);                          // a faixa da ficha continua
        Assert.Equal(new DestinoCampo(campo, e.Id), Assert.Single(focos)); // o foco continua
        Assert.Equal(chamadas, ambiente.Servidor.Recebidas.Count);      // nenhuma chamada HTTP
        Assert.False(e.ProcurandoCep);
    }

    [Fact]
    public async Task Corrigir_o_dado_atualiza_e_depois_tira_a_orientacao()
    {
        var (_, _, e, _) = await FichaAsync();
        e.Municipio.Definir(null, null, null);
        await e.BuscarCepPorEnderecoCommand.ExecuteAsync(null);
        Assert.Contains("escolha a UF", e.OrientacaoBuscaCep);

        e.Municipio.Definir(null, null, "MG");                          // UF escolhida: agora falta o município
        Assert.Contains("escolha o município na lista", e.OrientacaoBuscaCep);

        e.Municipio.Definir(3120904, "Curvelo", "MG");                  // tudo certo: a orientação some
        Assert.False(e.TemOrientacaoBuscaCep);
        Assert.Equal("", e.OrientacaoBuscaCep);
    }

    [Fact]
    public async Task Corrigir_o_logradouro_tira_a_orientacao()
    {
        var (_, _, e, _) = await FichaAsync();
        e.Logradouro = "R.";
        await e.BuscarCepPorEnderecoCommand.ExecuteAsync(null);
        Assert.True(e.TemOrientacaoBuscaCep);

        e.Logradouro = "Rua Barão";

        Assert.False(e.TemOrientacaoBuscaCep);
    }

    [Fact]
    public async Task Sem_clicar_em_nao_sei_o_cep_nenhuma_orientacao_aparece()
    {
        var (_, _, e, _) = await FichaAsync();

        e.Municipio.Definir(null, null, null);
        e.Logradouro = "";

        Assert.False(e.TemOrientacaoBuscaCep);                          // a orientação só responde ao clique
    }

    [Fact]
    public async Task Busca_valida_depois_da_correcao_nao_mostra_procurando_junto_de_orientacao()
    {
        var (tela, ambiente, e, _) = await FichaAsync();
        e.Logradouro = "";
        await e.BuscarCepPorEnderecoCommand.ExecuteAsync(null);
        Assert.True(e.TemOrientacaoBuscaCep);
        e.Logradouro = "R. Barao";
        ambiente.Servidor.Responder(HttpStatusCode.OK, Busca(ResultadoDecisaoCep.UmCandidato, "35790001"));

        var busca = await BuscaEmAndamentoAsync(ambiente, e);
        Assert.True(e.ProcurandoCep);
        Assert.False(e.TemOrientacaoBuscaCep);
        Assert.False(tela.TemMensagem);                                 // a faixa antiga também saiu
        ambiente.Servidor.Segurar!.SetResult();
        await busca.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Single(e.CandidatosCep);
        Assert.False(e.TemOrientacaoBuscaCep);
    }

    [Fact]
    public async Task Cancelar_nao_traz_de_volta_orientacao_antiga()
    {
        var (tela, ambiente, e, _) = await FichaAsync();
        e.Municipio.Definir(null, null, null);
        await e.BuscarCepPorEnderecoCommand.ExecuteAsync(null);
        e.Municipio.Definir(3120904, "Curvelo", "MG");

        var busca = await BuscaEmAndamentoAsync(ambiente, e);
        e.CancelarBuscaCepCommand.Execute(null);
        await busca.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(e.TemOrientacaoBuscaCep);
        Assert.False(tela.TemMensagem);
        Assert.False(e.TemCandidatosCep);
    }

    [Fact]
    public async Task Nova_pesquisa_mostra_so_a_orientacao_do_que_falta_agora_sem_resultado_antigo()
    {
        var (_, ambiente, e, _) = await FichaAsync();
        ambiente.Servidor.Responder(HttpStatusCode.OK, Busca(ResultadoDecisaoCep.VariosCandidatos, "35790001", "35790002"));
        await e.BuscarCepPorEnderecoCommand.ExecuteAsync(null);
        Assert.Equal(2, e.CandidatosCep.Count);

        e.Municipio.Definir(null, null, null);
        await e.BuscarCepPorEnderecoCommand.ExecuteAsync(null);

        Assert.Contains("escolha a UF", e.OrientacaoBuscaCep);
        Assert.False(e.TemCandidatosCep);                               // nada de resultado antigo junto da orientação
        Assert.False(e.TemConferenciaCep);
    }

    [Fact]
    public async Task Trocar_de_ficha_nao_leva_a_orientacao()
    {
        var (tela, _, e, _) = await FichaAsync();
        e.Logradouro = "";
        await e.BuscarCepPorEnderecoCommand.ExecuteAsync(null);
        Assert.True(e.TemOrientacaoBuscaCep);

        await tela.NovoCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromSeconds(5));

        var nova = tela.Formulario!.Enderecos[0];
        Assert.NotSame(e, nova);
        Assert.False(nova.TemOrientacaoBuscaCep);
        Assert.False(tela.TemMensagem);
    }

    [Fact]
    public async Task Digitar_o_cep_tira_a_orientacao_e_a_consulta_de_cep_segue_igual()
    {
        var (_, ambiente, e, _) = await FichaAsync();
        e.Logradouro = "";
        await e.BuscarCepPorEnderecoCommand.ExecuteAsync(null);
        Assert.True(e.TemOrientacaoBuscaCep);
        ambiente.Servidor.Responder(HttpStatusCode.OK, new DadosCep
        {
            Cep = "35790000", Logradouro = "Rua Barão", Bairro = "Centro", Cidade = "Curvelo", Uf = "MG", CodigoMunicipioIbge = "3120904"
        });

        e.Cep = "35790-000";
        await EsperarAsync(() => ambiente.Servidor.Recebidas[^1].Caminho == "/" + Rotas.Consultas.Cep("35790000"));

        Assert.False(e.TemOrientacaoBuscaCep);
        Assert.Equal("35790-000", e.Cep);
    }

    [Fact]
    public async Task Marcar_exterior_troca_a_orientacao_pelo_motivo_certo()
    {
        var (_, _, e, _) = await FichaAsync(NaturezaPessoa.Estrangeiro);
        e.Logradouro = "";
        await e.BuscarCepPorEnderecoCommand.ExecuteAsync(null);
        Assert.Contains("informe o logradouro", e.OrientacaoBuscaCep);

        e.NoExterior = true;

        Assert.Contains("só para endereço no Brasil", e.OrientacaoBuscaCep);
    }

    [Fact]
    public async Task Usar_e_conferir_continua_igual_depois_de_uma_orientacao()
    {
        var (_, ambiente, e, _) = await FichaAsync();
        e.Logradouro = "";
        await e.BuscarCepPorEnderecoCommand.ExecuteAsync(null);
        e.Logradouro = "R. Barao";
        ambiente.Servidor.Responder(HttpStatusCode.OK, Busca(ResultadoDecisaoCep.UmCandidato, "35790001"));
        await e.BuscarCepPorEnderecoCommand.ExecuteAsync(null);
        ambiente.Servidor.ResponderEm(CaminhoConferir, HttpStatusCode.OK,
            Conferencia(ResultadoDecisaoCep.Conferido, "35790001", "O CEP corresponde ao endereço informado."));

        e.CandidatosCep.Single().UsarCommand.Execute(null);
        await e.ConferenciaAposUsar.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("35790-001", e.Cep);
        Assert.Equal(1, ambiente.Servidor.Recebidas.Count(r => r.Caminho == CaminhoConferir));
        Assert.Equal(GravidadeConferenciaCep.Ok, e.GravidadeConferenciaCep);
        Assert.False(e.TemOrientacaoBuscaCep);
        Assert.Equal(("150", "Apto 2"), (e.Numero, e.Complemento));
    }
}
