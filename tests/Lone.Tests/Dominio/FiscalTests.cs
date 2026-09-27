using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Fiscal;
using Lone.Domain.Validacao;

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

    // ---- Pessoa física e estrangeiro: só o que se aplica ----

    private static (Pessoa Pessoa, Estabelecimento Estabelecimento) PessoaFisica()
    {
        var p = new Pessoa { Id = Guid.NewGuid(), Nome = "João", Natureza = NaturezaPessoa.Fisica };
        var e = new Estabelecimento { Id = Guid.NewGuid(), PessoaId = p.Id, Principal = true };
        p.Estabelecimentos.Add(e);
        return (p, e);
    }

    [Fact]
    public void Pessoa_fisica_sem_produtor_rural_vira_nao_contribuinte_e_indicador_gravado_e_mantido()
    {
        var (p, e) = PessoaFisica();
        PessoaNormalizador.Normalizar(p);
        Assert.Equal(IndicadorIE.NaoContribuinte, e.IndicadorIE);

        var (antigo, ea) = PessoaFisica();
        ea.IndicadorIE = IndicadorIE.Contribuinte;
        ea.InscricaoEstadual = "0012345670001";
        PessoaNormalizador.Normalizar(antigo);
        Assert.Equal(IndicadorIE.Contribuinte, ea.IndicadorIE);

        var (produtor, ep) = PessoaFisica();
        ep.ProdutorRural = true;
        PessoaNormalizador.Normalizar(produtor);
        Assert.Equal(IndicadorIE.NaoInformado, ep.IndicadorIE); // produtor: a escolha é do usuário
    }

    [Fact]
    public void Preencher_o_padrao_da_pessoa_fisica_corrige_o_periodo_aberto_sem_abrir_outro()
    {
        var (p, e) = PessoaFisica();
        RegrasFiscal.AtualizarHistorico(p, [], Hoje.AddDays(-100));
        var gravado = p.HistoricoFiscal.ToList();

        e.IndicadorIE = IndicadorIE.NaoContribuinte;
        RegrasFiscal.AtualizarHistorico(p, gravado, Hoje);

        var periodo = Assert.Single(p.HistoricoFiscal);
        Assert.Equal(IndicadorIE.NaoContribuinte, periodo.IndicadorIE);
        Assert.Equal(Hoje.AddDays(-100), periodo.InicioEm);
        Assert.Null(periodo.FimEm);
    }

    [Fact]
    public void Mudanca_real_abre_periodo_novo_e_preencher_dado_vazio_corrige_o_aberto()
    {
        // PF que vira produtor rural: situação nova.
        var (p, e) = PessoaFisica();
        RegrasFiscal.AtualizarHistorico(p, [], Hoje.AddDays(-100));
        var gravado = p.HistoricoFiscal.ToList();
        e.ProdutorRural = true;
        e.IndicadorIE = IndicadorIE.Contribuinte;
        e.InscricaoEstadual = "0012345670001";
        RegrasFiscal.AtualizarHistorico(p, gravado, Hoje);
        Assert.Equal(2, p.HistoricoFiscal.Count);

        // PJ: a consulta preenche indicador, IE, regime e situação que estavam vazios → mesmo período, corrigido.
        var (pj, ej) = ComEstabelecimento(RegimeTributario.NaoInformado);
        pj.Natureza = NaturezaPessoa.Juridica;
        RegrasFiscal.AtualizarHistorico(pj, [], Hoje.AddDays(-100));
        var gravadoPj = pj.HistoricoFiscal.ToList();
        ej.IndicadorIE = IndicadorIE.Contribuinte;
        ej.InscricaoEstadual = "133091229115";
        ej.RegimeTributario = RegimeTributario.RegimeNormal;
        ej.SituacaoReceita = "ATIVA";
        RegrasFiscal.AtualizarHistorico(pj, gravadoPj, Hoje);
        var periodo = Assert.Single(pj.HistoricoFiscal);
        Assert.Equal(Hoje.AddDays(-100), periodo.InicioEm);
        Assert.Equal("133091229115", periodo.InscricaoEstadual);
        Assert.Equal(RegimeTributario.RegimeNormal, periodo.RegimeTributario);

        // Depois de preenchido, alterar (contribuinte → não contribuinte) é situação nova.
        gravadoPj = pj.HistoricoFiscal.ToList();
        ej.IndicadorIE = IndicadorIE.NaoContribuinte;
        RegrasFiscal.AtualizarHistorico(pj, gravadoPj, Hoje.AddDays(1));
        Assert.Equal(2, pj.HistoricoFiscal.Count);
    }

    [Fact]
    public void Pessoa_fisica_nao_inclui_regime_cnae_nem_suframa_mas_mantem_ou_apaga_o_gravado()
    {
        var (nova, en) = PessoaFisica();
        en.RegimeTributario = RegimeTributario.SimplesNacional;
        en.CnaePrincipal = "4711302";
        en.InscricaoSuframa = "123456789";
        Assert.Equal(3, RegrasFiscal.ValidarCamposDeEmpresa(nova, null).Count);

        var (anterior, eg) = PessoaFisica();
        eg.RegimeTributario = RegimeTributario.SimplesNacional;
        eg.CnaePrincipal = "4711302";
        var (dados, ed) = PessoaFisica();
        dados.Id = anterior.Id;
        ed.Id = eg.Id;
        ed.RegimeTributario = RegimeTributario.SimplesNacional; // mantido
        ed.CnaePrincipal = null;                                 // apagado
        Assert.Empty(RegrasFiscal.ValidarCamposDeEmpresa(dados, anterior));

        ed.RegimeTributario = RegimeTributario.RegimeNormal;     // alterado
        Assert.Single(RegrasFiscal.ValidarCamposDeEmpresa(dados, anterior));

        var (pj, ej) = ComEstabelecimento(RegimeTributario.SimplesNacional);
        pj.Natureza = NaturezaPessoa.Juridica;
        ej.CnaePrincipal = "4711302";
        Assert.Empty(RegrasFiscal.ValidarCamposDeEmpresa(pj, null));
    }

    [Theory]
    [InlineData("2046", "204-6 · Sociedade Anônima Aberta")]
    [InlineData("206-2", "206-2 · Sociedade Empresária Limitada")]
    [InlineData("213", "213-5 · Empresário (Individual)")]
    [InlineData("3999", "399-9 · Associação Privada")]
    [InlineData("9997", "999-7")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Natureza_juridica_e_descrita_pelo_codigo(string? codigo, string esperado) =>
        Assert.Equal(esperado, NaturezasJuridicas.Descrever(codigo));
}
