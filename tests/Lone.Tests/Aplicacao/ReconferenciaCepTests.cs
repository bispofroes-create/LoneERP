using Lone.Application.Integracoes.ConferenciaCep;
using Lone.Domain.Enderecos.ConferenciaCep;
using Microsoft.Extensions.Time.Testing;

namespace Lone.Tests.Aplicacao;

/// <summary>
/// F6: reconferência em lote — detecção e acompanhamento, nunca correção. Usa o mesmo serviço de conferência; grava só
/// situação, fonte e data, e só se o endereço não mudou; fonte indisponível e informação anterior não gravam; candidatos só
/// são mostrados; cada endereço tem resultado próprio; cancelar preserva o concluído.
/// </summary>
public class ReconferenciaCepTests
{
    private static readonly DateTime Agora = new(2026, 10, 6, 10, 0, 0, DateTimeKind.Utc);

    /// <summary>Endereços "gravados" em memória. Gravar só mexe nas três colunas, com a mesma trava do SQL.</summary>
    private sealed class BancoFalso : IReconferenciaCepRepositorio, IManutencaoConsultasCep
    {
        public Dictionary<Guid, EnderecoReconferivel> Enderecos { get; } = new();
        public Dictionary<Guid, ConferenciaCepRealizada> Gravados { get; } = new();
        public (DateTime Limite, int Lote)? Limpeza { get; private set; }

        public Task<SelecaoReconferenciaCep> SelecionarAsync(CriterioReconferenciaCep c, DateTime agora, CancellationToken ct = default) =>
            Task.FromResult(new SelecaoReconferenciaCep(Enderecos.Count, Enderecos.Keys.Take(c.Limite).ToList(), 0));

        public Task<IReadOnlyList<EnderecoReconferivel>> ObterAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<EnderecoReconferivel>>(ids.Where(Enderecos.ContainsKey).Select(i => Enderecos[i]).ToList());

        public Task<bool> GravarAsync(EnderecoReconferivel lido, ConferenciaCepRealizada c, CancellationToken ct = default)
        {
            lock (this)
            {
                // Trava: os dados conferidos têm de continuar exatamente como foram lidos.
                if (!Enderecos.TryGetValue(lido.EnderecoId, out var atual) || atual with { Situacao = lido.Situacao } != lido)
                    return Task.FromResult(false);
                Gravados[lido.EnderecoId] = c;
                Enderecos[lido.EnderecoId] = atual with { Situacao = c.Situacao };
                return Task.FromResult(true);
            }
        }

        public Task<int> LimparAsync(DateTime limite, int tamanhoLote, CancellationToken ct = default)
        {
            Limpeza = (limite, tamanhoLote);
            return Task.FromResult(3);
        }
    }

    /// <summary>Conferência falsa: decide pelo CEP do endereço, conta chamadas e simultaneidade.</summary>
    private sealed class ConferenciaFalsa : IServicoConferenciaCep
    {
        public Func<EnderecoConferenciaCep, CancellationToken, Task<ResultadoConferenciaCep>> Responder { get; set; } = (_, _) => throw new InvalidOperationException();
        public int Chamadas;
        public int Simultaneas;
        public int MaximoSimultaneas;

        public async Task<ResultadoConferenciaCep> ReconferirAsync(EnderecoConferenciaCep e, CancellationToken ct = default)
        {
            Interlocked.Increment(ref Chamadas);
            var agora = Interlocked.Increment(ref Simultaneas);
            lock (this) MaximoSimultaneas = Math.Max(MaximoSimultaneas, agora);
            try { return await Responder(e, ct); }
            finally { Interlocked.Decrement(ref Simultaneas); }
        }

        public Task<ResultadoConferenciaCep> ConferirAsync(EnderecoConferenciaCep e, CancellationToken ct = default) =>
            throw new InvalidOperationException("a reconferência usa ReconferirAsync");
        public Task<ResultadoConferenciaCep> BuscarPorEnderecoAsync(EnderecoConferenciaCep e, CancellationToken ct = default) =>
            throw new InvalidOperationException();
        public Task<ComparacaoFontesCep> ConsultarOutraFonteAsync(string cep, CancellationToken ct = default) =>
            throw new InvalidOperationException("a reconferência nunca pede segunda opinião");
    }

