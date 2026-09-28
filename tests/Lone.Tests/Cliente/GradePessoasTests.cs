using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enums;

namespace Lone.Tests.Cliente;

/// <summary>Colunas da lista de pessoas: escolha, ordem, ordenação em 3 cliques, formatação e linha de filtro das colunas.</summary>
public class GradePessoasTests
{
    private static CatalogoFiltrosPessoasDto Catalogo() => new()
    {
        Campos =
        [
            new() { Id = CamposFiltroPessoas.Nome, Grupo = "Identificação", Nome = "Nome", Tipo = TipoCampoFiltro.Texto,
                    Operadores = [OperadorFiltro.Contem, OperadorFiltro.ComecaCom, OperadorFiltro.Igual] },
            new() { Id = CamposFiltroPessoas.Bairro, Grupo = "Endereços", Nome = "Bairro", Tipo = TipoCampoFiltro.Texto,
                    Operadores = [OperadorFiltro.Contem, OperadorFiltro.ComecaCom, OperadorFiltro.Igual] },
            new() { Id = CamposFiltroPessoas.Situacao, Grupo = "Situação", Nome = "Situação do cadastro", Tipo = TipoCampoFiltro.Lista,
                    Operadores = [OperadorFiltro.UmDestes, OperadorFiltro.NenhumDestes],
                    Opcoes = [new("Ativo", "Ativo"), new("Inativo", "Inativo")] },
            new() { Id = CamposFiltroPessoas.LimiteCredito, Grupo = "Comercial", Nome = "Limite", Tipo = TipoCampoFiltro.Numero,
                    Operadores = [OperadorFiltro.Entre, OperadorFiltro.APartirDe, OperadorFiltro.Ate] },
            new() { Id = CamposFiltroPessoas.CadastradoEm, Grupo = "Cadastro", Nome = "Cadastrado em", Tipo = TipoCampoFiltro.Data,
                    Operadores = [OperadorFiltro.Entre, OperadorFiltro.APartirDe, OperadorFiltro.Ate] }
        ],
        Colunas =
        [
            new() { Id = CamposFiltroPessoas.Documento, Grupo = "Identificação", Nome = "CPF / CNPJ", Tipo = TipoColunaLista.Documento, Largura = 180, Padrao = true },
            new() { Id = CamposFiltroPessoas.Sexo, Grupo = "Dados pessoais", Nome = "Sexo", Tipo = TipoColunaLista.Opcao, Largura = 120,
                    Opcoes = [new("Feminino", "Feminino"), new("NaoInformado", "Não informado")] },
            new() { Id = CamposFiltroPessoas.Telefone, Grupo = "Contatos", Nome = "Telefone principal", Tipo = TipoColunaLista.Telefone, Largura = 160 },
            new() { Id = CamposFiltroPessoas.Bairro, Grupo = "Endereços", Nome = "Bairro", Tipo = TipoColunaLista.Texto, Largura = 150, CampoFiltro = CamposFiltroPessoas.Bairro },
            new() { Id = CamposFiltroPessoas.LimiteCredito, Grupo = "Comercial", Nome = "Limite", Tipo = TipoColunaLista.Moeda, Largura = 150, CampoFiltro = CamposFiltroPessoas.LimiteCredito },
            new() { Id = CamposFiltroPessoas.Situacao, Grupo = "Situação", Nome = "Situação", Tipo = TipoColunaLista.Situacao, Largura = 120, Padrao = true, CampoFiltro = CamposFiltroPessoas.Situacao },
            new() { Id = CamposFiltroPessoas.CadastradoEm, Grupo = "Cadastro", Nome = "Cadastrado em", Tipo = TipoColunaLista.Data, Largura = 140, CampoFiltro = CamposFiltroPessoas.CadastradoEm }
        ]
    };

    private static (GradePessoas Grade, PainelFiltrosPessoas Painel, List<MudancaGrade> Mudancas) Montar(LayoutListaPessoas? layout = null)
    {
        var catalogo = Catalogo();
        var painel = new PainelFiltrosPessoas();
        painel.Carregar(catalogo);
        var grade = new GradePessoas();
        grade.Carregar(catalogo.Colunas, layout, painel);
        var mudancas = new List<MudancaGrade>();
        grade.Mudou = mudancas.Add;
        return (grade, painel, mudancas);
    }

