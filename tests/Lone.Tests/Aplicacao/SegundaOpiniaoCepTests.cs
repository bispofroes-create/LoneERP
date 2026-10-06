using Lone.Application.Integracoes;
using Lone.Application.Integracoes.ConferenciaCep;
using Lone.Application.Seguranca;
using Lone.Contracts.Integracoes;
using Lone.Contracts.Seguranca;
using Lone.Domain.Enderecos.ConferenciaCep;
using Lone.Domain.Validacao;
using Microsoft.Extensions.Time.Testing;

namespace Lone.Tests.Aplicacao;

/// <summary>
/// Checkpoint G, serviço: a segunda opinião só existe por pedido; a principal segue o caminho da conferência (cache,
/// principal, reserva); a outra fonte responde à mesma pergunta (consulta pelo CEP); indisponível não é conflito; nada é
/// escolhido, guardado como conferência ou alterado; tudo fica no histórico técnico como SegundaOpiniao.
/// </summary>
public class SegundaOpiniaoCepTests
{
    private sealed class PersistenciaEmMemoria : ICachePostalCep, IHistoricoConsultasCep
    {
        public Dictionary<string, RespostaGuardadaCep> Cache { get; } = new();
        public List<ConsultaCepOcorrida> Historico { get; } = [];

        public Task<RespostaGuardadaCep?> ObterAsync(string chave, CancellationToken ct = default) =>
            Task.FromResult(Cache.TryGetValue(chave, out var c) ? c : null);

        public Task GuardarAsync(string chave, TipoConsultaCep tipo, string? cep, RespostaGuardadaCep resposta, CancellationToken ct = default)
        {
            Cache[chave] = resposta;
            return Task.CompletedTask;
        }

        public Task RegistrarAsync(ConsultaCepOcorrida consulta)
        {
            lock (Historico) Historico.Add(consulta);
            return Task.CompletedTask;
        }
    }

    private sealed class FonteDeTeste(CepFonte fonte, bool busca = false) : IProvedorCep
    {
        public CepFonte Fonte { get; } = fonte;
        public bool BuscaPorEndereco { get; } = busca;
        public Func<string, CancellationToken, Task<ResultadoProvedorCep>> Consulta { get; set; } =
            (_, _) => throw new InvalidOperationException("consulta não esperada");
        public int Consultas;

        public Task<ResultadoProvedorCep> ConsultarPorCepAsync(string cep, CancellationToken ct = default)
        {
            Interlocked.Increment(ref Consultas);
            return Consulta(cep, ct);
        }

        public Task<ResultadoBuscaProvedorCep> BuscarPorEnderecoAsync(BuscaEnderecoCep busca, CancellationToken ct = default) =>
            throw new InvalidOperationException("a segunda opinião nunca busca por endereço");
    }

    private static readonly DateTime Inicio = new(2026, 10, 6, 9, 0, 0, DateTimeKind.Utc);
    private readonly FakeTimeProvider _relogio = new(new DateTimeOffset(Inicio));
    private readonly FonteDeTeste _viaCep = new(CepFonte.ViaCep, busca: true);
    private readonly FonteDeTeste _brasilApi = new(CepFonte.BrasilApi);
    private readonly PersistenciaEmMemoria _banco = new();
    private readonly ConferenciasCepEmitidas _conferencias;
    private readonly SugestoesCepEmitidas _sugestoes;
    private readonly CacheConferenciaCep _cache;

    public SegundaOpiniaoCepTests()
    {
        _conferencias = new ConferenciasCepEmitidas(_relogio);
        _sugestoes = new SugestoesCepEmitidas(_relogio);
        _cache = new CacheConferenciaCep(_relogio);
    }

    private ServicoConferenciaCep Servico(params IProvedorCep[] fontes) =>
        new(fontes.Length == 0 ? [_viaCep, _brasilApi] : fontes, _cache, _sugestoes, _conferencias, _relogio, _banco, _banco);

