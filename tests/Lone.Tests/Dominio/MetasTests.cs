using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Metas;

namespace Lone.Tests.Dominio;

public class MetasTests
{
    private static readonly Indicador Novos = new() { Id = Guid.NewGuid(), Nome = "Novos clientes", Fonte = FonteIndicador.NovosClientes };
    private static readonly Indicador Prazo = new() { Id = Guid.NewGuid(), Nome = "Prazo médio", Sentido = SentidoIndicador.MenorMelhor };
    private static Dictionary<Guid, Indicador> Indicadores => new[] { Novos, Prazo }.ToDictionary(i => i.Id);

    private static Meta MetaValida()
    {
        var m = new Meta { Id = Guid.NewGuid(), Nome = "Outubro", InicioEm = new(2026, 10, 1), FimEm = new(2026, 10, 31) };
        var i1 = new MetaItem { Id = Guid.NewGuid(), IndicadorId = Novos.Id, Peso = 60 };
        var i2 = new MetaItem { Id = Guid.NewGuid(), IndicadorId = Prazo.Id, Peso = 40 };
        var p = new MetaParticipante { Id = Guid.NewGuid(), Nivel = NivelParticipante.Colaborador, ReferenciaId = Guid.NewGuid() };
        m.Itens = [i1, i2];
        m.Faixas =
        [
            new MetaFaixa { Id = Guid.NewGuid(), InicioPercentual = 80, Nome = "Bronze", PercentualPremio = 50 },
            new MetaFaixa { Id = Guid.NewGuid(), InicioPercentual = 100, Nome = "Prata", PercentualPremio = 100 }
        ];
        m.Participantes = [p];
        m.Alvos =
        [
            new MetaAlvo { Id = Guid.NewGuid(), ParticipanteId = p.Id, ItemId = i1.Id, Alvo = 10 },
            new MetaAlvo { Id = Guid.NewGuid(), ParticipanteId = p.Id, ItemId = i2.Id, Alvo = 30 }
        ];
        return m;
    }

    [Fact]
    public void Meta_completa_e_valida() => Assert.Empty(RegrasMeta.Validar(MetaValida(), Indicadores, completa: true));

    [Fact]
    public void Pesos_precisam_somar_100()
    {
        var m = MetaValida();
        m.Itens[0].Peso = 50;
        Assert.Contains(RegrasMeta.Validar(m, Indicadores, completa: false), e => e.Contains("soma dos pesos"));
    }

    [Fact]
    public void Publicar_exige_alvo_em_cada_item()
    {
        var m = MetaValida();
        m.Alvos.RemoveAt(1);
        Assert.Empty(RegrasMeta.Validar(m, Indicadores, completa: false));
        Assert.Contains(RegrasMeta.Validar(m, Indicadores, completa: true), e => e.Contains("Faltam 1 alvo"));
    }

    [Theory]
    [InlineData(SituacaoMeta.Rascunho, SituacaoMeta.Publicada, true)]
    [InlineData(SituacaoMeta.Publicada, SituacaoMeta.EmApuracao, true)]
    [InlineData(SituacaoMeta.EmApuracao, SituacaoMeta.Fechada, true)]
    [InlineData(SituacaoMeta.Fechada, SituacaoMeta.EmApuracao, true)]
    [InlineData(SituacaoMeta.Rascunho, SituacaoMeta.Fechada, false)]
    [InlineData(SituacaoMeta.Fechada, SituacaoMeta.Rascunho, false)]
    public void Transicoes(SituacaoMeta de, SituacaoMeta para, bool permitida) =>
        Assert.Equal(permitida, RegrasMeta.ErroTransicao(de, para) is null);

    [Theory]
    [InlineData(10, 12, SentidoIndicador.MaiorMelhor, 120)]
    [InlineData(10, 30, SentidoIndicador.MaiorMelhor, 150)] // teto
    [InlineData(30, 20, SentidoIndicador.MenorMelhor, 150)]
    [InlineData(30, 60, SentidoIndicador.MenorMelhor, 50)]
    public void Atingimento_respeita_sentido_e_teto(decimal alvo, decimal realizado, SentidoIndicador sentido, decimal esperado) =>
        Assert.Equal(esperado, RegrasMeta.Atingimento(alvo, realizado, sentido, 150));

    [Fact]
    public void Apuracao_pondera_e_escolhe_a_faixa()
    {
        var m = MetaValida();
        var p = m.Participantes[0];
        var realizados = new Dictionary<(Guid, Guid), decimal?>
        {
            [(p.Id, m.Itens[0].Id)] = 11, // 110% × 60
            [(p.Id, m.Itens[1].Id)] = 40  // 75% × 40
        };
        var r = Assert.Single(RegrasMeta.Apurar(m, Indicadores, realizados));
        Assert.Equal(96m, r.Nota);
        Assert.Equal("Bronze", r.Faixa?.Nome);
    }

    [Fact]
    public void Item_sem_realizado_conta_zero()
    {
        var m = MetaValida();
        var r = Assert.Single(RegrasMeta.Apurar(m, Indicadores, new Dictionary<(Guid, Guid), decimal?>()));
        Assert.Equal(0m, r.Nota);
        Assert.Null(r.Faixa);
    }
}