    private static ColunaGrade Coluna(GradePessoas g, string id) => g.Visiveis.Single(c => c.Id == id);
    private static ItemSeletorColuna Item(GradePessoas g, string id) => g.Grupos.SelectMany(x => x.Itens).Single(i => i.Id == id);

    [Fact]
    public void Sem_escolha_guardada_mostra_o_padrao_e_nao_pede_consulta_extra()
    {
        var (grade, _, _) = Montar();
        Assert.Equal(new[] { CamposFiltroPessoas.Documento, CamposFiltroPessoas.Situacao }, grade.Visiveis.Select(c => c.Id).ToArray());
        Assert.Empty(grade.ColunasExtras());
        Assert.Equal("Colunas (3)", grade.TextoBotaoColunas); // + o nome
        Assert.Equal(300, grade.LarguraColunas);
    }

    [Fact]
    public void Escolha_guardada_volta_na_mesma_ordem_e_ignora_coluna_sem_permissao()
    {
        var (grade, _, _) = Montar(new LayoutListaPessoas
        {
            Colunas = [CamposFiltroPessoas.Bairro, "cliente.coluna-sem-permissao", CamposFiltroPessoas.Documento],
            Ordenacao = new OrdenacaoLista { Coluna = CamposFiltroPessoas.Bairro, Direcao = DirecaoOrdenacao.Decrescente },
            FiltroNasColunas = true
        });
        Assert.Equal(new[] { CamposFiltroPessoas.Bairro, CamposFiltroPessoas.Documento }, grade.Visiveis.Select(c => c.Id).ToArray());
        Assert.Equal(new[] { CamposFiltroPessoas.Bairro }, grade.ColunasExtras());
        Assert.Equal("▼", Coluna(grade, CamposFiltroPessoas.Bairro).Seta);
        Assert.True(grade.FiltroNasColunas);
        Assert.Contains("Bairro", grade.TextoOrdenacao);
    }

    [Fact]
    public void Titulo_ordena_em_tres_cliques_crescente_decrescente_e_padrao()
    {
        var (grade, _, mudancas) = Montar();
        var documento = Coluna(grade, CamposFiltroPessoas.Documento);

        documento.OrdenarCommand.Execute(null);
        Assert.Equal(DirecaoOrdenacao.Crescente, grade.Ordenacao!.Direcao);
        Assert.Equal("▲", documento.Seta);

        documento.OrdenarCommand.Execute(null);
        Assert.Equal(DirecaoOrdenacao.Decrescente, grade.Ordenacao!.Direcao);
        Assert.Equal("▼", documento.Seta);

        documento.OrdenarCommand.Execute(null);
        Assert.Null(grade.Ordenacao);
        Assert.Equal("↕", documento.Seta);
        Assert.Equal(string.Empty, grade.TextoOrdenacao);

        grade.Nome.OrdenarCommand.Execute(null); // o nome (fixo) também ordena
        Assert.Equal(ColunasPessoas.Nome, grade.Ordenacao!.Coluna);
        Assert.False(documento.Ordenando);
        Assert.All(mudancas, m => Assert.Equal(MudancaGrade.Ordenacao, m));
        Assert.Equal(4, mudancas.Count);
    }