    private static readonly RegistroCep DoViaCep = new("35790000", "Rua Barão", "até 999/1000", "Centro", "Curvelo", "MG", "3120904");
    private static readonly RegistroCep DaBrasilApi = new("35790000", "Rua Barão", null, "Centro", "Curvelo", "MG");

    private static Task<ResultadoProvedorCep> Ja(ResultadoProvedorCep r) => Task.FromResult(r);

    private void AsDuasRespondem(RegistroCep? brasil = null)
    {
        _viaCep.Consulta = (_, _) => Ja(ResultadoProvedorCep.Encontrado(CepFonte.ViaCep, DoViaCep));
        _brasilApi.Consulta = (_, _) => Ja(ResultadoProvedorCep.Encontrado(CepFonte.BrasilApi, brasil ?? DaBrasilApi));
    }

    [Fact]
    public async Task Concordam_e_registra_as_duas_consultas_como_segunda_opiniao()
    {
        AsDuasRespondem();

        var c = await Servico().ConsultarOutraFonteAsync("35790-000");

        Assert.Equal(ResultadoSegundaOpiniaoCep.Concordam, c.Resultado);
        Assert.Equal((1, 1), (_viaCep.Consultas, _brasilApi.Consultas));
        Assert.All(_banco.Historico, h => Assert.Equal(OperacaoConsultaCep.SegundaOpiniao, h.Operacao));
        Assert.Equal([(CepFonte?)CepFonte.ViaCep, CepFonte.BrasilApi], _banco.Historico.Select(h => h.Fonte));
        Assert.Equal([OrigemRespostaCep.Fonte, OrigemRespostaCep.SegundaFonte], _banco.Historico.Select(h => h.Origem));
        Assert.All(_banco.Historico, h => Assert.Equal("35790000", h.Cep)); // só o CEP: nenhum dado de pessoa ou endereço
    }

    [Fact]
    public async Task Divergem_sem_escolher_fonte_e_sem_registrar_conferencia_ou_sugestao()
    {
        AsDuasRespondem(DaBrasilApi with { Cidade = "Corinto" });
        var endereco = new EnderecoConferenciaCep("35790000", "R. Barao", "150", "Centro", "Curvelo", "MG", "3120904");

        var c = await Servico().ConsultarOutraFonteAsync("35790000");

        Assert.Equal(ResultadoSegundaOpiniaoCep.Divergem, c.Resultado);
        Assert.Contains("O Lone não escolhe qual está certa", c.Mensagem);
        Assert.Null(_conferencias.Obter(EstadoConferenciaCep.Assinatura(endereco))); // nada vale como conferência para o Salvar
        Assert.False(_sugestoes.FoiEmitida("35790000", "35790000", CepFonte.BrasilApi));
    }

    [Fact]
    public async Task A_resposta_da_segunda_fonte_nao_vai_para_o_cache()
    {
        AsDuasRespondem(DaBrasilApi with { Logradouro = "Rua Outra" });

        await Servico().ConsultarOutraFonteAsync("35790000");

        Assert.Equal(CepFonte.ViaCep, _banco.Cache[CacheConferenciaCep.ChaveCep("35790000")].Fonte);
        Assert.Equal(CepFonte.ViaCep, _cache.ObterConsulta("35790000")!.Fonte);
    }

    [Fact]
    public async Task Principal_do_cache_e_a_outra_fonte_e_consultada()
    {
        AsDuasRespondem();
        await Servico().ConferirAsync(new EnderecoConferenciaCep("35790000", "R. Barao", "150", "Centro", "Curvelo", "MG", "3120904"));
        _banco.Historico.Clear();

        var c = await Servico().ConsultarOutraFonteAsync("35790000");

        Assert.Equal(ResultadoSegundaOpiniaoCep.Concordam, c.Resultado);
        Assert.Equal((1, 1), (_viaCep.Consultas, _brasilApi.Consultas)); // a principal veio do cache
        Assert.Equal(OrigemRespostaCep.CacheMemoria, _banco.Historico[0].Origem);
    }

