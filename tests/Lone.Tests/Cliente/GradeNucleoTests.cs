using System.ComponentModel;
using Lone.Cliente.Grade;

namespace Lone.Tests.Cliente;

/// <summary>
/// Núcleo puro da GradeLista (P2-B2, Etapa 1): definição de coluna, célula sem largura, interface da linha,
/// calculadora de larguras e âncora lógica da rolagem. Sem MAUI.
/// </summary>
public class GradeNucleoTests
{
    private const double Tol = 0.0001;

    /// <summary>As colunas de Pessoas aprovadas (P2), com a parte fixa (nome) na mesma conta.</summary>
    private static List<ColunaGradeDef> ColunasPessoas() =>
    [
        ColunaGradeDef.Fixa("nome", "Nome", TipoCelula.Texto, 340, minima: 260),
        ColunaGradeDef.Fixa("documento", "CPF/CNPJ", TipoCelula.Texto, 180),
        ColunaGradeDef.Fixa("tipo", "Tipo", TipoCelula.Selo, 80),
        ColunaGradeDef.Proporcional("papeis", "Papéis", TipoCelula.Pilulas, peso: 2, minima: 160),
        ColunaGradeDef.Proporcional("cidade", "Cidade", TipoCelula.Texto, peso: 2, minima: 160),
        ColunaGradeDef.Fixa("situacao", "Situação", TipoCelula.Selo, 120),
    ];

    private static void Larguras(ResultadoLarguras r, params double[] esperadas)
    {
        Assert.Equal(esperadas.Length, r.Larguras.Count);
        for (var i = 0; i < esperadas.Length; i++) Assert.Equal(esperadas[i], r.Larguras[i], Tol);
    }

    // ---------- Coluna ----------