    [Fact]
    public void Coluna_nova_com_valor_da_api_rele_e_tirar_ou_mover_so_remonta()
    {
        var (grade, _, mudancas) = Montar();

        Item(grade, CamposFiltroPessoas.Telefone).Marcado = true;
        Assert.Equal(MudancaGrade.Colunas, mudancas[^1]);
        Assert.Equal(CamposFiltroPessoas.Telefone, grade.Visiveis[^1].Id);
        Assert.Contains(CamposFiltroPessoas.Telefone, grade.ColunasExtras());

        grade.SubirCommand.Execute(grade.NaLista.Single(i => i.Id == CamposFiltroPessoas.Telefone));
        Assert.Equal(MudancaGrade.Aparencia, mudancas[^1]);
        Assert.Equal(new[] { CamposFiltroPessoas.Documento, CamposFiltroPessoas.Telefone, CamposFiltroPessoas.Situacao },
            grade.Visiveis.Select(c => c.Id).ToArray());

        grade.TirarCommand.Execute(grade.NaLista.Single(i => i.Id == CamposFiltroPessoas.Documento));
        Assert.Equal(MudancaGrade.Aparencia, mudancas[^1]);
        Assert.False(Item(grade, CamposFiltroPessoas.Documento).Marcado); // a caixa do seletor acompanha

        grade.MarcarTodasCommand.Execute(null);
        Assert.Equal(7, grade.Visiveis.Count);
        Assert.Equal(MudancaGrade.Colunas, mudancas[^1]);

        grade.RestaurarPadraoCommand.Execute(null);
        Assert.Equal(new[] { CamposFiltroPessoas.Documento, CamposFiltroPessoas.Situacao }, grade.Visiveis.Select(c => c.Id).ToArray());
        Assert.Equal(MudancaGrade.Aparencia, mudancas[^1]);
    }

    [Fact]
    public void Busca_no_seletor_mostra_so_as_colunas_que_batem()
    {
        var (grade, _, _) = Montar();
        grade.BuscaColuna = "TELEFONE";
        Assert.True(Item(grade, CamposFiltroPessoas.Telefone).Visivel);
        Assert.False(Item(grade, CamposFiltroPessoas.Bairro).Visivel);
        Assert.False(grade.Grupos.Single(g => g.Nome == "Endereços").Visivel);
    }

    [Fact]
    public void Celulas_formatam_o_valor_da_api_no_jeito_da_tela()
    {
        var (grade, _, _) = Montar(new LayoutListaPessoas
        {
            Colunas = [CamposFiltroPessoas.Documento, CamposFiltroPessoas.Sexo, CamposFiltroPessoas.Telefone, CamposFiltroPessoas.LimiteCredito,
                       CamposFiltroPessoas.CadastradoEm, CamposFiltroPessoas.Bairro, CamposFiltroPessoas.Situacao]
        });
        var pessoa = new PessoaResumo
        {
            Id = Guid.NewGuid(), Codigo = 7, Nome = "Ana", Natureza = NaturezaPessoa.Fisica, DocumentoPrincipal = "52998224725",
            Situacao = SituacaoPessoa.Ativo,
            Valores = new()
            {
                [CamposFiltroPessoas.Sexo] = "Feminino",
                [CamposFiltroPessoas.Telefone] = "38999887766",
                [CamposFiltroPessoas.LimiteCredito] = "1500.5",
                [CamposFiltroPessoas.CadastradoEm] = "2026-09-27",
                [CamposFiltroPessoas.Bairro] = null
            }
        };

        var linha = grade.Linha(pessoa);
        Assert.Equal("Cód. 000007", linha.Subtitulo);
        Assert.Equal(new[] { "529.982.247-25", "Feminino", "(38) 99988-7766", "R$ 1.500,50", "27/09/2026", "—", "Ativo" },
            linha.Celulas.Select(c => c.Texto.Replace(' ', ' ')).ToArray());
        Assert.True(linha.Celulas[5].Vazia);
        Assert.Equal("Sucesso", linha.Celulas[6].TomSelo);

        grade.MostrarColunas = false; // celular: só o nome, com o documento embaixo
        linha = grade.Linha(pessoa);
        Assert.Empty(linha.Celulas);
        Assert.Equal("529.982.247-25", linha.Subtitulo);
    }