    private readonly BancoFalso _banco = new();
    private readonly ConferenciaFalsa _conferencia = new();
    private readonly FakeTimeProvider _relogio = new(new DateTimeOffset(Agora));
    private readonly List<TimeSpan> _esperas = [];

    private ServicoReconferenciaCep Servico(int paralelismo = 2, TimeSpan? intervalo = null) =>
        new(_banco, _conferencia, _banco, _relogio, (t, _) => { lock (_esperas) _esperas.Add(t); return Task.CompletedTask; },
            intervalo ?? TimeSpan.FromSeconds(1), paralelismo);

    private static readonly RegistroCep Registro = new("35790000", "Rua Barão", "até 999/1000", "Centro", "Curvelo", "MG", "3120904");

    private Guid Endereco(string cep = "35790000", string logradouro = "R. Barao", string numero = "150", bool ativo = true, bool brasil = true,
                          CepSituacao situacao = CepSituacao.NaoConferido)
    {
        var id = Guid.NewGuid();
        _banco.Enderecos[id] = new EnderecoReconferivel(id, Guid.NewGuid(), _banco.Enderecos.Count + 1, "Cliente " + id.ToString("N")[..4], cep,
            logradouro, numero, "Centro", "Curvelo", "MG", 3120904, "3120904", ativo, brasil, situacao);
        return id;
    }

    private static Task<ResultadoConferenciaCep> Decidir(EnderecoConferenciaCep e, RespostaConsultaCep consulta, RespostaBuscaEndereco? busca = null) =>
        Task.FromResult(new ResultadoConferenciaCep(MotorCep.Decidir(e, consulta, busca), []));

    private void TodosConferidos() => _conferencia.Responder = (e, _) => Decidir(e, RespostaConsultaCep.Encontrado(Registro with { Cep = Digitos(e.Cep) }, CepFonte.ViaCep));

    private static string Digitos(string? s) => new((s ?? "").Where(char.IsAsciiDigit).ToArray());

    // ---- Processamento ----

    [Fact]
    public async Task Todos_conferidos_gravam_situacao_fonte_e_data_e_nada_do_endereco()
    {
        var ids = Enumerable.Range(0, 3).Select(_ => Endereco()).ToList();
        var antes = ids.ToDictionary(i => i, i => _banco.Enderecos[i]);
        TodosConferidos();

        var r = await Servico().ProcessarAsync(ids);

        Assert.All(r.Itens, i => Assert.Equal(ResultadoItemReconferencia.Conferido, i.Resultado));
        Assert.All(ids, i => Assert.Equal(new ConferenciaCepRealizada(CepSituacao.Conferido, CepFonte.ViaCep, Agora), _banco.Gravados[i]));
        Assert.All(ids, i => Assert.Equal(antes[i] with { Situacao = CepSituacao.Conferido }, _banco.Enderecos[i])); // CEP, logradouro, número... iguais
    }

    [Fact]
    public async Task Resultados_mistos_cada_um_com_o_seu()
    {
        var conferido = Endereco();
        var divergente = Endereco(logradouro: "Rua Padre Corrêa");
        var inexistente = Endereco(cep: "35790999");
        var foraDoAr = Endereco(cep: "35790111", situacao: CepSituacao.Divergente);
        _conferencia.Responder = (e, _) => Digitos(e.Cep) switch
        {
            "35790999" => Decidir(e, RespostaConsultaCep.NaoEncontrado(CepFonte.ViaCep),
                RespostaBuscaEndereco.Realizada([Registro with { Cep = "35790001" }], CepFonte.ViaCep)),
            "35790111" => Decidir(e, RespostaConsultaCep.Indisponivel(CepFonte.ViaCep)),
            var cep => Decidir(e, RespostaConsultaCep.Encontrado(Registro with { Cep = cep }, CepFonte.ViaCep))
        };

        var r = await Servico().ProcessarAsync([conferido, divergente, inexistente, foraDoAr]);

        Assert.Equal([ResultadoItemReconferencia.Conferido, ResultadoItemReconferencia.Divergente, ResultadoItemReconferencia.NaoEncontrado,
            ResultadoItemReconferencia.Indisponivel], r.Itens.Select(i => i.Resultado));
        Assert.Contains(r.Itens[1].Decisao!.Componentes, c => c.Componente == ComponenteCep.Logradouro && c.Situacao == SituacaoComponenteCep.Divergente);
        Assert.Equal(CepSituacao.Divergente, _banco.Gravados[divergente].Situacao);       // estado, não endereço
        Assert.Equal("Rua Padre Corrêa", _banco.Enderecos[divergente].Logradouro);
        Assert.Equal(CepSituacao.NaoEncontrado, _banco.Gravados[inexistente].Situacao);
        Assert.Equal("35790999", _banco.Enderecos[inexistente].Cep);                       // não apaga o CEP
        Assert.Equal("35790001", Assert.Single(r.Itens[2].Decisao!.Candidatos).Cep);       // candidato só mostrado
        Assert.False(_banco.Gravados.ContainsKey(foraDoAr));                               // indisponível não grava
        Assert.Equal(CepSituacao.Divergente, _banco.Enderecos[foraDoAr].Situacao);         // e mantém a última situação
        Assert.Equal(4, r.Total);
        Assert.Equal(1, r.Quantos(ResultadoItemReconferencia.Indisponivel));
    }

