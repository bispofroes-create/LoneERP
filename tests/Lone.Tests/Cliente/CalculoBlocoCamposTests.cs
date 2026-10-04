using Lone.Cliente.Formularios;

namespace Lone.Tests.Cliente;

/// <summary>
/// Bloco de campos das fichas (03/10/2026): cada linha com a altura do seu item mais alto (o FlexLayout repartia a altura
/// do bloco igualmente e cortava os campos), meia linha vira um terço com largura de sobra, item escondido não ocupa
/// espaço, alinhamento na linha.
/// </summary>
public class CalculoBlocoCamposTests
{
    private static (IReadOnlyList<PosicaoItem> Posicoes, double Altura) Calcular(double largura, ItemBloco[] itens, double[] alturas,
        AlinhamentoLinha alinhamento = AlinhamentoLinha.Esticar, double[]? naturais = null)
    {
        var posicoes = CalculoBlocoCampos.Calcular(largura, itens, i => naturais?[i] ?? 0, (i, _) => alturas[i], alinhamento, out var altura);
        return (posicoes, altura);
    }

    [Fact]
    public void Cada_linha_tem_a_altura_do_item_mais_alto_e_o_bloco_e_a_soma()
    {
        // Aba Fiscal (medida real): campo 78, aviso de linha inteira 34, lista 66, campo 78, caixas 36.
        var itens = new[]
        {
            new ItemBloco(true, 0.5), new ItemBloco(true, 0.5), // nome fantasia, natureza
            new ItemBloco(true, 1),                             // aviso
            new ItemBloco(true, 0.5), new ItemBloco(true, 0.5), // indicador de IE (lista), inscrição estadual
            new ItemBloco(true, 0.5), new ItemBloco(true, 0.5)  // caixas de marcar
        };
        var (p, altura) = Calcular(988, itens, [78, 78, 34, 66, 78, 36, 36]);

        Assert.Equal(new PosicaoItem(0, 0, 494, 78), p[0]);
        Assert.Equal(new PosicaoItem(494, 0, 494, 78), p[1]);
        Assert.Equal(new PosicaoItem(0, 78, 988, 34), p[2]);       // o aviso não ganha sobra
        Assert.Equal(new PosicaoItem(0, 112, 494, 78), p[3]);      // a lista estica até a altura da linha
        Assert.Equal(new PosicaoItem(494, 112, 494, 78), p[4]);    // o campo recebe os 78 que precisa (antes 59,8)
        Assert.Equal(190, p[5].Y);
        Assert.Equal(36, p[5].Altura);
        Assert.Equal(78 + 34 + 78 + 36, altura);
    }

    [Theory]
    [InlineData(599, 1)]
    [InlineData(600, 2)]
    [InlineData(1299, 2)]
    [InlineData(1300, 3)]
    public void Grade_tem_1_2_ou_3_colunas_pela_largura(double largura, int colunas) =>
        Assert.Equal(colunas, CalculoBlocoCampos.ColunasDaGrade(largura));

    [Fact]
    public void Meia_linha_e_uma_coluna_da_grade()
    {
        var itens = Enumerable.Repeat(new ItemBloco(true, 0.5), 4).ToArray();
        var (celular, _) = Calcular(500, itens, [70, 70, 70, 70]);
        Assert.Equal(500, celular[0].Largura); // uma coluna: um por linha
        Assert.Equal(70, celular[1].Y);

        var (medio, _) = Calcular(1200, itens, [70, 70, 70, 70]);
        Assert.Equal(600, medio[0].Largura);
        Assert.Equal(70, medio[2].Y); // dois por linha até 1300

        var (largo, altura) = Calcular(1500, itens, [70, 70, 70, 70]);
        Assert.Equal(500, largo[0].Largura, 6);
        Assert.Equal(1000, largo[2].X, 6); // três por linha
        Assert.Equal(70, largo[3].Y);
        Assert.Equal(140, altura);

        var (inteira, _) = Calcular(1500, [new ItemBloco(true, 1)], [70]);
        Assert.Equal(1500, inteira[0].Largura); // linha inteira não muda
    }

