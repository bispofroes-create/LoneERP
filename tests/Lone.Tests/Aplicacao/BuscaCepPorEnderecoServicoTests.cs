using Lone.Application.Integracoes.ConferenciaCep;
using Lone.Domain.Enderecos.ConferenciaCep;
using Lone.Domain.Validacao;
using Microsoft.Extensions.Time.Testing;

namespace Lone.Tests.Aplicacao;

/// <summary>
/// Checkpoint D, serviço: <see cref="ServicoConferenciaCep.BuscarPorEnderecoAsync"/>. Mesmos dados mínimos da busca da
/// conferência (<see cref="BuscaEnderecoCep.De"/>), mesmo cache, mesmas fontes (só as que buscam), mesmo aviso de limite em
/// linguagem natural; fonte fora do ar ≠ nenhum candidato; cancelamento continua cancelamento; os candidatos emitidos
/// ficam registrados para a procedência (DM3), com CEP conferido vazio.
/// </summary>
public class BuscaCepPorEnderecoServicoTests
{
    private sealed class FonteFalsa(CepFonte fonte, bool busca) : IProvedorCep
    {
        public CepFonte Fonte { get; } = fonte;
        public bool BuscaPorEndereco { get; } = busca;
        public Func<BuscaEnderecoCep, CancellationToken, Task<ResultadoBuscaProvedorCep>> Busca { get; set; } =
            (_, _) => throw new InvalidOperationException("busca não esperada");
        public int Buscas { get; private set; }
        public int Consultas { get; private set; }

        public Task<ResultadoProvedorCep> ConsultarPorCepAsync(string cep, CancellationToken ct = default)
        {
            Consultas++;
            throw new InvalidOperationException("a busca sem CEP não consulta CEP");
        }

        public Task<ResultadoBuscaProvedorCep> BuscarPorEnderecoAsync(BuscaEnderecoCep busca, CancellationToken ct = default)
        {
            Buscas++;
            return Busca(busca, ct);
        }
    }

