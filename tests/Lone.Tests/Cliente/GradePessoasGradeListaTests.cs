using System.ComponentModel;
using Lone.Cliente.Grade;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enums;
using GradeCelula = Lone.Cliente.Grade.CelulaGrade;

namespace Lone.Tests.Cliente;

/// <summary>
/// P2-B2, Etapa 3 (passo 3.1): a lista de pessoas no formato da GradeLista — regra de largura aprovada (P2), células
/// sem largura (avisos como texto com tom), linha pela chave, seleção = prévia, altura na grade e troca coerente.
/// </summary>
public class GradePessoasGradeListaTests
{
    private static List<ColunaListaDto> Colunas() =>
    [
        new() { Id = CamposFiltroPessoas.Documento, Grupo = "Identificação", Nome = "CPF / CNPJ", Tipo = TipoColunaLista.Documento, Largura = 180, Padrao = true },
        new() { Id = CamposFiltroPessoas.Natureza, Grupo = "Identificação", Nome = "Tipo", Tipo = TipoColunaLista.Natureza, Largura = 80, Padrao = true },
        new() { Id = CamposFiltroPessoas.Papeis, Grupo = "Identificação", Nome = "Papéis", Tipo = TipoColunaLista.Papeis, Largura = 210, Padrao = true },
        new() { Id = CamposFiltroPessoas.Cidade, Grupo = "Endereços", Nome = "Cidade", Tipo = TipoColunaLista.Cidade, Largura = 200, Padrao = true },
        new() { Id = CamposFiltroPessoas.Uf, Grupo = "Endereços", Nome = "UF", Tipo = TipoColunaLista.Uf, Largura = 80, Padrao = true },
        new() { Id = CamposFiltroPessoas.Situacao, Grupo = "Situação", Nome = "Situação", Tipo = TipoColunaLista.Situacao, Largura = 120, Padrao = true },
        new() { Id = CamposFiltroPessoas.Bairro, Grupo = "Endereços", Nome = "Bairro", Tipo = TipoColunaLista.Texto, Largura = 150 }
    ];

    private static GradePessoas Montar(LayoutListaPessoas? layout = null)
    {
        var grade = new GradePessoas();
        grade.Carregar(Colunas(), layout, new PainelFiltrosPessoas());
        return grade;
    }

    private static PessoaResumo Pessoa(string nome = "Ana") => new()
    {
        Id = Guid.NewGuid(), Codigo = 7, Nome = nome, Natureza = NaturezaPessoa.Fisica, Situacao = SituacaoPessoa.Ativo,
        DocumentoPrincipal = "52998224725", Cidade = "Curvelo", Uf = "MG", Papeis = ["Cliente", "Fornecedor"]
    };

    private static ColunaGradeDef Def(GradePessoas g, string id) => g.Visiveis.Single(c => c.Id == id).Def;

    [Fact]
    public void Colunas_seguem_a_regra_de_largura_aprovada()
    {
        var grade = Montar(new LayoutListaPessoas { Colunas = [.. Colunas().Select(c => c.Id)] });

        // O nome é a coluna que mais cresce (peso 3) e encolhe até 260.
        Assert.Equal(ModoLargura.Proporcional, grade.ColunaFixa.Modo);
        Assert.Equal(3, grade.ColunaFixa.Peso);
        Assert.Equal(260, grade.ColunaFixa.Minima);

        Assert.Equal((ModoLargura.Fixa, 180.0, TipoCelula.Texto), Regra(Def(grade, CamposFiltroPessoas.Documento)));
        Assert.Equal((ModoLargura.Fixa, 80.0, TipoCelula.Selo), Regra(Def(grade, CamposFiltroPessoas.Natureza)));
        Assert.Equal((ModoLargura.Fixa, 120.0, TipoCelula.Selo), Regra(Def(grade, CamposFiltroPessoas.Situacao)));
        Assert.Equal((ModoLargura.Proporcional, 160.0, TipoCelula.Pilulas), Regra(Def(grade, CamposFiltroPessoas.Papeis)));
        Assert.Equal((ModoLargura.Proporcional, 160.0, TipoCelula.Texto), Regra(Def(grade, CamposFiltroPessoas.Cidade)));
        Assert.Equal((ModoLargura.Fixa, 80.0, TipoCelula.Texto), Regra(Def(grade, CamposFiltroPessoas.Uf))); // dado curto
        Assert.Equal(1, Def(grade, CamposFiltroPessoas.Papeis).Peso);
        Assert.Equal(1, Def(grade, CamposFiltroPessoas.Cidade).Peso);
        // Coluna extra de texto livre: proporcional de peso 1, com a largura do catálogo como mínima.
        Assert.Equal((ModoLargura.Proporcional, 150.0, TipoCelula.Texto), Regra(Def(grade, CamposFiltroPessoas.Bairro)));
        Assert.Equal(1, Def(grade, CamposFiltroPessoas.Bairro).Peso);
        Assert.Equal("CPF / CNPJ".ToUpperInvariant(), Def(grade, CamposFiltroPessoas.Documento).Titulo);

        static (ModoLargura, double, TipoCelula) Regra(ColunaGradeDef d) => (d.Modo, d.Modo == ModoLargura.Fixa ? d.Largura : d.Minima, d.Tipo);
    }

