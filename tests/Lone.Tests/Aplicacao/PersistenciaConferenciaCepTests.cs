using Lone.Application.Integracoes;
using Lone.Application.Integracoes.ConferenciaCep;
using Lone.Contracts.Integracoes;
using Lone.Application.Seguranca;
using Lone.Domain.Enderecos.ConferenciaCep;
using Microsoft.Extensions.Time.Testing;

namespace Lone.Tests.Aplicacao;

/// <summary>
/// F3, serviço: cache postal persistente (resposta atual dentro da validade; CEP encontrado como "última informação"
/// quando a fonte está fora do ar), histórico técnico (sem dados da pessoa) e o registro das conferências que sustentam o
/// estado gravado. Invariantes: indisponível ≠ inexistente; cache ≠ confirmação atual; informação negativa expira antes e
/// nunca vira offline; falha técnica nunca é guardada; a fonte original nunca vira "cache".
/// </summary>
public class PersistenciaConferenciaCepTests
{
    /// <summary>Cache postal e histórico em memória (o SQL tem testes próprios).</summary>
    private sealed class PersistenciaEmMemoria : ICachePostalCep, IHistoricoConsultasCep
    {
        public Dictionary<string, (TipoConsultaCep Tipo, string? Cep, RespostaGuardadaCep Resposta)> Cache { get; } = new();
        public List<ConsultaCepOcorrida> Historico { get; } = [];
        public int Gravacoes { get; private set; }

        public Task<RespostaGuardadaCep?> ObterAsync(string chave, CancellationToken ct = default) =>
            Task.FromResult(Cache.TryGetValue(chave, out var c) ? c.Resposta : null);

        public Task GuardarAsync(string chave, TipoConsultaCep tipo, string? cep, RespostaGuardadaCep resposta, CancellationToken ct = default)
        {
            Gravacoes++;
            Cache[chave] = (tipo, cep, resposta);
            return Task.CompletedTask;
        }

        public Task RegistrarAsync(ConsultaCepOcorrida consulta)
        {
            Historico.Add(consulta);
            return Task.CompletedTask;
        }
    }

    private sealed class FonteDeTeste(CepFonte fonte, bool busca = false) : IProvedorCep
    {
        public CepFonte Fonte { get; } = fonte;
        public bool BuscaPorEndereco { get; } = busca;
        public Func<string, CancellationToken, Task<ResultadoProvedorCep>> Consulta { get; set; } =
            (_, _) => throw new InvalidOperationException("consulta não esperada");
        public Func<BuscaEnderecoCep, CancellationToken, Task<ResultadoBuscaProvedorCep>> Busca { get; set; } =
            (_, _) => throw new InvalidOperationException("busca não esperada");
        public int Consultas { get; private set; }

        public Task<ResultadoProvedorCep> ConsultarPorCepAsync(string cep, CancellationToken ct = default)
        {
            Consultas++;
            return Consulta(cep, ct);
        }

        public Task<ResultadoBuscaProvedorCep> BuscarPorEnderecoAsync(BuscaEnderecoCep busca, CancellationToken ct = default) => Busca(busca, ct);
    }

    private static readonly DateTime Inicio = new(2026, 10, 5, 20, 30, 0, DateTimeKind.Utc);
    private readonly FakeTimeProvider _relogio = new(new DateTimeOffset(Inicio));
    private readonly FonteDeTeste _viaCep = new(CepFonte.ViaCep, busca: true);
    private readonly FonteDeTeste _brasilApi = new(CepFonte.BrasilApi);
    private readonly PersistenciaEmMemoria _banco = new();
    private readonly ConferenciasCepEmitidas _conferencias;

    public PersistenciaConferenciaCepTests() => _conferencias = new ConferenciasCepEmitidas(_relogio);

    /// <summary>Um serviço novo a cada chamada com cache em memória novo (simula outra instância/reinício: só o banco fica).</summary>
    private ServicoConferenciaCep Servico() =>
        new([_viaCep, _brasilApi], new CacheConferenciaCep(_relogio), new SugestoesCepEmitidas(_relogio), _conferencias, _relogio, _banco, _banco);

    private static EnderecoConferenciaCep Endereco(string numero = "150", string bairro = "Centro") =>
        new("35790-000", "R. Barao", numero, bairro, "Curvelo", "MG", "3120904");

    private static readonly RegistroCep Registro =
        new("35790000", "Rua Barão", "até 999/1000", "Centro", "Curvelo", "MG", "3120904", "Edifício Central");

