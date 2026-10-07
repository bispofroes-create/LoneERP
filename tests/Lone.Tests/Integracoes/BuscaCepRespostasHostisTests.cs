using System.Net;
using Lone.Application.Integracoes.ConferenciaCep;
using Lone.Domain.Enderecos.ConferenciaCep;
using Lone.Infrastructure.Integracoes.Cep;
using Microsoft.Extensions.DependencyInjection;
using static Lone.Tests.Integracoes.ProvedoresCepTests;

namespace Lone.Tests.Integracoes;

/// <summary>
/// Busca de CEP por endereço (D5), respostas hostis ou inesperadas do ViaCEP, com o pipeline de resiliência de verdade e
/// um servidor HTTP falso (nenhuma chamada real): status de erro, tempo esgotado, sem conexão, JSON vazio, malformado ou
/// parcial, campos nulos, CEP inválido e textos enormes. Falha técnica nunca vira "nenhum CEP"; resposta fora do contrato
/// é recusada inteira (nunca meia lista); nada é inventado; o número e o complemento nunca vão para a fonte.
/// </summary>
public class BuscaCepRespostasHostisTests
{
    private static readonly BuscaEnderecoCep Curvelo = new("MG", "Curvelo", "RUA BARAO");

    private static async Task<(ResultadoBuscaProvedorCep Resultado, ProvedoresCepTests.ServidorFalso Servidor)> BuscarAsync(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        var servidor = new ProvedoresCepTests.ServidorFalso(responder);
        using var sp = Servicos(servidor);
        var r = await sp.GetRequiredService<ViaCepProvedor>().BuscarPorEnderecoAsync(Curvelo);
        return (r, servidor);
    }

    private static Task<HttpResponseMessage> Json(string corpo, HttpStatusCode status = HttpStatusCode.OK) =>
        Task.FromResult(ProvedoresCepTests.ServidorFalso.Json(corpo, status));

