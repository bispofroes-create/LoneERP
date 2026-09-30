using Lone.Application.Comercial;
using Lone.Application.Enderecos;
using Lone.Application.Metas;
using Lone.Application.Papeis;
using Lone.Application.Seguranca;
using Lone.Application.Territorios;
using Lone.Contracts.Comercial;
using Lone.Contracts.Seguranca;
using Lone.Contracts.Territorios;
using Lone.Domain.Comercial;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Territorios;
using Lone.Domain.Validacao;
using Lone.Tests.Apoio;
using Microsoft.Extensions.Time.Testing;

namespace Lone.Tests.Aplicacao;

/// <summary>
/// Serviço dos territórios (Fase 2b-1a): posição criada e corrigida sem uso, serialização pela trava da árvore (D1 = B) só
/// nas mudanças de estrutura — com a versão que a tela mostrava, sem tocar na versão do cadastro do mapa (e a disputa A→B ×
/// B→A que não pode formar ciclo) —, uso operacional que bloqueia mover, encerrar com motivo, responsáveis gravados que
/// nunca somem e permissões (quem configura também enxerga).
/// </summary>
public class TerritorioAppServiceTests
{
    private static readonly DateOnly Hoje = new(2026, 9, 29);
    private static readonly Guid Geografico = TiposTerritorioIniciais.Todos[0].Id;
    private static readonly Guid Vendedor = Guid.NewGuid();
    private static readonly Guid ClassificacaoVendedor = Guid.NewGuid();
    private static readonly Guid Joao = Guid.NewGuid();

    private readonly MapasEmMemoria _mapas = new();
    private readonly TerritoriosEmMemoria _territorios;
    private readonly UsoFixo _uso = new();
    private readonly AutorizacaoFixa _autorizacao = new();
    private readonly MotivoEmMemoria _motivo = new();
    private readonly TerritorioAppService _servico;
    private readonly MapaTerritorial _mapa;

    public TerritorioAppServiceTests()
    {
        _mapa = new MapaTerritorial { Id = Guid.NewGuid(), Codigo = "GEOGRAFIA", Nome = "Geografia", FinalidadeEnderecoReferenciaId = Guid.NewGuid(), Versao = [1] };
        _mapas.Mapas.Add(_mapa);
        _territorios = new TerritoriosEmMemoria(_mapas);
        var relogio = new FakeTimeProvider(new DateTimeOffset(Hoje.ToDateTime(new TimeOnly(10, 0)), TimeSpan.Zero));
        relogio.SetLocalTimeZone(TimeZoneInfo.Utc);
        var consultas = new ConsultasFixas();
        consultas.Pessoas[Joao] = new PessoaElegivel("João", new HashSet<Guid> { ClassificacaoVendedor });
        var funcao = new TipoCarteira
        {
            Id = Vendedor, Nome = "Vendedor",
            Classificacoes = [new TipoCarteiraClassificacao { Id = Guid.NewGuid(), TipoCarteiraId = Vendedor, PapelId = ClassificacaoVendedor }]
        };
        _servico = new TerritorioAppService(_territorios, _mapas, new TiposFixos(), _uso,
            new ReferenciasComercial(new PerfisVazios(), new CondicoesVazias(), new FuncoesFixas(funcao), consultas, new ClassificacoesVazias()),
            consultas, new EquipesVazias(), new EmpresasFixas(), new ClassificacoesVazias(), new FinalidadesVazias(), _autorizacao, _motivo, relogio);
    }

    private TerritorioDto Novo(string nome, Guid mapaId, Guid? pai = null) => new()
    {
        Id = IdSequencial.Novo(), MapaId = mapaId, Codigo = nome, Nome = nome, TipoId = Geografico, PaiId = pai,
        VersaoArvore = _territorios.VersaoArvore(mapaId) // como a tela: a versão da árvore que ela mostra
    };

