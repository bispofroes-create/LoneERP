using Lone.Domain.Comercial;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;

namespace Lone.Tests.Dominio;

/// <summary>Ausências e coberturas da carteira (Motor Comercial, Fase 1c).</summary>
public class CoberturaTests
{
    private static readonly DateOnly Hoje = new(2026, 9, 28);
    private static readonly Guid Joao = Guid.NewGuid();
    private static readonly Guid Maria = Guid.NewGuid();

    private static CoberturaComercial Nova(DateOnly inicio, DateOnly fim, Guid? papel = null, Guid? empresa = null) => new()
    {
        Id = Guid.NewGuid(), TitularId = Joao, SubstitutoId = Maria, TipoAusenciaId = Guid.NewGuid(),
        InicioEm = inicio, FimEm = fim, TipoCarteiraId = papel, EmpresaId = empresa
    };

    private static CoberturaComercial Copia(CoberturaComercial c) => new()
    {
        Id = c.Id, TitularId = c.TitularId, SubstitutoId = c.SubstitutoId, EquipeSubstitutaId = c.EquipeSubstitutaId,
        TipoAusenciaId = c.TipoAusenciaId, InicioEm = c.InicioEm, FimEm = c.FimEm, TipoCarteiraId = c.TipoCarteiraId,
        EmpresaId = c.EmpresaId, RegraCredito = c.RegraCredito, PercentualSubstituto = c.PercentualSubstituto,
        PermiteAcesso = c.PermiteAcesso, Observacao = c.Observacao, Cancelada = c.Cancelada
    };

    [Fact]
    public void Cobertura_precisa_de_titular_tipo_periodo_e_uma_so_forma_de_cobrir()
    {
        var c = new CoberturaComercial { Id = Guid.NewGuid() };
        var erros = RegrasCobertura.Validar(c, null, [], Hoje);
        Assert.Contains(erros, e => e.Contains("quem vai se ausentar"));
        Assert.Contains(erros, e => e.Contains("tipo de ausência"));
        Assert.Contains(erros, e => e.Contains("Informe o início"));
        Assert.Contains(erros, e => e.Contains("Informe o fim"));
        Assert.Contains(erros, e => e.Contains("uma pessoa ou uma equipe"));

        var duas = Nova(Hoje, Hoje.AddDays(5));
        duas.EquipeSubstitutaId = Guid.NewGuid();
        Assert.Contains(RegrasCobertura.Validar(duas, null, [], Hoje), e => e.Contains("não as duas"));

        var ela = Nova(Hoje, Hoje.AddDays(5));
        ela.SubstitutoId = Joao;
        Assert.Contains(RegrasCobertura.Validar(ela, null, [], Hoje), e => e.Contains("próprio titular"));

        var invertida = Nova(Hoje, Hoje.AddDays(-1));
        Assert.Contains(RegrasCobertura.Validar(invertida, null, [], Hoje), e => e.Contains("anterior ao início"));

        Assert.Empty(RegrasCobertura.Validar(Nova(Hoje, Hoje.AddDays(14)), null, [], Hoje));
    }

    [Fact]
    public void Credito_dividido_pede_percentual_e_as_outras_regras_limpam()
    {
        var c = Nova(Hoje, Hoje.AddDays(5));
        c.RegraCredito = RegraCreditoAusencia.Dividido;
        Assert.Contains(RegrasCobertura.Validar(c, null, [], Hoje), e => e.Contains("informe o percentual"));
        c.PercentualSubstituto = 100;
        Assert.Contains(RegrasCobertura.Validar(c, null, [], Hoje), e => e.Contains("sem os extremos"));
        c.PercentualSubstituto = 40;
        Assert.Empty(RegrasCobertura.Validar(c, null, [], Hoje));

        c.RegraCredito = RegraCreditoAusencia.Titular;
        RegrasCobertura.Normalizar(c);
        Assert.Null(c.PercentualSubstituto);
    }

    [Fact]
    public void Duas_coberturas_do_mesmo_titular_nao_se_sobrepoem_no_mesmo_escopo()
    {
        var papelA = Guid.NewGuid();
        var papelB = Guid.NewGuid();
        var ferias = Nova(Hoje.AddDays(10), Hoje.AddDays(20));
        var todos = Nova(Hoje.AddDays(15), Hoje.AddDays(25));
        Assert.Contains(RegrasCobertura.Validar(todos, null, [ferias], Hoje), e => e.Contains("Já existe cobertura"));

        // Papéis diferentes não se cruzam; um "todos os papéis" cruza com qualquer papel.
        var soA = Nova(Hoje.AddDays(10), Hoje.AddDays(20), papel: papelA);
        Assert.Empty(RegrasCobertura.Validar(Nova(Hoje.AddDays(15), Hoje.AddDays(25), papel: papelB), null, [soA], Hoje));
        Assert.NotEmpty(RegrasCobertura.Validar(Nova(Hoje.AddDays(15), Hoje.AddDays(25)), null, [soA], Hoje));

        // Cancelada não conta; a própria (edição) também não.
        ferias.Cancelada = true;
        Assert.Empty(RegrasCobertura.Validar(todos, null, [ferias, todos], Hoje));
    }