    // ---- Status HTTP ----

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, SituacaoProvedorCep.RespostaInvalida, 1)]   // dados já conferidos: 400 é contrato
    [InlineData(HttpStatusCode.NotFound, SituacaoProvedorCep.RespostaInvalida, 1)]
    [InlineData(HttpStatusCode.TooManyRequests, SituacaoProvedorCep.Indisponivel, 1)]  // limite de taxa: não insiste
    [InlineData(HttpStatusCode.InternalServerError, SituacaoProvedorCep.Indisponivel, 2)]
    [InlineData(HttpStatusCode.ServiceUnavailable, SituacaoProvedorCep.Indisponivel, 2)]
    public async Task Status_de_erro_vira_falha_tecnica_e_nunca_lista_vazia(HttpStatusCode status, SituacaoProvedorCep esperada, int chamadas)
    {
        var (r, servidor) = await BuscarAsync((_, _) => Json("", status));

        Assert.Equal(esperada, r.Situacao);
        Assert.True(r.Situacao.EhFalhaTecnica());
        Assert.Empty(r.Registros);
        Assert.Equal(chamadas, servidor.ChamadasViaCep);
    }

    [Fact]
    public async Task Tempo_esgotado_tenta_uma_vez_mais_e_vira_indisponivel()
    {
        var (r, servidor) = await BuscarAsync(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return ProvedoresCepTests.ServidorFalso.Json("[]");
        });

        Assert.Equal(SituacaoProvedorCep.Indisponivel, r.Situacao);
        Assert.Empty(r.Registros);
        Assert.Equal(2, servidor.ChamadasViaCep);
    }

    [Fact]
    public async Task Conexao_recusada_vira_indisponivel()
    {
        var (r, _) = await BuscarAsync((_, _) => throw new HttpRequestException("Conexão recusada."));

        Assert.Equal(SituacaoProvedorCep.Indisponivel, r.Situacao);
        Assert.Empty(r.Registros);
    }

    // ---- Corpo ----

    [Theory]
    [InlineData("")]                                             // JSON vazio
    [InlineData("{ isto não é json")]                             // malformado
    [InlineData("""[ { "cep": "35790-001", "logradouro": "Rua """)] // cortado no meio (parcial)
    [InlineData("null")]
    [InlineData("\"texto\"")]
    [InlineData("""{ "cep": "35790-001" }""")]                   // objeto em vez de lista
    public async Task Corpo_fora_do_contrato_e_resposta_invalida(string corpo)
    {
        var (r, servidor) = await BuscarAsync((_, _) => Json(corpo));

        Assert.Equal(SituacaoProvedorCep.RespostaInvalida, r.Situacao);
        Assert.Empty(r.Registros);
        Assert.Equal(1, servidor.ChamadasViaCep); // resposta inválida não é repetida
    }

    [Theory]
    [InlineData("""[ { "cep": "35790-001" }, { "cep": "123" } ]""")]              // um item com CEP inválido
    [InlineData("""[ { "cep": "35790-001" }, { "logradouro": "Rua Barão" } ]""")] // um item sem CEP
    [InlineData("""[ { "cep": "35790-001" }, 42 ]""")]                            // um item que não é objeto
    [InlineData("""[ { "cep": "ABCDEFGH" } ]""")]
    public async Task Lista_com_item_invalido_e_recusada_inteira_nunca_pela_metade(string corpo)
    {
        var (r, _) = await BuscarAsync((_, _) => Json(corpo));

        Assert.Equal(SituacaoProvedorCep.RespostaInvalida, r.Situacao);
        Assert.Empty(r.Registros);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("""{ "erro": true }""")]
    [InlineData("""{ "erro": "true" }""")]
    public async Task Lista_vazia_ou_erro_do_viacep_e_nenhum_cep_funcional(string corpo)
    {
        var (r, _) = await BuscarAsync((_, _) => Json(corpo));

        Assert.Equal(SituacaoProvedorCep.NaoEncontrado, r.Situacao);
        Assert.False(r.Situacao.EhFalhaTecnica());
        Assert.Empty(r.Registros);
    }

    [Fact]
    public async Task Campos_nulos_ficam_nulos_e_nada_e_inventado()
    {
        var (r, _) = await BuscarAsync((_, _) => Json("""
            [ { "cep": "35790-001", "logradouro": null, "complemento": "", "bairro": null, "localidade": null, "uf": null, "ibge": null } ]
            """));

        var registro = Assert.Single(r.Registros);
        Assert.Equal("35790001", registro.Cep);
        Assert.Null(registro.Logradouro);
        Assert.Null(registro.Complemento); // vazio não vira faixa
        Assert.Null(registro.Bairro);
        Assert.Null(registro.Cidade);
        Assert.Null(registro.Uf);
        Assert.Null(registro.CodigoMunicipioIbge);
    }

    [Fact]
    public async Task Textos_enormes_nao_derrubam_a_leitura()
    {
        var enorme = new string('A', 100_000);
        var (r, _) = await BuscarAsync((_, _) => Json(
            "[ { \"cep\": \"35790-001\", \"logradouro\": \"" + enorme + "\", \"complemento\": \"" + enorme + "\", \"bairro\": \"Centro\" } ]"));

        var registro = Assert.Single(r.Registros);
        Assert.Equal(100_000, registro.Logradouro!.Length);
    }

    // ---- Privacidade ----

    [Fact]
    public void So_uf_cidade_e_logradouro_normalizado_vao_para_a_fonte()
    {
        var busca = BuscaEnderecoCep.De(new EnderecoConferenciaCep(null, "  r.  Barão ", "150 apto 2", "Centro", "Curvelo", "mg", "3120904"))!;

        Assert.Equal(("MG", "Curvelo", "RUA BARAO"), (busca.Uf, busca.Cidade, busca.Logradouro));
        Assert.Equal(["Cidade", "Logradouro", "Uf"], typeof(BuscaEnderecoCep).GetProperties()
            .Where(p => p.Name != nameof(BuscaEnderecoCep.Chave)).Select(p => p.Name).OrderBy(n => n));
    }

    [Fact]
    public async Task O_caminho_pedido_ao_viacep_nao_leva_numero_nem_complemento()
    {
        var busca = BuscaEnderecoCep.De(new EnderecoConferenciaCep(null, "Rua Barão", "150", "Centro", "Curvelo", "MG", "3120904"))!;
        var servidor = new ProvedoresCepTests.ServidorFalso((_, _) => Json("[]"));
        using var sp = Servicos(servidor);

        await sp.GetRequiredService<ViaCepProvedor>().BuscarPorEnderecoAsync(busca);

        Assert.Equal("/ws/MG/Curvelo/RUA%20BARAO/json/", servidor.Caminhos.Single());
    }
}
