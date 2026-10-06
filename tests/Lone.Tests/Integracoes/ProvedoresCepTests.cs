using System.Net;
using System.Text;
using Lone.Application.Integracoes;
using Lone.Application.Integracoes.ConferenciaCep;
using Lone.Domain.Enderecos.ConferenciaCep;
using Lone.Infrastructure.Integracoes;
using Lone.Infrastructure.Integracoes.Cep;
using Microsoft.Extensions.DependencyInjection;

namespace Lone.Tests.Integracoes;

/// <summary>
/// F2 do motor de CEP: as fontes (ViaCEP e BrasilAPI) com o pipeline de resiliência de verdade
/// (Microsoft.Extensions.Http.Resilience) e um servidor HTTP falso. Resultado funcional (encontrado, inexistente) ≠ falha
/// técnica (indisponível, resposta inválida); nova tentativa só em falha técnica; disjuntor por fonte; cancelamento de
/// quem chamou não é falha.
/// </summary>
public class ProvedoresCepTests
{
    /// <summary>Servidor falso: responde por função e conta as chamadas por host.</summary>
    internal sealed class ServidorFalso : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;
        private int _viaCep;
        private int _brasilApi;

        public ServidorFalso(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) => _responder = responder;

        public int ChamadasViaCep => _viaCep;
        public int ChamadasBrasilApi => _brasilApi;
        public List<string> Caminhos { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.Host.Contains("viacep")) Interlocked.Increment(ref _viaCep);
            else Interlocked.Increment(ref _brasilApi);
            lock (Caminhos) Caminhos.Add(request.RequestUri.AbsolutePath);
            return _responder(request, cancellationToken);
        }

        public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
            new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }

    /// <summary>Mesmo pipeline do sistema, com tempos curtos para o teste não demorar.</summary>
    internal static readonly OpcoesResilienciaCep Rapidas = new()
    {
        TempoPorTentativa = TimeSpan.FromMilliseconds(300),
        EsperaAntesDaNovaTentativa = TimeSpan.Zero,
        TempoTotalPorFonte = TimeSpan.FromSeconds(3),
        JanelaAmostragem = TimeSpan.FromSeconds(30),
        DuracaoAbertura = TimeSpan.FromSeconds(30)
    };

    internal static ServiceProvider Servicos(ServidorFalso servidor, OpcoesResilienciaCep? opcoes = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMetrics(); // a API já registra (host web); aqui o teste monta o mínimo
        services.AddLoneProvedoresCep(opcoes ?? Rapidas, b => b.ConfigurePrimaryHttpMessageHandler(() => servidor));
        return services.BuildServiceProvider();
    }

    private const string Curvelo = """
        { "cep": "35790-000", "logradouro": "Rua Barão", "complemento": "até 999/1000", "bairro": "Centro",
          "localidade": "Curvelo", "uf": "MG", "ibge": "3120904" }
        """;

    // ---- ViaCEP: consulta por CEP ----

    [Fact]
    public async Task ViaCep_encontrado_traduz_os_campos_sem_inventar()
    {
        var servidor = new ServidorFalso((_, _) => Task.FromResult(ServidorFalso.Json(Curvelo)));
        using var sp = Servicos(servidor);

        var r = await sp.GetRequiredService<ViaCepProvedor>().ConsultarPorCepAsync("35790-000");

        Assert.Equal(SituacaoProvedorCep.Encontrado, r.Situacao);
        Assert.Equal(CepFonte.ViaCep, r.Fonte);
        Assert.Equal(new RegistroCep("35790000", "Rua Barão", "até 999/1000", "Centro", "Curvelo", "MG", "3120904"), r.Registro);
        Assert.Equal(1, servidor.ChamadasViaCep);
        Assert.Equal("/ws/35790000/json/", servidor.Caminhos.Single());
    }

    [Fact]
    public async Task ViaCep_inexistente_e_resultado_funcional_sem_nova_tentativa()
    {
        var servidor = new ServidorFalso((_, _) => Task.FromResult(ServidorFalso.Json("""{ "erro": "true" }""")));
        using var sp = Servicos(servidor);

        var r = await sp.GetRequiredService<ViaCepProvedor>().ConsultarPorCepAsync("35790000");

        Assert.Equal(SituacaoProvedorCep.NaoEncontrado, r.Situacao);
        Assert.False(r.Situacao.EhFalhaTecnica());
        Assert.Equal(1, servidor.ChamadasViaCep);
    }

    [Fact]
    public async Task ViaCep_400_e_cep_nao_encontrado_sem_nova_tentativa()
    {
        var servidor = new ServidorFalso((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)));
        using var sp = Servicos(servidor);

        var r = await sp.GetRequiredService<ViaCepProvedor>().ConsultarPorCepAsync("35790000");

        Assert.Equal(SituacaoProvedorCep.NaoEncontrado, r.Situacao);
        Assert.Equal(1, servidor.ChamadasViaCep);
    }

    [Fact]
    public async Task Tempo_esgotado_tenta_de_novo_uma_vez_e_vira_indisponivel_nunca_nao_encontrado()
    {
        var servidor = new ServidorFalso(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return ServidorFalso.Json(Curvelo);
        });
        using var sp = Servicos(servidor);

        var r = await sp.GetRequiredService<ViaCepProvedor>().ConsultarPorCepAsync("35790000");

        Assert.Equal(SituacaoProvedorCep.Indisponivel, r.Situacao);
        Assert.NotEqual(SituacaoProvedorCep.NaoEncontrado, r.Situacao);
        Assert.Contains("tempo esgotado", r.Detalhe);
        Assert.Equal(2, servidor.ChamadasViaCep); // 1 + 1 nova tentativa
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    public async Task Erro_tecnico_transitorio_tenta_de_novo_uma_vez(HttpStatusCode status)
    {
        var servidor = new ServidorFalso((_, _) => Task.FromResult(new HttpResponseMessage(status)));
        using var sp = Servicos(servidor);

        var r = await sp.GetRequiredService<ViaCepProvedor>().ConsultarPorCepAsync("35790000");

        Assert.Equal(SituacaoProvedorCep.Indisponivel, r.Situacao);
        Assert.Equal(2, servidor.ChamadasViaCep);
    }

    [Fact]
    public async Task Falha_na_primeira_tentativa_e_sucesso_na_segunda_e_encontrado()
    {
        var vez = 0;
        var servidor = new ServidorFalso((_, _) => Task.FromResult(Interlocked.Increment(ref vez) == 1
            ? new HttpResponseMessage(HttpStatusCode.BadGateway)
            : ServidorFalso.Json(Curvelo)));
        using var sp = Servicos(servidor);

        var r = await sp.GetRequiredService<ViaCepProvedor>().ConsultarPorCepAsync("35790000");

        Assert.Equal(SituacaoProvedorCep.Encontrado, r.Situacao);
        Assert.Equal(2, servidor.ChamadasViaCep);
    }

    [Fact]
    public async Task Limite_de_taxa_429_e_indisponivel_sem_nova_tentativa()
    {
        var servidor = new ServidorFalso((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)));
        using var sp = Servicos(servidor);

        var r = await sp.GetRequiredService<ViaCepProvedor>().ConsultarPorCepAsync("35790000");

        Assert.Equal(SituacaoProvedorCep.Indisponivel, r.Situacao);
        Assert.Equal(1, servidor.ChamadasViaCep);
    }

    [Theory]
    [InlineData("{ isto não é json", 200)]
    [InlineData("[1, 2, 3]", 200)]
    [InlineData("""{ "cep": "35790-001", "logradouro": "Rua Outra" }""", 200)] // outro CEP
    [InlineData("""{ "logradouro": "Rua sem CEP" }""", 200)]
    [InlineData("", 404)]
    public async Task Resposta_fora_do_contrato_e_resposta_invalida_sem_nova_tentativa(string corpo, int status)
    {
        var servidor = new ServidorFalso((_, _) => Task.FromResult(ServidorFalso.Json(corpo, (HttpStatusCode)status)));
        using var sp = Servicos(servidor);

        var r = await sp.GetRequiredService<ViaCepProvedor>().ConsultarPorCepAsync("35790000");

        Assert.Equal(SituacaoProvedorCep.RespostaInvalida, r.Situacao);
        Assert.True(r.Situacao.EhFalhaTecnica());
        Assert.Null(r.Registro);
        Assert.Equal(1, servidor.ChamadasViaCep);
    }

    [Fact]
    public async Task Sem_conexao_e_indisponivel()
    {
        var servidor = new ServidorFalso((_, _) => throw new HttpRequestException("rede fora"));
        using var sp = Servicos(servidor);

        var r = await sp.GetRequiredService<ViaCepProvedor>().ConsultarPorCepAsync("35790000");

        Assert.Equal(SituacaoProvedorCep.Indisponivel, r.Situacao);
        Assert.Equal(2, servidor.ChamadasViaCep);
    }

    [Fact]
    public async Task Cancelamento_de_quem_chamou_sobe_como_cancelamento_sem_nova_tentativa()
    {
        // Quem chamou cancela enquanto a fonte responde (sem depender de tempo: o cancelamento sai do próprio servidor falso).
        using var cancelar = new CancellationTokenSource();
        var servidor = new ServidorFalso(async (_, ct) =>
        {
            await cancelar.CancelAsync();
            await Task.Delay(Timeout.Infinite, ct);
            return ServidorFalso.Json(Curvelo);
        });
        using var sp = Servicos(servidor);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            sp.GetRequiredService<ViaCepProvedor>().ConsultarPorCepAsync("35790000", cancelar.Token));
        Assert.Equal(1, servidor.ChamadasViaCep);
    }

    // ---- Disjuntor ----

    [Fact]
    public async Task Disjuntor_abre_com_falhas_tecnicas_e_para_de_chamar_a_fonte()
    {
        var servidor = new ServidorFalso((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        using var sp = Servicos(servidor);
        var viaCep = sp.GetRequiredService<ViaCepProvedor>();

        await viaCep.ConsultarPorCepAsync("35790000"); // 2 tentativas
        await viaCep.ConsultarPorCepAsync("35790000"); // 4 tentativas, todas falhas: abre
        var aberto = await viaCep.ConsultarPorCepAsync("35790000");

        Assert.Equal(4, servidor.ChamadasViaCep);       // a terceira não chegou à fonte
        Assert.Equal(SituacaoProvedorCep.Indisponivel, aberto.Situacao);
        Assert.Contains("disjuntor", aberto.Detalhe);
    }

    [Fact]
    public async Task Disjuntor_nao_abre_com_cep_inexistente()
    {
        var servidor = new ServidorFalso((_, _) => Task.FromResult(ServidorFalso.Json("""{ "erro": true }""")));
        using var sp = Servicos(servidor);
        var viaCep = sp.GetRequiredService<ViaCepProvedor>();

        for (var i = 0; i < 10; i++)
            Assert.Equal(SituacaoProvedorCep.NaoEncontrado, (await viaCep.ConsultarPorCepAsync("35790000")).Situacao);

        Assert.Equal(10, servidor.ChamadasViaCep);
    }

    [Fact]
    public async Task Disjuntor_e_independente_por_fonte()
    {
        var servidor = new ServidorFalso((req, _) => Task.FromResult(req.RequestUri!.Host.Contains("viacep")
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : ServidorFalso.Json("""{ "cep": "35790000", "state": "MG", "city": "Curvelo", "neighborhood": "Centro", "street": "Rua Barão" }""")));
        using var sp = Servicos(servidor);
        var viaCep = sp.GetRequiredService<ViaCepProvedor>();
        for (var i = 0; i < 3; i++) await viaCep.ConsultarPorCepAsync("35790000");

        var reserva = await sp.GetRequiredService<BrasilApiCepProvedor>().ConsultarPorCepAsync("35790000");

        Assert.Equal(SituacaoProvedorCep.Encontrado, reserva.Situacao);
        Assert.Equal(1, servidor.ChamadasBrasilApi);
    }

    // ---- ViaCEP: busca por endereço ----

    [Fact]
    public async Task Busca_por_endereco_devolve_todos_os_candidatos_sem_escolher()
    {
        var servidor = new ServidorFalso((_, _) => Task.FromResult(ServidorFalso.Json("""
            [ { "cep": "35790-001", "logradouro": "Rua Barão", "complemento": "até 999/1000", "bairro": "Centro", "localidade": "Curvelo", "uf": "MG", "ibge": "3120904" },
              { "cep": "35790-002", "logradouro": "Rua Barão", "complemento": "de 1001/1002 ao fim", "bairro": "Centro", "localidade": "Curvelo", "uf": "MG", "ibge": "3120904" } ]
            """)));
        using var sp = Servicos(servidor);

        var r = await sp.GetRequiredService<ViaCepProvedor>().BuscarPorEnderecoAsync(new BuscaEnderecoCep("MG", "São João del Rei", "RUA BARAO"));

        Assert.Equal(SituacaoProvedorCep.Encontrado, r.Situacao);
        Assert.Equal(["35790001", "35790002"], r.Registros.Select(x => x.Cep));
        Assert.Equal("/ws/MG/S%C3%A3o%20Jo%C3%A3o%20del%20Rei/RUA%20BARAO/json/", servidor.Caminhos.Single());
    }

    [Theory]
    [InlineData(50, true)]
    [InlineData(49, false)]
    public async Task Busca_com_50_resultados_marca_o_limite_do_ViaCep(int quantidade, bool limite)
    {
        var itens = string.Join(",", Enumerable.Range(1, quantidade).Select(i =>
            "{ \"cep\": \"01310-" + i.ToString("000") + "\", \"logradouro\": \"Avenida Paulista\", \"complemento\": \"" + i + "\" }"));
        var servidor = new ServidorFalso((_, _) => Task.FromResult(ServidorFalso.Json("[" + itens + "]")));
        using var sp = Servicos(servidor);

        var r = await sp.GetRequiredService<ViaCepProvedor>().BuscarPorEnderecoAsync(new BuscaEnderecoCep("SP", "São Paulo", "AVENIDA PAULISTA"));

        Assert.Equal(quantidade, r.Registros.Count);
        Assert.Equal(limite, r.LimiteAtingido);
        Assert.Equal(50, ViaCepProvedor.LimiteBusca);
    }

    [Fact]
    public async Task Busca_sem_resultado_e_nao_encontrado_funcional()
    {
        var servidor = new ServidorFalso((_, _) => Task.FromResult(ServidorFalso.Json("[]")));
        using var sp = Servicos(servidor);

        var r = await sp.GetRequiredService<ViaCepProvedor>().BuscarPorEnderecoAsync(new BuscaEnderecoCep("MG", "Curvelo", "RUA X"));

        Assert.Equal(SituacaoProvedorCep.NaoEncontrado, r.Situacao);
        Assert.Empty(r.Registros);
    }

    [Theory]
    [InlineData("""{ "qualquer": 1 }""", 200, SituacaoProvedorCep.RespostaInvalida)]
    [InlineData("""[ { "logradouro": "sem cep" } ]""", 200, SituacaoProvedorCep.RespostaInvalida)]
    [InlineData("", 400, SituacaoProvedorCep.RespostaInvalida)]
    [InlineData("", 503, SituacaoProvedorCep.Indisponivel)]
    public async Task Busca_com_falha_nao_vira_lista_vazia(string corpo, int status, SituacaoProvedorCep esperada)
    {
        var servidor = new ServidorFalso((_, _) => Task.FromResult(ServidorFalso.Json(corpo, (HttpStatusCode)status)));
        using var sp = Servicos(servidor);

        var r = await sp.GetRequiredService<ViaCepProvedor>().BuscarPorEnderecoAsync(new BuscaEnderecoCep("MG", "Curvelo", "RUA X"));

        Assert.Equal(esperada, r.Situacao);
        Assert.True(r.Situacao.EhFalhaTecnica());
    }

    // ---- BrasilAPI (reserva) ----

    [Fact]
    public async Task BrasilApi_encontrado_sem_ibge_nem_faixa_e_404_e_nao_encontrado()
    {
        var servidor = new ServidorFalso((req, _) => Task.FromResult(req.RequestUri!.AbsolutePath.EndsWith("35790000")
            ? ServidorFalso.Json("""{ "cep": "35790000", "state": "MG", "city": "Curvelo", "neighborhood": "Centro", "street": "Rua Barão", "service": "x" }""")
            : ServidorFalso.Json("""{ "name": "CepPromiseError" }""", HttpStatusCode.NotFound)));
        using var sp = Servicos(servidor);
        var brasilApi = sp.GetRequiredService<BrasilApiCepProvedor>();

        var achado = await brasilApi.ConsultarPorCepAsync("35790000");
        var inexistente = await brasilApi.ConsultarPorCepAsync("35790999");

        Assert.Equal(new RegistroCep("35790000", "Rua Barão", null, "Centro", "Curvelo", "MG", null), achado.Registro);
        Assert.Equal(CepFonte.BrasilApi, achado.Fonte);
        Assert.Equal(SituacaoProvedorCep.NaoEncontrado, inexistente.Situacao);
        Assert.Equal(2, servidor.ChamadasBrasilApi);
        Assert.Equal("/api/cep/v1/35790000", servidor.Caminhos[0]);
        Assert.False(brasilApi.BuscaPorEndereco);
    }

    // ---- Um caminho só até o ViaCEP: a consulta antiga usa a mesma integração ----

    [Fact]
    public async Task Consulta_antiga_usa_o_mesmo_provedor_e_mantem_o_contrato()
    {
        var resposta = Curvelo;
        var status = HttpStatusCode.OK;
        var servidor = new ServidorFalso((_, _) => Task.FromResult(ServidorFalso.Json(resposta, status)));
        using var sp = Servicos(servidor);
        var antiga = sp.GetRequiredService<ICepConsulta>();

        Assert.IsType<ViaCepProvedor>(antiga);
        Assert.Equal("Curvelo", (await antiga.ConsultarAsync("35790-000"))!.Cidade);

        resposta = """{ "erro": true }""";
        Assert.Null(await antiga.ConsultarAsync("35790000"));                          // inexistente = nulo (404 na rota)

        resposta = "";
        status = HttpStatusCode.ServiceUnavailable;
        await Assert.ThrowsAsync<ServicoExternoException>(() => antiga.ConsultarAsync("35790000")); // falha = 502
    }

    [Fact]
    public void A_ordem_das_fontes_e_ViaCep_depois_BrasilApi()
    {
        using var sp = Servicos(new ServidorFalso((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));

        Assert.Equal([CepFonte.ViaCep, CepFonte.BrasilApi], sp.GetServices<IProvedorCep>().Select(p => p.Fonte));
    }

    [Fact]
    public void Pipeline_so_repete_falha_tecnica_e_o_disjuntor_conta_tambem_o_429()
    {
        static Polly.Outcome<HttpResponseMessage> Status(HttpStatusCode s) => Polly.Outcome.FromResult(new HttpResponseMessage(s));

        Assert.True(ResilienciaCep.DeveRepetir(Status(HttpStatusCode.ServiceUnavailable)));
        Assert.True(ResilienciaCep.DeveRepetir(Status(HttpStatusCode.RequestTimeout)));
        Assert.True(ResilienciaCep.DeveRepetir(Polly.Outcome.FromException<HttpResponseMessage>(new HttpRequestException())));
        Assert.True(ResilienciaCep.DeveRepetir(Polly.Outcome.FromException<HttpResponseMessage>(new Polly.Timeout.TimeoutRejectedException())));
        Assert.False(ResilienciaCep.DeveRepetir(Status(HttpStatusCode.OK)));
        Assert.False(ResilienciaCep.DeveRepetir(Status(HttpStatusCode.BadRequest)));
        Assert.False(ResilienciaCep.DeveRepetir(Status(HttpStatusCode.NotFound)));
        Assert.False(ResilienciaCep.DeveRepetir(Status(HttpStatusCode.TooManyRequests)));
        Assert.False(ResilienciaCep.DeveRepetir(Polly.Outcome.FromException<HttpResponseMessage>(new OperationCanceledException())));
        Assert.True(ResilienciaCep.ContaParaDisjuntor(Status(HttpStatusCode.TooManyRequests)));
        Assert.False(ResilienciaCep.ContaParaDisjuntor(Status(HttpStatusCode.OK)));
        Assert.False(ResilienciaCep.ContaParaDisjuntor(Status(HttpStatusCode.NotFound)));
    }

    [Fact]
    public void Parametros_efetivos_do_pipeline()
    {
        var p = OpcoesResilienciaCep.Padrao;

        Assert.Equal(TimeSpan.FromSeconds(5), p.TempoPorTentativa);
        Assert.Equal(1, p.NovasTentativas);
        Assert.True(p.TempoPorTentativa * (p.NovasTentativas + 1) + p.EsperaAntesDaNovaTentativa <= p.TempoTotalPorFonte);
        // Duas fontes em sequência cabem no teto da conferência, abaixo dos 30 s do aplicativo.
        Assert.True(p.TempoTotalPorFonte * 2 <= ServicoConferenciaCep.TetoTotal);
        Assert.True(ServicoConferenciaCep.TetoTotal < TimeSpan.FromSeconds(30));
    }
}