    [Fact]
    public void Coluna_nasce_com_a_largura_da_regra_e_fixa_sem_minima_nunca_encolhe()
    {
        var fixa = ColunaGradeDef.Fixa("a", "A", TipoCelula.Texto, 180);
        var prop = ColunaGradeDef.Proporcional("b", "B", TipoCelula.Texto, 2, 160, 400);

        Assert.Equal(180, fixa.LarguraEfetiva);
        Assert.Equal(180, fixa.Minima);
        Assert.Equal(0, fixa.Peso);
        Assert.Equal(160, prop.LarguraEfetiva);
        Assert.Equal(2, prop.Peso);
        Assert.Equal(400, prop.Maxima);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Coluna_recusa_largura_peso_e_minima_invalidos(double valor)
    {
        Assert.ThrowsAny<ArgumentException>(() => ColunaGradeDef.Fixa("a", "A", TipoCelula.Texto, valor));
        Assert.ThrowsAny<ArgumentException>(() => ColunaGradeDef.Proporcional("a", "A", TipoCelula.Texto, valor, 100));
        Assert.ThrowsAny<ArgumentException>(() => ColunaGradeDef.Proporcional("a", "A", TipoCelula.Texto, 1, valor));
    }

    [Fact]
    public void Coluna_recusa_minima_acima_da_largura_maxima_abaixo_da_minima_e_chave_vazia()
    {
        Assert.ThrowsAny<ArgumentException>(() => ColunaGradeDef.Fixa("a", "A", TipoCelula.Texto, 100, minima: 120));
        Assert.ThrowsAny<ArgumentException>(() => ColunaGradeDef.Proporcional("a", "A", TipoCelula.Texto, 1, 160, maxima: 100));
        Assert.ThrowsAny<ArgumentException>(() => ColunaGradeDef.Fixa(" ", "A", TipoCelula.Texto, 100));
    }

    // ---------- Célula ----------

    [Fact]
    public void Celula_nao_tem_largura_propria()
    {
        Assert.Null(typeof(CelulaGrade).GetProperty("Largura"));
        Assert.DoesNotContain(typeof(CelulaGrade).GetFields(System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public), f => f.FieldType == typeof(double));
    }

    [Fact]
    public void Celula_acompanha_a_largura_da_coluna_sem_ser_recriada()
    {
        var colunas = ColunasPessoas();
        var celula = CelulaGrade.DeTexto(colunas[4], "Belo Horizonte");
        var avisos = new List<string?>();
        colunas[4].PropertyChanged += (_, e) => avisos.Add(e.PropertyName);

        CalculadoraLarguras.Aplicar(colunas, CalculadoraLarguras.Calcular(colunas, 1280));

        Assert.Same(colunas[4], celula.Coluna);
        Assert.Equal(280, celula.Coluna.LarguraEfetiva, Tol);
        Assert.Equal(new string?[] { nameof(ColunaGradeDef.LarguraEfetiva) }, avisos);
    }

    [Fact]
    public void Celula_tem_o_tipo_da_coluna_e_recusa_tipo_diferente()
    {
        var texto = ColunaGradeDef.Fixa("doc", "Doc", TipoCelula.Texto, 180);
        var selo = ColunaGradeDef.Fixa("sit", "Situação", TipoCelula.Selo, 120);
        var pilulas = ColunaGradeDef.Proporcional("pap", "Papéis", TipoCelula.Pilulas, 2, 160);

        Assert.Equal(TipoCelula.Texto, CelulaGrade.DeTexto(texto, "123").Tipo);
        Assert.Equal(TipoCelula.Selo, CelulaGrade.DeSelo(selo, "Ativa", "Sucesso").Tipo);
        Assert.Equal(TipoCelula.Pilulas, CelulaGrade.DePilulas(pilulas, [new("Cliente", "Informacao")]).Tipo);

        Assert.Throws<ArgumentException>(() => CelulaGrade.DeSelo(texto, "Ativa", "Sucesso"));
        Assert.Throws<ArgumentException>(() => CelulaGrade.DeTexto(selo, "Ativa"));
        Assert.Throws<ArgumentException>(() => CelulaGrade.DePilulas(texto, []));
    }

    [Fact]
    public void Celula_sem_valor_mostra_traco_e_fica_vazia()
    {
        var texto = ColunaGradeDef.Fixa("doc", "Doc", TipoCelula.Texto, 180);
        var selo = ColunaGradeDef.Fixa("sit", "Situação", TipoCelula.Selo, 120);
        var pilulas = ColunaGradeDef.Proporcional("pap", "Papéis", TipoCelula.Pilulas, 2, 160);

        var vazia = CelulaGrade.DeTexto(texto, "  ");
        Assert.Equal(CelulaGrade.SemValor, vazia.Texto);
        Assert.True(vazia.Vazia);
        Assert.Null(vazia.Tom);
        Assert.Empty(vazia.Selos);

        Assert.True(CelulaGrade.DeSelo(selo, null, "Neutro").Vazia);
        Assert.False(CelulaGrade.DeSelo(selo, "Ativa", "Sucesso").Vazia);

        var semPapeis = CelulaGrade.DePilulas(pilulas, null);
        Assert.True(semPapeis.Vazia);
        Assert.Equal(CelulaGrade.SemValor, semPapeis.Texto);

        var papeis = CelulaGrade.DePilulas(pilulas, [new("Cliente", "Informacao"), new("Fornecedor", "Aviso")]);
        Assert.False(papeis.Vazia);
        Assert.Equal("Cliente, Fornecedor", papeis.Texto);
        Assert.Equal(2, papeis.Selos.Count);
    }

    // ---------- Calculadora de larguras ----------

    [Fact]
    public void Sem_colunas_nao_ha_largura()
    {
        var r = CalculadoraLarguras.Calcular([], 1000);
        Assert.Empty(r.Larguras);
        Assert.Equal(0, r.Total);
        Assert.False(r.RolagemLateral);
    }

    [Fact]
    public void Proporcionais_dividem_o_espaco_livre_pelo_peso()
    {
        ColunaGradeDef[] colunas =
        [
            ColunaGradeDef.Fixa("a", "A", TipoCelula.Texto, 200),
            ColunaGradeDef.Proporcional("b", "B", TipoCelula.Texto, peso: 1, minima: 100),
            ColunaGradeDef.Proporcional("c", "C", TipoCelula.Texto, peso: 3, minima: 100),
        ];

        var r = CalculadoraLarguras.Calcular(colunas, 800);

        // Livre: 800 − 200 − 100 − 100 = 400, dividido 1:3 → +100 e +300.
        Larguras(r, 200, 100 + 100, 100 + 300);
        Assert.Equal(800, r.Total, Tol);
        Assert.False(r.RolagemLateral);
    }

    [Fact]
    public void Coluna_que_atinge_a_maxima_para_e_o_resto_vai_para_as_outras()
    {
        ColunaGradeDef[] colunas =
        [
            ColunaGradeDef.Proporcional("a", "A", TipoCelula.Texto, peso: 1, minima: 100, maxima: 150),
            ColunaGradeDef.Proporcional("b", "B", TipoCelula.Texto, peso: 1, minima: 100),
            ColunaGradeDef.Fixa("c", "C", TipoCelula.Texto, 100),
        ];

        var r = CalculadoraLarguras.Calcular(colunas, 700);

        Larguras(r, 150, 450, 100);
        Assert.Equal(700, r.Total, Tol);
    }

    [Fact]
    public void Sobra_sem_proporcional_para_receber_vai_para_a_ultima_coluna()
    {
        ColunaGradeDef[] soFixas =
        [
            ColunaGradeDef.Fixa("a", "A", TipoCelula.Texto, 200),
            ColunaGradeDef.Fixa("b", "B", TipoCelula.Texto, 100),
        ];
        Larguras(CalculadoraLarguras.Calcular(soFixas, 500), 200, 300);

        ColunaGradeDef[] todasNoLimite =
        [
            ColunaGradeDef.Proporcional("a", "A", TipoCelula.Texto, 1, 100, maxima: 150),
            ColunaGradeDef.Fixa("b", "B", TipoCelula.Texto, 100),
        ];
        var r = CalculadoraLarguras.Calcular(todasNoLimite, 500);
        Larguras(r, 150, 350);
        Assert.False(r.RolagemLateral);
    }

    [Fact]
    public void Nao_cabe_a_fixa_com_minima_encolhe_so_o_necessario_sem_rolagem()
    {
        // Pessoas: fixas 340+180+80+120 = 720; mínimas das proporcionais 160+160 = 320; precisa de 1.040.
        var r = CalculadoraLarguras.Calcular(ColunasPessoas(), 1000);

        Larguras(r, 300, 180, 80, 160, 160, 120);
        Assert.Equal(1000, r.Total, Tol);
        Assert.False(r.RolagemLateral);
    }

    [Fact]
    public void Nao_cabe_nem_na_minima_todas_ficam_na_minima_e_aparece_a_rolagem_lateral()
    {
        var r = CalculadoraLarguras.Calcular(ColunasPessoas(), 900);

        Larguras(r, 260, 180, 80, 160, 160, 120);
        Assert.Equal(960, r.Total, Tol);
        Assert.True(r.RolagemLateral);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(-50)]
    [InlineData(0)]
    public void Largura_disponivel_invalida_conta_como_zero(double disponivel)
    {
        var r = CalculadoraLarguras.Calcular(ColunasPessoas(), disponivel);

        Larguras(r, 260, 180, 80, 160, 160, 120);
        Assert.True(r.RolagemLateral);
    }

    /// <summary>
    /// A mesma tela (1920 px de largura útil) em 100, 125 e 150%: a regra é em pontos de tela, então a largura disponível
    /// cai com a escala; a soma fecha com o disponível enquanto couber e a rolagem lateral só aparece quando não cabe.
    /// </summary>
    [Theory]
    [InlineData(1.00, 1920.0, false)]
    [InlineData(1.25, 1920.0, false)]
    [InlineData(1.50, 1920.0, false)]
    [InlineData(1.50, 1350.0, true)]   // 150% com prévia e painel de filtros abertos: 900 pontos úteis
    public void Mesma_regra_nas_escalas(double escala, double pixels, bool rolagem)
    {
        var pontos = pixels / escala;
        var colunas = ColunasPessoas();
        var r = CalculadoraLarguras.Calcular(colunas, pontos);

        Assert.Equal(rolagem, r.RolagemLateral);
        if (!rolagem)
        {
            Assert.Equal(pontos, r.Total, Tol);
            Assert.Equal(340, r.Larguras[0], Tol);                       // fixas não mudam quando cabe
            Assert.Equal(r.Larguras[3], r.Larguras[4], Tol);             // pesos iguais, larguras iguais
            Assert.True(r.Larguras[3] >= 160);
        }
        else
        {
            Assert.All(r.Larguras.Zip(colunas), p => Assert.Equal(p.Second.Minima, p.First, Tol));
        }
    }

    [Fact]
    public void Largura_varia_sem_saltos_ao_estreitar_a_janela()
    {
        var colunas = ColunasPessoas();
        double? anterior = null;
        for (var disponivel = 1400.0; disponivel >= 800; disponivel -= 5)
        {
            var total = CalculadoraLarguras.Calcular(colunas, disponivel).Total;
            if (anterior is { } a) Assert.True(a - total <= 5 + Tol, $"salto em {disponivel}: {a} → {total}");
            Assert.True(total >= Math.Min(disponivel, 960) - Tol);
            anterior = total;
        }
    }

    [Fact]
    public void Aplicar_so_avisa_quem_mudou_e_diz_se_algo_mudou()
    {
        var colunas = ColunasPessoas();
        var avisadas = new List<string>();
        foreach (var c in colunas)
            c.PropertyChanged += (s, e) => avisadas.Add(((ColunaGradeDef)s!).Chave + "." + e.PropertyName);

        Assert.True(CalculadoraLarguras.Aplicar(colunas, CalculadoraLarguras.Calcular(colunas, 1280)));
        Assert.Equal(new[] { "papeis.LarguraEfetiva", "cidade.LarguraEfetiva" }, avisadas);

        avisadas.Clear();
        Assert.False(CalculadoraLarguras.Aplicar(colunas, CalculadoraLarguras.Calcular(colunas, 1280)));
        Assert.Empty(avisadas);

        Assert.True(CalculadoraLarguras.Aplicar(colunas, CalculadoraLarguras.Calcular(colunas, 900)));
        Assert.Equal(new[] { "nome.LarguraEfetiva", "papeis.LarguraEfetiva", "cidade.LarguraEfetiva" }, avisadas);
    }

    [Fact]
    public void Aplicar_recusa_calculo_de_outro_conjunto_de_colunas()
    {
        var colunas = ColunasPessoas();
        var outro = CalculadoraLarguras.Calcular(colunas.Take(3).ToList(), 1000);
        Assert.Throws<ArgumentException>(() => CalculadoraLarguras.Aplicar(colunas, outro));
    }

    // ---------- Âncora lógica ----------

    [Theory]
    [InlineData(5, 50, 5)]
    [InlineData(0, 50, 0)]
    [InlineData(-3, 50, 0)]
    [InlineData(49, 50, 49)]
    [InlineData(120, 50, 49)]
    [InlineData(7, 1, 0)]
    public void Ancora_limita_o_indice_a_lista_atual(int indice, int quantidade, int esperado) =>
        Assert.Equal(esperado, AncoraLogica.Limitar(indice, quantidade));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Ancora_em_lista_vazia_nao_restaura(int quantidade) =>
        Assert.Null(AncoraLogica.Limitar(10, quantidade));

    [Theory]
    [InlineData(30, 30, false, true)]   // chegou
    [InlineData(30, 22, true, true)]    // perto do fim: não dá para subir mais
    [InlineData(30, 22, false, false)]  // parou antes e ainda dá para rolar: tentar de novo
    [InlineData(30, 31, false, false)]  // passou do alvo
    [InlineData(30, 31, true, false)]
    [InlineData(30, -1, true, false)]   // motor ainda sem linhas visíveis
    public void Ancora_confirma_o_primeiro_visivel(int alvo, int primeiro, bool noFim, bool esperado) =>
        Assert.Equal(esperado, AncoraLogica.Confirmada(alvo, primeiro, noFim));

    [Fact]
    public void Ancora_acha_o_registro_pela_chave()
    {
        var a = new LinhaTeste(Guid.NewGuid());
        var b = new LinhaTeste(Guid.NewGuid());
        ILinhaGrade[] linhas = [a, b];

        Assert.Equal(1, AncoraLogica.IndiceDaChave(linhas, b.Chave));
        Assert.Null(AncoraLogica.IndiceDaChave(linhas, Guid.NewGuid()));
        Assert.Null(AncoraLogica.IndiceDaChave(linhas, null));
        Assert.Null(AncoraLogica.IndiceDaChave([], a.Chave));
    }

    [Fact]
    public void Linha_da_grade_avisa_destaque_e_selecao()
    {
        var linha = new LinhaTeste(Guid.NewGuid());
        var avisos = new List<string?>();
        ((ILinhaGrade)linha).PropertyChanged += (_, e) => avisos.Add(e.PropertyName);

        linha.Destacada = true;
        linha.Selecionada = true;

        Assert.Equal(new string?[] { nameof(ILinhaGrade.Destacada), nameof(ILinhaGrade.Selecionada) }, avisos);
    }

    // ---------- Conteúdo (troca atômica) ----------

    private static LinhaTeste Linha(params CelulaGrade[] celulas) => new(Guid.NewGuid()) { Celulas = celulas };

    [Fact]
    public void Conteudo_aceita_linhas_coerentes_com_as_colunas()
    {
        var doc = ColunaGradeDef.Fixa("doc", "Doc", TipoCelula.Texto, 180);
        var sit = ColunaGradeDef.Fixa("sit", "Situação", TipoCelula.Selo, 120);
        ColunaGradeDef[] colunas = [doc, sit];
        ILinhaGrade[] linhas =
        [
            Linha(CelulaGrade.DeTexto(doc, "1"), CelulaGrade.DeSelo(sit, "Ativa", "Sucesso")),
            Linha(CelulaGrade.DeTexto(doc, "2"), CelulaGrade.DeSelo(sit, "Inativa", "Neutro")),
        ];

        var conteudo = new ConteudoGrade(colunas, linhas);

        Assert.Same(colunas, conteudo.Colunas);
        Assert.Same(linhas, conteudo.Linhas);
        Assert.Empty(ConteudoGrade.Vazio.Colunas);
        Assert.Empty(ConteudoGrade.Vazio.Linhas);
    }

    [Fact]
    public void Conteudo_recusa_linha_com_celulas_a_mais_a_menos_ou_fora_de_ordem()
    {
        var doc = ColunaGradeDef.Fixa("doc", "Doc", TipoCelula.Texto, 180);
        var cid = ColunaGradeDef.Fixa("cid", "Cidade", TipoCelula.Texto, 160);
        ColunaGradeDef[] colunas = [doc, cid];

        Assert.Throws<ArgumentException>(() => new ConteudoGrade(colunas, [Linha(CelulaGrade.DeTexto(doc, "1"))]));
        Assert.Throws<ArgumentException>(() => new ConteudoGrade(colunas,
            [Linha(CelulaGrade.DeTexto(doc, "1"), CelulaGrade.DeTexto(cid, "BH"), CelulaGrade.DeTexto(cid, "BH"))]));
        Assert.Throws<ArgumentException>(() => new ConteudoGrade(colunas,
            [Linha(CelulaGrade.DeTexto(cid, "BH"), CelulaGrade.DeTexto(doc, "1"))]));
    }

    [Fact]
    public void Conteudo_sabe_quando_so_as_linhas_mudaram()
    {
        var doc = ColunaGradeDef.Fixa("doc", "Doc", TipoCelula.Texto, 180);
        var cid = ColunaGradeDef.Fixa("cid", "Cidade", TipoCelula.Texto, 160);
        var a = new ConteudoGrade([doc, cid], []);

        Assert.True(a.MesmasColunas(new ConteudoGrade([doc, cid], [])));
        Assert.False(a.MesmasColunas(new ConteudoGrade([cid, doc], [])));
        Assert.False(a.MesmasColunas(new ConteudoGrade([doc], [])));
        Assert.False(a.MesmasColunas(new ConteudoGrade([doc, ColunaGradeDef.Fixa("cid", "Cidade", TipoCelula.Texto, 160)], [])));
        Assert.False(a.MesmasColunas(null));
    }

    /// <summary>Linha mínima de teste (sem gerador de código): avisa destaque e seleção.</summary>
    private sealed class LinhaTeste(Guid chave) : ILinhaGrade
    {
        private bool _destacada, _selecionada;

        public event PropertyChangedEventHandler? PropertyChanged;

        public Guid Chave { get; } = chave;
        public IReadOnlyList<CelulaGrade> Celulas { get; init; } = [];

        public bool Destacada
        {
            get => _destacada;
            set { _destacada = value; PropertyChanged?.Invoke(this, new(nameof(Destacada))); }
        }

        public bool Selecionada
        {
            get => _selecionada;
            set { _selecionada = value; PropertyChanged?.Invoke(this, new(nameof(Selecionada))); }
        }
    }
}
