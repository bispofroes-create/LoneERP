using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enums;

namespace Lone.Tests.Cliente;

/// <summary>Painel de filtros da tela de Pessoas: campos marcados viram condições e chips; natureza do atalho; limpar.</summary>
public class PainelFiltrosTests
{
    private static CatalogoFiltrosPessoasDto Catalogo() => new()
    {
        Campos =
        [
            new() { Id = CamposFiltroPessoas.Uf, Grupo = "Endereços", Nome = "UF", Tipo = TipoCampoFiltro.Lista,
                    Operadores = [OperadorFiltro.UmDestes, OperadorFiltro.NenhumDestes],
                    Opcoes = [new("MG", "MG"), new("SP", "SP")] },
            new() { Id = CamposFiltroPessoas.Bairro, Grupo = "Endereços", Nome = "Bairro", Tipo = TipoCampoFiltro.Texto,
                    Operadores = [OperadorFiltro.Contem, OperadorFiltro.ComecaCom, OperadorFiltro.Igual] },
            new() { Id = CamposFiltroPessoas.Regime, Grupo = "Fiscal", Nome = "Regime tributário", Tipo = TipoCampoFiltro.Lista,
                    Operadores = [OperadorFiltro.UmDestes], ValePara = ValeParaNatureza.Juridica,
                    Opcoes = [new("SimplesNacional", "Simples Nacional")] },
            new() { Id = CamposFiltroPessoas.Bloqueado, Grupo = "Situação", Nome = "Com bloqueio ativo", Tipo = TipoCampoFiltro.SimNao,
                    Operadores = [OperadorFiltro.Sim, OperadorFiltro.Nao] },
            new() { Id = CamposFiltroPessoas.CadastradoEm, Grupo = "Cadastro", Nome = "Cadastrado em", Tipo = TipoCampoFiltro.Data,
                    Operadores = [OperadorFiltro.Entre, OperadorFiltro.APartirDe, OperadorFiltro.Ate] },
            new() { Id = CamposFiltroPessoas.SemInteracao, Grupo = "Interações", Nome = "Sem interação há", Tipo = TipoCampoFiltro.Numero,
                    Operadores = [OperadorFiltro.HaMaisDeDias] }
        ]
    };

    private static (PainelFiltrosPessoas Painel, List<int> Mudancas) Montar()
    {
        var painel = new PainelFiltrosPessoas();
        var mudancas = new List<int>();
        painel.Mudou = () => mudancas.Add(painel.Condicoes().Count);
        painel.Carregar(Catalogo());
        return (painel, mudancas);
    }

    private static CampoFiltroItem Campo(PainelFiltrosPessoas p, string id) => p.Grupos.SelectMany(g => g.Campos).Single(c => c.Id == id);

    [Fact]
    public void Grupos_seguem_o_catalogo_e_montar_nao_rele_a_lista()
    {
        var (painel, mudancas) = Montar();
        Assert.Equal(new[] { "Endereços", "Fiscal", "Situação", "Cadastro", "Interações" }, painel.Grupos.Select(g => g.Nome).ToArray());
        Assert.Empty(mudancas);
        Assert.Equal("Filtros", painel.TextoBotao);
    }

    [Fact]
    public void Lista_so_filtra_com_opcao_marcada_e_vira_chip()
    {
        var (painel, mudancas) = Montar();
        var uf = Campo(painel, CamposFiltroPessoas.Uf);

        uf.Marcado = true;
        Assert.Empty(painel.Condicoes()); // incompleto: nada marcado
        Assert.Empty(mudancas);

        uf.Opcoes[0].Marcado = true;
        uf.Opcoes[1].Marcado = true;
        var condicao = Assert.Single(painel.Condicoes());
        Assert.Equal(["MG", "SP"], condicao.Valores);
        Assert.Equal("UF: MG, SP", Assert.Single(painel.Chips).Texto);
        Assert.Equal("Filtros (1)", painel.TextoBotao);
        Assert.Equal("Endereços (1)", painel.Grupos[0].Titulo);
        Assert.Equal(2, mudancas.Count);

        painel.Chips[0].RemoverCommand.Execute(null);
        Assert.Empty(painel.Condicoes());
        Assert.Empty(painel.Chips);
    }

    [Fact]
    public void Sim_nao_data_e_dias()
    {
        var (painel, _) = Montar();
        var bloqueio = Campo(painel, CamposFiltroPessoas.Bloqueado);
        bloqueio.Marcado = true;
        Assert.Equal(OperadorFiltro.Sim, Assert.Single(painel.Condicoes()).Operador);
        bloqueio.AlternarSimNaoCommand.Execute(null);
        Assert.Equal(OperadorFiltro.Nao, Assert.Single(painel.Condicoes()).Operador);
        bloqueio.Marcado = false;

        var data = Campo(painel, CamposFiltroPessoas.CadastradoEm);
        data.Marcado = true;
        data.Valor = "01/01/2026";
        Assert.Empty(painel.Condicoes()); // "Entre" precisa do fim
        data.ValorFinal = "30/06/2026";
        Assert.Equal(["2026-01-01", "2026-06-30"], Assert.Single(painel.Condicoes()).Valores);
        Assert.Equal("Cadastrado em: 01/01/2026 a 30/06/2026", painel.Chips[0].Texto);
        data.Marcado = false;

        var dias = Campo(painel, CamposFiltroPessoas.SemInteracao);
        dias.Marcado = true;
        dias.Valor = "90";
        Assert.Equal(["90"], Assert.Single(painel.Condicoes()).Valores);
    }

