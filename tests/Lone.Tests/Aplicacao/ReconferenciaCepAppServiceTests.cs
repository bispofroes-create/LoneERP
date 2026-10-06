using Lone.Application.Integracoes.ConferenciaCep;
using Lone.Application.Seguranca;
using Lone.Contracts.Integracoes;
using Lone.Contracts.Seguranca;
using Lone.Domain.Enderecos.ConferenciaCep;
using Lone.Domain.Enums;
using Microsoft.Extensions.Time.Testing;

namespace Lone.Tests.Aplicacao;

/// <summary>
/// F6, segurança da reconferência: exige editar pessoas + tabelas oficiais + alcance a toda a base (a seleção percorre todos
/// os cadastros e devolve nomes). Sem isso, nada é lido, consultado ou gravado.
/// </summary>
public class ReconferenciaCepAppServiceTests
{
    private sealed class Autorizacao(params string[] permissoes) : IAutorizacao
    {
        public bool Possui(string permissao) => permissoes.Contains(permissao);
        public void Exigir(string permissao)
        {
            if (!Possui(permissao)) throw new AcessoNegadoException(permissao);
        }
    }

    private sealed class AlcanceFixo(AlcanceComercial alcance) : IAlcanceDoUsuario
    {
        public AlcanceComercial Alcance { get; } = alcance;
        public Guid? PessoaId => null;
    }

    private sealed class Repositorio : IReconferenciaCepRepositorio, IManutencaoConsultasCep
    {
        public int Acessos;
        public CriterioReconferenciaCep? Criterio;

        public Task<SelecaoReconferenciaCep> SelecionarAsync(CriterioReconferenciaCep c, DateTime agora, CancellationToken ct = default)
        {
            Acessos++;
            Criterio = c;
            return Task.FromResult(new SelecaoReconferenciaCep(3, [Guid.NewGuid()], 2));
        }

        public Task<IReadOnlyList<EnderecoReconferivel>> ObterAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
        {
            Acessos++;
            return Task.FromResult<IReadOnlyList<EnderecoReconferivel>>([]);
        }

        public Task<bool> GravarAsync(EnderecoReconferivel lido, ConferenciaCepRealizada c, CancellationToken ct = default)
        {
            Acessos++;
            return Task.FromResult(true);
        }

        public Task<int> LimparAsync(DateTime limite, int tamanhoLote, CancellationToken ct = default)
        {
            Acessos++;
            return Task.FromResult(4);
        }
    }

    private sealed class SemConferencia : IServicoConferenciaCep
    {
        public Task<ResultadoConferenciaCep> ConferirAsync(EnderecoConferenciaCep e, CancellationToken ct = default) => throw new InvalidOperationException();
        public Task<ResultadoConferenciaCep> BuscarPorEnderecoAsync(EnderecoConferenciaCep e, CancellationToken ct = default) => throw new InvalidOperationException();
        public Task<ResultadoConferenciaCep> ReconferirAsync(EnderecoConferenciaCep e, CancellationToken ct = default) => throw new InvalidOperationException();
        public Task<ComparacaoFontesCep> ConsultarOutraFonteAsync(string cep, CancellationToken ct = default) => throw new InvalidOperationException();
    }

    private readonly Repositorio _repo = new();

    private ReconferenciaCepAppService Servico(AlcanceComercial alcance, params string[] permissoes) =>
        new(new ServicoReconferenciaCep(_repo, new SemConferencia(), _repo, new FakeTimeProvider()), new Autorizacao(permissoes), new AlcanceFixo(alcance));

    private static readonly string[] Todas = [Permissoes.Pessoas.Editar, Permissoes.Cadastros.TabelasOficiais];

    public static TheoryData<string[]> PermissoesFaltando => new()
    {
        new[] { Permissoes.Pessoas.Editar },
        new[] { Permissoes.Cadastros.TabelasOficiais },
        Array.Empty<string>()
    };

    [Theory]
    [MemberData(nameof(PermissoesFaltando))]
    public async Task Sem_as_duas_permissoes_nada_e_lido_nem_gravado(string[] permissoes)
    {
        var s = Servico(AlcanceComercial.Tudo, permissoes);

        await Assert.ThrowsAsync<AcessoNegadoException>(() => s.SelecionarAsync(new FiltroReconferenciaCepDto()));
        await Assert.ThrowsAsync<AcessoNegadoException>(() => s.ProcessarAsync(new ProcessarReconferenciaCepRequisicao { Enderecos = [Guid.NewGuid()] }));
        await Assert.ThrowsAsync<AcessoNegadoException>(() => s.LimparHistoricoAsync());
        Assert.Equal(0, _repo.Acessos);
    }

    [Theory]
    [InlineData(AlcanceComercial.MinhaEquipe)]
    [InlineData(AlcanceComercial.MinhaCarteira)]
    public async Task Alcance_restrito_e_recusado_mesmo_com_as_permissoes(AlcanceComercial alcance)
    {
        var s = Servico(alcance, Todas);

        var erro = await Assert.ThrowsAsync<AcessoNegadoException>(() => s.SelecionarAsync(new FiltroReconferenciaCepDto()));
        await Assert.ThrowsAsync<AcessoNegadoException>(() => s.ProcessarAsync(new ProcessarReconferenciaCepRequisicao { Enderecos = [Guid.NewGuid()] }));

        Assert.Contains("toda a base", erro.Message);
        Assert.Equal(0, _repo.Acessos);
    }