    private async Task<TerritorioDto> Criar(string nome, Guid? pai = null) => await _servico.SalvarAsync(Novo(nome, _mapa.Id, pai));

    /// <summary>A ficha relida, com a versão da árvore de agora (como a tela depois de recarregar).</summary>
    private async Task<TerritorioDto> Abrir(Guid id)
    {
        var dto = (await _servico.ObterAsync(id))!;
        dto.VersaoArvore = (await _servico.ListarDoMapaAsync(dto.MapaId)).VersaoArvore;
        return dto;
    }

    private AlterarSituacaoTerritorioRequisicao Situacao(TerritorioDto t, string? motivo = null) =>
        new() { Versao = t.Versao, VersaoArvore = _territorios.VersaoArvore(t.MapaId), Motivo = motivo };

    [Fact]
    public async Task Criar_grava_a_posicao_desde_hoje_e_serializa_pela_arvore_sem_tocar_no_cadastro_do_mapa()
    {
        var versaoDoMapa = _mapa.Versao;
        var vista = _territorios.VersaoArvore(_mapa.Id);
        var mg = await Criar("MG");

        Assert.Equal(Hoje, mg.InicioEm);
        var posicao = Assert.Single(mg.Posicoes);
        Assert.Null(posicao.PaiId);
        Assert.Equal(vista, _territorios.UltimaVersaoArvoreExigida); // mudança de estrutura: exige a versão que a tela mostrava
        Assert.NotEqual(vista, _territorios.VersaoArvore(_mapa.Id)); // e a versão da árvore mudou
        Assert.Equal(versaoDoMapa, _mapa.Versao); // a ficha do mapa aberta continua válida (D1 = B)
        Assert.Contains(_territorios.Eventos[mg.Id], e => e.Contains("criado"));
    }

    [Fact]
    public async Task Mudanca_de_estrutura_com_arvore_velha_e_recusada_antes_de_conferir_e_nada_grava()
    {
        var mg = await Criar("MG");
        var sp = await Criar("SP");
        var curvelo = await Abrir((await Criar("Curvelo", mg.Id)).Id);
        await Criar("RJ"); // outra janela mudou a árvore depois que a tela de Curvelo foi carregada
        var gravacoes = _territorios.Gravacoes;

        curvelo.PaiId = sp.Id;
        var conflito = await Assert.ThrowsAsync<ConflitoDeEdicaoException>(() => _servico.SalvarAsync(curvelo));

        Assert.Equal(RegrasArvoreTerritorial.MensagemArvoreAlterada, conflito.Message);
        Assert.Equal(gravacoes, _territorios.Gravacoes);
        var encerrar = await Assert.ThrowsAsync<ConflitoDeEdicaoException>(() =>
            _servico.EncerrarAsync(curvelo.Id, new AlterarSituacaoTerritorioRequisicao { Versao = curvelo.Versao, VersaoArvore = curvelo.VersaoArvore }));
        Assert.Equal(RegrasArvoreTerritorial.MensagemArvoreAlterada, encerrar.Message);
        Assert.Equal(gravacoes, _territorios.Gravacoes);
    }

    [Fact]
    public async Task Mudanca_de_estrutura_sem_a_versao_da_arvore_e_recusada()
    {
        var sem = Novo("MG", _mapa.Id);
        sem.VersaoArvore = null;
        var erro = await Assert.ThrowsAsync<ValidacaoException>(() => _servico.SalvarAsync(sem));
        Assert.Contains(RegrasArvoreTerritorial.MensagemSemVersaoArvore, erro.Erros);

        var mg = await Criar("MG");
        await Assert.ThrowsAsync<ValidacaoException>(() => _servico.EncerrarAsync(mg.Id, new AlterarSituacaoTerritorioRequisicao { Versao = mg.Versao }));
        Assert.Equal(1, _territorios.Gravacoes);
    }