    [Fact]
    public async Task Um_candidato_nunca_e_escolhido_nem_aplicado()
    {
        var id = Endereco(cep: "35790999");
        _conferencia.Responder = (e, _) => Decidir(e, RespostaConsultaCep.NaoEncontrado(CepFonte.ViaCep),
            RespostaBuscaEndereco.Realizada([Registro with { Cep = "35790001" }], CepFonte.ViaCep));

        var r = await Servico().ProcessarAsync([id]);

        Assert.Equal(ResultadoDecisaoCep.UmCandidato, r.Itens[0].Decisao!.Resultado);
        Assert.Equal("35790999", _banco.Enderecos[id].Cep);
        Assert.Equal(CepSituacao.NaoEncontrado, _banco.Gravados[id].Situacao);
    }

    [Fact]
    public async Task Informacao_anterior_nao_conta_como_conferencia()
    {
        var id = Endereco(situacao: CepSituacao.Divergente);
        _conferencia.Responder = (e, _) =>
        {
            var indisponivel = MotorCep.Decidir(e, RespostaConsultaCep.Indisponivel(CepFonte.ViaCep));
            var comAntiga = MotorCep.Decidir(e, RespostaConsultaCep.Encontrado(Registro, CepFonte.ViaCep));
            return Task.FromResult(new ResultadoConferenciaCep(indisponivel, [])
            {
                Anterior = new InformacaoAnteriorCep(Registro, CepFonte.ViaCep, Agora.AddDays(-3), comAntiga)
            });
        };

        var r = await Servico().ProcessarAsync([id]);

        Assert.Equal(ResultadoItemReconferencia.Indisponivel, r.Itens[0].Resultado);
        Assert.Contains("não conta como conferência", r.Itens[0].Detalhe);
        Assert.Empty(_banco.Gravados);
        Assert.Equal(CepSituacao.Divergente, _banco.Enderecos[id].Situacao);
    }

    [Fact]
    public async Task Falha_de_um_endereco_nao_derruba_o_bloco()
    {
        var ok1 = Endereco();
        var falha = Endereco(cep: "35790222");
        var ok2 = Endereco();
        _conferencia.Responder = (e, _) => Digitos(e.Cep) == "35790222"
            ? throw new InvalidOperationException("erro inesperado")
            : Decidir(e, RespostaConsultaCep.Encontrado(Registro, CepFonte.ViaCep));

        var r = await Servico().ProcessarAsync([ok1, falha, ok2]);

        Assert.Equal([ResultadoItemReconferencia.Conferido, ResultadoItemReconferencia.Indisponivel, ResultadoItemReconferencia.Conferido],
            r.Itens.Select(i => i.Resultado));
        Assert.False(_banco.Gravados.ContainsKey(falha));
    }

