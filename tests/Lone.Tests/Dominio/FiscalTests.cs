using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Fiscal;

namespace Lone.Tests.Dominio;

public class FiscalTests
{
    private static readonly DateOnly Hoje = new(2026, 9, 25);

    private static (Pessoa Pessoa, Estabelecimento Estabelecimento) ComEstabelecimento(RegimeTributario regime)
    {
        var p = new Pessoa { Id = Guid.NewGuid(), Nome = "Empresa" };
        var e = new Estabelecimento { Id = Guid.NewGuid(), PessoaId = p.Id, RegimeTributario = regime };
        p.Estabelecimentos.Add(e);
        return (p, e);
    }

    [Fact]
    public void Primeira_gravacao_abre_o_periodo_e_mudanca_encerra_na_vespera()
    {
        var (p, e) = ComEstabelecimento(RegimeTributario.SimplesNacional);
        RegrasFiscal.AtualizarHistorico(p, [], Hoje.AddDays(-30));
        var gravado = p.HistoricoFiscal.ToList();
        Assert.Single(gravado);

        e.RegimeTributario = RegimeTributario.RegimeNormal;
        RegrasFiscal.AtualizarHistorico(p, gravado, Hoje);

        Assert.Equal(2, p.HistoricoFiscal.Count);
        var antigo = p.HistoricoFiscal.Single(h => h.RegimeTributario == RegimeTributario.SimplesNacional);
        Assert.Equal(Hoje.AddDays(-1), antigo.FimEm);
        Assert.Equal(RegimeTributario.SimplesNacional, RegrasFiscal.Vigente(p.HistoricoFiscal, e.Id, Hoje.AddDays(-10))!.RegimeTributario);
        Assert.Equal(RegimeTributario.RegimeNormal, RegrasFiscal.Vigente(p.HistoricoFiscal, e.Id, Hoje)!.RegimeTributario);
        Assert.Null(gravado[0].FimEm); // o gravado original não é alterado (cópia)
    }

    [Fact]
    public void Duas_mudancas_no_mesmo_dia_corrigem_o_periodo_de_hoje_e_sem_mudanca_nada_acontece()
    {
        var (p, e) = ComEstabelecimento(RegimeTributario.SimplesNacional);
        RegrasFiscal.AtualizarHistorico(p, [], Hoje);
        var gravado = p.HistoricoFiscal.ToList();

        RegrasFiscal.AtualizarHistorico(p, gravado, Hoje);
        Assert.Single(p.HistoricoFiscal);

        e.ProdutorRural = true;
        RegrasFiscal.AtualizarHistorico(p, gravado, Hoje);
        Assert.True(Assert.Single(p.HistoricoFiscal).ProdutorRural);
    }

    [Fact]
    public void Cnaes_saem_dos_campos_de_texto_sem_repetir_e_reaproveitam_ids()
    {
        var (p, e) = ComEstabelecimento(RegimeTributario.SimplesNacional);
        e.CnaePrincipal = "4711302";
        e.CnaesSecundarios = "4711302,4712100, 47.21-1/02,123";

        RegrasFiscal.SincronizarCnaes(p, []);
        var primeira = p.Cnaes.ToList();
        RegrasFiscal.SincronizarCnaes(p, primeira);

        Assert.Equal(new[] { 4711302, 4712100, 4721102 }, p.Cnaes.Select(c => c.Codigo).ToArray());
        Assert.True(p.Cnaes[0].Principal);
        Assert.Equal(primeira.Select(c => c.Id), p.Cnaes.Select(c => c.Id));
    }

    [Fact]
    public void Codigo_cnae_e_formatado() => Assert.Equal("0111-3/01", Cnae.Formatar(111301));
}