    [Fact]
    public void Filtro_da_coluna_escreve_no_campo_do_painel_e_vira_chip()
    {
        var (grade, painel, _) = Montar(new LayoutListaPessoas { Colunas = [CamposFiltroPessoas.Bairro, CamposFiltroPessoas.Situacao] });

        Coluna(grade, CamposFiltroPessoas.Bairro).Filtro!.Texto = "centro";
        var bairro = Assert.Single(painel.Condicoes());
        Assert.Equal(CamposFiltroPessoas.Bairro, bairro.Campo);
        Assert.Equal(OperadorFiltro.Contem, bairro.Operador);
        Assert.Equal(new[] { "centro" }, bairro.Valores);
        Assert.Contains(painel.Chips, c => c.Texto.StartsWith("Bairro"));

        var situacao = Coluna(grade, CamposFiltroPessoas.Situacao).Filtro!;
        Assert.True(situacao.EhEscolha);
        situacao.Escolha = situacao.Escolhas.Single(o => o.Valor == "Inativo");
        Assert.Contains(painel.Condicoes(), c => c.Campo == CamposFiltroPessoas.Situacao && c.Valores.SequenceEqual(["Inativo"]));

        situacao.Escolha = FiltroColuna.Todos;
        Assert.DoesNotContain(painel.Condicoes(), c => c.Campo == CamposFiltroPessoas.Situacao);

        // Mudou no painel: a linha de filtro mostra o mesmo
        painel.Limpar();
        Assert.Equal(string.Empty, Coluna(grade, CamposFiltroPessoas.Bairro).Filtro!.Texto);

        grade.Nome.Filtro!.Texto = "silva"; // o nome também filtra
        Assert.Contains(painel.Condicoes(), c => c.Campo == CamposFiltroPessoas.Nome);
    }

    [Fact]
    public void Filtro_de_numero_e_data_entende_faixas()
    {
        var (grade, painel, _) = Montar(new LayoutListaPessoas { Colunas = [CamposFiltroPessoas.LimiteCredito, CamposFiltroPessoas.CadastradoEm] });
        var limite = Coluna(grade, CamposFiltroPessoas.LimiteCredito).Filtro!;
        var cadastro = Coluna(grade, CamposFiltroPessoas.CadastradoEm).Filtro!;
        CondicaoFiltro Condicao(string campo) => painel.Condicoes().Single(c => c.Campo == campo);

        limite.Texto = "10..50";
        Assert.Equal(OperadorFiltro.Entre, Condicao(CamposFiltroPessoas.LimiteCredito).Operador);
        Assert.Equal(new[] { "10", "50" }, Condicao(CamposFiltroPessoas.LimiteCredito).Valores);

        limite.Texto = ">=1.000,50";
        Assert.Equal(OperadorFiltro.APartirDe, Condicao(CamposFiltroPessoas.LimiteCredito).Operador);
        Assert.Equal(new[] { "1000.50" }, Condicao(CamposFiltroPessoas.LimiteCredito).Valores);

        limite.Texto = "..500";
        Assert.Equal(OperadorFiltro.Ate, Condicao(CamposFiltroPessoas.LimiteCredito).Operador);

        limite.Texto = "abc";
        Assert.True(limite.Invalido);
        Assert.DoesNotContain(painel.Condicoes(), c => c.Campo == CamposFiltroPessoas.LimiteCredito);

        cadastro.Texto = "09/2026";
        Assert.Equal(new[] { "2026-09-01", "2026-09-30" }, Condicao(CamposFiltroPessoas.CadastradoEm).Valores);
        cadastro.Texto = "2025";
        Assert.Equal(new[] { "2025-01-01", "2025-12-31" }, Condicao(CamposFiltroPessoas.CadastradoEm).Valores);
        cadastro.Texto = "15/03/2026";
        Assert.Equal(new[] { "2026-03-15", "2026-03-15" }, Condicao(CamposFiltroPessoas.CadastradoEm).Valores);
        cadastro.Texto = ">=01/09/2026";
        Assert.Equal(OperadorFiltro.APartirDe, Condicao(CamposFiltroPessoas.CadastradoEm).Operador);
        cadastro.Texto = "32/13";
        Assert.True(cadastro.Invalido);
        cadastro.Texto = "";
        Assert.False(cadastro.Invalido);
        Assert.Empty(painel.Condicoes());
    }