    [Fact]
    public async Task Renomear_e_mexer_nos_responsaveis_nao_serializa_a_arvore()
    {
        var mg = await Criar("MG");
        mg.Nome = "Minas Gerais";
        mg.Responsaveis.Add(new TerritorioResponsavelDto { PessoaId = Joao, TipoCarteiraId = Vendedor, InicioEm = Hoje });

        var salvo = await _servico.SalvarAsync(mg);

        Assert.Equal("Minas Gerais", salvo.Nome);
        Assert.Equal("João", Assert.Single(salvo.Responsaveis).Nome);
        Assert.Null(_territorios.UltimaVersaoArvoreExigida);
    }

    [Fact]
    public async Task Mover_sem_uso_corrige_a_posicao_e_serializa()
    {
        var mg = await Criar("MG");
        var sp = await Criar("SP");
        var curvelo = await Abrir((await Criar("Curvelo", mg.Id)).Id);

        curvelo.PaiId = sp.Id;
        var movido = await _servico.SalvarAsync(curvelo);

        Assert.Equal(sp.Id, movido.PaiId);
        Assert.Equal(sp.Id, Assert.Single(movido.Posicoes).PaiId);
        Assert.Equal("SP", movido.Caminho);
        Assert.NotNull(_territorios.UltimaVersaoArvoreExigida);
    }

    [Fact]
    public async Task Com_uso_abaixo_mover_e_recusado_e_nada_e_gravado()
    {
        var mg = await Criar("MG");
        var sp = await Criar("SP");
        var norte = await Criar("Norte de MG", mg.Id);
        var curvelo = await Criar("Curvelo", norte.Id);
        _uso.ComUso.Add(curvelo.Id);
        var gravacoes = _territorios.Gravacoes;

        norte = await Abrir(norte.Id);
        norte.PaiId = sp.Id;
        var erro = await Assert.ThrowsAsync<ValidacaoException>(() => _servico.SalvarAsync(norte));

        Assert.Contains(erro.Erros, e => e.Contains("só por uma operação territorial"));
        Assert.Equal(gravacoes, _territorios.Gravacoes);
    }

    [Fact]
    public async Task A_abaixo_de_B_e_B_abaixo_de_A_ao_mesmo_tempo_nao_formam_ciclo()
    {
        var a = await Criar("A");
        var b = await Criar("B");

        // As duas janelas carregaram a mesma árvore. Enquanto "A abaixo de B" é conferido e ainda não gravou, a outra grava
        // "B abaixo de A".
        a = await Abrir(a.Id);
        var outra = await Abrir(b.Id);
        outra.PaiId = a.Id;
        _territorios.AntesDeGravar = async () =>
        {
            _territorios.AntesDeGravar = null;
            await _servico.SalvarAsync(outra);
        };
        a.PaiId = b.Id;
        var conflito = await Assert.ThrowsAsync<ConflitoDeEdicaoException>(() => _servico.SalvarAsync(a));

        Assert.Equal(RegrasArvoreTerritorial.MensagemArvoreAlterada, conflito.Message);
        var arvore = _territorios.Gravados.ToDictionary(t => t.Id);
        Assert.Null(arvore[a.Id].PaiId); // a primeira não gravou nada
        Assert.Equal(a.Id, arvore[b.Id].PaiId); // só a segunda valeu: árvore válida, sem ciclo
    }

    [Fact]
    public async Task Encerrar_serializa_registra_o_motivo_e_fecha_a_posicao()
    {
        var mg = await Criar("MG");
        var encerrado = await _servico.EncerrarAsync(mg.Id, Situacao(mg, "  Região incorporada  "));

        Assert.Equal(SituacaoTerritorio.Encerrado, encerrado.Situacao);
        Assert.Equal(Hoje, encerrado.FimEm);
        Assert.Equal(Hoje, Assert.Single(encerrado.Posicoes).FimEm);
        Assert.Equal("Região incorporada", _motivo.Motivo);
        Assert.NotNull(_territorios.UltimaVersaoArvoreExigida);
    }