    [Fact]
    public async Task Endereco_alterado_durante_a_reconferencia_nao_recebe_o_resultado_antigo()
    {
        var id = Endereco();
        _conferencia.Responder = (e, _) =>
        {
            // O lote leu o endereço A; enquanto a fonte respondia, o usuário salvou o endereço B (outro número).
            _banco.Enderecos[id] = _banco.Enderecos[id] with { Numero = "999" };
            return Decidir(e, RespostaConsultaCep.Encontrado(Registro, CepFonte.ViaCep));
        };

        var r = await Servico().ProcessarAsync([id]);

        Assert.Equal(ResultadoItemReconferencia.AlteradoDuranteAReconferencia, r.Itens[0].Resultado);
        Assert.Empty(_banco.Gravados);
        Assert.Equal(("999", CepSituacao.NaoConferido), (_banco.Enderecos[id].Numero, _banco.Enderecos[id].Situacao));
    }

    [Fact]
    public async Task Nao_processa_inexistente_inativo_exterior_e_sem_cep_valido()
    {
        var inativo = Endereco(ativo: false);
        var exterior = Endereco(brasil: false);
        var semCep = Endereco(cep: "123");
        var removido = Guid.NewGuid();
        TodosConferidos();

        var r = await Servico().ProcessarAsync([removido, inativo, exterior, semCep]);

        Assert.All(r.Itens, i => Assert.Equal(ResultadoItemReconferencia.NaoProcessado, i.Resultado));
        Assert.Equal(0, _conferencia.Chamadas);
        Assert.Empty(_banco.Gravados);
    }

    [Fact]
    public async Task Lote_grande_processa_no_maximo_um_bloco_por_chamada()
    {
        var ids = Enumerable.Range(0, 25).Select(_ => Endereco()).ToList();
        TodosConferidos();

        var r = await Servico().ProcessarAsync(ids);

        Assert.Equal(PoliticaReconferenciaCep.ItensPorChamada, r.Total);
        Assert.Equal(PoliticaReconferenciaCep.ItensPorChamada, _conferencia.Chamadas);
        Assert.Equal(ids.Take(PoliticaReconferenciaCep.ItensPorChamada), r.Itens.Select(i => i.EnderecoId)); // na ordem pedida
    }

    [Fact]
    public async Task Paralelismo_limitado()
    {
        var ids = Enumerable.Range(0, 8).Select(_ => Endereco()).ToList();
        _conferencia.Responder = async (e, _) =>
        {
            await Task.Delay(30);
            return new ResultadoConferenciaCep(MotorCep.Decidir(e, RespostaConsultaCep.Encontrado(Registro, CepFonte.ViaCep)), []);
        };

        await Servico(paralelismo: 2, intervalo: TimeSpan.Zero).ProcessarAsync(ids);

        Assert.InRange(_conferencia.MaximoSimultaneas, 1, 2);
        Assert.Equal(8, _conferencia.Chamadas);
    }

    [Fact]
    public async Task Ritmo_espera_entre_inicios_e_dobra_enquanto_a_fonte_esta_fora()
    {
        var ids = Enumerable.Range(0, 5).Select(i => Endereco(cep: i is 1 or 2 ? "35790111" : "35790000")).ToList();
        _conferencia.Responder = (e, _) => Digitos(e.Cep) == "35790111"
            ? Decidir(e, RespostaConsultaCep.Indisponivel(CepFonte.ViaCep))
            : Decidir(e, RespostaConsultaCep.Encontrado(Registro, CepFonte.ViaCep));

        await Servico(paralelismo: 1).ProcessarAsync(ids);

        // 1º ok → 1 s; 2º fora → 2 s; 3º fora → 4 s; 4º ok → volta a 1 s.
        Assert.Equal([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(1)], _esperas);
    }

    [Fact]
    public void A_politica_e_conservadora_e_documentada()
    {
        Assert.Equal(2, PoliticaReconferenciaCep.Paralelismo);
        Assert.Equal(TimeSpan.FromSeconds(1), PoliticaReconferenciaCep.Intervalo);
        Assert.Equal(TimeSpan.FromSeconds(16), PoliticaReconferenciaCep.IntervaloMaximo);
        Assert.Equal(TimeSpan.FromDays(180), PoliticaReconferenciaCep.IdadeParaReconferir);
        Assert.True(PoliticaReconferenciaCep.IdadeParaReconferir > PoliticaCachePostalCep.UsoOfflineEncontrado); // idade ≠ TTL do cache
    }