    [Fact]
    public void Layout_guarda_colunas_ordenacao_e_linha_de_filtro()
    {
        var (grade, _, mudancas) = Montar();
        grade.AlternarFiltroNasColunasCommand.Execute(null);
        Assert.Equal(MudancaGrade.Aparencia, mudancas[^1]);
        Coluna(grade, CamposFiltroPessoas.Situacao).OrdenarCommand.Execute(null);

        var layout = grade.Layout();
        Assert.Equal(new[] { CamposFiltroPessoas.Documento, CamposFiltroPessoas.Situacao }, layout.Colunas);
        Assert.Equal(CamposFiltroPessoas.Situacao, layout.Ordenacao!.Coluna);
        Assert.True(layout.FiltroNasColunas);

        var outra = new GradePessoas();
        outra.Carregar(Catalogo().Colunas, layout, new PainelFiltrosPessoas());
        Assert.Equal(layout.Colunas, outra.Layout().Colunas);
        Assert.Equal(DirecaoOrdenacao.Crescente, outra.Ordenacao!.Direcao);
    }
    [Fact]
    public void Tirar_a_coluna_que_ordena_volta_a_ordem_padrao_e_rele()
    {
        var (grade, _, mudancas) = Montar();
        Coluna(grade, CamposFiltroPessoas.Situacao).OrdenarCommand.Execute(null);
        Assert.NotNull(grade.Ordenacao);

        grade.TirarCommand.Execute(grade.NaLista.Single(i => i.Id == CamposFiltroPessoas.Situacao));
        Assert.Null(grade.Ordenacao);
        Assert.Equal(string.Empty, grade.TextoOrdenacao);
        Assert.Equal(MudancaGrade.Ordenacao, mudancas[^1]);

        // Ordenado pelo nome (fixo), tirar outra coluna não mexe na ordenação.
        grade.Nome.OrdenarCommand.Execute(null);
        grade.TirarCommand.Execute(grade.NaLista.Single(i => i.Id == CamposFiltroPessoas.Documento));
        Assert.Equal(ColunasPessoas.Nome, grade.Ordenacao!.Coluna);
        Assert.Equal(MudancaGrade.Aparencia, mudancas[^1]);
    }

    [Fact]
    public void Coluna_guardada_sem_permissao_nao_aparece_mas_continua_na_preferencia()
    {
        var (grade, _, _) = Montar(new LayoutListaPessoas { Colunas = [CamposFiltroPessoas.Documento, "cliente.coluna-sem-permissao"] });
        Assert.Equal(new[] { CamposFiltroPessoas.Documento }, grade.Visiveis.Select(c => c.Id).ToArray());

        Item(grade, CamposFiltroPessoas.Bairro).Marcado = true;
        Assert.Equal(new[] { CamposFiltroPessoas.Documento, CamposFiltroPessoas.Bairro, "cliente.coluna-sem-permissao" },
            grade.Layout().Colunas);
    }

    [Fact]
    public void Linha_de_filtro_so_aparece_com_colunas_na_tela()
    {
        var (grade, _, _) = Montar(new LayoutListaPessoas { Colunas = [CamposFiltroPessoas.Documento], FiltroNasColunas = true });
        Assert.True(grade.MostrarLinhaFiltro);
        grade.MostrarColunas = false; // celular: sem o botão para desligar, a linha também some
        Assert.False(grade.MostrarLinhaFiltro);
        Assert.True(grade.Layout().FiltroNasColunas); // a preferência não muda
    }
    [Fact]
    public void Linha_mostra_iniciais_papeis_em_selos_e_avisa_documento_que_falta()
    {
        var (grade, _, _) = Montar(new LayoutListaPessoas { Colunas = [CamposFiltroPessoas.Documento] });
        grade.Carregar([.. Catalogo().Colunas, new ColunaListaDto { Id = CamposFiltroPessoas.Papeis, Grupo = "Identificação", Nome = "Papéis",
            Tipo = TipoColunaLista.Papeis, Largura = 210 }],
            new LayoutListaPessoas { Colunas = [CamposFiltroPessoas.Documento, CamposFiltroPessoas.Papeis] }, new PainelFiltrosPessoas());
        var pessoa = new PessoaResumo
        {
            Id = Guid.NewGuid(), Codigo = 6, Nome = "rafael", Natureza = NaturezaPessoa.Fisica, Situacao = SituacaoPessoa.Ativo,
            Papeis = ["Cliente", "Fornecedor", "Empresa do grupo", "Papel do usuário"]
        };

        var linha = grade.Linha(pessoa);
        Assert.Equal("R", linha.Iniciais);
        Assert.True(linha.EhPessoaFisica);
        Assert.Equal("Sem CPF", linha.Celulas[0].Texto);
        Assert.Equal("Aviso", linha.Celulas[0].TomSelo);
        Assert.True(linha.Celulas[1].EhPapeis);
        Assert.False(linha.Celulas[1].EhTexto);
        Assert.Equal(new[] { "Informacao", "Aviso", "Grupo", "Neutro" }, linha.Celulas[1].Papeis.Select(p => p.Tom).ToArray());
        Assert.False(linha.MostrarPapeisNoCartao); // com colunas, os papéis ficam na coluna deles

        Assert.Equal("MG", LinhaPessoa.IniciaisDe("MERCEARIA GREGORIO LTDA"));
        Assert.Equal("GB", LinhaPessoa.IniciaisDe("GRUPO CASAS BAHIA S.A."));
        Assert.Equal("?", LinhaPessoa.IniciaisDe("  "));
    }

