using Lone.Cliente.Formularios;

namespace Lone.Tests.Cliente;

/// <summary>Calendário dos campos de data (03/10/2026): grade fixa de 6 semanas começando no domingo.</summary>
public class MesCalendarioTests
{
    [Fact]
    public void Grade_do_mes_tem_42_dias_a_partir_do_domingo()
    {
        var dias = MesCalendario.Dias(2026, 10, new DateOnly(2026, 10, 3), new DateOnly(2026, 10, 15));

        Assert.Equal(42, dias.Count);
        Assert.Equal(new DateOnly(2026, 9, 27), dias[0].Data); // 01/10/2026 é quinta
        Assert.Equal(DayOfWeek.Sunday, dias[0].Data.DayOfWeek);
        Assert.False(dias[0].DoMes);
        Assert.True(dias[4].DoMes);
        Assert.Equal(31, dias.Count(d => d.DoMes));
        Assert.Single(dias, d => d.Hoje);
        Assert.Equal(new DateOnly(2026, 10, 3), dias.Single(d => d.Hoje).Data);
        Assert.Equal(new DateOnly(2026, 10, 15), dias.Single(d => d.Escolhido).Data);
    }

    [Fact]
    public void Mes_que_comeca_no_domingo_nao_mostra_dias_do_mes_anterior()
    {
        var dias = MesCalendario.Dias(2026, 11, new DateOnly(2026, 10, 3), null); // 01/11/2026 é domingo

        Assert.Equal(new DateOnly(2026, 11, 1), dias[0].Data);
        Assert.DoesNotContain(dias, d => d.Escolhido || d.Hoje);
    }

    [Fact]
    public void Titulo_e_texto_em_portugues()
    {
        Assert.Equal("Outubro de 2026", MesCalendario.Titulo(2026, 10));
        Assert.Equal("05/03/2026", MesCalendario.Texto(new DateOnly(2026, 3, 5)));
    }
}
