using Lone.Cliente.ViewModels.Comum;

namespace Lone.Tests.Cliente;

/// <summary>Filtro Situação das listas de cadastro (04/10/2026): Ativos, Inativos ou Todos, com a última escolha guardada.</summary>
public class FiltroSituacaoTests
{
    [Fact]
    public void Comeca_em_ativos_e_filtra_pela_escolha()
    {
        var filtro = new FiltroSituacao("prazos-periodo", preferencias: null);
        Assert.Equal(SituacaoLista.Ativos, filtro.Valor);
        Assert.True(filtro.Inclui(ativo: true));
        Assert.False(filtro.Inclui(ativo: false));

        var mudou = 0;
        filtro.Mudou += () => mudou++;
        filtro.Selecionada = FiltroSituacao.Opcoes[1];
        Assert.False(filtro.Inclui(ativo: true));
        Assert.True(filtro.Inclui(ativo: false));
        filtro.Selecionada = FiltroSituacao.Opcoes[2];
        Assert.True(filtro.Inclui(ativo: true) && filtro.Inclui(ativo: false));
        Assert.Equal(2, mudou);
    }

    [Fact]
    public void Guarda_e_le_a_escolha()
    {
        Assert.Equal("{\"situacao\":\"Inativos\"}", FiltroSituacao.Gravar(SituacaoLista.Inativos));
        Assert.Equal(SituacaoLista.Todos, FiltroSituacao.Ler(FiltroSituacao.Gravar(SituacaoLista.Todos))!.Valor);
        Assert.Null(FiltroSituacao.Ler(""));
        Assert.Null(FiltroSituacao.Ler("isto não é json"));
        Assert.Null(FiltroSituacao.Ler("{\"situacao\":\"Outra\"}"));
    }
}
