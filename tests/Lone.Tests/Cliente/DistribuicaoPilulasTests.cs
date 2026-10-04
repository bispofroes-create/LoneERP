using Lone.Cliente.Grade;

namespace Lone.Tests.Cliente;

/// <summary>Coluna Papéis (D-UX-01): uma linha, duas linhas, "+N"; nunca abrevia nem cresce a linha.</summary>
public class DistribuicaoPilulasTests
{
    private static double Mais(int n) => 30;

    [Fact]
    public void Tudo_numa_linha_quando_cabe()
    {
        var d = DistribuicaoPilulas.Calcular([60, 80], 200, 4, 2, Mais);
        Assert.Equal(new Distribuicao(2, 0, 0), d);
    }

    [Fact]
    public void Quebra_em_duas_linhas_sem_mais_quando_cabe()
    {
        // 60+4+80 = 144 cabe; +4+70 = 218 não: o terceiro desce.
        var d = DistribuicaoPilulas.Calcular([60, 80, 70], 160, 4, 2, Mais);
        Assert.Equal(new Distribuicao(2, 1, 0), d);
    }

    [Fact]
    public void Mais_no_fim_da_segunda_linha_com_os_que_sobram()
    {
        // Linha 1: 60, 80. Linha 2: 70, 70 (144) não deixa lugar para o +N (30): fica 70 + "+2".
        var d = DistribuicaoPilulas.Calcular([60, 80, 70, 70, 50], 160, 4, 2, Mais);
        Assert.Equal(new Distribuicao(2, 1, 2), d);
        Assert.Equal(3, d.Visiveis);
    }

    [Fact]
    public void Linha_baixa_usa_uma_linha_so_com_mais()
    {
        var d = DistribuicaoPilulas.Calcular([60, 80, 70], 160, 4, 1, Mais);
        Assert.Equal(new Distribuicao(1, 0, 2), d); // 60 + "+2" (60+4+30=94); 60+80 não deixa lugar para o +N
    }

    [Fact]
    public void Selo_mais_largo_que_a_celula_ainda_aparece()
    {
        var d = DistribuicaoPilulas.Calcular([300, 50], 160, 4, 2, Mais);
        Assert.Equal(1, d.NaLinha1);
        Assert.Equal(new Distribuicao(1, 1, 0), d);
    }

    [Fact]
    public void Sem_selos_nada()
    {
        Assert.Equal(new Distribuicao(0, 0, 0), DistribuicaoPilulas.Calcular([], 160, 4, 2, Mais));
    }

    [Fact]
    public void Dica_lista_os_ocultos_pelo_nome_completo()
    {
        string[] nomes = ["Cliente", "Fornecedor", "Parceiro", "Representante"];
        Assert.Equal("Mais 2 papéis: Parceiro, Representante", DistribuicaoPilulas.DicaOcultos(nomes, 2));
        Assert.Equal("Mais 1 papel: Representante", DistribuicaoPilulas.DicaOcultos(nomes, 3));
    }
}