    [Fact]
    public void Linha_traz_as_celulas_da_grade_na_ordem_das_colunas_e_o_conteudo_e_coerente()
    {
        var grade = Montar();
        var pessoas = new[] { Pessoa("Ana"), Pessoa("Bia") };
        var linhas = pessoas.Select(grade.Linha).ToList();

        var conteudo = grade.Conteudo(linhas); // confere célula i = coluna i (lança se não)
        Assert.Equal(grade.Visiveis.Select(c => c.Def), conteudo.Colunas);
        Assert.Equal(2, conteudo.Linhas.Count);

        ILinhaGrade linha = linhas[0];
        Assert.Equal(pessoas[0].Id, linha.Chave);
        Assert.Equal(6, linha.Celulas.Count);
        Assert.Equal("529.982.247-25", linha.Celulas[0].Texto);
        Assert.Null(linha.Celulas[0].Tom);
        Assert.Equal(TipoCelula.Selo, linha.Celulas[1].Tipo);
        Assert.Equal(new[] { "Informacao", "Aviso" }, linha.Celulas[2].Selos.Select(s => s.Tom).ToArray());
        Assert.Equal("Curvelo", linha.Celulas[3].Texto); // a UF tem coluna própria
        Assert.Equal("MG", linha.Celulas[4].Texto);
        Assert.Equal("Sucesso", linha.Celulas[5].Tom);
        // A tela atual continua com as células antigas: mesma quantidade e mesmos textos (fora os papéis, que a grade
        // nova junta com vírgula para a dica).
        Assert.Equal(linhas[0].Celulas.Count, linha.Celulas.Count);
        foreach (var i in new[] { 0, 1, 3, 4, 5 }) Assert.Equal(linhas[0].Celulas[i].Texto, linha.Celulas[i].Texto);
    }

    [Fact]
    public void Avisos_viram_texto_com_tom_sem_trocar_o_molde_da_coluna()
    {
        var grade = Montar();
        var pessoa = Pessoa();
        pessoa.DocumentoPrincipal = null;
        pessoa.MunicipioACorrigir = true;

        ILinhaGrade linha = grade.Linha(pessoa);
        var documento = linha.Celulas[0];
        var cidade = linha.Celulas[3];
        Assert.Equal(TipoCelula.Texto, documento.Tipo);
        Assert.Equal("Sem CPF", documento.Texto);
        Assert.Equal("Aviso", documento.Tom);
        Assert.Equal(TipoCelula.Texto, cidade.Tipo);
        Assert.Equal("Curvelo (a corrigir)", cidade.Texto);
        Assert.Equal("Aviso", cidade.Tom);
    }

    [Fact]
    public void Selecionada_acompanha_a_previa_e_avisa_a_grade()
    {
        var linha = Montar().Linha(Pessoa());
        var avisos = new List<string?>();
        ((INotifyPropertyChanged)linha).PropertyChanged += (_, e) => avisos.Add(e.PropertyName);

        Assert.False(((ILinhaGrade)linha).Selecionada);
        linha.NaPrevia = true;
        Assert.True(((ILinhaGrade)linha).Selecionada);
        Assert.Contains(nameof(ILinhaGrade.Selecionada), avisos);

        linha.Destacada = true;
        Assert.True(((ILinhaGrade)linha).Destacada);
    }

    [Fact]
    public void Altura_fica_na_grade_densidade_e_cartao_avisam_sem_depender_da_linha()
    {
        var grade = Montar();
        var avisos = new List<string?>();
        grade.PropertyChanged += (_, e) => avisos.Add(e.PropertyName);

        Assert.Equal(LinhaPessoa.AlturaConfortavel, grade.AlturaLinha);
        grade.AlternarDensidadeCommand.Execute(null);
        Assert.Equal(LinhaPessoa.AlturaCompacta, grade.AlturaLinha);
        Assert.Contains(nameof(GradePessoas.AlturaLinha), avisos);

        avisos.Clear();
        grade.MostrarColunas = false; // cartão: sem colunas de célula
        Assert.Equal(LinhaPessoa.AlturaCartao, grade.AlturaLinha);
        Assert.Contains(nameof(GradePessoas.AlturaLinha), avisos);
        Assert.Empty(grade.ColunasGrade);
        var cartao = grade.Conteudo([grade.Linha(Pessoa())]);
        Assert.Empty(cartao.Colunas);
        Assert.Empty(cartao.Linhas[0].Celulas);
    }