    private static Task<T> Ja<T>(T v) => Task.FromResult(v);
    private void FonteFora() =>
        _viaCep.Consulta = _brasilApi.Consulta = (_, _) => Ja(ResultadoProvedorCep.Indisponivel(CepFonte.ViaCep, "timeout"));

    // ---- Cache persistente ----

    [Fact]
    public async Task Cep_encontrado_e_guardado_com_fonte_original_data_unidade_e_validade()
    {
        _viaCep.Consulta = (_, _) => Ja(ResultadoProvedorCep.Encontrado(CepFonte.ViaCep, Registro));

        await Servico().ConferirAsync(Endereco());

        var (tipo, cep, r) = _banco.Cache[CacheConferenciaCep.ChaveCep("35790000")];
        Assert.Equal((TipoConsultaCep.PorCep, "35790000"), (tipo, cep));
        Assert.Equal((SituacaoCachePostal.Encontrado, CepFonte.ViaCep, Inicio), (r.Situacao, r.Fonte, r.ConsultadoEm));
        Assert.Equal(Inicio + PoliticaCachePostalCep.ValidadeEncontrado, r.ExpiraEm);
        Assert.Equal(Inicio + PoliticaCachePostalCep.UsoOfflineEncontrado, r.UtilizavelAte);
        Assert.Equal("Edifício Central", Assert.Single(r.Registros).Unidade);
    }

    [Fact]
    public async Task Cep_inexistente_e_guardado_com_vida_curta_e_nunca_para_uso_offline()
    {
        _viaCep.Consulta = (_, _) => Ja(ResultadoProvedorCep.NaoEncontrado(CepFonte.ViaCep));
        _viaCep.Busca = (_, _) => Ja(ResultadoBuscaProvedorCep.Com(CepFonte.ViaCep, [])); // CEP inexistente aciona a busca

        await Servico().ConferirAsync(Endereco());

        var r = _banco.Cache[CacheConferenciaCep.ChaveCep("35790000")].Resposta;
        Assert.Equal(SituacaoCachePostal.NaoEncontrado, r.Situacao);
        Assert.Equal(Inicio + PoliticaCachePostalCep.ValidadeInexistente, r.ExpiraEm);
        Assert.True(r.ExpiraEm < Inicio + PoliticaCachePostalCep.ValidadeEncontrado); // negativo expira antes do positivo
        Assert.Null(r.UtilizavelAte);
    }

    [Fact]
    public async Task Falha_tecnica_nunca_e_guardada()
    {
        FonteFora();

        var r = await Servico().ConferirAsync(Endereco());

        Assert.Equal(ResultadoDecisaoCep.FonteIndisponivel, r.Decisao.Resultado);
        Assert.Equal(0, _banco.Gravacoes);
    }

    [Fact]
    public async Task Cache_persistente_valido_evita_a_fonte_e_mantem_a_fonte_original()
    {
        _viaCep.Consulta = (_, _) => Ja(ResultadoProvedorCep.Encontrado(CepFonte.ViaCep, Registro));
        await Servico().ConferirAsync(Endereco());
        _relogio.Advance(TimeSpan.FromHours(2));

        var r = await Servico().ConferirAsync(Endereco()); // outra instância: memória vazia, banco com a resposta

        Assert.Equal(1, _viaCep.Consultas);
        Assert.Equal((ResultadoDecisaoCep.Conferido, (CepFonte?)CepFonte.ViaCep), (r.Decisao.Resultado, r.Decisao.Fonte));
        Assert.Equal(OrigemRespostaCep.CachePersistente, _banco.Historico.Last().Origem);
        Assert.Equal(Inicio, _banco.Cache[CacheConferenciaCep.ChaveCep("35790000")].Resposta.ConsultadoEm); // ler não muda a data
    }

    [Fact]
    public async Task Cache_persistente_expirado_consulta_a_fonte_de_novo()
    {
        _viaCep.Consulta = (_, _) => Ja(ResultadoProvedorCep.Encontrado(CepFonte.ViaCep, Registro));
        await Servico().ConferirAsync(Endereco());
        _relogio.Advance(PoliticaCachePostalCep.ValidadeEncontrado + TimeSpan.FromMinutes(1));

        await Servico().ConferirAsync(Endereco());

        Assert.Equal(2, _viaCep.Consultas);
        Assert.Equal(Inicio + PoliticaCachePostalCep.ValidadeEncontrado + TimeSpan.FromMinutes(1),
            _banco.Cache[CacheConferenciaCep.ChaveCep("35790000")].Resposta.ConsultadoEm);
    }