    [Fact]
    public async Task Reativar_com_arvore_velha_e_recusado_e_com_a_atual_serializa_e_reabre()
    {
        var mg = await Criar("MG");
        var encerrado = await _servico.EncerrarAsync(mg.Id, Situacao(mg));
        var vistaAntes = _territorios.VersaoArvore(_mapa.Id); // a tela de A mostra esta árvore
        await Criar("SP");                                    // B muda a árvore
        var gravacoes = _territorios.Gravacoes;

        var conflito = await Assert.ThrowsAsync<ConflitoDeEdicaoException>(() => _servico.ReativarAsync(mg.Id,
            new AlterarSituacaoTerritorioRequisicao { Versao = encerrado.Versao, VersaoArvore = vistaAntes }));
        Assert.Equal(RegrasArvoreTerritorial.MensagemArvoreAlterada, conflito.Message);
        Assert.Equal(gravacoes, _territorios.Gravacoes); // nada gravado
        Assert.Equal(SituacaoTerritorio.Encerrado, (await _servico.ObterAsync(mg.Id))!.Situacao);

        var reativado = await _servico.ReativarAsync(mg.Id, Situacao(encerrado));
        Assert.Equal(SituacaoTerritorio.Ativo, reativado.Situacao);
        Assert.Null(Assert.Single(reativado.Posicoes).FimEm);
        Assert.NotNull(_territorios.UltimaVersaoArvoreExigida);
    }

    [Fact]
    public async Task Mudar_o_existe_desde_e_mudanca_de_estrutura_e_exige_a_arvore_vista()
    {
        var mg = await Abrir((await Criar("MG")).Id);
        await Criar("SP"); // a árvore mudou depois que a ficha de MG foi aberta
        mg.InicioEm = Hoje.AddDays(-10);

        var conflito = await Assert.ThrowsAsync<ConflitoDeEdicaoException>(() => _servico.SalvarAsync(mg));
        Assert.Equal(RegrasArvoreTerritorial.MensagemArvoreAlterada, conflito.Message);
        Assert.Equal(Hoje, (await _servico.ObterAsync(mg.Id))!.InicioEm);
    }

    [Fact]
    public async Task Responsavel_gravado_que_nao_veio_continua_como_estava()
    {
        var mg = await Criar("MG");
        mg.Responsaveis.Add(new TerritorioResponsavelDto { PessoaId = Joao, TipoCarteiraId = Vendedor, InicioEm = Hoje });
        mg = await _servico.SalvarAsync(mg);

        mg.Responsaveis.Clear(); // a tela mandou a lista vazia
        var salvo = await _servico.SalvarAsync(mg);

        Assert.Single(salvo.Responsaveis, r => r.Ativo);
    }

    [Fact]
    public async Task Quem_so_visualiza_le_mas_nao_grava_e_quem_configura_tambem_le()
    {
        var mg = await Criar("MG");

        _autorizacao.Negadas.Add(Permissoes.Territorios.Configurar);
        Assert.NotNull(await _servico.ObterAsync(mg.Id));
        await Assert.ThrowsAsync<AcessoNegadoException>(() => _servico.SalvarAsync(Novo("SP", _mapa.Id)));

        _autorizacao.Negadas.Clear();
        _autorizacao.Negadas.Add(Permissoes.Territorios.Visualizar);
        Assert.NotNull(await _servico.ObterAsync(mg.Id));
        Assert.Single((await _servico.ListarDoMapaAsync(_mapa.Id)).Territorios);

        _autorizacao.Negadas.Add(Permissoes.Territorios.Configurar);
        await Assert.ThrowsAsync<AcessoNegadoException>(() => _servico.ObterAsync(mg.Id));
    }

    // ------------------------------------------------------------------ Falsos

