using Lone.Cliente.ViewModels.Comercial;
using Lone.Contracts.Comercial;

namespace Lone.Tests.Cliente;

/// <summary>Carteira vencendo (03/10/2026): filtro, resumo, cor do prazo, opções dos filtros e impressão.</summary>
public class CarteiraVencendoTests
{
    private static VinculoVencendoDto V(string cliente, string papel, string pessoa, int dias, string? empresa = null) => new()
    {
        VinculoId = Guid.NewGuid(), ClienteId = Guid.NewGuid(), Cliente = cliente, Papel = papel, Pessoa = pessoa, Empresa = empresa,
        InicioEm = new DateOnly(2026, 1, 1), FimEm = new DateOnly(2026, 10, 3).AddDays(dias), DiasRestantes = dias
    };

    private static readonly VinculoVencendoDto[] Todos =
    [
        V("Açougue São José", "Vendedor", "Diego", 40, "Matriz"),
        V("Padaria Central", "Vendedor", "Edna", 5, "Filial"),
        V("Mercado Bom Preço", "Representante", "Diego", 0, "Matriz"),
        V("Bar do Zé", "Vendedor", "Diego", 12, "Matriz")
    ];

    [Fact]
    public void Filtro_por_papel_pessoa_empresa_e_cliente_sem_acento_ordenado_pelo_fim()
    {
        var diego = CarteiraVencendo.Filtrar(Todos, null, "Diego", null, null);
        Assert.Equal(new[] { "Mercado Bom Preço", "Bar do Zé", "Açougue São José" }, diego.Select(v => v.Cliente));
        Assert.Single(CarteiraVencendo.Filtrar(Todos, "Representante", null, null, null));
        Assert.Single(CarteiraVencendo.Filtrar(Todos, null, null, "Filial", null));
        Assert.Equal("Açougue São José", CarteiraVencendo.Filtrar(Todos, null, null, null, "acougue sao").Single().Cliente);
        Assert.Empty(CarteiraVencendo.Filtrar(Todos, "Representante", "Edna", null, null));
    }

    [Fact]
    public void Resumo_conta_semana_e_hoje()
    {
        Assert.Equal("4 vínculos terminam nos próximos 90 dias · 1 nesta semana · 1 hoje", CarteiraVencendo.Resumo(Todos, 90));
        Assert.Equal("Nenhum vínculo termina no prazo do aviso configurado", CarteiraVencendo.Resumo([], null));
        Assert.Equal("1 vínculo termina nos próximos 30 dias", CarteiraVencendo.Resumo([Todos[3]], 30));
    }

    [Theory]
    [InlineData(0, "Termina hoje", "Erro")]
    [InlineData(1, "Falta 1 dia", "Erro")]
    [InlineData(7, "Faltam 7 dias", "Erro")]
    [InlineData(8, "Faltam 8 dias", "Aviso")]
    [InlineData(30, "Faltam 30 dias", "Aviso")]
    [InlineData(31, "Faltam 31 dias", "Neutro")]
    public void Prazo_e_cor(int dias, string texto, string tom)
    {
        Assert.Equal(texto, CarteiraVencendo.Faltam(dias));
        Assert.Equal(tom, CarteiraVencendo.Tom(dias));
    }

    [Fact]
    public void Opcoes_dos_filtros_vem_dos_vencimentos()
    {
        var pessoas = CarteiraVencendo.Opcoes(Todos.Select(v => v.Pessoa), CarteiraVencendo.Todos);
        Assert.Equal(new[] { "Todos", "Diego", "Edna" }, pessoas.Select(o => o.Texto));
        Assert.Null(pessoas[0].Valor);
    }

    [Fact]
    public void Impressao_escapa_texto_e_mostra_filtro()
    {
        var html = CarteiraVencendoImpressao.Html("Quem atende: Diego", [new LinhaVencendo(V("A & B <Ltda>", "Vendedor", "Diego", 3))],
            "1 vínculo termina nos próximos 30 dias", new DateTime(2026, 10, 3, 15, 30, 0));
        Assert.Contains("A &amp; B &lt;Ltda&gt;", html);
        Assert.Contains("Quem atende: Diego", html);
        Assert.Contains("gerado em 03/10/2026 15:30", html);
        Assert.Contains("window.print()", html);
    }