    [Fact]
    public void Campo_de_outra_natureza_fica_desabilitado_e_nao_filtra()
    {
        var (painel, _) = Montar();
        var regime = Campo(painel, CamposFiltroPessoas.Regime);
        regime.Marcado = true;
        regime.Opcoes[0].Marcado = true;
        Assert.Single(painel.Condicoes());

        painel.DefinirNatureza(NaturezaPessoa.Fisica);
        Assert.False(regime.Habilitado);
        Assert.Empty(painel.Condicoes());

        painel.DefinirNatureza(null);
        Assert.Single(painel.Condicoes());
    }

    [Fact]
    public void Buscar_campo_mostra_so_os_que_batem_e_limpar_tira_tudo()
    {
        var (painel, _) = Montar();
        painel.BuscaCampo = "regime";
        Assert.True(Campo(painel, CamposFiltroPessoas.Regime).Visivel);
        Assert.False(Campo(painel, CamposFiltroPessoas.Uf).Visivel);
        Assert.False(painel.Grupos[0].Visivel);
        Assert.True(painel.Grupos[1].Expandido);

        var uf = Campo(painel, CamposFiltroPessoas.Uf);
        uf.Marcado = true;
        uf.Opcoes[0].Marcado = true;
        painel.Limpar();
        Assert.Empty(painel.Condicoes());
        Assert.False(uf.Marcado);
        Assert.Equal(string.Empty, painel.BuscaCampo);
    }

    [Fact]
    public void Recarregar_o_catalogo_mantem_o_que_estava_marcado()
    {
        var (painel, _) = Montar();
        var uf = Campo(painel, CamposFiltroPessoas.Uf);
        uf.Marcado = true;
        uf.Opcoes[1].Marcado = true;

        painel.Carregar(Catalogo());
        Assert.Equal(["SP"], Assert.Single(painel.Condicoes()).Valores);
    }

    [Fact]
    public void Abrir_e_fechar_todos_os_grupos_e_fechar_o_painel_deixa_abertos_so_os_que_filtram()
    {
        var (painel, _) = Montar();
        Assert.Equal("Abrir todos", painel.TextoExpandirTodos);
        painel.ExpandirTodosCommand.Execute(null);
        Assert.All(painel.Grupos, g => Assert.True(g.Expandido));
        Assert.Equal("Fechar todos", painel.TextoExpandirTodos);

        var uf = Campo(painel, CamposFiltroPessoas.Uf);
        uf.Marcado = true;
        uf.Opcoes[0].Marcado = true;
        painel.Aberto = true;
        painel.FecharCommand.Execute(null);
        Assert.True(painel.Grupos[0].Expandido);                  // Endereços: está filtrando
        Assert.All(painel.Grupos.Skip(1), g => Assert.False(g.Expandido));

        painel.Limpar();
        Assert.All(painel.Grupos, g => Assert.False(g.Expandido));
    }

    [Fact]
    public void Texto_digitado_nao_e_apagado_pelos_editores_escondidos_de_outros_tipos()
    {
        var (painel, _) = Montar();
        var bairro = Campo(painel, CamposFiltroPessoas.Bairro);
        bairro.Marcado = true;
        bairro.ValorTexto = "Centro";

        bairro.DataInicial = string.Empty;   // o editor de data (escondido) com máscara devolve vazio
        bairro.NumeroInicial = string.Empty; // idem o de número
        Assert.Equal("Centro", bairro.ValorTexto);
        Assert.Equal(string.Empty, bairro.DataInicial);
        Assert.Equal(["Centro"], Assert.Single(painel.Condicoes()).Valores);
        Assert.Equal("Bairro: contém Centro", painel.Chips[0].Texto);
    }

    [Fact]
    public void Municipio_escolhido_filtra_na_hora()
    {
        var painel = new PainelFiltrosPessoas();
        var mudancas = 0;
        painel.Mudou = () => mudancas++;
        painel.Carregar(new CatalogoFiltrosPessoasDto
        {
            Campos =
            [
                new() { Id = CamposFiltroPessoas.Municipio, Grupo = "Endereços", Nome = "Município", Tipo = TipoCampoFiltro.Lista,
                        Operadores = [OperadorFiltro.UmDestes, OperadorFiltro.NenhumDestes], OpcoesSobDemanda = "municipios" }
            ]
        });
        var campo = Campo(painel, CamposFiltroPessoas.Municipio);
        campo.Marcado = true;

        campo.Municipio!.Escolher(new Lone.Contracts.Municipios.MunicipioDto { Id = 3120904, Nome = "Curvelo", Uf = "MG" });

        Assert.Equal(["3120904"], Assert.Single(painel.Condicoes()).Valores);
        Assert.Equal(1, mudancas);
        Assert.Equal("Município: Curvelo - MG", Assert.Single(painel.Chips).Texto);
    }

    [Fact]
    public void Texto_so_de_digitos_fica_incompleto_ate_ter_o_tamanho_certo()
    {
        var painel = new PainelFiltrosPessoas();
        painel.Carregar(new CatalogoFiltrosPessoasDto
        {
            Campos =
            [
                new() { Id = CamposFiltroPessoas.Ddd, Grupo = "Telefones e e-mails", Nome = "DDD", Tipo = TipoCampoFiltro.Texto,
                        Operadores = [OperadorFiltro.Igual], SomenteDigitos = true, TamanhoMinimo = 2, TamanhoMaximo = 2 }
            ]
        });
        var ddd = Campo(painel, CamposFiltroPessoas.Ddd);
        ddd.Marcado = true;

        ddd.ValorTexto = "3";
        Assert.Empty(painel.Condicoes());   // incompleto: não vai para a API (sem erro na tela)
        ddd.ValorTexto = "(38)";
        Assert.Equal(["38"], Assert.Single(painel.Condicoes()).Valores);
        ddd.ValorTexto = "abc";
        Assert.Empty(painel.Condicoes());
    }
}
