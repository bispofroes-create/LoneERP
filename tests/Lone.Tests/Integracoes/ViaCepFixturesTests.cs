using System.Net;
using System.Text.Json;
using Lone.Application.Integracoes.ConferenciaCep;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Integracoes;
using Lone.Domain.Enderecos.ConferenciaCep;
using Lone.Infrastructure.Integracoes.Cep;
using Microsoft.Extensions.DependencyInjection;

namespace Lone.Tests.Integracoes;

/// <summary>
/// Fase 1 (T-3): respostas <b>reais</b> do ViaCEP, guardadas como fixtures (Integracoes/Fixtures/ViaCep, capturadas no teste
/// real de 05/10/2026: busca SP/São Paulo/Paulista com 50 itens, o CEP 01310-100 e o inexistente 01310-999). O servidor HTTP é
/// falso: nenhum teste depende do ViaCEP no ar. Cobre o parser do provedor, a leitura de faixa, lado e prédio, e o motor
/// sobre a lista real.
/// </summary>
public class ViaCepFixturesTests
{
    private const string BuscaPaulista = "busca-SP-Sao-Paulo-Paulista.json";
    private const string Cep01310100 = "cep-01310100.json";
    private const string Cep01310999 = "cep-01310999-inexistente.json";

    private static string Fixture(string nome)
    {
        using var s = typeof(ViaCepFixturesTests).Assembly.GetManifestResourceStream("ViaCep/" + nome)
                      ?? throw new InvalidOperationException($"Fixture {nome} não encontrada.");
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }

    /// <summary>O ViaCEP falso: devolve a fixture certa pelo caminho pedido.</summary>
    private static ProvedoresCepTests.ServidorFalso ViaCepGravado() => new((req, _) =>
    {
        var caminho = Uri.UnescapeDataString(req.RequestUri!.AbsolutePath);
        var arquivo = caminho.Contains("/SP/São Paulo/Paulista/", StringComparison.Ordinal) ? BuscaPaulista
            : caminho.Contains("01310100", StringComparison.Ordinal) ? Cep01310100
            : caminho.Contains("01310999", StringComparison.Ordinal) ? Cep01310999
            : null;
        return Task.FromResult(arquivo is null
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : ProvedoresCepTests.ServidorFalso.Json(Fixture(arquivo)));
    });

    private static async Task<ResultadoBuscaProvedorCep> BuscarPaulista()
    {
        using var sp = ProvedoresCepTests.Servicos(ViaCepGravado());
        return await sp.GetRequiredService<ViaCepProvedor>().BuscarPorEnderecoAsync(new BuscaEnderecoCep("SP", "São Paulo", "Paulista"));
    }

    // ---- As fixtures guardam os campos reais ----

    [Fact]
    public void Fixture_da_busca_tem_os_campos_reais_do_ViaCep()
    {
        using var json = JsonDocument.Parse(Fixture(BuscaPaulista));
        var itens = json.RootElement.EnumerateArray().ToList();

        Assert.Equal(50, itens.Count);
        foreach (var campo in new[] { "cep", "logradouro", "complemento", "unidade", "bairro", "localidade", "uf", "ibge" })
            Assert.All(itens, i => Assert.True(i.TryGetProperty(campo, out _), $"sem o campo {campo}"));
        var predio = itens.Single(i => i.GetProperty("cep").GetString() == "01310-911");
        Assert.Equal(("960", "Edifício Paulicéia"), (predio.GetProperty("complemento").GetString(), predio.GetProperty("unidade").GetString()));
    }

    // ---- Provedor: parser sobre a resposta real ----

    [Fact]
    public async Task Busca_real_com_50_itens_traduz_todos_e_marca_o_limite()
    {
        var r = await BuscarPaulista();

        Assert.Equal(SituacaoProvedorCep.Encontrado, r.Situacao);
        Assert.Equal(50, r.Registros.Count);
        Assert.True(r.LimiteAtingido);                               // a lista pode estar cortada (D-F2-1 C)
        Assert.All(r.Registros, x => Assert.Equal(8, x.Cep.Length));
        Assert.Contains(new RegistroCep("01310911", "Avenida Paulista", "960", "Bela Vista", "São Paulo", "SP", "3550308", "Edifício Paulicéia"), r.Registros); // Checkpoint D: unidade lida (só explicação)
        Assert.Contains(new RegistroCep("01311000", "Avenida Paulista", "até 609 - lado ímpar", "Bela Vista", "São Paulo", "SP", "3550308"), r.Registros);
        Assert.Contains(new RegistroCep("05440001", "Rua Paulistânia", "de 453/454 ao fim", "Sumarezinho", "São Paulo", "SP", "3550308"), r.Registros);
        Assert.Contains(new RegistroCep("08190461", "Viela Paulista", null, "Vila Itaim", "São Paulo", "SP", "3550308"), r.Registros);
    }

