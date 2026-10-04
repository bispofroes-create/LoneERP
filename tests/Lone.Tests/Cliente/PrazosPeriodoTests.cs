using Lone.Application.Parametros;
using Lone.Cliente.Api;
using Lone.Cliente.Formularios;
using Lone.Contracts.Comum;
using Lone.Domain.Entidades;

namespace Lone.Tests.Cliente;

/// <summary>Cadastro "Prazos de período" (03/10/2026): nome, limites, ordem e a conta do campo Prazo.</summary>
public class PrazosPeriodoTests
{
    private static PrazoPeriodo P(int q, UnidadePrazo u) => new() { Id = Guid.NewGuid(), Quantidade = q, Unidade = u };

    [Fact]
    public void Nome_no_singular_e_no_plural()
    {
        Assert.Equal("1 dia", RegrasPrazoPeriodo.Nome(1, UnidadePrazo.Dias));
        Assert.Equal("30 dias", RegrasPrazoPeriodo.Nome(30, UnidadePrazo.Dias));
        Assert.Equal("1 mês", RegrasPrazoPeriodo.Nome(1, UnidadePrazo.Meses));
        Assert.Equal("6 meses", RegrasPrazoPeriodo.Nome(6, UnidadePrazo.Meses));
        Assert.Equal("1 ano", RegrasPrazoPeriodo.Nome(1, UnidadePrazo.Anos));
        Assert.Equal("2 anos", RegrasPrazoPeriodo.Nome(2, UnidadePrazo.Anos));
    }

    [Fact]
    public void Valida_limites_e_repeticao()
    {
        var existente = P(30, UnidadePrazo.Dias);
        Assert.Empty(RegrasPrazoPeriodo.Validar(P(60, UnidadePrazo.Dias), [existente]));
        Assert.Contains("Já existe", Assert.Single(RegrasPrazoPeriodo.Validar(P(30, UnidadePrazo.Dias), [existente])));
        Assert.Empty(RegrasPrazoPeriodo.Validar(P(30, UnidadePrazo.Meses), [existente])); // outra unidade
        Assert.Single(RegrasPrazoPeriodo.Validar(P(0, UnidadePrazo.Dias), []));
        Assert.Single(RegrasPrazoPeriodo.Validar(P(11, UnidadePrazo.Anos), []));
        Assert.Single(RegrasPrazoPeriodo.Validar(P(5, (UnidadePrazo)9), []));
        var mesmo = P(30, UnidadePrazo.Dias);
        mesmo.Id = existente.Id;
        Assert.Empty(RegrasPrazoPeriodo.Validar(mesmo, [existente])); // salvar o próprio registro
    }

    [Fact]
    public void Ordena_do_menor_para_o_maior_entre_unidades()
    {
        var ordem = PrazoPeriodoAppService.Ordenar([P(1, UnidadePrazo.Anos), P(2, UnidadePrazo.Meses), P(30, UnidadePrazo.Dias), P(90, UnidadePrazo.Dias)])
            .Select(p => p.Nome);
        Assert.Equal(["30 dias", "2 meses", "90 dias", "1 ano"], ordem);
    }

    [Fact]
    public void Os_iniciais_sao_os_prazos_de_antes_do_cadastro()
    {
        Assert.Equal(CalculoPrazo.Padrao.Select(p => p.Nome), RegrasPrazoPeriodo.Iniciais.Select(i => RegrasPrazoPeriodo.Nome(i.Quantidade, i.Unidade)));
    }

    [Fact]
    public void Meses_e_anos_seguem_o_calendario_e_dias_contam_o_inicio()
    {
        var inicio = new DateOnly(2026, 10, 3);
        PrazoPronto Pronto(int q, UnidadePrazo u) => PrazosProntos.Converter(new PrazoPeriodoDto { Quantidade = q, Unidade = u, Nome = RegrasPrazoPeriodo.Nome(q, u) });
        Assert.Equal(new DateOnly(2026, 11, 1), CalculoPrazo.Fim(inicio, Pronto(30, UnidadePrazo.Dias)));
        Assert.Equal(new DateOnly(2027, 4, 2), CalculoPrazo.Fim(inicio, Pronto(6, UnidadePrazo.Meses)));
        Assert.Equal(new DateOnly(2027, 10, 2), CalculoPrazo.Fim(inicio, Pronto(1, UnidadePrazo.Anos)));
    }
}