    [Fact]
    public void Depois_que_comecou_so_fim_observacao_e_acesso_mudam_e_nao_cancela()
    {
        var gravada = Nova(Hoje.AddDays(-5), Hoje.AddDays(10));

        var prorrogada = Copia(gravada);
        prorrogada.FimEm = Hoje.AddDays(20);
        prorrogada.Observacao = "voltou mais tarde";
        prorrogada.PermiteAcesso = false;
        Assert.Empty(RegrasCobertura.Validar(prorrogada, gravada, [], Hoje));

        var encerrada = Copia(gravada);
        encerrada.FimEm = Hoje;
        Assert.Empty(RegrasCobertura.Validar(encerrada, gravada, [], Hoje));

        var passado = Copia(gravada);
        passado.FimEm = Hoje.AddDays(-3);
        Assert.Contains(RegrasCobertura.Validar(passado, gravada, [], Hoje), e => e.Contains("o passado não é reescrito"));

        var outraPessoa = Copia(gravada);
        outraPessoa.SubstitutoId = Guid.NewGuid();
        outraPessoa.InicioEm = Hoje.AddDays(-4);
        Assert.Contains(RegrasCobertura.Validar(outraPessoa, gravada, [], Hoje), e => e.Contains("início, quem cobre não muda(m)"));

        var cancelada = Copia(gravada);
        cancelada.Cancelada = true;
        Assert.Contains(RegrasCobertura.Validar(cancelada, gravada, [], Hoje), e => e.Contains("não pode ser cancelada"));
    }

    [Fact]
    public void Agendada_pode_ser_corrigida_e_cancelada_e_cancelada_nao_volta()
    {
        var agendada = Nova(Hoje.AddDays(5), Hoje.AddDays(10));
        var corrigida = Copia(agendada);
        corrigida.InicioEm = Hoje.AddDays(6);
        corrigida.SubstitutoId = Guid.NewGuid();
        Assert.Empty(RegrasCobertura.Validar(corrigida, agendada, [], Hoje));

        var cancelada = Copia(agendada);
        cancelada.Cancelada = true;
        cancelada.MotivoCancelamento = "férias adiadas";
        Assert.Empty(RegrasCobertura.Validar(cancelada, agendada, [], Hoje));

        var reaberta = Copia(cancelada);
        reaberta.Cancelada = false;
        Assert.Contains(RegrasCobertura.Validar(reaberta, cancelada, [], Hoje), e => e.Contains("não volta"));
    }

    [Fact]
    public void Situacao_vem_das_datas_e_acaba_sozinha()
    {
        var c = Nova(Hoje.AddDays(1), Hoje.AddDays(3));
        Assert.Equal(SituacaoCobertura.Agendada, c.Situacao(Hoje));
        Assert.Equal(SituacaoCobertura.Vigente, c.Situacao(Hoje.AddDays(2)));
        Assert.Equal(SituacaoCobertura.Encerrada, c.Situacao(Hoje.AddDays(4)));
        Assert.True(c.Vigente(Hoje.AddDays(3)));
        c.Cancelada = true;
        Assert.Equal(SituacaoCobertura.Cancelada, c.Situacao(Hoje.AddDays(2)));
        Assert.False(c.Vigente(Hoje.AddDays(2)));
    }

    [Fact]
    public void Descricao_diz_periodo_quem_cobre_e_credito()
    {
        var c = Nova(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 15));
        Assert.Equal("Férias de 01/10/2026 a 15/10/2026 · atendimento por Maria (crédito do titular)",
            RegrasCobertura.Descrever(c, "Férias", "Maria"));
        c.RegraCredito = RegraCreditoAusencia.Dividido;
        c.PercentualSubstituto = 30;
        Assert.EndsWith("(crédito dividido: 30% para quem cobre)", RegrasCobertura.Descrever(c, "Férias", "Maria"));
    }

    [Fact]
    public void Tipos_de_ausencia_e_parametros_sao_conferidos()
    {
        var todos = TiposAusenciaIniciais.Todos.Select(t => new TipoAusencia { Id = t.Id, Nome = t.Nome }).ToList();
        Assert.Contains(TiposAusenciaIniciais.Validar(new TipoAusencia { Id = Guid.NewGuid(), Nome = "ferias" }, todos), e => e.Contains("Já existe"));
        Assert.Empty(TiposAusenciaIniciais.Validar(new TipoAusencia { Id = Guid.NewGuid(), Nome = "Home office" }, todos));

        Assert.Contains(RegrasParametrosComerciais.Validar(new ParametrosComerciais { DiasAvisoFimVinculo = 0 }), e => e.Contains("de 1 a 365"));
        Assert.Contains(RegrasParametrosComerciais.Validar(new ParametrosComerciais { CreditoNaAusencia = RegraCreditoAusencia.Dividido }),
            e => e.Contains("percentual"));
        var padrao = new ParametrosComerciais();
        Assert.Empty(RegrasParametrosComerciais.Validar(padrao));
        Assert.Equal(30, padrao.DiasAvisoFimVinculo);
        Assert.Equal(RegraCreditoAusencia.Titular, padrao.CreditoNaAusencia);
    }
}