    [Fact]
    public async Task Falso_negativo_expirado_da_lugar_a_nova_realidade()
    {
        _viaCep.Consulta = (_, _) => Ja(ResultadoProvedorCep.NaoEncontrado(CepFonte.ViaCep));
        _viaCep.Busca = (_, _) => Ja(ResultadoBuscaProvedorCep.Com(CepFonte.ViaCep, []));
        Assert.Equal(ResultadoDecisaoCep.NenhumCandidato, (await Servico().ConferirAsync(Endereco())).Decisao.Resultado);

        _relogio.Advance(PoliticaCachePostalCep.ValidadeInexistente + TimeSpan.FromMinutes(1));
        _viaCep.Consulta = (_, _) => Ja(ResultadoProvedorCep.Encontrado(CepFonte.ViaCep, Registro)); // o CEP passou a existir

        var r = await Servico().ConferirAsync(Endereco());

        Assert.Equal(ResultadoDecisaoCep.Conferido, r.Decisao.Resultado);
        Assert.Equal(SituacaoCachePostal.Encontrado, _banco.Cache[CacheConferenciaCep.ChaveCep("35790000")].Resposta.Situacao);
    }

    [Fact]
    public async Task Busca_por_endereco_guarda_e_devolve_a_lista_ainda_limitada()
    {
        _viaCep.Consulta = (_, _) => Ja(ResultadoProvedorCep.NaoEncontrado(CepFonte.ViaCep));
        _viaCep.Busca = (_, _) => Ja(ResultadoBuscaProvedorCep.Com(CepFonte.ViaCep, [Registro with { Cep = "35790001" }], limiteAtingido: true));
        await Servico().BuscarPorEnderecoAsync(Endereco());
        _viaCep.Busca = (_, _) => throw new InvalidOperationException("não deveria chamar a fonte");

        var r = await Servico().BuscarPorEnderecoAsync(Endereco()); // outra instância: vem do banco

        var guardada = _banco.Cache[CacheConferenciaCep.ChaveBusca(BuscaEnderecoCep.De(Endereco())!)];
        Assert.Equal((TipoConsultaCep.PorEndereco, (string?)null, true), (guardada.Tipo, guardada.Cep, guardada.Resposta.LimiteAtingido));
        Assert.Equal(Inicio + PoliticaCachePostalCep.ValidadeBusca, guardada.Resposta.ExpiraEm);
        Assert.Null(guardada.Resposta.UtilizavelAte);                                    // busca nunca é usada offline
        Assert.Equal([ServicoConferenciaCep.AvisoBuscaListaIncompleta], r.Avisos);      // 50 guardados ≠ lista completa
        Assert.DoesNotContain("150", CacheConferenciaCep.ChaveBusca(BuscaEnderecoCep.De(Endereco())!)); // número só filtra; não entra na chave
    }

    // ---- Offline ----

    [Fact]
    public async Task Offline_com_informacao_anterior_mostra_a_anterior_sem_chamar_de_confirmacao_atual()
    {
        _viaCep.Consulta = (_, _) => Ja(ResultadoProvedorCep.Encontrado(CepFonte.ViaCep, Registro));
        await Servico().ConferirAsync(Endereco());
        _relogio.Advance(TimeSpan.FromDays(3)); // passou a validade; ainda dentro dos 30 dias de uso offline
        FonteFora();

        var r = await Servico().ConferirAsync(Endereco());

        Assert.Equal(ResultadoDecisaoCep.FonteIndisponivel, r.Decisao.Resultado);      // nada foi confirmado agora
        var anterior = r.Anterior!;
        Assert.Equal((CepFonte.ViaCep, Inicio), (anterior.Fonte, anterior.ConsultadoEm)); // fonte e data originais
        Assert.Equal(ResultadoDecisaoCep.Conferido, anterior.DecisaoComEla.Resultado);  // o que se sabia antes
        Assert.Equal(Inicio, _banco.Cache[CacheConferenciaCep.ChaveCep("35790000")].Resposta.ConsultadoEm); // a data não muda
        Assert.Equal(OrigemRespostaCep.InformacaoAnterior, _banco.Historico.Last().Origem);
        // A conferência de 3 dias atrás já venceu (1 h) e a resposta offline não registra conferência nova para o Salvar.
        Assert.Null(_conferencias.Obter(EstadoConferenciaCep.Assinatura(Endereco())));
    }