    /// <summary>Guarda cópias e confere a versão da trava da árvore como o banco faria (UPDATE ... WHERE Versao = @vista).</summary>
    private sealed class TerritoriosEmMemoria : ITerritorioRepositorio
    {
        private readonly MapasEmMemoria _mapas;
        private int _proximaVersao = 10;

        public TerritoriosEmMemoria(MapasEmMemoria mapas) => _mapas = mapas;

        public List<Territorio> Gravados { get; } = new();
        public int Gravacoes { get; private set; }
        public byte[]? UltimaVersaoArvoreExigida { get; private set; }
        private readonly Dictionary<Guid, byte[]> _arvores = new();

        /// <summary>A versão atual da trava da árvore do mapa (toda árvore começa na versão 1).</summary>
        public byte[] VersaoArvore(Guid mapaId) => _arvores.TryGetValue(mapaId, out var v) ? v : _arvores[mapaId] = [1];

        public Task<byte[]?> ObterVersaoArvoreAsync(Guid mapaId, CancellationToken ct) =>
            Task.FromResult<byte[]?>(_mapas.Mapas.Any(m => m.Id == mapaId) ? VersaoArvore(mapaId) : null);
        public Func<Task>? AntesDeGravar { get; set; }

        /// <summary>Os eventos de negócio entregues a cada gravação, por território.</summary>
        public Dictionary<Guid, List<string>> Eventos { get; } = new();

        public Task<List<Territorio>> ListarDoMapaAsync(Guid mapaId, CancellationToken ct) =>
            Task.FromResult(Gravados.Where(t => t.MapaId == mapaId).Select(Copia).ToList());

        public Task<Territorio?> ObterAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Gravados.FirstOrDefault(t => t.Id == id) is { } t ? Copia(t) : null);

        /// <summary>O uso que o serviço conferiu na última gravação (a real o relê dentro da transação).</summary>
        public IReadOnlySet<Guid>? UltimoUsoConferido { get; private set; }

        public async Task SalvarAsync(Territorio territorio, bool novo, byte[]? versaoArvoreVista, IReadOnlySet<Guid>? usoConferido, CancellationToken ct)
        {
            if (AntesDeGravar is { } antes) await antes();
            UltimaVersaoArvoreExigida = versaoArvoreVista;
            UltimoUsoConferido = usoConferido;
            if (versaoArvoreVista is not null)
            {
                if (!VersaoArvore(territorio.MapaId).SequenceEqual(versaoArvoreVista))
                    throw new ConflitoDeEdicaoException(RegrasArvoreTerritorial.MensagemArvoreAlterada);
                _arvores[territorio.MapaId] = [(byte)_proximaVersao++];
            }
            var gravado = Copia(territorio);
            gravado.Versao = [(byte)_proximaVersao++];
            if (!Eventos.TryGetValue(territorio.Id, out var eventos)) Eventos[territorio.Id] = eventos = new List<string>();
            eventos.AddRange(territorio.RetirarEventos());
            Gravados.RemoveAll(t => t.Id == territorio.Id);
            Gravados.Add(gravado);
            Gravacoes++;
        }

