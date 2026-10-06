using Lone.Application.Integracoes.ConferenciaCep;
using Lone.Domain.Enderecos.ConferenciaCep;
using Lone.Domain.Validacao;
using Microsoft.Extensions.Time.Testing;

namespace Lone.Tests.Aplicacao;

/// <summary>
/// F2 do motor de CEP: o serviço de conferência com fontes falsas (sem rede). Fonte → cache → reserva só em falha técnica
/// → busca por endereço → MotorCep (F1). Nunca altera o endereço; não encontrado ≠ indisponível.
/// </summary>
public class ServicoConferenciaCepTests
{
    /// <summary>Fonte falsa: responde pelo roteiro e conta as chamadas.</summary>
    private sealed class FonteFalsa(CepFonte fonte, bool busca = false) : IProvedorCep
    {
        public CepFonte Fonte { get; } = fonte;
        public bool BuscaPorEndereco { get; } = busca;
        public Func<string, CancellationToken, Task<ResultadoProvedorCep>> Consulta { get; set; } =
            (_, _) => throw new InvalidOperationException("consulta não esperada");
        public Func<BuscaEnderecoCep, CancellationToken, Task<ResultadoBuscaProvedorCep>> Busca { get; set; } =
            (_, _) => throw new InvalidOperationException("busca não esperada");
        public int Consultas { get; private set; }
        public int Buscas { get; private set; }
        public List<BuscaEnderecoCep> Pedidos { get; } = [];

        public Task<ResultadoProvedorCep> ConsultarPorCepAsync(string cep, CancellationToken ct = default)
        {
            Consultas++;
            return Consulta(cep, ct);
        }

        public Task<ResultadoBuscaProvedorCep> BuscarPorEnderecoAsync(BuscaEnderecoCep busca, CancellationToken ct = default)
        {
            Buscas++;
            Pedidos.Add(busca);
            return Busca(busca, ct);
        }
    }