    [Fact]
    public async Task Consulta_real_por_cep_traduz_os_campos()
    {
        using var sp = ProvedoresCepTests.Servicos(ViaCepGravado());

        var r = await sp.GetRequiredService<ViaCepProvedor>().ConsultarPorCepAsync("01310-100");

        Assert.Equal(SituacaoProvedorCep.Encontrado, r.Situacao);
        Assert.Equal(new RegistroCep("01310100", "Avenida Paulista", "de 612 a 1510 - lado par", "Bela Vista", "São Paulo", "SP", "3550308"),
            r.Registro);
    }

    [Fact]
    public async Task Cep_inexistente_real_com_erro_em_texto_e_nao_encontrado_e_nao_falha()
    {
        using var sp = ProvedoresCepTests.Servicos(ViaCepGravado());

        var r = await sp.GetRequiredService<ViaCepProvedor>().ConsultarPorCepAsync("01310-999");

        Assert.Equal(SituacaoProvedorCep.NaoEncontrado, r.Situacao); // { "erro": "true" } (texto) é resultado, não falha técnica
    }

    // ---- FaixaNumeracao: todos os complementos reais são lidos ----

    [Fact]
    public async Task Todo_complemento_real_da_busca_e_interpretado()
    {
        var r = await BuscarPaulista();

        Assert.All(r.Registros, x => Assert.NotNull(FaixaNumeracao.Interpretar(x.Complemento)));
    }

    [Theory]
    [InlineData("de 612 a 1510 - lado par", 612, 1510, LadoFaixa.Par)]
    [InlineData("de 1512 a 2132 - lado par", 1512, 2132, LadoFaixa.Par)]
    [InlineData("de 2134 ao fim - lado par", 2134, null, LadoFaixa.Par)]
    [InlineData("até 609 - lado ímpar", null, 609, LadoFaixa.Impar)]
    [InlineData("de 611 a 1045 - lado ímpar", 611, 1045, LadoFaixa.Impar)]
    [InlineData("de 1047 a 1865 - lado ímpar", 1047, 1865, LadoFaixa.Impar)]
    [InlineData("até 451/452", null, 452, LadoFaixa.Ambos)]
    [InlineData("de 453/454 ao fim", 453, null, LadoFaixa.Ambos)]
    [InlineData("960", 960, 960, LadoFaixa.Ambos)]
    [InlineData("1374 12 Andar", 1374, 1374, LadoFaixa.Ambos)]
    [InlineData("37 3 Andar Conjunto 31 e 32", 37, 37, LadoFaixa.Ambos)]
    public void Complementos_reais_viram_faixa_lado_ou_predio(string complemento, int? inicio, int? fim, LadoFaixa lado)
    {
        var f = FaixaNumeracao.Interpretar(complemento)!;

        Assert.Equal((inicio, fim, lado), (f.Inicio, f.Fim, f.Lado));
    }

    [Theory]
    [InlineData("de 612 a 1510 - lado par", "1000", PertinenciaFaixa.Dentro)]
    [InlineData("de 612 a 1510 - lado par", "612", PertinenciaFaixa.Dentro)]
    [InlineData("de 612 a 1510 - lado par", "1510", PertinenciaFaixa.Dentro)]
    [InlineData("de 612 a 1510 - lado par", "1001", PertinenciaFaixa.Fora)]    // ímpar
    [InlineData("de 612 a 1510 - lado par", "1512", PertinenciaFaixa.Fora)]
    [InlineData("até 609 - lado ímpar", "609", PertinenciaFaixa.Dentro)]
    [InlineData("até 609 - lado ímpar", "608", PertinenciaFaixa.Fora)]         // par
    [InlineData("até 609 - lado ímpar", "611", PertinenciaFaixa.Fora)]
    [InlineData("de 2134 ao fim - lado par", "9998", PertinenciaFaixa.Dentro)]
    [InlineData("de 2134 ao fim - lado par", "2132", PertinenciaFaixa.Fora)]
    [InlineData("960", "960", PertinenciaFaixa.Dentro)]
    [InlineData("960", "962", PertinenciaFaixa.Fora)]
    [InlineData("960", "S/N", PertinenciaFaixa.Indeterminado)]               // número indeterminado não elimina
    [InlineData("de 612 a 1510 - lado par", "KM 23", PertinenciaFaixa.Indeterminado)]
    public void Paridade_faixa_e_predio_reais(string complemento, string numero, PertinenciaFaixa esperado)
    {
        Assert.Equal(esperado, FaixaNumeracao.Interpretar(complemento)!.Contem(numero));
    }