    [Fact]
    public void Colunas_sao_as_mesmas_instancias_enquanto_a_escolha_nao_muda()
    {
        var grade = Montar(new LayoutListaPessoas { Colunas = [CamposFiltroPessoas.Documento, CamposFiltroPessoas.Situacao] });
        var antes = grade.Conteudo([grade.Linha(Pessoa())]);
        var depois = grade.Conteudo([grade.Linha(Pessoa("Bia"))]); // só as linhas mudaram (pesquisa, página)
        Assert.True(depois.MesmasColunas(antes));

        grade.SubirCommand.Execute(grade.NaLista.Single(i => i.Id == CamposFiltroPessoas.Situacao)); // ordem nova: colunas outras
        var reordenado = grade.Conteudo([grade.Linha(Pessoa())]);
        Assert.False(reordenado.MesmasColunas(antes));
        Assert.Same(antes.Colunas[0], reordenado.Colunas[1]); // a coluna é a mesma, só mudou de lugar
    }

    [Fact]
    public void Titulo_da_grade_ordena_em_tres_toques_e_informa_a_seta()
    {
        var grade = Montar();
        var avisos = new List<string?>();
        grade.PropertyChanged += (_, e) => avisos.Add(e.PropertyName);
        var documento = Def(grade, CamposFiltroPessoas.Documento);

        grade.OrdenarColunaCommand.Execute(documento);
        Assert.Equal((CamposFiltroPessoas.Documento, false), (grade.ColunaOrdenadaChave!, grade.OrdemDecrescente));
        Assert.Contains(nameof(GradePessoas.ColunaOrdenadaChave), avisos);
        grade.OrdenarColunaCommand.Execute(documento);
        Assert.True(grade.OrdemDecrescente);
        grade.OrdenarColunaCommand.Execute(documento);
        Assert.Null(grade.ColunaOrdenadaChave);

        grade.OrdenarColunaCommand.Execute(grade.ColunaFixa); // o nome também ordena
        Assert.Equal(ColunasPessoas.Nome, grade.ColunaOrdenadaChave);
    }

    [Fact]
    public void Ponteiro_da_grade_acende_e_apaga_a_linha()
    {
        var grade = Montar();
        var linha = grade.Linha(Pessoa());
        grade.EntrarNaLinhaCommand.Execute(linha);
        Assert.True(linha.Destacada);
        grade.SairDaLinhaCommand.Execute(linha);
        Assert.False(linha.Destacada);
    }

    [Fact]
    public void Nome_ocupa_o_espaco_livre_e_dado_curto_nao_cresce()
    {
        var codigo = new ColunaListaDto { Id = "cliente.codigo", Grupo = "Identificação", Nome = "Código", Tipo = TipoColunaLista.Codigo, Largura = 150 };
        var data = new ColunaListaDto { Id = CamposFiltroPessoas.CadastradoEm, Grupo = "Cadastro", Nome = "Cadastrado em", Tipo = TipoColunaLista.Data, Largura = 140 };
        var bairro = Colunas().Single(c => c.Id == CamposFiltroPessoas.Bairro);
        var grade = new GradePessoas();
        grade.Carregar([codigo, data, bairro], new LayoutListaPessoas { Colunas = [codigo.Id] }, new PainelFiltrosPessoas());

        // Nome + Código numa tabela de 1180: o código fica com a largura dele; o nome com todo o resto.
        var r = CalculadoraLarguras.Calcular([grade.ColunaFixa, .. grade.ColunasGrade], 1180);
        Assert.Equal(1030, r.Larguras[0], 3);
        Assert.Equal(150, r.Larguras[1], 3);
        Assert.False(r.RolagemLateral);

        // Com um texto livre (bairro, peso 1), o nome continua crescendo mais (peso 3); data e código não crescem.
        grade.AplicarLayout(new LayoutListaPessoas { Colunas = [codigo.Id, data.Id, bairro.Id] });
        r = CalculadoraLarguras.Calcular([grade.ColunaFixa, .. grade.ColunasGrade], 1180);
        Assert.Equal(150, r.Larguras[1], 3);
        Assert.Equal(140, r.Larguras[2], 3);
        Assert.Equal(1180 - 150 - 140, r.Larguras[0] + r.Larguras[3], 3);
        Assert.True(r.Larguras[0] - 260 > 2.9 * (r.Larguras[3] - 150)); // sobra dividida 3 : 1

        // Sem espaço: o nome encolhe até 260 e a tabela rola para o lado.
        r = CalculadoraLarguras.Calcular([grade.ColunaFixa, .. grade.ColunasGrade], 500);
        Assert.Equal(260, r.Larguras[0], 3);
        Assert.True(r.RolagemLateral);
    }

    [Fact]
    public void Celula_de_texto_aceita_tom_opcional_no_nucleo()
    {
        var coluna = ColunaGradeDef.Fixa("doc", "CPF", TipoCelula.Texto, 180);
        Assert.Null(GradeCelula.DeTexto(coluna, "123").Tom);
        var aviso = GradeCelula.DeTexto(coluna, "Sem CPF", "Aviso");
        Assert.Equal(TipoCelula.Texto, aviso.Tipo);
        Assert.Equal("Aviso", aviso.Tom);
        Assert.False(aviso.Vazia);
    }
}
