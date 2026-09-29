using Lone.Application.Metas;
using Lone.Application.Seguranca;
using Lone.Contracts.Metas;
using Lone.Contracts.Seguranca;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Validacao;
using Microsoft.Extensions.Time.Testing;

namespace Lone.Tests.Aplicacao;

/// <summary>
/// Metas com alcance restrito (Fase 2a-3, E13: permissão ≠ alcance): a lista, a leitura e a apuração mostram só os
/// participantes do alcance; mudar a estrutura ou a situação exige a meta inteira no alcance; lançar vale só para os seus.
/// </summary>
public class MetasEscopoTests
{
    private readonly Guid _carla = Guid.NewGuid();   // na equipe do gerente
    private readonly Guid _diego = Guid.NewGuid();   // fora
    private readonly Guid _item = Guid.NewGuid();
    private readonly Guid _indicador = Guid.NewGuid();
    private readonly MetasEmMemoria _metas = new();
    private readonly Lone.Tests.Apoio.EscopoFixo _escopo;
    private readonly MetaAppService _servico;

    public MetasEscopoTests()
    {
        _escopo = new Lone.Tests.Apoio.EscopoFixo { Restrito = true };
        _escopo.Gerenciadas.Add(_carla);
        var indicadores = new IndicadoresFixos(new Indicador { Id = _indicador, Codigo = "VND", Nome = "Vendas", Fonte = FonteIndicador.Informado });
        var nomes = new NomesFixos { [_carla] = "Carla", [_diego] = "Diego" };
        _servico = new MetaAppService(_metas, indicadores, nomes, new FonteVazia(), new TudoPermitido(), new Usuario(), new MotivoFixo(),
            new FakeTimeProvider(new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero)), _escopo);
    }

    private Meta Gravar(SituacaoMeta situacao, params Guid[] colaboradores)
    {
        var meta = new Meta
        {
            Id = Guid.NewGuid(), Nome = "Meta de outubro", InicioEm = new(2026, 10, 1), FimEm = new(2026, 10, 31), Situacao = situacao,
            Itens = [new MetaItem { Id = _item, IndicadorId = _indicador, Peso = 100 }]
        };
        foreach (var c in colaboradores)
        {
            var p = new MetaParticipante { Id = Guid.NewGuid(), MetaId = meta.Id, Nivel = NivelParticipante.Colaborador, ReferenciaId = c };
            meta.Participantes.Add(p);
            meta.Alvos.Add(new MetaAlvo { Id = Guid.NewGuid(), MetaId = meta.Id, ParticipanteId = p.Id, ItemId = _item, Alvo = 1000 });
        }
        _metas.Gravadas[meta.Id] = meta;
        return meta;
    }

    [Fact]
    public async Task Lista_e_leitura_mostram_so_os_participantes_do_alcance()
    {
        var mista = Gravar(SituacaoMeta.Rascunho, _carla, _diego);
        var deFora = Gravar(SituacaoMeta.Rascunho, _diego);

        var resumo = Assert.Single(await _servico.ListarAsync(incluirInativas: false));
        Assert.Equal(mista.Id, resumo.Id);
        Assert.Equal(1, resumo.Participantes);

        var dto = await _servico.ObterAsync(mista.Id);
        Assert.NotNull(dto);
        Assert.True(dto!.ParcialPorAlcance);
        Assert.Equal(_carla, Assert.Single(dto.Participantes).ReferenciaId);
        Assert.Equal(dto.Participantes[0].Id, Assert.Single(dto.Alvos).ParticipanteId);

        Assert.Null(await _servico.ObterAsync(deFora.Id));
        await Assert.ThrowsAsync<ForaDoEscopoException>(() => _servico.ApurarAsync(deFora.Id));
        Assert.Equal("Carla", Assert.Single((await _servico.ApurarAsync(mista.Id)).Participantes).Nome);
    }

    [Fact]
    public async Task Meta_com_gente_de_fora_nao_muda_mas_o_realizado_dos_seus_pode_ser_lancado()
    {
        var mista = Gravar(SituacaoMeta.Publicada, _carla, _diego);
        var daCarla = mista.Participantes.Single(p => p.ReferenciaId == _carla).Id;
        var doDiego = mista.Participantes.Single(p => p.ReferenciaId == _diego).Id;

        var erro = await Assert.ThrowsAsync<ValidacaoException>(() =>
            _servico.AlterarSituacaoAsync(mista.Id, new AlterarSituacaoMetaRequisicao { Situacao = SituacaoMeta.EmApuracao }));
        Assert.Contains(erro.Erros, e => e.Contains("fora do seu alcance"));

        await Assert.ThrowsAsync<ValidacaoException>(() => _servico.LancarRealizadoAsync(mista.Id, new LancarRealizadoRequisicao
        {
            Lancamentos = [new LancamentoRealizadoDto { ParticipanteId = doDiego, ItemId = _item, Valor = 10 }]
        }));

        await _servico.LancarRealizadoAsync(mista.Id, new LancarRealizadoRequisicao
        {
            Lancamentos = [new LancamentoRealizadoDto { ParticipanteId = daCarla, ItemId = _item, Valor = 800 }]
        });
        Assert.Equal(800, _metas.Gravadas[mista.Id].Alvos.Single(a => a.ParticipanteId == daCarla).Realizado);
    }

    [Fact]
    public async Task Gerente_cria_meta_so_com_participantes_do_alcance()
    {
        MetaDto Nova(params Guid[] colaboradores) => new()
        {
            Nome = "Meta da equipe", InicioEm = new(2026, 11, 1), FimEm = new(2026, 11, 30),
            Itens = [new MetaItemDto { Id = _item, IndicadorId = _indicador, Peso = 100 }],
            Faixas = [new MetaFaixaDto { Id = Guid.NewGuid(), InicioPercentual = 100, Nome = "Atingida", PercentualPremio = 0 }],
            Participantes = [.. colaboradores.Select(c => new MetaParticipanteDto { Id = Guid.NewGuid(), Nivel = NivelParticipante.Colaborador, ReferenciaId = c })]
        };

        var fora = await Assert.ThrowsAsync<ValidacaoException>(() => _servico.SalvarAsync(Nova(_carla, _diego)));
        Assert.Contains(fora.Erros, e => e.Contains("fora do seu alcance"));
        var vazia = await Assert.ThrowsAsync<ValidacaoException>(() => _servico.SalvarAsync(Nova()));
        Assert.Contains(vazia.Erros, e => e.Contains("ao menos um participante"));

        var criada = await _servico.SalvarAsync(Nova(_carla));
        Assert.False(criada.ParcialPorAlcance);
        Assert.Single(criada.Participantes);
    }

    // ---------------------------------------------------------------- Apoio

    private sealed class MetasEmMemoria : IMetaRepositorio
    {
        public Dictionary<Guid, Meta> Gravadas { get; } = new();

        public Task<List<MetaResumoDto>> ListarAsync(bool incluirInativas, CancellationToken ct) =>
            Task.FromResult(Gravadas.Values.Where(m => incluirInativas || m.Ativo).Select(m => new MetaResumoDto
            {
                Id = m.Id, Nome = m.Nome, InicioEm = m.InicioEm, FimEm = m.FimEm, Situacao = m.Situacao,
                Participantes = m.Participantes.Count, Ativo = m.Ativo
            }).ToList());

        public Task<Meta?> ObterAsync(Guid id, CancellationToken ct) => Task.FromResult(Gravadas.GetValueOrDefault(id));

        public Task<Dictionary<Guid, List<(NivelParticipante Nivel, Guid ReferenciaId)>>> ParticipantesAsync(IReadOnlyCollection<Guid> metas,
                                                                                                            CancellationToken ct) =>
            Task.FromResult(Gravadas.Values.Where(m => metas.Contains(m.Id))
                .ToDictionary(m => m.Id, m => m.Participantes.Select(p => (p.Nivel, p.ReferenciaId)).ToList()));

        public Task SalvarAsync(Meta meta, bool novo, CancellationToken ct)
        {
            meta.RetirarEventos();
            Gravadas[meta.Id] = meta;
            return Task.CompletedTask;
        }
    }

    private sealed class IndicadoresFixos(Indicador indicador) : IIndicadorRepositorio
    {
        public Task<List<Indicador>> ListarAsync(CancellationToken ct) => Task.FromResult(new List<Indicador> { indicador });
        public Task<Indicador?> ObterAsync(Guid id, CancellationToken ct) => Task.FromResult<Indicador?>(id == indicador.Id ? indicador : null);
        public Task SalvarAsync(Indicador item, bool novo, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class NomesFixos : Dictionary<Guid, string>, IMetaConsultas
    {
        public Task<Dictionary<(NivelParticipante, Guid), string>> NomesAsync(IEnumerable<(NivelParticipante Nivel, Guid Id)> participantes,
                                                                             CancellationToken ct) =>
            Task.FromResult(participantes.Where(p => ContainsKey(p.Id)).Distinct().ToDictionary(p => (p.Nivel, p.Id), p => this[p.Id]));

        public Task<List<ParticipanteOpcaoDto>> OpcoesAsync(DateOnly hoje, CancellationToken ct) =>
            Task.FromResult(this.Select(p => new ParticipanteOpcaoDto(NivelParticipante.Colaborador, p.Key, p.Value)).ToList());

        public Task<Dictionary<Guid, string>> NomesPessoasAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
            Task.FromResult(ids.Where(ContainsKey).ToDictionary(id => id, id => this[id]));
    }

    private sealed class FonteVazia : IFonteIndicadores
    {
        public Task<Dictionary<Guid, decimal>> CalcularAsync(FonteIndicador fonte, DateOnly inicio, DateOnly fim,
                                                             IReadOnlyList<MetaParticipante> participantes, CancellationToken ct) =>
            Task.FromResult(new Dictionary<Guid, decimal>());
    }

    private sealed class TudoPermitido : IAutorizacao
    {
        public bool Possui(string permissao) => true;
        public void Exigir(string permissao) { }
    }

    private sealed class Usuario : IUsuarioAtual
    {
        public Guid? Id => Guid.Empty;
        public string Nome => "gerente";
    }

    private sealed class MotivoFixo : IMotivoDaOperacao
    {
        public string? Motivo { get; set; }
    }
}
