using System.Collections.ObjectModel;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Pessoas;

namespace Lone.Tests.Cliente;

/// <summary>Sócios da Receita na Identificação: prévia, "Ver todos os N" e busca (03/10/2026).</summary>
public class QuadroSociosTests
{
    private static ObservableCollection<SocioDto> Socios(int n) =>
        new(Enumerable.Range(1, n).Select(i => new SocioDto { Nome = i == 3 ? "João Gonçalves" : $"Sócio {i}", Qualificacao = i == 5 ? "Conselheiro" : "Diretor" }));

    [Fact]
    public void Lista_curta_mostra_todos_sem_botao_nem_busca()
    {
        var quadro = new QuadroSocios(Socios(4));
        Assert.Equal(4, quadro.Visiveis.Count);
        Assert.False(quadro.MostrarBotao);
        Assert.False(quadro.MostrarBusca);
        Assert.Equal("Sócios e administradores (Receita Federal) · 4", quadro.Titulo);
    }

    [Fact]
    public void Lista_longa_mostra_a_previa_e_ver_todos_abre_e_recolhe()
    {
        var quadro = new QuadroSocios(Socios(48));
        Assert.Equal(QuadroSocios.Previa, quadro.Visiveis.Count);
        Assert.True(quadro.MostrarBotao);
        Assert.True(quadro.MostrarBusca);
        Assert.Equal("Ver todos os 48 ▾", quadro.TextoBotao);

        quadro.AlternarCommand.Execute(null);
        Assert.Equal(48, quadro.Visiveis.Count);
        Assert.Equal("Recolher ▴", quadro.TextoBotao);
        quadro.AlternarCommand.Execute(null);
        Assert.Equal(QuadroSocios.Previa, quadro.Visiveis.Count);
    }

    [Fact]
    public void Busca_ignora_acentos_e_maiusculas_e_esconde_o_botao()
    {
        var quadro = new QuadroSocios(Socios(48));
        quadro.Busca = "joao goncalves";
        Assert.Equal("João Gonçalves", Assert.Single(quadro.Visiveis).Nome);
        Assert.False(quadro.MostrarBotao);
        quadro.Busca = "conselheiro"; // também pela qualificação
        Assert.Single(quadro.Visiveis);
        quadro.Busca = "ninguém";
        Assert.True(quadro.SemResultado);
    }

    [Fact]
    public void Outra_pessoa_carregada_volta_a_previa()
    {
        var socios = Socios(48);
        var quadro = new QuadroSocios(socios);
        quadro.AlternarCommand.Execute(null);
        socios.Clear();
        Assert.False(quadro.Expandido);
        Assert.False(quadro.TemSocios);
        foreach (var s in Socios(10)) socios.Add(s);
        Assert.Equal(QuadroSocios.Previa, quadro.Visiveis.Count);
    }
}