    [Fact]
    public async Task Com_permissao_e_alcance_total_seleciona_com_a_politica()
    {
        var s = Servico(AlcanceComercial.Tudo, Todas);

        var r = await s.SelecionarAsync(new FiltroReconferenciaCepDto { Uf = " mg ", Limite = 50_000 });

        Assert.Equal((3, 2, true), (r.Total, r.SemCepValido, r.Truncada));
        Assert.Equal((PoliticaReconferenciaCep.ConfirmarAcimaDe, PoliticaReconferenciaCep.ItensPorChamada, PoliticaReconferenciaCep.MaximoAdiamentos),
            (r.ConfirmarAcimaDe, r.ItensPorChamada, r.MaximoAdiamentos));
        Assert.Equal("MG", _repo.Criterio!.Uf);
        Assert.Equal(PoliticaReconferenciaCep.LimiteSelecao, _repo.Criterio.Limite); // limite pedido acima do teto é cortado
    }

    [Fact]
    public async Task Limpeza_informa_removidos_e_retencao()
    {
        var r = await Servico(AlcanceComercial.Tudo, Todas).LimparHistoricoAsync();

        Assert.Equal((4, 90), (r.Removidos, r.RetencaoDias));
    }

    // ---- G, ajuste 2.2: evento de auditoria por execução ----

    private sealed class Execucoes : IRegistroExecucoesReconferenciaCep
    {
        public List<ExecucaoReconferenciaCep> Registradas { get; } = [];
        public Task<bool> RegistrarAsync(ExecucaoReconferenciaCep e, CancellationToken ct = default)
        {
            Registradas.Add(e);
            return Task.FromResult(true);
        }
    }

    private sealed class Usuario : IUsuarioAtual
    {
        public Guid? Id => Guid.NewGuid();
        public string Nome => "maria";
    }

    private static readonly DateTime Agora = new(2026, 10, 6, 12, 0, 0, DateTimeKind.Utc);

    private ReconferenciaCepAppService ComEvento(Execucoes execucoes, AlcanceComercial alcance, params string[] permissoes) =>
        new(new ServicoReconferenciaCep(_repo, new SemConferencia(), _repo, new FakeTimeProvider()), new Autorizacao(permissoes),
            new AlcanceFixo(alcance), execucoes, new Usuario(), new FakeTimeProvider(new DateTimeOffset(Agora)));

    private static ConcluirReconferenciaCepRequisicao Conclusao(Guid id) => new()
    {
        ExecucaoId = id, Cancelada = true, Total = 240, Conferidos = 200, Divergentes = 20, NaoEncontrados = 10, Indisponiveis = 5,
        Alterados = 2, NaoProcessados = 3, DuracaoSegundos = 252,
        Filtro = new FiltroReconferenciaCepDto { NaoConferidos = true, Divergentes = true, NaoEncontrados = false, ConferidosAntigos = false, Uf = "mg" }
    };

    [Fact]
    public async Task Conclusao_grava_um_evento_com_ator_filtro_quantidades_e_duracao()
    {
        var execucoes = new Execucoes();
        var id = Guid.NewGuid();

        await ComEvento(execucoes, AlcanceComercial.Tudo, Todas).ConcluirAsync(Conclusao(id));

        var e = Assert.Single(execucoes.Registradas);
        Assert.Equal((id, "maria", Agora), (e.ExecucaoId, e.Usuario, e.Em));
        Assert.Equal("Reconferência de CEPs em lote cancelada pelo usuário: 240 endereço(s); 200 conferido(s), 20 com divergência, " +
                     "10 não encontrado(s), 5 não consultado(s) agora, 2 alterado(s) durante, 3 não processado(s). " +
                     "Filtro: não conferidos, divergentes; UF MG. Duração: 4 min 12 s. Quantidades somadas pela tela a partir dos blocos.",
            e.Descricao);
        Assert.True(e.Descricao.Length <= 500);
    }

    [Fact]
    public async Task Conclusao_exige_as_mesmas_permissoes_e_identificacao()
    {
        var execucoes = new Execucoes();

        await Assert.ThrowsAsync<AcessoNegadoException>(() => ComEvento(execucoes, AlcanceComercial.MinhaEquipe, Todas).ConcluirAsync(Conclusao(Guid.NewGuid())));
        await Assert.ThrowsAsync<AcessoNegadoException>(() => ComEvento(execucoes, AlcanceComercial.Tudo).ConcluirAsync(Conclusao(Guid.NewGuid())));
        await Assert.ThrowsAsync<Lone.Domain.Validacao.ValidacaoException>(() =>
            ComEvento(execucoes, AlcanceComercial.Tudo, Todas).ConcluirAsync(Conclusao(Guid.Empty)));
        Assert.Empty(execucoes.Registradas);
    }

    [Fact]
    public void Texto_do_evento_limita_quantidades_e_tamanho()
    {
        var t = EventoReconferenciaCep.Descricao(false, -5, int.MaxValue, 0, 0, 0, 0, 0, -1, false, false, false, true, null, 3120904);

        Assert.Contains("concluída: 0 endereço(s); 1000000 conferido(s)", t);
        Assert.Contains("Filtro: conferidos há mais de 180 dias; município 3120904.", t);
        Assert.Contains("Duração: 0 min 00 s.", t);
        Assert.True(t.Length <= 500);
    }
}