    [Fact]
    public void Periodo_mostra_o_aviso_real_os_prazos_e_personalizado()
    {
        var periodos = CarteiraVencendo.Periodos(30);
        Assert.Equal("Aviso padrão (30 dias)", periodos[0].Texto);
        Assert.Null(periodos[0].Dias);
        Assert.Equal("Próximos 7 dias", periodos[1].Texto);
        Assert.True(periodos[^1].Personalizado);
        Assert.Equal("Aviso padrão", CarteiraVencendo.Periodos(null)[0].Texto);
    }

    [Fact]
    public void Faixas_dos_atalhos()
    {
        Assert.Equal(1, Todos.Count(v => CarteiraVencendo.NaFaixa(v, FaixaVencimento.Hoje)));
        Assert.Equal(2, Todos.Count(v => CarteiraVencendo.NaFaixa(v, FaixaVencimento.AteSete)));
        Assert.Equal(3, Todos.Count(v => CarteiraVencendo.NaFaixa(v, FaixaVencimento.AteTrinta)));
        Assert.Equal(2, CarteiraVencendo.Filtrar(Todos, null, "Diego", null, null, FaixaVencimento.AteTrinta).Count);
    }

    [Fact]
    public void Ordem_pela_coluna_e_inversa()
    {
        Assert.Equal("Açougue São José", CarteiraVencendo.Ordenar(Todos, "titulo", false)[0].Cliente);
        Assert.Equal("Padaria Central", CarteiraVencendo.Ordenar(Todos, "titulo", true)[0].Cliente);
        Assert.Equal("Açougue São José", CarteiraVencendo.Ordenar(Todos, "faltam", true)[0].Cliente);
    }

    [Fact]
    public void Lista_vazia_oferece_o_proximo_prazo_e_mostra_o_periodo()
    {
        Assert.Equal(60, CarteiraVencendo.ProximoPrazo(30));
        Assert.Equal(90, CarteiraVencendo.ProximoPrazo(60));
        Assert.Null(CarteiraVencendo.ProximoPrazo(365));
        Assert.Equal("Período consultado: 03/10/2026 a 02/11/2026",
            CarteiraVencendo.TextoPeriodo(new DateOnly(2026, 10, 3), new DateOnly(2026, 11, 2)));
        Assert.Equal("Vínculos vencendo (12)", CarteiraVencendo.TituloLista(12));
    }

    [Fact]
    public void Csv_para_o_Excel_com_ponto_e_virgula_e_aspas()
    {
        var csv = CarteiraVencendo.Csv([new LinhaVencendo(V("Bar; do \"Zé\"", "Vendedor", "Diego", 3, "Matriz"))]);
        var linhas = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Cliente;Papel;Quem atende;Empresa;Início;Fim;Faltam (dias)", linhas[0]);
        Assert.StartsWith("\"Bar; do \"\"Zé\"\"\";Vendedor;Diego;Matriz;01/01/2026;06/10/2026;3", linhas[1]);
    }

    [Fact]
    public void Atalho_que_repete_o_periodo_some_e_a_lista_vazia_diz_o_periodo_numa_frase()
    {
        Assert.False(CarteiraVencendo.FaixaUtil(FaixaVencimento.AteTrinta, 30)); // igual a "Todos"
        Assert.True(CarteiraVencendo.FaixaUtil(FaixaVencimento.AteTrinta, 60));
        Assert.False(CarteiraVencendo.FaixaUtil(FaixaVencimento.AteSete, 7));
        Assert.True(CarteiraVencendo.FaixaUtil(FaixaVencimento.AteSete, 15));
        Assert.True(CarteiraVencendo.FaixaUtil(FaixaVencimento.Hoje, 7));
        Assert.True(CarteiraVencendo.FaixaUtil(FaixaVencimento.Todos, 7));

        Assert.Equal("Nenhum vínculo termina nos próximos 30 dias", CarteiraVencendo.TituloSemVinculos(30));
        Assert.Equal("Nenhum vínculo termina até amanhã", CarteiraVencendo.TituloSemVinculos(1));
        Assert.Equal("Nenhum vínculo termina hoje", CarteiraVencendo.TituloSemVinculos(0));
    }
}