    // ---- Motor sobre a lista real (CEP 01310-999 inexistente na Av. Paulista) ----

    private static async Task<DecisaoCep> DecidirNaPaulista(string numero)
    {
        var r = await BuscarPaulista();
        var endereco = new EnderecoConferenciaCep("01310-999", "Av. Paulista", numero, "Bela Vista", "São Paulo", "SP", "3550308");
        return MotorCep.Decidir(endereco, RespostaConsultaCep.NaoEncontrado(CepFonte.ViaCep),
            RespostaBuscaEndereco.Realizada(r.Registros, CepFonte.ViaCep));
    }

    [Theory]
    [InlineData("1000", ResultadoDecisaoCep.UmCandidato, new[] { "01310100" })]                         // só a faixa par
    [InlineData("960", ResultadoDecisaoCep.VariosCandidatos, new[] { "01310100", "01310911" })]         // faixa + prédio
    [InlineData("1374", ResultadoDecisaoCep.VariosCandidatos, new[] { "01310100", "01310916", "01310946" })]
    [InlineData("807", ResultadoDecisaoCep.VariosCandidatos, new[] { "01311100", "01311915", "01311941" })]
    [InlineData("2500", ResultadoDecisaoCep.UmCandidato, new[] { "01310300" })]                         // "de 2134 ao fim - lado par"
    [InlineData("2501", ResultadoDecisaoCep.NenhumCandidato, new string[0])]                             // a faixa ímpar não veio nos 50
    public async Task Decisao_sobre_a_lista_real(string numero, ResultadoDecisaoCep resultado, string[] ceps)
    {
        var d = await DecidirNaPaulista(numero);

        Assert.Equal(resultado, d.Resultado);
        Assert.Equal(ceps, d.Candidatos.Select(c => c.Cep));
        Assert.DoesNotContain(d.Candidatos, c => c.Logradouro != "Avenida Paulista"); // Viela Paulista e Rua Paulistânia saem
    }

    [Fact]
    public async Task Sem_numero_nada_e_eliminado_pela_faixa_e_nada_e_escolhido()
    {
        var d = await DecidirNaPaulista("S/N");

        Assert.Equal(ResultadoDecisaoCep.VariosCandidatos, d.Resultado);
        Assert.Equal(47, d.Candidatos.Count);                      // os 47 da Av. Paulista na Bela Vista (como no teste real)
        Assert.Null(d.CepSugerido);
    }

    [Fact]
    public async Task Na_tela_o_predio_do_numero_vem_antes_da_faixa_sem_escolher()
    {
        var d = await DecidirNaPaulista("1374");
        var dto = new DecisaoCepDto
        {
            Resultado = d.Resultado, CepInformado = d.CepInformado, Fonte = d.Fonte,
            Candidatos = d.Candidatos.Select(c => new CandidatoCepDto { Cep = c.Cep, Logradouro = c.Logradouro, Faixa = c.Complemento, Bairro = c.Bairro })
                .ToList()
        };

        var ordem = EnderecoFormulario.OrdenarParaExibicao(dto.Candidatos, "1374");

        Assert.Equal(["01310916", "01310946", "01310100"], ordem.Select(c => c.Cep));
        Assert.Equal(["01310100", "01310916", "01310946"], d.Candidatos.Select(c => c.Cep)); // a ordem do domínio não muda
    }

    // ---- Checkpoint C: componentes sobre a resposta real do 01310-100 (os exemplos do relatório) ----

    private static async Task<DecisaoCep> ConferirNoCep01310100(string logradouro, string numero, bool fonteForaDoAr = false)
    {
        using var sp = ProvedoresCepTests.Servicos(ViaCepGravado());
        var consulta = await sp.GetRequiredService<ViaCepProvedor>().ConsultarPorCepAsync("01310-100");
        var endereco = new EnderecoConferenciaCep("01310-100", logradouro, numero, "Bela Vista", "São Paulo", "SP", "3550308");
        return MotorCep.Decidir(endereco,
            fonteForaDoAr ? RespostaConsultaCep.Indisponivel(CepFonte.ViaCep) : RespostaConsultaCep.Encontrado(consulta.Registro!, CepFonte.ViaCep));
    }