    [Fact]
    public async Task Offline_sem_cache_e_fonte_indisponivel_sem_informacao_anterior()
    {
        FonteFora();

        var r = await Servico().ConferirAsync(Endereco());

        Assert.Equal(ResultadoDecisaoCep.FonteIndisponivel, r.Decisao.Resultado);
        Assert.Null(r.Anterior);
    }

    [Fact]
    public async Task Offline_com_so_informacao_negativa_nao_mostra_inexistencia()
    {
        _viaCep.Consulta = (_, _) => Ja(ResultadoProvedorCep.NaoEncontrado(CepFonte.ViaCep));
        _viaCep.Busca = (_, _) => Ja(ResultadoBuscaProvedorCep.Com(CepFonte.ViaCep, []));
        await Servico().ConferirAsync(Endereco());
        _relogio.Advance(TimeSpan.FromHours(2));
        FonteFora();

        var r = await Servico().ConferirAsync(Endereco());

        Assert.Equal(ResultadoDecisaoCep.FonteIndisponivel, r.Decisao.Resultado);   // nunca "não encontrado" por histórico
        Assert.Null(r.Anterior);
    }

    [Fact]
    public async Task Offline_depois_dos_30_dias_nao_usa_a_informacao_antiga()
    {
        _viaCep.Consulta = (_, _) => Ja(ResultadoProvedorCep.Encontrado(CepFonte.ViaCep, Registro));
        await Servico().ConferirAsync(Endereco());
        _relogio.Advance(PoliticaCachePostalCep.UsoOfflineEncontrado + TimeSpan.FromMinutes(1));
        FonteFora();

        Assert.Null((await Servico().ConferirAsync(Endereco())).Anterior);
    }

    // ---- Conferências registradas para o estado gravado ----

    [Fact]
    public async Task Conferencia_com_conclusao_fica_registrada_para_o_salvar_e_a_indisponivel_nao()
    {
        _viaCep.Consulta = (_, _) => Ja(ResultadoProvedorCep.Encontrado(CepFonte.ViaCep, Registro));
        await Servico().ConferirAsync(Endereco());
        var assinatura = EstadoConferenciaCep.Assinatura(Endereco());
        Assert.Equal(new ConferenciaCepRealizada(CepSituacao.Conferido, CepFonte.ViaCep, Inicio), _conferencias.Obter(assinatura));

        // Passada a validade da resposta guardada (24 h), a fonte cai: a resposta é "indisponível" e não é registrada.
        _relogio.Advance(PoliticaCachePostalCep.ValidadeEncontrado + TimeSpan.FromMinutes(1));
        FonteFora();
        var r = await Servico().ConferirAsync(Endereco(numero: "151"));
        Assert.Equal(ResultadoDecisaoCep.FonteIndisponivel, r.Decisao.Resultado);
        Assert.Null(_conferencias.Obter(EstadoConferenciaCep.Assinatura(Endereco(numero: "151"))));
    }

    [Fact]
    public async Task Busca_por_endereco_nao_registra_conferencia()
    {
        _viaCep.Busca = (_, _) => Ja(ResultadoBuscaProvedorCep.Com(CepFonte.ViaCep, [Registro]));

        await Servico().BuscarPorEnderecoAsync(Endereco());

        Assert.Null(_conferencias.Obter(EstadoConferenciaCep.Assinatura(Endereco())));
    }

    // ---- Histórico técnico ----

    [Fact]
    public async Task Historico_da_conferencia_registra_fonte_duracao_resultado_e_origem()
    {
        _viaCep.Consulta = (_, _) =>
        {
            _relogio.Advance(TimeSpan.FromMilliseconds(320));
            return Ja(ResultadoProvedorCep.Encontrado(CepFonte.ViaCep, Registro));
        };

        await Servico().ConferirAsync(Endereco());

        var h = Assert.Single(_banco.Historico);
        Assert.Equal((OperacaoConsultaCep.Conferencia, TipoConsultaCep.PorCep, "cep:35790000", "35790000"), (h.Operacao, h.Tipo, h.Chave, h.Cep));
        Assert.Equal((CepFonte?)CepFonte.ViaCep, h.Fonte);
        Assert.Equal((ResultadoConsultaCep.Encontrado, OrigemRespostaCep.Fonte), (h.Resultado, h.Origem));
        Assert.Equal(TimeSpan.FromMilliseconds(320), h.Duracao);
    }