        private static Territorio Copia(Territorio t)
        {
            return new Territorio
            {
                Id = t.Id, Versao = t.Versao, MapaId = t.MapaId, Codigo = t.Codigo, Nome = t.Nome, TipoId = t.TipoId, PaiId = t.PaiId,
                Descricao = t.Descricao, Situacao = t.Situacao, FimEm = t.FimEm,
                Posicoes = [.. t.Posicoes.Select(p => new TerritorioPosicao
                {
                    Id = p.Id, MapaId = p.MapaId, TerritorioId = p.TerritorioId, PaiId = p.PaiId, InicioEm = p.InicioEm, FimEm = p.FimEm, Ativo = p.Ativo
                })],
                Responsaveis = [.. t.Responsaveis.Select(r => new TerritorioResponsavel
                {
                    Id = r.Id, TerritorioId = r.TerritorioId, PessoaId = r.PessoaId, EquipeId = r.EquipeId, TipoCarteiraId = r.TipoCarteiraId,
                    InicioEm = r.InicioEm, FimEm = r.FimEm, Observacao = r.Observacao, Ativo = r.Ativo
                })]
            };
        }
    }

    private sealed class MapasEmMemoria : IMapaTerritorialRepositorio
    {
        public List<MapaTerritorial> Mapas { get; } = new();
        public Task<List<MapaTerritorial>> ListarAsync(CancellationToken ct) => Task.FromResult(Mapas.ToList());
        public Task<MapaTerritorial?> ObterAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Mapas.FirstOrDefault(m => m.Id == id) is { } m
                ? new MapaTerritorial { Id = m.Id, Codigo = m.Codigo, Nome = m.Nome, Ativo = m.Ativo, Versao = m.Versao, FinalidadeEnderecoReferenciaId = m.FinalidadeEnderecoReferenciaId }
                : null);
        public Task SalvarAsync(MapaTerritorial mapa, bool novo, CancellationToken ct) => throw new NotImplementedException();
        public Task<Dictionary<Guid, int>> ContarTerritoriosAtivosAsync(CancellationToken ct) => Task.FromResult(new Dictionary<Guid, int>());
    }

    private sealed class UsoFixo : IUsoTerritorial
    {
        public HashSet<Guid> ComUso { get; } = new();
        public Task<IReadOnlySet<Guid>> TerritoriosComUsoAsync(Guid mapaId, CancellationToken ct) => Task.FromResult<IReadOnlySet<Guid>>(ComUso.ToHashSet());
        public Task<IReadOnlySet<Guid>> MapasEmUsoAsync(CancellationToken ct) => Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());
    }

    private sealed class TiposFixos : ITipoTerritorioRepositorio
    {
        public Task<List<TipoTerritorio>> ListarAsync(CancellationToken ct) =>
            Task.FromResult(TiposTerritorioIniciais.Todos.Select(t => new TipoTerritorio { Id = t.Id, Codigo = t.Codigo, Nome = t.Nome }).ToList());
        public Task<TipoTerritorio?> ObterAsync(Guid id, CancellationToken ct) => throw new NotImplementedException();
        public Task SalvarAsync(TipoTerritorio tipo, bool novo, CancellationToken ct) => throw new NotImplementedException();
        public Task<Dictionary<Guid, int>> ContarUsosAsync(CancellationToken ct) => Task.FromResult(new Dictionary<Guid, int>());
    }

    private sealed class ConsultasFixas : IComercialConsultas
    {
        public Dictionary<Guid, PessoaElegivel> Pessoas { get; } = new();

        public Task<List<AtendenteOpcaoDto>> ListarAtendentesAsync(IReadOnlyCollection<Guid> classificacoes, CancellationToken ct) =>
            Task.FromResult(Pessoas.Select(p => new AtendenteOpcaoDto(p.Key, p.Value.Nome, [.. p.Value.Classificacoes])).ToList());

        public Task<Dictionary<Guid, PessoaElegivel>> PessoasElegiveisAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
            Task.FromResult(Pessoas.Where(p => ids.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value));

        public Task<Dictionary<Guid, string>> NomesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
            Task.FromResult(Pessoas.Where(p => ids.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value.Nome));
    }

    private sealed class FuncoesFixas : ITipoCarteiraRepositorio
    {
        private readonly TipoCarteira _tipo;
        public FuncoesFixas(TipoCarteira tipo) => _tipo = tipo;
        public Task<List<TipoCarteira>> ListarAsync(CancellationToken ct) => Task.FromResult(new List<TipoCarteira> { _tipo });
        public Task<TipoCarteira?> ObterAsync(Guid id, CancellationToken ct) => throw new NotImplementedException();
        public Task<Dictionary<Guid, TipoCarteira>> ObterVariosAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => throw new NotImplementedException();
        public Task<Dictionary<Guid, int>> ContarUsosAsync(Guid? somenteId, CancellationToken ct) => throw new NotImplementedException();
        public Task SalvarAsync(TipoCarteira item, bool novo, CancellationToken ct) => throw new NotImplementedException();
        public Task<List<CarteiraCliente>> VinculosAtivosAsync(Guid tipoId, DateOnly desde, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class PerfisVazios : IPerfilComercialRepositorio
    {
        public Task<List<PerfilComercial>> ListarAsync(CancellationToken ct) => Task.FromResult(new List<PerfilComercial>());
        public Task<PerfilComercial?> ObterAsync(Guid id, CancellationToken ct) => Task.FromResult<PerfilComercial?>(null);
        public Task<Dictionary<Guid, PerfilComercial>> ObterVariosAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => Task.FromResult(new Dictionary<Guid, PerfilComercial>());
        public Task<Dictionary<Guid, int>> ContarUsosAsync(Guid? somenteId, CancellationToken ct) => throw new NotImplementedException();
        public Task SalvarAsync(PerfilComercial item, bool novo, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class CondicoesVazias : ICondicaoPagamentoRepositorio
    {
        public Task<List<CondicaoPagamento>> ListarAsync(CancellationToken ct) => Task.FromResult(new List<CondicaoPagamento>());
        public Task<CondicaoPagamento?> ObterAsync(Guid id, CancellationToken ct) => Task.FromResult<CondicaoPagamento?>(null);
        public Task<Dictionary<Guid, CondicaoPagamento>> ObterVariosAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => Task.FromResult(new Dictionary<Guid, CondicaoPagamento>());
        public Task<Dictionary<Guid, int>> ContarUsosAsync(Guid? somenteId, CancellationToken ct) => throw new NotImplementedException();
        public Task SalvarAsync(CondicaoPagamento item, bool novo, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class ClassificacoesVazias : IPapelRepositorio
    {
        public Task<List<Papel>> ListarAsync(bool incluirInativos, CancellationToken ct) => Task.FromResult(new List<Papel>());
        public Task<Papel?> ObterAsync(Guid id, CancellationToken ct) => throw new NotImplementedException();
        public Task<Dictionary<Guid, Papel>> ObterVariosAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => Task.FromResult(new Dictionary<Guid, Papel>());
        public Task<(bool Nome, bool Codigo)> EmUsoAsync(string nome, string codigo, Guid ignorarId, CancellationToken ct) => throw new NotImplementedException();
        public Task<Dictionary<Guid, int>> ContarPessoasAsync(Guid? somentePapelId, CancellationToken ct) => throw new NotImplementedException();
        public Task<int> ProximaOrdemAsync(CancellationToken ct) => throw new NotImplementedException();
        public Task SalvarAsync(Papel papel, bool novo, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class EquipesVazias : IEquipeRepositorio
    {
        public Task<List<Equipe>> ListarAsync(CancellationToken ct) => Task.FromResult(new List<Equipe>());
        public Task<Equipe?> ObterAsync(Guid id, CancellationToken ct) => Task.FromResult<Equipe?>(null);
        public Task SalvarAsync(Equipe equipe, bool novo, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class FinalidadesVazias : IFinalidadeEnderecoRepositorio
    {
        public Task<List<FinalidadeEnderecoCadastro>> ListarAsync(CancellationToken ct) => Task.FromResult(new List<FinalidadeEnderecoCadastro>());
    }

    private sealed class AutorizacaoFixa : IAutorizacao
    {
        public HashSet<string> Negadas { get; } = new();
        public bool Possui(string permissao) => !Negadas.Contains(permissao);
        public void Exigir(string permissao)
        {
            if (!Possui(permissao)) throw new AcessoNegadoException(permissao);
        }
    }

    private sealed class MotivoEmMemoria : IMotivoDaOperacao
    {
        public string? Motivo { get; set; }
    }
}