    private static IEnumerable<(ComponenteCep, SituacaoComponenteCep, string)> Linhas(DecisaoCep d) =>
        d.Componentes.Select(c => (c.Componente, c.Situacao, c.Motivo));

    private const string CepExiste = "O CEP existe na fonte consultada (isso, sozinho, não confirma o endereço).";

    [Fact]
    public async Task Exemplo_real_totalmente_confirmado()
    {
        var d = await ConferirNoCep01310100("Av. Paulista", "1000");

        Assert.Equal(ResultadoDecisaoCep.Conferido, d.Resultado);
        Assert.Equal(["CEP conferido: corresponde ao endereço."], d.Motivos);
        Assert.Equal(
        [
            (ComponenteCep.Cep, SituacaoComponenteCep.Confirmado, CepExiste),
            (ComponenteCep.Uf, SituacaoComponenteCep.Confirmado, "UF confirmada (SP)."),
            (ComponenteCep.Municipio, SituacaoComponenteCep.Confirmado, "Município confirmado pelo código IBGE (São Paulo)."),
            (ComponenteCep.Logradouro, SituacaoComponenteCep.Confirmado, "Logradouro equivalente ao do CEP (Avenida Paulista)."),
            (ComponenteCep.Numero, SituacaoComponenteCep.Confirmado, "O número está na faixa do CEP (de 612 a 1510 - lado par).")
        ], Linhas(d));
    }

    [Fact]
    public async Task Exemplo_real_divergente_no_logradouro()
    {
        var d = await ConferirNoCep01310100("Rua Augusta", "1000");

        Assert.Equal(ResultadoDecisaoCep.Divergente, d.Resultado);
        Assert.Equal(["O CEP informado corresponde a outro logradouro (Avenida Paulista)."], d.Motivos);
        Assert.Equal((SituacaoComponenteCep.Divergente, "O CEP informado corresponde a outro logradouro (Avenida Paulista)."),
            (d.Componentes[3].Situacao, d.Componentes[3].Motivo));
        Assert.Equal(SituacaoComponenteCep.Confirmado, d.Componentes[4].Situacao); // o número cabe na faixa; o que diverge é a rua
    }

    [Fact]
    public async Task Exemplo_real_divergente_no_lado_da_rua()
    {
        var d = await ConferirNoCep01310100("Av. Paulista", "1001");

        Assert.Equal(ResultadoDecisaoCep.Divergente, d.Resultado);
        Assert.Equal((ComponenteCep.Numero, SituacaoComponenteCep.Divergente,
                      "O número informado é incompatível com a faixa do CEP (de 612 a 1510 - lado par)."), Linhas(d).Last());
    }

    [Fact]
    public async Task Exemplo_real_parcialmente_confirmado()
    {
        var d = await ConferirNoCep01310100("Av. Paulista", "S/N");

        Assert.Equal(ResultadoDecisaoCep.Conferido, d.Resultado);
        Assert.Equal(["CEP conferido: corresponde ao endereço.",
                      "O endereço não tem número legível; a faixa de numeração do CEP não foi conferida."], d.Motivos);
        Assert.Equal((ComponenteCep.Numero, SituacaoComponenteCep.NaoValidavel,
                      "O endereço não tem número legível; a faixa de numeração do CEP não foi conferida."), Linhas(d).Last());
        Assert.All(d.Componentes.Take(4), c => Assert.Equal(SituacaoComponenteCep.Confirmado, c.Situacao));
    }

    [Fact]
    public async Task Exemplo_real_fonte_indisponivel()
    {
        var d = await ConferirNoCep01310100("Av. Paulista", "1000", fonteForaDoAr: true);

        Assert.Equal(ResultadoDecisaoCep.FonteIndisponivel, d.Resultado);
        Assert.Equal((ComponenteCep.Cep, SituacaoComponenteCep.NaoValidavel, "A fonte não respondeu: o CEP não foi conferido."), Linhas(d).First());
        Assert.All(d.Componentes.Skip(1), c => Assert.Equal((SituacaoComponenteCep.NaoValidavel, "Não conferido: a fonte não respondeu."),
            (c.Situacao, c.Motivo)));
    }