    [Fact]
    public async Task Segunda_fonte_indisponivel_nao_e_conflito()
    {
        _viaCep.Consulta = (_, _) => Ja(ResultadoProvedorCep.Encontrado(CepFonte.ViaCep, DoViaCep));
        _brasilApi.Consulta = (_, _) => Ja(ResultadoProvedorCep.Indisponivel(CepFonte.BrasilApi, "503"));

        var c = await Servico().ConsultarOutraFonteAsync("35790000");

        Assert.Equal(ResultadoSegundaOpiniaoCep.SegundaFonteIndisponivel, c.Resultado);
        Assert.Contains("Isso não é conflito", c.Mensagem);
        Assert.Equal(ResultadoConsultaCep.Indisponivel, _banco.Historico.Last().Resultado);
        Assert.Equal(OrigemRespostaCep.SegundaFonte, _banco.Historico.Last().Origem);
    }

    [Fact]
    public async Task Principal_indisponivel_usa_o_fallback_atual_e_nao_chama_de_novo_a_fonte_que_falhou()
    {
        _viaCep.Consulta = (_, _) => Ja(ResultadoProvedorCep.Indisponivel(CepFonte.ViaCep, "timeout"));
        _brasilApi.Consulta = (_, _) => Ja(ResultadoProvedorCep.Encontrado(CepFonte.BrasilApi, DaBrasilApi));

        var c = await Servico().ConsultarOutraFonteAsync("35790000");

        Assert.Equal((CepFonte?)CepFonte.BrasilApi, c.FontePrincipal); // a reserva respondeu pela conferência
        Assert.Equal(ResultadoSegundaOpiniaoCep.SegundaFonteIndisponivel, c.Resultado);
        Assert.Equal(1, _viaCep.Consultas);                              // não insiste na fonte que acabou de falhar
    }

    [Fact]
    public async Task Todas_indisponiveis_nao_compara_e_informacao_anterior_nao_conta()
    {
        AsDuasRespondem();
        await Servico().ConsultarOutraFonteAsync("35790000"); // guarda a resposta no cache postal (utilizável offline)
        _relogio.Advance(TimeSpan.FromDays(2));
        var limpo = new CacheConferenciaCep(_relogio);
        _viaCep.Consulta = (_, _) => Ja(ResultadoProvedorCep.Indisponivel(CepFonte.ViaCep, "503"));
        _brasilApi.Consulta = (_, _) => Ja(ResultadoProvedorCep.Indisponivel(CepFonte.BrasilApi, "503"));

        var c = await new ServicoConferenciaCep([_viaCep, _brasilApi], limpo, _sugestoes, _conferencias, _relogio, _banco, _banco)
            .ConsultarOutraFonteAsync("35790000");

        Assert.Equal(ResultadoSegundaOpiniaoCep.FontePrincipalIndisponivel, c.Resultado);
        Assert.Empty(c.Componentes);
    }

    [Fact]
    public async Task So_uma_fonte_configurada()
    {
        _viaCep.Consulta = (_, _) => Ja(ResultadoProvedorCep.Encontrado(CepFonte.ViaCep, DoViaCep));

        var c = await Servico(_viaCep).ConsultarOutraFonteAsync("35790000");

        Assert.Equal(ResultadoSegundaOpiniaoCep.SemOutraFonte, c.Resultado);
    }