    [Fact]
    public async Task Historico_registra_falha_tecnica_categorizada_de_cada_fonte()
    {
        _viaCep.Consulta = (_, _) => Ja(ResultadoProvedorCep.Invalida(CepFonte.ViaCep, "json"));
        _brasilApi.Consulta = (_, _) => Ja(ResultadoProvedorCep.Indisponivel(CepFonte.BrasilApi, "timeout"));

        await Servico().ConferirAsync(Endereco());

        Assert.Equal([(CepFonte.ViaCep, ResultadoConsultaCep.RespostaInvalida), (CepFonte.BrasilApi, ResultadoConsultaCep.Indisponivel)],
            _banco.Historico.Select(h => (h.Fonte!.Value, h.Resultado)));
    }

    [Fact]
    public async Task Historico_da_busca_registra_o_limite_e_o_uso_do_cache_em_memoria()
    {
        _viaCep.Busca = (_, _) => Ja(ResultadoBuscaProvedorCep.Com(CepFonte.ViaCep, [Registro], limiteAtingido: true));
        var servico = Servico();

        await servico.BuscarPorEnderecoAsync(Endereco());
        await servico.BuscarPorEnderecoAsync(Endereco());

        Assert.Equal([(OrigemRespostaCep.Fonte, (bool?)true), (OrigemRespostaCep.CacheMemoria, (bool?)true)],
            _banco.Historico.Select(h => (h.Origem, h.LimiteAtingido)));
        Assert.All(_banco.Historico, h => Assert.Equal((OperacaoConsultaCep.BuscaPorEndereco, TipoConsultaCep.PorEndereco), (h.Operacao, h.Tipo)));
    }

    [Fact]
    public async Task Historico_registra_o_cancelamento_como_cancelamento()
    {
        using var cts = new CancellationTokenSource();
        _viaCep.Consulta = (_, ct) =>
        {
            cts.Cancel();
            ct.ThrowIfCancellationRequested();
            return Ja(ResultadoProvedorCep.NaoEncontrado(CepFonte.ViaCep));
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Servico().ConferirAsync(Endereco(), cts.Token));

        Assert.Equal(ResultadoConsultaCep.Cancelado, Assert.Single(_banco.Historico).Resultado);
        Assert.Equal(0, _banco.Gravacoes);
    }

    [Fact]
    public async Task Historico_nao_leva_numero_bairro_nem_dados_da_pessoa()
    {
        _viaCep.Consulta = (_, _) => Ja(ResultadoProvedorCep.NaoEncontrado(CepFonte.ViaCep));
        _viaCep.Busca = (_, _) => Ja(ResultadoBuscaProvedorCep.Com(CepFonte.ViaCep, []));

        await Servico().ConferirAsync(Endereco(numero: "4321", bairro: "Jardim Secreto"));

        Assert.All(_banco.Historico, h => Assert.DoesNotContain("4321", h.Chave + h.Cep));
        Assert.All(_banco.Historico, h => Assert.DoesNotContain("JARDIM", h.Chave));
        Assert.DoesNotContain(typeof(ConsultaCepOcorrida).GetProperties(),
            p => p.Name is "Numero" or "Bairro" or "Complemento" or "PessoaId" or "Nome" or "Documento" or "Detalhe");
        Assert.DoesNotContain(typeof(ConsultaCep).GetProperties(),
            p => p.Name is "Numero" or "Bairro" or "Complemento" or "PessoaId" or "Nome" or "Documento" or "Detalhe");
    }

    [Fact]
    public async Task Historico_da_consulta_direta_antiga()
    {
        var api = new ConsultasAppService(new CepAntigo(), null!, null!, new Liberado(), Servico(), _banco, _relogio);

        await api.ConsultarCepAsync("35790-000");
        await api.ConsultarCepAsync("99999-999");

        Assert.Equal([(OperacaoConsultaCep.ConsultaDireta, ResultadoConsultaCep.Encontrado), (OperacaoConsultaCep.ConsultaDireta, ResultadoConsultaCep.NaoEncontrado)],
            _banco.Historico.Select(h => (h.Operacao, h.Resultado)));
    }

    private sealed class CepAntigo : ICepConsulta
    {
        public Task<DadosCep?> ConsultarAsync(string cep, CancellationToken ct = default) =>
            Task.FromResult<DadosCep?>(cep == "35790000" ? new DadosCep { Cep = cep } : null);
    }

    private sealed class Liberado : IAutorizacao
    {
        public bool Possui(string permissao) => true;
        public void Exigir(string permissao) { }
    }
}