    private readonly FakeTimeProvider _relogio = new(new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero));
    private readonly FonteFalsa _viaCep = new(CepFonte.ViaCep, busca: true);
    private readonly FonteFalsa _brasilApi = new(CepFonte.BrasilApi, busca: false);
    private readonly CacheConferenciaCep _cache;
    private readonly SugestoesCepEmitidas _sugestoes;

    public BuscaCepPorEnderecoServicoTests()
    {
        _cache = new CacheConferenciaCep(_relogio);
        _sugestoes = new SugestoesCepEmitidas(_relogio);
    }

    private ServicoConferenciaCep Servico(params IProvedorCep[] fontes) =>
        new(fontes.Length > 0 ? fontes : [_viaCep, _brasilApi], _cache, _sugestoes, _relogio);

    private static EnderecoConferenciaCep Endereco(string? uf = "MG", string? cidade = "Curvelo", string? logradouro = "R. Barao",
                                                   string? numero = "150", string? bairro = "Centro", string? cep = null) =>
        new(cep, logradouro, numero, bairro, cidade, uf, "3120904");

    private static RegistroCep Registro(string cep, string? faixa = "até 999/1000") =>
        new(cep, "Rua Barão", faixa, "Centro", "Curvelo", "MG", "3120904");

    private static Task<T> Ja<T>(T valor) => Task.FromResult(valor);

    // ---- Entrada ----

    [Theory]
    [InlineData(null, "Curvelo", "R. Barao")]      // UF ausente
    [InlineData("MG", null, "R. Barao")]           // município ausente
    [InlineData("MG", "Curvelo", null)]            // logradouro ausente
    [InlineData("MG", "Curvelo", "Ab")]            // logradouro abaixo do mínimo (3)
    [InlineData("MG", "Cu", "R. Barao")]           // município abaixo do mínimo
    [InlineData("EX", "Curvelo", "R. Barao")]      // exterior
    public async Task Dados_insuficientes_param_antes_de_chamar_a_fonte(string? uf, string? cidade, string? logradouro)
    {
        var e = await Assert.ThrowsAsync<ValidacaoException>(() => Servico().BuscarPorEnderecoAsync(Endereco(uf, cidade, logradouro)));

        Assert.Contains(ServicoConferenciaCep.MensagemDadosInsuficientes, e.Erros);
        Assert.Equal(0, _viaCep.Buscas);
    }

    [Fact]
    public async Task Logradouro_curto_segue_a_mesma_regra_da_busca_da_conferencia()
    {
        // A regra é a de BuscaEnderecoCep.De (não há uma segunda validação): "R. B" normaliza para "RUA B" (5 letras) e passa.
        Assert.NotNull(BuscaEnderecoCep.De(Endereco(logradouro: "R. B")));
        Assert.Null(BuscaEnderecoCep.De(Endereco(logradouro: "Ab")));
        _viaCep.Busca = (_, _) => Ja(ResultadoBuscaProvedorCep.Com(CepFonte.ViaCep, []));

        await Assert.ThrowsAsync<ValidacaoException>(() => Servico().BuscarPorEnderecoAsync(Endereco(logradouro: "Ab")));
        Assert.Equal(ResultadoDecisaoCep.NenhumCandidato, (await Servico().BuscarPorEnderecoAsync(Endereco(logradouro: "R. B"))).Decisao.Resultado);
    }

    [Theory]
    [InlineData(null, null)]          // CEP vazio, bairro e número opcionais
    [InlineData("S/N", "Centro")]
    [InlineData("150", null)]
    public async Task Endereco_minimo_valido_busca_sem_cep(string? numero, string? bairro)
    {
        _viaCep.Busca = (_, _) => Ja(ResultadoBuscaProvedorCep.Com(CepFonte.ViaCep, [Registro("35790001")]));

        var r = await Servico().BuscarPorEnderecoAsync(Endereco(numero: numero, bairro: bairro));

        Assert.Equal(ResultadoDecisaoCep.UmCandidato, r.Decisao.Resultado);
        Assert.Equal("", r.Decisao.CepInformado);
        Assert.Equal(1, _viaCep.Buscas);
        Assert.Equal(0, _viaCep.Consultas + _brasilApi.Consultas + _brasilApi.Buscas); // nenhuma consulta por CEP; BrasilAPI não busca
    }

    // ---- Resultados e avisos ----

    [Fact]
    public async Task Candidatos_emitidos_ficam_registrados_para_a_procedencia_com_cep_conferido_vazio()
    {
        _viaCep.Busca = (_, _) => Ja(ResultadoBuscaProvedorCep.Com(CepFonte.ViaCep, [Registro("35790001"), Registro("35790002", null)]));

        var r = await Servico().BuscarPorEnderecoAsync(Endereco());

        Assert.Equal(ResultadoDecisaoCep.VariosCandidatos, r.Decisao.Resultado);
        Assert.True(_sugestoes.FoiEmitida("", "35790001", CepFonte.ViaCep));
        Assert.True(_sugestoes.FoiEmitida("", "35790002", CepFonte.ViaCep));
        Assert.False(_sugestoes.FoiEmitida("", "35790009", CepFonte.ViaCep));
        Assert.Empty(r.Avisos);
    }

    [Fact]
    public async Task Lista_abaixo_do_limite_nao_avisa()
    {
        _viaCep.Busca = (_, _) => Ja(ResultadoBuscaProvedorCep.Com(CepFonte.ViaCep, [Registro("35790001")], limiteAtingido: false));

        Assert.Empty((await Servico().BuscarPorEnderecoAsync(Endereco())).Avisos);
    }

    [Fact]
    public async Task Lista_no_limite_avisa_em_linguagem_natural()
    {
        _viaCep.Busca = (_, _) => Ja(ResultadoBuscaProvedorCep.Com(CepFonte.ViaCep, [Registro("35790001")], limiteAtingido: true));

        var r = await Servico().BuscarPorEnderecoAsync(Endereco());

        Assert.Equal([ServicoConferenciaCep.AvisoBuscaListaIncompleta], r.Avisos);
        Assert.DoesNotContain(r.Avisos, a => a.Contains("LimiteAtingido", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Nenhum_candidato_com_lista_no_limite_diz_que_isso_nao_prova_inexistencia()
    {
        _viaCep.Busca = (_, _) => Ja(ResultadoBuscaProvedorCep.Com(CepFonte.ViaCep, [Registro("35790001", "de 1001/1002 ao fim")], limiteAtingido: true));

        var r = await Servico().BuscarPorEnderecoAsync(Endereco());

        Assert.Equal(ResultadoDecisaoCep.NenhumCandidato, r.Decisao.Resultado);
        Assert.Empty(r.Decisao.Candidatos);
        Assert.Equal([ServicoConferenciaCep.AvisoBuscaSemCandidatoNoLimite], r.Avisos);
        Assert.Contains("não prova que o CEP não exista", r.Avisos[0]);
    }

    [Fact]
    public async Task A_mesma_busca_usa_o_cache_e_nao_chama_a_fonte_de_novo()
    {
        _viaCep.Busca = (_, _) => Ja(ResultadoBuscaProvedorCep.Com(CepFonte.ViaCep, [Registro("35790001")], limiteAtingido: true));

        await Servico().BuscarPorEnderecoAsync(Endereco());
        var segunda = await Servico().BuscarPorEnderecoAsync(Endereco(logradouro: "Rua Barão", numero: "151"));

        Assert.Equal(1, _viaCep.Buscas);                                     // mesma chave (UF, cidade, logradouro normalizado)
        Assert.Equal([ServicoConferenciaCep.AvisoBuscaListaIncompleta], segunda.Avisos); // o limite também vem do cache
    }

    [Fact]
    public async Task Fonte_fora_do_ar_e_indisponivel_e_nao_nenhum_candidato()
    {
        _viaCep.Busca = (_, _) => Ja(ResultadoBuscaProvedorCep.Indisponivel(CepFonte.ViaCep, "timeout"));

        var r = await Servico().BuscarPorEnderecoAsync(Endereco());

        Assert.Equal(ResultadoDecisaoCep.FonteIndisponivel, r.Decisao.Resultado);
        Assert.Empty(r.Decisao.Candidatos);
        Assert.Null(_cache.ObterBusca(BuscaEnderecoCep.De(Endereco())!)); // falha técnica não entra no cache
    }

    [Fact]
    public async Task Resposta_fora_do_contrato_tambem_e_indisponivel()
    {
        _viaCep.Busca = (_, _) => Ja(ResultadoBuscaProvedorCep.Invalida(CepFonte.ViaCep, "não é lista"));

        Assert.Equal(ResultadoDecisaoCep.FonteIndisponivel, (await Servico().BuscarPorEnderecoAsync(Endereco())).Decisao.Resultado);
    }

    [Fact]
    public async Task Sem_fonte_que_busque_por_endereco_nao_finge_e_avisa()
    {
        var r = await Servico(_brasilApi).BuscarPorEnderecoAsync(Endereco());

        Assert.Equal(ResultadoDecisaoCep.FonteIndisponivel, r.Decisao.Resultado);
        Assert.Equal([ServicoConferenciaCep.AvisoSemFonteDeBusca], r.Avisos);
        Assert.Equal(0, _brasilApi.Buscas);
    }

    [Fact]
    public async Task Cancelamento_de_quem_chamou_continua_cancelamento()
    {
        using var cts = new CancellationTokenSource();
        _viaCep.Busca = (_, ct) =>
        {
            cts.Cancel();
            ct.ThrowIfCancellationRequested();
            return Ja(ResultadoBuscaProvedorCep.Com(CepFonte.ViaCep, []));
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Servico().BuscarPorEnderecoAsync(Endereco(), cts.Token));
        Assert.Null(_cache.ObterBusca(BuscaEnderecoCep.De(Endereco())!));
    }

    [Fact]
    public async Task A_conferencia_continua_igual()
    {
        _viaCep.Busca = (_, _) => Ja(ResultadoBuscaProvedorCep.Com(CepFonte.ViaCep, [Registro("35790001")], limiteAtingido: true));

        await Assert.ThrowsAsync<ValidacaoException>(() => Servico().ConferirAsync(Endereco(cep: null))); // conferir exige CEP
        Assert.Equal(0, _viaCep.Buscas);
        Assert.Equal("A lista de candidatos pode estar incompleta devido ao limite da consulta externa; informe o número e o bairro.",
            ServicoConferenciaCep.AvisoListaIncompleta);
    }
}