    [Fact]
    public async Task Cancelamento_do_usuario_sobe_e_fica_no_historico()
    {
        using var cts = new CancellationTokenSource();
        _viaCep.Consulta = (_, _) => Ja(ResultadoProvedorCep.Encontrado(CepFonte.ViaCep, DoViaCep));
        _brasilApi.Consulta = async (_, ct) =>
        {
            cts.Cancel();
            await Task.Delay(Timeout.Infinite, ct);
            return ResultadoProvedorCep.Encontrado(CepFonte.BrasilApi, DaBrasilApi);
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Servico().ConsultarOutraFonteAsync("35790000", cts.Token));

        Assert.Equal((ResultadoConsultaCep.Cancelado, OrigemRespostaCep.SegundaFonte), (_banco.Historico.Last().Resultado, _banco.Historico.Last().Origem));
    }

    [Fact]
    public async Task Tempo_esgotado_na_segunda_fonte_e_indisponivel_nao_conflito()
    {
        var entrou = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _viaCep.Consulta = (_, _) => Ja(ResultadoProvedorCep.Encontrado(CepFonte.ViaCep, DoViaCep));
        _brasilApi.Consulta = async (_, ct) =>
        {
            entrou.SetResult();
            await Task.Delay(Timeout.Infinite, ct);
            return ResultadoProvedorCep.Encontrado(CepFonte.BrasilApi, DaBrasilApi);
        };

        var tarefa = Servico().ConsultarOutraFonteAsync("35790000");
        await entrou.Task;
        _relogio.Advance(ServicoConferenciaCep.TetoTotal + TimeSpan.FromSeconds(1));
        var c = await tarefa;

        Assert.Equal(ResultadoSegundaOpiniaoCep.SegundaFonteIndisponivel, c.Resultado);
    }

    [Fact]
    public async Task Cep_invalido_e_recusado_sem_consultar()
    {
        await Assert.ThrowsAsync<ValidacaoException>(() => Servico().ConsultarOutraFonteAsync("3579"));
        Assert.Equal((0, 0), (_viaCep.Consultas, _brasilApi.Consultas));
    }

    // ---- API (app service): permissão e contrato ----

    private sealed class Autorizacao(params string[] permissoes) : IAutorizacao
    {
        public bool Possui(string permissao) => permissoes.Contains(permissao);
        public void Exigir(string permissao)
        {
            if (!Possui(permissao)) throw new AcessoNegadoException(permissao);
        }
    }

    private sealed class SemConsultaAntiga : ICepConsulta, ICnpjConsulta, IInscricaoEstadualConsulta
    {
        Task<DadosCep?> ICepConsulta.ConsultarAsync(string cep, CancellationToken ct) => throw new InvalidOperationException();
        Task<DadosCnpj?> ICnpjConsulta.ConsultarAsync(string cnpj, CancellationToken ct) => throw new InvalidOperationException();
        Task<List<InscricaoEstadualEncontrada>> IInscricaoEstadualConsulta.ConsultarAsync(string cnpj, CancellationToken ct) => throw new InvalidOperationException();
    }

    private ConsultasAppService App(params string[] permissoes)
    {
        var antiga = new SemConsultaAntiga();
        return new ConsultasAppService(antiga, antiga, antiga, new Autorizacao(permissoes), Servico());
    }

    [Fact]
    public async Task Api_exige_ver_pessoas_e_devolve_os_componentes()
    {
        AsDuasRespondem(DaBrasilApi with { Uf = "BA" });

        await Assert.ThrowsAsync<AcessoNegadoException>(() => App().ConsultarOutraFonteAsync(new SegundaOpiniaoCepRequisicao { Cep = "35790000" }));
        var dto = await App(Permissoes.Pessoas.Visualizar).ConsultarOutraFonteAsync(new SegundaOpiniaoCepRequisicao { Cep = "35790-000" });

        Assert.Equal((ResultadoSegundaOpiniaoCep.Divergem, "35790000"), (dto.Resultado, dto.Cep));
        var uf = dto.Componentes.Single(c => c.Componente == ComponenteComparacaoFontes.Uf);
        Assert.Equal((SituacaoComparacaoFontes.Divergem, "MG", "BA"), (uf.Situacao, uf.ValorPrincipal, uf.ValorSegunda));
    }
}