    [Fact]
    public void Campo_de_duas_colunas_ocupa_duas_ou_a_linha_toda()
    {
        var itens = new[] { new ItemBloco(true, Colunas: 2), new ItemBloco(true, 0.5) };
        var (largo, _) = Calcular(1500, itens, [70, 70]);
        Assert.Equal(1000, largo[0].Largura, 6);  // 2 de 3 colunas
        Assert.Equal(1000, largo[1].X, 6);        // a terceira coluna na mesma linha

        var (medio, _) = Calcular(1200, itens, [70, 70]);
        Assert.Equal(1200, medio[0].Largura);     // grade de 2: as duas colunas = linha inteira
        Assert.Equal(70, medio[1].Y);

        var (celular, _) = Calcular(500, itens, [70, 70]);
        Assert.Equal(500, celular[0].Largura);
    }

    [Fact]
    public void Item_escondido_nao_ocupa_espaco()
    {
        var itens = new[] { new ItemBloco(true, 0.5), new ItemBloco(false, 0.5), new ItemBloco(false, 1), new ItemBloco(true, 0.5) };
        var (p, altura) = Calcular(800, itens, [78, 500, 500, 78]);
        Assert.Equal(default, p[1]);
        Assert.Equal(default, p[2]);
        Assert.Equal(new PosicaoItem(400, 0, 400, 78), p[3]); // ao lado do primeiro
        Assert.Equal(78, altura);
    }

    [Fact]
    public void Item_sem_base_usa_a_largura_natural_e_largura_fixa_e_respeitada()
    {
        var itens = new[] { new ItemBloco(true), new ItemBloco(true, LarguraFixa: 150), new ItemBloco(true) };
        var (p, _) = Calcular(500, itens, [20, 40, 30], naturais: [304, 0, 900]);
        Assert.Equal(new PosicaoItem(0, 0, 304, 40), p[0]);
        Assert.Equal(new PosicaoItem(304, 0, 150, 40), p[1]);
        Assert.Equal(new PosicaoItem(0, 40, 500, 30), p[2]); // natural maior que o bloco: limitado à largura
    }

    [Theory]
    [InlineData(AlinhamentoLinha.Esticar, 0, 80)]
    [InlineData(AlinhamentoLinha.Inicio, 0, 40)]
    [InlineData(AlinhamentoLinha.Centro, 20, 40)]
    [InlineData(AlinhamentoLinha.Fim, 40, 40)]
    public void Alinhamento_do_item_mais_baixo_na_linha(AlinhamentoLinha alinhamento, double y, double altura)
    {
        var (p, total) = Calcular(600, [new ItemBloco(true, 0.5), new ItemBloco(true, 0.5)], [80, 40], alinhamento);
        Assert.Equal(y, p[1].Y);
        Assert.Equal(altura, p[1].Altura);
        Assert.Equal(80, total);
    }

    [Fact]
    public void Itens_com_peso_dividem_a_sobra_e_a_linha_fica_cheia()
    {
        // 1000 de largura: fixo 160, dois com peso (mínima 200; pesos 1 e 3) e um natural de 100 → sobra 340.
        ItemBloco[] itens = [new(true, LarguraFixa: 160), new(true, LarguraFixa: 200, Peso: 1), new(true, LarguraFixa: 200, Peso: 3), new(true)];
        var (pos, _) = Calcular(1000, itens, [40, 40, 40, 40], naturais: [0, 0, 0, 100]);
        Assert.Equal(160, pos[0].Largura, 3);
        Assert.Equal(285, pos[1].Largura, 3);
        Assert.Equal(455, pos[2].Largura, 3);
        Assert.Equal(900, pos[3].X, 3);
        Assert.Equal(1000, pos[3].X + pos[3].Largura, 3);
    }
}