    // ---- Checkpoint D: busca pelo endereço sem CEP sobre a lista real (D-1: 50 ≠ lista completa) ----

    /// <summary>
    /// O ViaCEP falso da busca: devolve a lista real da Paulista (50 itens) — ou só os <paramref name="itens"/> primeiros, para
    /// a lista abaixo do limite — ou falha (503) se <paramref name="foraDoAr"/>.
    /// </summary>
    private static ProvedoresCepTests.ServidorFalso BuscaGravada(int itens = 50, bool foraDoAr = false) => new((req, _) =>
    {
        if (foraDoAr) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var json = JsonDocument.Parse(Fixture(BuscaPaulista));
        var lista = json.RootElement.EnumerateArray().Take(itens).Select(e => e.GetRawText());
        return Task.FromResult(ProvedoresCepTests.ServidorFalso.Json("[" + string.Join(",", lista) + "]"));
    });

    private static async Task<ResultadoConferenciaCep> BuscarSemCep(string? numero, int itens = 50, bool foraDoAr = false)
    {
        using var sp = ProvedoresCepTests.Servicos(BuscaGravada(itens, foraDoAr));
        var servico = new ServicoConferenciaCep([sp.GetRequiredService<ViaCepProvedor>()], new CacheConferenciaCep(TimeProvider.System),
            new SugestoesCepEmitidas(TimeProvider.System), TimeProvider.System);
        return await servico.BuscarPorEnderecoAsync(
            new EnderecoConferenciaCep(null, "Av. Paulista", numero, "Bela Vista", "São Paulo", "SP", "3550308"));
    }

    [Fact]
    public async Task Exemplo_real_busca_sem_cep_nenhum_candidato_com_lista_limitada()
    {
        var r = await BuscarSemCep("2501"); // ímpar depois de 1865: a faixa ímpar dele não veio nos 50

        Assert.Equal(ResultadoDecisaoCep.NenhumCandidato, r.Decisao.Resultado);
        Assert.Empty(r.Decisao.Candidatos);
        Assert.Equal(["Não encontramos um CEP compatível com os dados informados."], r.Decisao.Motivos);
        Assert.Equal([ServicoConferenciaCep.AvisoBuscaSemCandidatoNoLimite], r.Avisos); // não prova inexistência
    }

    [Fact]
    public async Task Exemplo_real_busca_sem_cep_um_candidato()
    {
        var r = await BuscarSemCep("2500");

        Assert.Equal(ResultadoDecisaoCep.UmCandidato, r.Decisao.Resultado);
        var c = Assert.Single(r.Decisao.CandidatosAvaliados);
        Assert.Equal(("01310300", "de 2134 ao fim - lado par"), (c.Registro.Cep, c.Registro.Complemento));
        Assert.All(c.Componentes, x => Assert.Equal(SituacaoComponenteCep.Confirmado, x.Situacao));
        Assert.Null(r.Decisao.CepSugerido);
        Assert.Equal([ServicoConferenciaCep.AvisoBuscaListaIncompleta], r.Avisos);
    }

    [Fact]
    public async Task Exemplo_real_busca_sem_cep_varios_candidatos_com_predio_e_unidade()
    {
        var r = await BuscarSemCep("960");

        Assert.Equal(ResultadoDecisaoCep.VariosCandidatos, r.Decisao.Resultado);
        Assert.Equal(["01310100", "01310911"], r.Decisao.Candidatos.Select(c => c.Cep));
        var predio = r.Decisao.CandidatosAvaliados[1];
        Assert.Equal(("960", "Edifício Paulicéia"), (predio.Registro.Complemento, predio.Registro.Unidade));
        Assert.Equal("O número é o do prédio do CEP (960).", predio.Componentes.Single(x => x.Componente == ComponenteCep.Numero).Motivo);
        Assert.Equal("O número está na faixa do CEP (de 612 a 1510 - lado par).",
            r.Decisao.CandidatosAvaliados[0].Componentes.Single(x => x.Componente == ComponenteCep.Numero).Motivo);
    }

    [Fact]
    public async Task Exemplo_real_busca_sem_numero_mantem_todos_e_explica()
    {
        var r = await BuscarSemCep("S/N");

        Assert.Equal(47, r.Decisao.Candidatos.Count);
        Assert.All(r.Decisao.CandidatosAvaliados, c =>
            Assert.Equal(SituacaoComponenteCep.NaoValidavel, c.Componentes.Single(x => x.Componente == ComponenteCep.Numero).Situacao));
    }