    [Fact]
    public async Task Cancelar_preserva_o_concluido_e_nao_comeca_os_seguintes()
    {
        var ids = Enumerable.Range(0, 4).Select(_ => Endereco()).ToList();
        using var cts = new CancellationTokenSource();
        var chamada = 0;
        _conferencia.Responder = async (e, ct) =>
        {
            if (Interlocked.Increment(ref chamada) == 2)
            {
                cts.Cancel();                        // o usuário cancelou com o 2º em andamento
                await Task.Delay(Timeout.Infinite, ct);
            }
            return new ResultadoConferenciaCep(MotorCep.Decidir(e, RespostaConsultaCep.Encontrado(Registro, CepFonte.ViaCep)), []);
        };

        var r = await Servico(paralelismo: 1).ProcessarAsync(ids, cts.Token);

        Assert.Equal([ResultadoItemReconferencia.Conferido, ResultadoItemReconferencia.Cancelado, ResultadoItemReconferencia.NaoProcessado,
            ResultadoItemReconferencia.NaoProcessado], r.Itens.Select(i => i.Resultado));
        Assert.Single(_banco.Gravados);              // só o concluído
        Assert.Equal(2, _conferencia.Chamadas);      // os seguintes não começaram
    }

    [Fact]
    public async Task Orcamento_da_chamada_esgotado_adia_o_em_andamento_e_os_seguintes_sem_gravar()
    {
        var ids = Enumerable.Range(0, 4).Select(_ => Endereco()).ToList();
        var chamada = 0;
        _conferencia.Responder = async (e, ct) =>
        {
            if (Interlocked.Increment(ref chamada) == 2)
            {
                _relogio.Advance(PoliticaReconferenciaCep.OrcamentoPorChamada + TimeSpan.FromSeconds(1)); // fonte lenta
                await Task.Delay(Timeout.Infinite, ct);
            }
            return new ResultadoConferenciaCep(MotorCep.Decidir(e, RespostaConsultaCep.Encontrado(Registro, CepFonte.ViaCep)), []);
        };

        var r = await Servico(paralelismo: 1).ProcessarAsync(ids);

        Assert.Equal([ResultadoItemReconferencia.Conferido, ResultadoItemReconferencia.Adiado, ResultadoItemReconferencia.Adiado,
            ResultadoItemReconferencia.Adiado], r.Itens.Select(i => i.Resultado));
        Assert.Single(_banco.Gravados);              // o adiado em andamento não grava nada
        Assert.Equal(2, _conferencia.Chamadas);      // os seguintes não começaram
        Assert.All(r.Itens.Skip(1), i => Assert.Contains("próximo bloco", i.Detalhe));
    }

    [Fact]
    public void Orcamento_fica_abaixo_do_tempo_que_o_aplicativo_espera()
    {
        Assert.True(PoliticaReconferenciaCep.OrcamentoPorChamada < TimeSpan.FromSeconds(30));
        Assert.Equal(2, PoliticaReconferenciaCep.MaximoAdiamentos);
    }

    [Fact]
    public async Task Bloco_vazio_nao_faz_nada()
    {
        var r = await Servico().ProcessarAsync([]);

        Assert.Empty(r.Itens);
        Assert.Equal(0, _conferencia.Chamadas);
    }

    [Fact]
    public async Task Selecao_respeita_o_limite_da_politica()
    {
        for (var i = 0; i < 3; i++) Endereco();

        var s = await Servico().SelecionarAsync(new CriterioReconferenciaCep(Limite: 2));
        var acima = await Servico().SelecionarAsync(new CriterioReconferenciaCep(Limite: 99999));

        Assert.Equal((3, 2, true), (s.Total, s.Enderecos.Count, s.Truncada));
        Assert.Equal(3, acima.Enderecos.Count);
    }

    [Fact]
    public async Task Limpeza_do_historico_usa_a_retencao_de_90_dias_em_lotes()
    {
        var removidos = await Servico().LimparHistoricoAsync();

        Assert.Equal(3, removidos);
        Assert.Equal((Agora - TimeSpan.FromDays(90), ServicoReconferenciaCep.TamanhoLoteLimpeza), _banco.Limpeza);
    }
}

