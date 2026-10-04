using Lone.Cliente.ViewModels.Cadastros;
using Lone.Cliente.ViewModels.Comum;

namespace Lone.Tests.Cliente;

/// <summary>Lista de cadastro vazia: nada cadastrado × nada na situação × nada para a pesquisa (04/10/2026).</summary>
public class EstadoListaVaziaTests
{
    [Fact]
    public void Nada_cadastrado_usa_o_texto_da_tela()
    {
        Assert.False(EstadoListaVazia.Calcular(0, "", 0, 0, total: 0, "Ativos").PorFiltro);
        Assert.False(EstadoListaVazia.Calcular(3, "", 3, 0, 3, "Ativos").PorFiltro); // há itens: não está vazia
    }

    [Fact]
    public void Pesquisa_sem_resultado_oferece_limpar()
    {
        var e = EstadoListaVazia.Calcular(0, "45", naSituacao: 5, comPesquisa: 0, total: 8, "Ativos");
        Assert.True(e.PorFiltro);
        Assert.Equal("Nenhum resultado para \"45\"", e.Titulo);
        Assert.Equal("Limpar pesquisa", e.Acao);
        Assert.Equal("Confira o texto ou limpe a pesquisa.", e.Texto);
    }

    [Fact]
    public void Pesquisa_que_acha_em_outra_situacao_diz_quantos()
    {
        var e = EstadoListaVazia.Calcular(0, "meses", naSituacao: 7, comPesquisa: 1, total: 8, "Ativos");
        Assert.Equal("1 cadastro com este texto está fora de \"Ativos\". Mude a Situação para ver.", e.Texto);
    }

    [Fact]
    public void Situacao_sem_nada_oferece_mostrar_todos()
    {
        var e = EstadoListaVazia.Calcular(0, "", naSituacao: 0, comPesquisa: 0, total: 3, "Inativos");
        Assert.True(e.PorFiltro);
        Assert.Equal("Nenhum cadastro em \"Inativos\"", e.Titulo);
        Assert.Equal("Há 3 cadastros em outra situação.", e.Texto);
        Assert.Equal("Mostrar todos", e.Acao);
    }

    [Fact]
    public void Pesquisa_ignora_acentos_e_maiusculas()
    {
        Assert.True(CadastroViewModelBase<object>.ContemTexto("Prazos de período", "PERIODO"));
        Assert.True(CadastroViewModelBase<object>.ContemTexto("6 meses Meses", "meses"));
        Assert.False(CadastroViewModelBase<object>.ContemTexto("7 dias", "anos"));
    }
}