    [Theory]
    [InlineData(49, false)] // abaixo do limite: sem aviso
    [InlineData(50, true)]  // exatamente no limite (ViaCepProvedor.LimiteBusca): lista possivelmente incompleta
    public async Task Limite_de_50_do_ViaCep_vira_aviso_de_lista_possivelmente_incompleta(int itens, bool aviso)
    {
        var r = await BuscarSemCep("960", itens);

        Assert.Equal(50, ViaCepProvedor.LimiteBusca);
        Assert.Equal(aviso, r.Avisos.Contains(ServicoConferenciaCep.AvisoBuscaListaIncompleta));
    }

    [Fact]
    public async Task Exemplo_real_busca_sem_cep_com_fonte_fora_do_ar()
    {
        var r = await BuscarSemCep("960", foraDoAr: true);

        Assert.Equal(ResultadoDecisaoCep.FonteIndisponivel, r.Decisao.Resultado);
        Assert.Empty(r.Decisao.Candidatos);
        Assert.Empty(r.Avisos);
    }

    // ---- F3: exemplo real de offline (resposta real do 01310-100 guardada; depois a fonte cai) ----

    private sealed class CacheEmMemoria : ICachePostalCep, IHistoricoConsultasCep
    {
        private readonly Dictionary<string, RespostaGuardadaCep> _itens = new();
        public List<ConsultaCepOcorrida> Historico { get; } = [];
        public Task<RespostaGuardadaCep?> ObterAsync(string chave, CancellationToken ct = default) =>
            Task.FromResult(_itens.TryGetValue(chave, out var r) ? r : null);
        public Task GuardarAsync(string chave, TipoConsultaCep tipo, string? cep, RespostaGuardadaCep resposta, CancellationToken ct = default)
        {
            _itens[chave] = resposta;
            return Task.CompletedTask;
        }
        public Task RegistrarAsync(ConsultaCepOcorrida consulta)
        {
            Historico.Add(consulta);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Exemplo_real_offline_mostra_a_ultima_informacao_do_viacep_sem_confirmar_agora()
    {
        var foraDoAr = false;
        var servidor = new ProvedoresCepTests.ServidorFalso((req, _) => Task.FromResult(foraDoAr
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : ProvedoresCepTests.ServidorFalso.Json(Fixture(Cep01310100))));
        using var sp = ProvedoresCepTests.Servicos(servidor);
        var banco = new CacheEmMemoria();
        var relogio = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(new DateTimeOffset(2026, 10, 5, 20, 30, 0, TimeSpan.Zero));
        ServicoConferenciaCep Servico() => new([sp.GetRequiredService<ViaCepProvedor>()], new CacheConferenciaCep(relogio),
            new SugestoesCepEmitidas(relogio), new ConferenciasCepEmitidas(relogio), relogio, banco, banco);
        var endereco = new EnderecoConferenciaCep("01310-100", "Av. Paulista", "1000", "Bela Vista", "São Paulo", "SP", "3550308");

        var online = await Servico().ConferirAsync(endereco);
        relogio.Advance(TimeSpan.FromDays(2));
        foraDoAr = true;
        var offline = await Servico().ConferirAsync(endereco);

        Assert.Equal(ResultadoDecisaoCep.Conferido, online.Decisao.Resultado);
        Assert.Equal(ResultadoDecisaoCep.FonteIndisponivel, offline.Decisao.Resultado);
        Assert.Equal((CepFonte.ViaCep, new DateTime(2026, 10, 5, 20, 30, 0)), (offline.Anterior!.Fonte, offline.Anterior.ConsultadoEm));
        Assert.Equal("de 612 a 1510 - lado par", offline.Anterior.Registro.Complemento);
        Assert.Equal(ResultadoDecisaoCep.Conferido, offline.Anterior.DecisaoComEla.Resultado);
        Assert.Equal([OrigemRespostaCep.Fonte, OrigemRespostaCep.Fonte, OrigemRespostaCep.InformacaoAnterior],
            banco.Historico.Select(h => h.Origem));
        Assert.Equal([ResultadoConsultaCep.Encontrado, ResultadoConsultaCep.Indisponivel, ResultadoConsultaCep.Encontrado],
            banco.Historico.Select(h => h.Resultado));
    }
}