/// <summary>
/// F6 com o serviço de conferência de verdade (fontes falsas, cache postal em memória): reconferência usa o cache válido, o
/// offline não grava, a lista limitada não vira escolha e o histórico registra "reconferência em lote". R-E3: a data gravada
/// é a da avaliação; a da evidência (cache) continua a original.
/// </summary>
public class ReconferenciaCepComServicoRealTests
{
    private sealed class Persistencia : ICachePostalCep, IHistoricoConsultasCep
    {
        public Dictionary<string, RespostaGuardadaCep> Cache { get; } = new();
        public List<ConsultaCepOcorrida> Historico { get; } = [];
        public Task<RespostaGuardadaCep?> ObterAsync(string chave, CancellationToken ct = default) =>
            Task.FromResult(Cache.TryGetValue(chave, out var r) ? r : null);
        public Task GuardarAsync(string chave, TipoConsultaCep tipo, string? cep, RespostaGuardadaCep resposta, CancellationToken ct = default)
        {
            Cache[chave] = resposta;
            return Task.CompletedTask;
        }
        public Task RegistrarAsync(ConsultaCepOcorrida c)
        {
            Historico.Add(c);
            return Task.CompletedTask;
        }
    }

    private sealed class FonteDeTeste : IProvedorCep
    {
        public CepFonte Fonte { get; } = CepFonte.ViaCep;
        public bool BuscaPorEndereco => true;
        public bool ForaDoAr { get; set; }
        public int Consultas { get; private set; }
        public Func<string, ResultadoProvedorCep> Consulta { get; set; } = _ => throw new InvalidOperationException();
        public Func<BuscaEnderecoCep, ResultadoBuscaProvedorCep> Busca { get; set; } = _ => throw new InvalidOperationException();
        public Task<ResultadoProvedorCep> ConsultarPorCepAsync(string cep, CancellationToken ct = default)
        {
            Consultas++;
            return Task.FromResult(ForaDoAr ? ResultadoProvedorCep.Indisponivel(CepFonte.ViaCep, "503") : Consulta(cep));
        }
        public Task<ResultadoBuscaProvedorCep> BuscarPorEnderecoAsync(BuscaEnderecoCep b, CancellationToken ct = default) =>
            Task.FromResult(ForaDoAr ? ResultadoBuscaProvedorCep.Indisponivel(CepFonte.ViaCep, "503") : Busca(b));
    }

    private sealed class Banco : IReconferenciaCepRepositorio, IManutencaoConsultasCep
    {
        public Dictionary<Guid, EnderecoReconferivel> Enderecos { get; } = new();
        public Dictionary<Guid, ConferenciaCepRealizada> Gravados { get; } = new();
        public Task<SelecaoReconferenciaCep> SelecionarAsync(CriterioReconferenciaCep c, DateTime agora, CancellationToken ct = default) =>
            throw new InvalidOperationException();
        public Task<IReadOnlyList<EnderecoReconferivel>> ObterAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<EnderecoReconferivel>>(ids.Where(Enderecos.ContainsKey).Select(i => Enderecos[i]).ToList());
        public Task<bool> GravarAsync(EnderecoReconferivel lido, ConferenciaCepRealizada c, CancellationToken ct = default)
        {
            Gravados[lido.EnderecoId] = c;
            return Task.FromResult(true);
        }
        public Task<int> LimparAsync(DateTime limite, int lote, CancellationToken ct = default) => Task.FromResult(0);
    }

    private static readonly DateTime Inicio = new(2026, 10, 6, 10, 0, 0, DateTimeKind.Utc);
    private readonly FakeTimeProvider _relogio = new(new DateTimeOffset(Inicio));
    private readonly FonteDeTeste _fonte = new();
    private readonly Persistencia _persistencia = new();
    private readonly Banco _banco = new();

    private ServicoReconferenciaCep Reconferencia()
    {
        var conferencia = new ServicoConferenciaCep([_fonte], new CacheConferenciaCep(_relogio), new SugestoesCepEmitidas(_relogio),
            new ConferenciasCepEmitidas(_relogio), _relogio, _persistencia, _persistencia);
        return new ServicoReconferenciaCep(_banco, conferencia, _banco, _relogio, (_, _) => Task.CompletedTask, TimeSpan.Zero, 1);
    }