    [Fact]
    public void Densidade_compacta_baixa_as_linhas_e_fica_guardada()
    {
        var (grade, _, mudancas) = Montar();
        var pessoa = new PessoaResumo { Id = Guid.NewGuid(), Codigo = 1, Nome = "Ana", Natureza = NaturezaPessoa.Fisica };
        Assert.Equal(LinhaPessoa.AlturaConfortavel, grade.Linha(pessoa).Altura);

        grade.AlternarDensidadeCommand.Execute(null);
        Assert.Equal(MudancaGrade.Aparencia, mudancas[^1]);
        Assert.Equal(LinhaPessoa.AlturaCompacta, grade.Linha(pessoa).Altura);
        Assert.True(grade.Layout().Compacta);
        Assert.Equal("Densidade: compacta", grade.TextoDensidade);

        var outra = new GradePessoas();
        outra.Carregar(Catalogo().Colunas, grade.Layout(), new PainelFiltrosPessoas());
        Assert.True(outra.Compacta);
    }

    [Fact]
    public void Celular_vira_cartao_com_documento_cidade_papeis_e_ordena_pelo_botao()
    {
        var (grade, _, mudancas) = Montar();
        grade.MostrarColunas = false;
        Assert.True(grade.SemColunas);
        var pessoa = new PessoaResumo
        {
            Id = Guid.NewGuid(), Codigo = 3, Nome = "João", Natureza = NaturezaPessoa.Fisica, DocumentoPrincipal = "52998224725",
            Cidade = "Curvelo", Uf = "MG", Papeis = ["Cliente"],
            Valores = new() { [CamposFiltroPessoas.Telefone] = "38999887766" }
        };
        var linha = grade.Linha(pessoa);
        Assert.True(linha.Cartao);
        Assert.Equal(LinhaPessoa.AlturaCartao, linha.Altura);
        Assert.Equal("529.982.247-25 · Curvelo/MG", linha.Subtitulo);
        Assert.True(linha.MostrarPapeisNoCartao);
        Assert.True(linha.MostrarLigar); // no celular, sem precisar do mouse

        Assert.Equal("Ordenar", grade.TextoBotaoOrdenar);
        Assert.Contains(grade.ColunasOrdenaveis(), c => c.Id == ColunasPessoas.Nome);
        grade.OrdenarPor(CamposFiltroPessoas.CadastradoEm, DirecaoOrdenacao.Decrescente);
        Assert.Equal(MudancaGrade.Ordenacao, mudancas[^1]);
        Assert.Equal("Cadastrado em ↓", grade.TextoBotaoOrdenar);
        grade.OrdenarPor(null, DirecaoOrdenacao.Crescente);
        Assert.Null(grade.Ordenacao);
    }

    [Fact]
    public void Acoes_rapidas_so_aparecem_com_o_mouse_em_cima_e_com_o_contato_cadastrado()
    {
        var (grade, _, _) = Montar();
        var semEmail = grade.Linha(new PessoaResumo
        {
            Id = Guid.NewGuid(), Nome = "Ana", Natureza = NaturezaPessoa.Fisica,
            Valores = new() { [CamposFiltroPessoas.Telefone] = "3837210001", [CamposFiltroPessoas.Email] = null }
        });
        Assert.False(semEmail.MostrarLigar);
        semEmail.EntrarCommand.Execute(null);
        Assert.True(semEmail.MostrarLigar);
        Assert.False(semEmail.MostrarEmail);
        semEmail.SairCommand.Execute(null);
        Assert.False(semEmail.MostrarLigar);
    }
}