    private readonly FakeTimeProvider _relogio = new(new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero));
    private readonly FonteFalsa _viaCep = new(CepFonte.ViaCep, busca: true);
    private readonly FonteFalsa _brasilApi = new(CepFonte.BrasilApi);
    private readonly CacheConferenciaCep _cache;
    private readonly SugestoesCepEmitidas _sugestoes;

    public ServicoConferenciaCepTests()
    {
        _cache = new CacheConferenciaCep(_relogio);
        _sugestoes = new SugestoesCepEmitidas(_relogio);
    }

    private ServicoConferenciaCep Servico() => new([_viaCep, _brasilApi], _cache, _sugestoes, _relogio);

    /// <summary>Só a decisão do motor (os avisos têm testes próprios).</summary>
    private async Task<DecisaoCep> Conferir(EnderecoConferenciaCep endereco, CancellationToken ct = default) =>
        (await Servico().ConferirAsync(endereco, ct)).Decisao;

    private static EnderecoConferenciaCep Endereco(string cep = "35790-000", string? logradouro = "R. Barao", string? numero = "150") =>
        new(cep, logradouro, numero, "Centro", "Curvelo", "MG", "3120904");

    private static RegistroCep Registro(string cep = "35790000", string logradouro = "Rua Barão", string? faixa = "até 999/1000") =>
        new(cep, logradouro, faixa, "Centro", "Curvelo", "MG", "3120904");

    private static Task<T> Ja<T>(T valor) => Task.FromResult(valor);

    private static ResultadoProvedorCep Achou(CepFonte f, RegistroCep r) => ResultadoProvedorCep.Encontrado(f, r);

    // ---- Fonte → MotorCep ----

    [Fact]
    public async Task Cep_encontrado_no_ViaCep_e_conferido_sem_chamar_a_reserva()
    {
        _viaCep.Consulta = (_, _) => Ja(Achou(CepFonte.ViaCep, Registro()));

        var d = await Conferir(Endereco());

        Assert.Equal(ResultadoDecisaoCep.Conferido, d.Resultado);
        Assert.Equal(CepFonte.ViaCep, d.Fonte);
        Assert.Equal(0, _brasilApi.Consultas);
        Assert.Equal(0, _viaCep.Buscas);
    }

    [Fact]
    public async Task Cep_de_outro_logradouro_e_divergente_e_nao_busca_nem_sugere()
    {
        _viaCep.Consulta = (_, _) => Ja(Achou(CepFonte.ViaCep, Registro(logradouro: "Rua Padre Corrêa")));

        var d = await Conferir(Endereco());

        Assert.Equal(ResultadoDecisaoCep.Divergente, d.Resultado);
        Assert.Null(d.CepSugerido);
        Assert.Empty(d.Candidatos);
        Assert.Equal(0, _viaCep.Buscas);
    }

    [Fact]
    public async Task Cep_inexistente_nao_aciona_a_reserva_e_busca_pelo_endereco_normalizado()
    {
        _viaCep.Consulta = (_, _) => Ja(ResultadoProvedorCep.NaoEncontrado(CepFonte.ViaCep));
        _viaCep.Busca = (_, _) => Ja(ResultadoBuscaProvedorCep.Com(CepFonte.ViaCep, [Registro("35790001")]));

        var d = await Conferir(Endereco());

        Assert.Equal(0, _brasilApi.Consultas);                 // inexistente é resposta, não falha
        Assert.Equal(new BuscaEnderecoCep("MG", "Curvelo", "RUA BARAO"), Assert.Single(_viaCep.Pedidos)); // normalizador da F1
        Assert.Equal(ResultadoDecisaoCep.UmCandidato, d.Resultado);
        Assert.Equal(CepSituacao.PendenteDeDecisao, d.Situacao);
        Assert.Equal("35790001", d.CepSugerido);
        Assert.True(_sugestoes.FoiEmitida("35790000", "35790001", CepFonte.ViaCep));
    }

    [Fact]
    public async Task Varios_candidatos_vao_todos_para_escolha_e_ficam_registrados()
    {
        _viaCep.Consulta = (_, _) => Ja(ResultadoProvedorCep.NaoEncontrado(CepFonte.ViaCep));
        _viaCep.Busca = (_, _) => Ja(ResultadoBuscaProvedorCep.Com(CepFonte.ViaCep,
            [Registro("35790007", faixa: null), Registro("35790001"), Registro("35790009", faixa: "de 1001/1002 ao fim")]));

        var d = await Conferir(Endereco());

        Assert.Equal(ResultadoDecisaoCep.VariosCandidatos, d.Resultado);
        Assert.Null(d.CepSugerido);
        Assert.Equal(["35790001", "35790007"], d.Candidatos.Select(c => c.Cep));
        Assert.True(_sugestoes.FoiEmitida("35790000", "35790007", CepFonte.ViaCep));
        Assert.False(_sugestoes.FoiEmitida("35790000", "35790009", CepFonte.ViaCep)); // fora da faixa: não é candidato
    }

    [Fact]
    public async Task Busca_sem_candidato_e_nao_localizado_sem_inventar()
    {
        _viaCep.Consulta = (_, _) => Ja(ResultadoProvedorCep.NaoEncontrado(CepFonte.ViaCep));
        _viaCep.Busca = (_, _) => Ja(ResultadoBuscaProvedorCep.Com(CepFonte.ViaCep, []));

        var d = await Conferir(Endereco());

        Assert.Equal(ResultadoDecisaoCep.NenhumCandidato, d.Resultado);
        Assert.Null(d.CepSugerido);
    }

    [Fact]
    public async Task Sem_logradouro_suficiente_nao_busca_e_fica_nao_encontrado()
    {
        _viaCep.Consulta = (_, _) => Ja(ResultadoProvedorCep.NaoEncontrado(CepFonte.ViaCep));

        var d = await Conferir(Endereco(logradouro: "R."));

        Assert.Equal(ResultadoDecisaoCep.NaoEncontrado, d.Resultado);
        Assert.True(d.DeveBuscarPorEndereco);
        Assert.Equal(0, _viaCep.Buscas);
    }

    // ---- Reserva só em falha técnica; indisponível ≠ não encontrado ----

    [Theory]
    [InlineData(SituacaoProvedorCep.Indisponivel)]
    [InlineData(SituacaoProvedorCep.RespostaInvalida)]
    public async Task Falha_tecnica_no_ViaCep_usa_a_BrasilApi(SituacaoProvedorCep falha)
    {
        _viaCep.Consulta = (_, _) => Ja(new ResultadoProvedorCep(falha, CepFonte.ViaCep, Detalhe: "x"));
        _brasilApi.Consulta = (_, _) => Ja(Achou(CepFonte.BrasilApi, Registro(faixa: null)));

        var d = await Conferir(Endereco());

        Assert.Equal(ResultadoDecisaoCep.Conferido, d.Resultado);
        Assert.Equal(CepFonte.BrasilApi, d.Fonte);
        Assert.Equal(1, _brasilApi.Consultas);
    }

    [Fact]
    public async Task Todas_as_fontes_fora_e_fonte_indisponivel_nunca_nao_encontrado()
    {
        _viaCep.Consulta = (_, _) => Ja(ResultadoProvedorCep.Indisponivel(CepFonte.ViaCep, "tempo esgotado"));
        _brasilApi.Consulta = (_, _) => Ja(ResultadoProvedorCep.Indisponivel(CepFonte.BrasilApi, "HTTP 503"));

        var d = await Conferir(Endereco());

        Assert.Equal(ResultadoDecisaoCep.FonteIndisponivel, d.Resultado);
        Assert.Equal(CepSituacao.FonteIndisponivel, d.Situacao);
        Assert.NotEqual(CepSituacao.NaoEncontrado, d.Situacao);
        Assert.Equal(0, _viaCep.Buscas);
        Assert.Equal(0, _cache.Quantidade);                      // indisponível não vai para o cache
    }

    [Fact]
    public async Task ViaCep_fora_e_reserva_diz_inexistente_nao_chama_o_ViaCep_de_novo_para_buscar()
    {
        _viaCep.Consulta = (_, _) => Ja(ResultadoProvedorCep.Indisponivel(CepFonte.ViaCep, "tempo esgotado"));
        _brasilApi.Consulta = (_, _) => Ja(ResultadoProvedorCep.NaoEncontrado(CepFonte.BrasilApi));

        var d = await Conferir(Endereco());

        Assert.Equal(ResultadoDecisaoCep.NaoEncontrado, d.Resultado); // D-F1-3: busca indisponível continua "não encontrado"
        Assert.True(d.DeveBuscarPorEndereco);
        Assert.Equal(0, _viaCep.Buscas);
    }

    [Fact]
    public async Task Busca_indisponivel_continua_nao_encontrado_e_nao_vai_para_o_cache()
    {
        _viaCep.Consulta = (_, _) => Ja(ResultadoProvedorCep.NaoEncontrado(CepFonte.ViaCep));
        _viaCep.Busca = (_, _) => Ja(ResultadoBuscaProvedorCep.Indisponivel(CepFonte.ViaCep, "HTTP 503"));

        var d = await Conferir(Endereco());
        await Conferir(Endereco());

        Assert.Equal(ResultadoDecisaoCep.NaoEncontrado, d.Resultado);
        Assert.True(d.DeveBuscarPorEndereco);
        Assert.Equal(2, _viaCep.Buscas);       // a falha não ficou guardada
        Assert.Equal(1, _viaCep.Consultas);    // o inexistente ficou
    }

    // ---- Cache transitório ----

    [Fact]
    public async Task Cep_encontrado_fica_24_horas_no_cache_com_chave_normalizada()
    {
        _viaCep.Consulta = (_, _) => Ja(Achou(CepFonte.ViaCep, Registro()));

        await Conferir(Endereco("35790-000"));
        await Conferir(Endereco("35790000"));
        _relogio.Advance(TimeSpan.FromHours(23));
        await Conferir(Endereco("35.790-000"));
        Assert.Equal(1, _viaCep.Consultas);

        _relogio.Advance(TimeSpan.FromHours(1) + TimeSpan.FromSeconds(1));
        await Conferir(Endereco());
        Assert.Equal(2, _viaCep.Consultas);    // venceu: consulta de novo
    }

    [Fact]
    public async Task Cep_inexistente_fica_1_hora_no_cache_separado_do_encontrado()
    {
        _viaCep.Consulta = (_, _) => Ja(ResultadoProvedorCep.NaoEncontrado(CepFonte.ViaCep));
        _viaCep.Busca = (_, _) => Ja(ResultadoBuscaProvedorCep.Com(CepFonte.ViaCep, []));

        await Conferir(Endereco());
        _relogio.Advance(TimeSpan.FromMinutes(59));
        var guardado = _cache.ObterConsulta("35790000");
        Assert.Equal(SituacaoProvedorCep.NaoEncontrado, guardado!.Situacao);
        await Conferir(Endereco());
        Assert.Equal(1, _viaCep.Consultas);
        Assert.Equal(1, _viaCep.Buscas);       // a busca também ficou (1 h)

        _relogio.Advance(TimeSpan.FromMinutes(2));
        await Conferir(Endereco());
        Assert.Equal(2, _viaCep.Consultas);
        Assert.Equal(2, _viaCep.Buscas);
    }

    [Fact]
    public async Task Busca_usa_a_mesma_chave_para_o_logradouro_abreviado_e_por_extenso()
    {
        _viaCep.Consulta = (_, _) => Ja(ResultadoProvedorCep.NaoEncontrado(CepFonte.ViaCep));
        _viaCep.Busca = (_, _) => Ja(ResultadoBuscaProvedorCep.Com(CepFonte.ViaCep, [Registro("35790001")]));

        await Conferir(Endereco(logradouro: "R. Barao"));
        var d = await Conferir(Endereco(logradouro: "Rua Barão"));

        Assert.Equal(1, _viaCep.Buscas);
        Assert.Equal(ResultadoDecisaoCep.UmCandidato, d.Resultado);
    }

    [Fact]
    public void Cache_nao_guarda_falha_tecnica()
    {
        _cache.GuardarConsulta("35790000", ResultadoProvedorCep.Indisponivel(CepFonte.ViaCep, "x"));
        _cache.GuardarConsulta("35790001", ResultadoProvedorCep.Invalida(CepFonte.ViaCep, "x"));
        _cache.GuardarBusca(new BuscaEnderecoCep("MG", "Curvelo", "RUA X"), ResultadoBuscaProvedorCep.Indisponivel(CepFonte.ViaCep, "x"));

        Assert.Equal(0, _cache.Quantidade);
        Assert.Null(_cache.ObterConsulta("35790000"));
    }

    // ---- Teto, cancelamento e contrato ----

    [Fact]
    public async Task Teto_esgotado_vira_indisponivel_e_nao_chama_mais_fontes()
    {
        var chamou = new TaskCompletionSource();
        _viaCep.Consulta = async (_, ct) =>
        {
            chamou.SetResult();
            await Task.Delay(Timeout.Infinite, ct);
            return ResultadoProvedorCep.NaoEncontrado(CepFonte.ViaCep);
        };

        var conferencia = Conferir(Endereco());
        await chamou.Task;
        _relogio.Advance(ServicoConferenciaCep.TetoTotal + TimeSpan.FromSeconds(1));
        var d = await conferencia;

        Assert.Equal(ResultadoDecisaoCep.FonteIndisponivel, d.Resultado);
        Assert.Equal(0, _brasilApi.Consultas);
    }

    [Fact]
    public async Task Cancelamento_de_quem_chamou_sobe_como_cancelamento()
    {
        using var cancelar = new CancellationTokenSource();
        _viaCep.Consulta = async (_, ct) =>
        {
            await cancelar.CancelAsync();
            ct.ThrowIfCancellationRequested();
            return ResultadoProvedorCep.NaoEncontrado(CepFonte.ViaCep);
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Conferir(Endereco(), cancelar.Token));
        Assert.Equal(0, _brasilApi.Consultas);
        Assert.Equal(0, _cache.Quantidade);
    }

    [Theory]
    [InlineData("")]
    [InlineData("3579")]
    [InlineData("00000000")]
    public async Task Cep_invalido_e_erro_de_validacao_sem_consultar(string cep)
    {
        await Assert.ThrowsAsync<ValidacaoException>(() => Conferir(Endereco(cep)));
        Assert.Equal(0, _viaCep.Consultas);
    }

    [Fact]
    public async Task Mesmas_entradas_mesma_decisao()
    {
        _viaCep.Consulta = (_, _) => Ja(ResultadoProvedorCep.NaoEncontrado(CepFonte.ViaCep));
        _viaCep.Busca = (_, _) => Ja(ResultadoBuscaProvedorCep.Com(CepFonte.ViaCep, [Registro("35790007"), Registro("35790001")]));

        var a = await Conferir(Endereco());
        var b = await Conferir(Endereco());

        Assert.Equal(a.Resultado, b.Resultado);
        Assert.Equal(a.Candidatos, b.Candidatos);
        Assert.Equal(a.Motivos, b.Motivos);
    }

    // ---- D-F2-1: CEPs de prédio e limite da busca ----

    [Fact]
    public async Task Cep_de_predio_so_e_candidato_para_o_numero_do_predio()
    {
        _viaCep.Consulta = (_, _) => Ja(ResultadoProvedorCep.NaoEncontrado(CepFonte.ViaCep));
        _viaCep.Busca = (_, _) => Ja(ResultadoBuscaProvedorCep.Com(CepFonte.ViaCep,
            [Registro("35790911", faixa: "960"), Registro("35790946", faixa: "1374 12 Andar"), Registro("35790001", faixa: "até 999/1000")]));

        var comum = await Conferir(Endereco(numero: "150"));
        var predio = await Conferir(Endereco(numero: "960"));

        Assert.Equal(ResultadoDecisaoCep.UmCandidato, comum.Resultado);   // os prédios 960 e 1374 saem; fica a faixa
        Assert.Equal("35790001", comum.CepSugerido);
        Assert.Equal(["35790001", "35790911"], predio.Candidatos.Select(c => c.Cep)); // no nº 960, o prédio e a faixa
    }

    [Fact]
    public async Task Busca_no_limite_da_fonte_avisa_que_a_lista_pode_estar_incompleta()
    {
        _viaCep.Consulta = (_, _) => Ja(ResultadoProvedorCep.NaoEncontrado(CepFonte.ViaCep));
        _viaCep.Busca = (_, _) => Ja(ResultadoBuscaProvedorCep.Com(CepFonte.ViaCep, [Registro("35790911", faixa: "960")], limiteAtingido: true));

        var r = await Servico().ConferirAsync(Endereco(numero: "1000"));
        var doCache = await Servico().ConferirAsync(Endereco(numero: "1000"));

        Assert.Equal(ResultadoDecisaoCep.NenhumCandidato, r.Decisao.Resultado);
        Assert.Equal([ServicoConferenciaCep.AvisoListaIncompleta], r.Avisos);
        Assert.Equal([ServicoConferenciaCep.AvisoListaIncompleta], doCache.Avisos); // o limite fica guardado com a busca
        Assert.Equal(1, _viaCep.Buscas);
        Assert.Null(r.Decisao.CepSugerido);
    }

    [Fact]
    public async Task Sem_limite_ou_sem_busca_nao_ha_aviso()
    {
        _viaCep.Consulta = (_, _) => Ja(ResultadoProvedorCep.NaoEncontrado(CepFonte.ViaCep));
        _viaCep.Busca = (_, _) => Ja(ResultadoBuscaProvedorCep.Com(CepFonte.ViaCep, [Registro("35790001")]));
        Assert.Empty((await Servico().ConferirAsync(Endereco())).Avisos);

        _cache.GuardarConsulta("35790002", ResultadoProvedorCep.Encontrado(CepFonte.ViaCep, Registro("35790002")));
        Assert.Empty((await Servico().ConferirAsync(Endereco("35790002"))).Avisos);
    }

    // ---- Sugestões emitidas (base da procedência na gravação, DM3) ----

    [Fact]
    public void Sugestao_emitida_vale_1_hora_e_so_para_o_mesmo_par_e_fonte()
    {
        _sugestoes.Registrar("35790-000", "35790-001", CepFonte.ViaCep);

        Assert.True(_sugestoes.FoiEmitida("35790000", "35790001", CepFonte.ViaCep));
        Assert.False(_sugestoes.FoiEmitida("35790000", "35790001", CepFonte.BrasilApi));
        Assert.False(_sugestoes.FoiEmitida("35790000", "35790002", CepFonte.ViaCep));
        Assert.False(_sugestoes.FoiEmitida("35790009", "35790001", CepFonte.ViaCep));

        _relogio.Advance(SugestoesCepEmitidas.Validade + TimeSpan.FromSeconds(1));
        Assert.False(_sugestoes.FoiEmitida("35790000", "35790001", CepFonte.ViaCep));
    }

    // ---- Fase 1 (T-5): bairro estrito na busca, documentado como é hoje (não flexibilizado) ----

    [Fact]
    public async Task Documenta_bairro_estrito_candidato_de_bairro_parecido_e_descartado()
    {
        // Comportamento atual: "Centro" × "Centro Histórico" são bairros diferentes; o candidato sai e nenhum CEP é inventado.
        _viaCep.Consulta = (_, _) => Ja(ResultadoProvedorCep.NaoEncontrado(CepFonte.ViaCep));
        _viaCep.Busca = (_, _) => Ja(ResultadoBuscaProvedorCep.Com(CepFonte.ViaCep, [Registro("35790001") with { Bairro = "Centro Histórico" }]));

        var d = await Conferir(Endereco());

        Assert.Equal(ResultadoDecisaoCep.NenhumCandidato, d.Resultado);
        Assert.Empty(d.Candidatos);
        Assert.Null(d.CepSugerido);
    }

    [Fact]
    public async Task Documenta_bairro_estrito_compara_sem_acento_e_sem_caixa_e_ignora_bairro_vazio()
    {
        _viaCep.Consulta = (_, _) => Ja(ResultadoProvedorCep.NaoEncontrado(CepFonte.ViaCep));
        _viaCep.Busca = (_, _) => Ja(ResultadoBuscaProvedorCep.Com(CepFonte.ViaCep,
            [Registro("35790001") with { Bairro = "CENTRO" }, Registro("35790002") with { Bairro = null }]));

        var d = await Conferir(Endereco());

        Assert.Equal(ResultadoDecisaoCep.VariosCandidatos, d.Resultado);
        Assert.Equal(["35790001", "35790002"], d.Candidatos.Select(c => c.Cep));
    }
}