    private Guid Endereco(string cep = "35790000")
    {
        var id = Guid.NewGuid();
        _banco.Enderecos[id] = new EnderecoReconferivel(id, Guid.NewGuid(), 1, "Cliente", cep, "R. Barao", "150", "Centro", "Curvelo", "MG",
            3120904, "3120904", true, true, CepSituacao.NaoConferido);
        return id;
    }

    private static readonly RegistroCep Registro = new("35790000", "Rua Barão", "até 999/1000", "Centro", "Curvelo", "MG", "3120904");

    [Fact]
    public async Task Reconferencia_usa_o_cache_valido_e_registra_a_operacao_no_historico()
    {
        _fonte.Consulta = _ => ResultadoProvedorCep.Encontrado(CepFonte.ViaCep, Registro);
        var a = Endereco();
        var b = Endereco();

        await Reconferencia().ProcessarAsync([a]);
        await Reconferencia().ProcessarAsync([b]); // outro "processo": memória vazia, cache postal válido

        Assert.Equal(1, _fonte.Consultas);
        Assert.All(_persistencia.Historico, h => Assert.Equal(OperacaoConsultaCep.ReconferenciaLote, h.Operacao));
        Assert.Equal(OrigemRespostaCep.CachePersistente, _persistencia.Historico.Last().Origem);
    }

    [Fact]
    public async Task R_E3_data_gravada_e_a_da_avaliacao_e_a_da_evidencia_continua_a_original()
    {
        _fonte.Consulta = _ => ResultadoProvedorCep.Encontrado(CepFonte.ViaCep, Registro);
        await Reconferencia().ProcessarAsync([Endereco()]);
        _relogio.Advance(TimeSpan.FromHours(5)); // dentro da validade (24 h): vem do cache
        var id = Endereco();

        await Reconferencia().ProcessarAsync([id]);

        var evidencia = _persistencia.Cache[CacheConferenciaCep.ChaveCep("35790000")].ConsultadoEm;
        var avaliacao = _banco.Gravados[id].ConferidoEm;
        Assert.Equal(Inicio, evidencia);                       // quando a fonte respondeu
        Assert.Equal(Inicio.AddHours(5), avaliacao);           // quando o Lone avaliou o endereço
        Assert.NotEqual(evidencia, avaliacao);
        Assert.Equal(CepFonte.ViaCep, _banco.Gravados[id].Fonte); // a fonte é a original, nunca "cache"
    }

    [Fact]
    public async Task Offline_com_informacao_anterior_nao_grava_estado()
    {
        _fonte.Consulta = _ => ResultadoProvedorCep.Encontrado(CepFonte.ViaCep, Registro);
        await Reconferencia().ProcessarAsync([Endereco()]);
        _relogio.Advance(TimeSpan.FromDays(2));
        _fonte.ForaDoAr = true;
        _banco.Gravados.Clear();
        var id = Endereco();

        var r = await Reconferencia().ProcessarAsync([id]);

        Assert.Equal(ResultadoItemReconferencia.Indisponivel, r.Itens[0].Resultado);
        Assert.Empty(_banco.Gravados);
    }

    [Fact]
    public async Task Lista_limitada_continua_so_candidatos_sem_escolha()
    {
        _fonte.Consulta = _ => ResultadoProvedorCep.NaoEncontrado(CepFonte.ViaCep);
        _fonte.Busca = _ => ResultadoBuscaProvedorCep.Com(CepFonte.ViaCep,
            [Registro with { Cep = "35790001" }, Registro with { Cep = "35790002", Complemento = null }], limiteAtingido: true);
        var id = Endereco("35790999");

        var r = await Reconferencia().ProcessarAsync([id]);

        Assert.Equal(ResultadoItemReconferencia.NaoEncontrado, r.Itens[0].Resultado);
        Assert.Equal(ResultadoDecisaoCep.VariosCandidatos, r.Itens[0].Decisao!.Resultado);
        Assert.Null(r.Itens[0].Decisao!.CepSugerido);
        Assert.Equal("35790999", _banco.Enderecos[id].Cep);
        Assert.Equal(CepSituacao.NaoEncontrado, _banco.Gravados[id].Situacao);
    }
}
