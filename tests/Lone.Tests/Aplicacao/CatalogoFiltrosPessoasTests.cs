using Lone.Application.Consultas;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enums;
using Lone.Infrastructure.Persistencia.Consultas;

namespace Lone.Tests.Aplicacao;

/// <summary>Catálogo do filtro de pessoas: um motor só (critérios antigos viram condições), validação e cobertura no banco.</summary>
public class CatalogoFiltrosPessoasTests
{
    private static CondicaoFiltro Condicao(string campo, OperadorFiltro operador, params string[] valores) =>
        new() { Campo = campo, Operador = operador, Valores = [.. valores] };

    [Fact]
    public void Todo_campo_do_catalogo_tem_condicao_no_banco_e_vice_versa()
    {
        var catalogo = CatalogoFiltrosPessoas.Campos.Select(c => c.Id).Order().ToList();
        Assert.Equal(catalogo, FiltrosPessoasSql.Implementados.Order().ToList());
        Assert.Equal(catalogo.Count, catalogo.Distinct().Count());
    }

    [Fact]
    public void Criterios_antigos_viram_condicoes_do_catalogo()
    {
        var campo = Guid.NewGuid();
        var papel = Guid.NewGuid();
        var criterios = new CriteriosPessoas
        {
            Naturezas = [NaturezaPessoa.Juridica],
            PapeisIds = [papel],
            TodosOsPapeis = true,
            Uf = "MG",
            Cnae = "47",
            SomenteCnaePrincipal = true,
            ProdutorRural = false,
            SemInteracaoDias = 90,
            CampoId = campo,
            CadastradoDe = new DateOnly(2026, 1, 1),
            Condicoes = [Condicao(CamposFiltroPessoas.Bloqueado, OperadorFiltro.Sim)]
        };

        var condicoes = ConsultaPessoasAppService.CondicoesDe(criterios);

        Assert.Contains(condicoes, c => c.Campo == CamposFiltroPessoas.Natureza && c.Valores.SequenceEqual(["Juridica"]));
        Assert.Contains(condicoes, c => c.Campo == CamposFiltroPessoas.Papeis && c.Operador == OperadorFiltro.TodosDestes && c.Valores[0] == papel.ToString("D"));
        Assert.Contains(condicoes, c => c.Campo == CamposFiltroPessoas.Uf && c.Valores[0] == "MG");
        Assert.Contains(condicoes, c => c.Campo == CamposFiltroPessoas.CnaePrincipal && c.Valores[0] == "47");
        Assert.Contains(condicoes, c => c.Campo == CamposFiltroPessoas.ProdutorRural && c.Operador == OperadorFiltro.Nao);
        Assert.Contains(condicoes, c => c.Campo == CamposFiltroPessoas.SemInteracao && c.Valores[0] == "90");
        Assert.Contains(condicoes, c => c.Campo == CamposFiltroPessoas.CampoPersonalizado(campo) && c.Operador == OperadorFiltro.NaoVazio);
        Assert.Contains(condicoes, c => c.Campo == CamposFiltroPessoas.CadastradoEm && c.Operador == OperadorFiltro.APartirDe && c.Valores[0] == "2026-01-01");
        Assert.Contains(condicoes, c => c.Campo == CamposFiltroPessoas.Bloqueado); // as novas vêm junto
        Assert.Empty(CatalogoFiltrosPessoas.Normalizar(condicoes)); // e todas são válidas
    }

    [Fact]
    public void Condicoes_validas_passam_e_sao_limpas()
    {
        var cnae = Condicao(CamposFiltroPessoas.Cnae, OperadorFiltro.ComecaCom, " 47.11-3 ");
        var sim = Condicao(CamposFiltroPessoas.DocumentosVencidos, OperadorFiltro.Sim, "sobra");
        List<CondicaoFiltro> condicoes =
        [
            cnae, sim,
            Condicao(CamposFiltroPessoas.Uf, OperadorFiltro.NenhumDestes, "SP", "SP", "RJ"),
            Condicao(CamposFiltroPessoas.CadastradoEm, OperadorFiltro.Entre, "2026-01-01", "2026-06-30"),
            Condicao(CamposFiltroPessoas.CampoPersonalizado(Guid.NewGuid()), OperadorFiltro.ComecaCom, "abc")
        ];

        Assert.Empty(CatalogoFiltrosPessoas.Normalizar(condicoes));
        Assert.Equal("47113", cnae.Valores[0]);
        Assert.Empty(sim.Valores);
        Assert.Equal(["SP", "RJ"], condicoes[2].Valores);
    }

    [Theory]
    [InlineData("nao.existe", OperadorFiltro.Sim, new string[0], "desconhecido")]
    [InlineData(CamposFiltroPessoas.Uf, OperadorFiltro.Contem, new[] { "MG" }, "operador")]
    [InlineData(CamposFiltroPessoas.Uf, OperadorFiltro.UmDestes, new[] { "XX" }, "opção inválida")]
    [InlineData(CamposFiltroPessoas.Uf, OperadorFiltro.UmDestes, new string[0], "pelo menos uma")]
    [InlineData(CamposFiltroPessoas.Natureza, OperadorFiltro.UmDestes, new[] { "1" }, "opção inválida")]
    [InlineData(CamposFiltroPessoas.SemInteracao, OperadorFiltro.HaMaisDeDias, new[] { "0" }, "dias")]
    [InlineData(CamposFiltroPessoas.CadastradoEm, OperadorFiltro.Entre, new[] { "2026-06-30", "2026-01-01" }, "anterior")]
    [InlineData(CamposFiltroPessoas.CadastradoEm, OperadorFiltro.APartirDe, new[] { "30/06/2026" }, "data inválida")]
    [InlineData(CamposFiltroPessoas.Cnae, OperadorFiltro.ComecaCom, new[] { "12345678" }, "valor inválido")]
    public void Condicoes_invalidas_sao_recusadas(string campo, OperadorFiltro operador, string[] valores, string trecho)
    {
        var erros = CatalogoFiltrosPessoas.Normalizar([Condicao(campo, operador, valores)]);
        Assert.Contains(erros, e => e.Contains(trecho));
    }

    [Fact]
    public void Criterios_com_condicao_invalida_sao_recusados_pelo_servico()
    {
        var criterios = new CriteriosPessoas { Condicoes = [Condicao("nao.existe", OperadorFiltro.Sim)] };
        Assert.Throws<Lone.Domain.Validacao.ValidacaoException>(() => ConsultaPessoasAppService.Normalizar(criterios));
    }

    [Fact]
    public void Limite_de_condicoes()
    {
        var muitas = Enumerable.Range(0, CatalogoFiltrosPessoas.MaximoCondicoes + 1)
            .Select(_ => Condicao(CamposFiltroPessoas.Bloqueado, OperadorFiltro.Sim)).ToList();
        Assert.Contains(CatalogoFiltrosPessoas.Normalizar(muitas), e => e.Contains("no máximo"));
    }

    [Fact]
    public void Campos_da_fase_3_validam_e_limpam_os_valores()
    {
        var cep = Condicao(CamposFiltroPessoas.Cep, OperadorFiltro.ComecaCom, "35.790-");
        var ie = Condicao(CamposFiltroPessoas.InscricaoEstadual, OperadorFiltro.Vazio);
        List<CondicaoFiltro> condicoes =
        [
            cep, ie,
            Condicao(CamposFiltroPessoas.Aniversario, OperadorFiltro.UmDestes, "1", "12"),
            Condicao(CamposFiltroPessoas.Idade, OperadorFiltro.Entre, "18", "30"),
            Condicao(CamposFiltroPessoas.Sexo, OperadorFiltro.UmDestes, "Feminino"),
            Condicao(CamposFiltroPessoas.FinalidadeContato, OperadorFiltro.NenhumDestes, "NFe"),
            Condicao(CamposFiltroPessoas.LimiteCredito, OperadorFiltro.APartirDe, "5000.50"),
            Condicao(CamposFiltroPessoas.Porte, OperadorFiltro.UmDestes, "MICRO EMPRESA")
        ];
        Assert.Empty(CatalogoFiltrosPessoas.Normalizar(condicoes));
        Assert.Equal("35790", cep.Valores[0]);

        Assert.NotEmpty(CatalogoFiltrosPessoas.Normalizar([Condicao(CamposFiltroPessoas.Aniversario, OperadorFiltro.UmDestes, "13")]));
        Assert.NotEmpty(CatalogoFiltrosPessoas.Normalizar([Condicao(CamposFiltroPessoas.FinalidadeContato, OperadorFiltro.UmDestes, "Nenhuma")]));
        Assert.NotEmpty(CatalogoFiltrosPessoas.Normalizar([Condicao(CamposFiltroPessoas.Idade, OperadorFiltro.Entre, "30", "18")]));
    }

    [Fact]
    public void Campos_com_permissao_propria_estao_marcados()
    {
        Assert.Equal("PESSOAS.VISUALIZAR_FINANCEIRO", CatalogoFiltrosPessoas.Obter(CamposFiltroPessoas.LimiteCredito)!.Permissao);
        Assert.Equal("PESSOAS.COLABORADOR", CatalogoFiltrosPessoas.Obter(CamposFiltroPessoas.Cargo)!.Permissao);
        Assert.Equal("PESSOAS.PRIVACIDADE", CatalogoFiltrosPessoas.Obter(CamposFiltroPessoas.ConsentimentoEmVigor)!.Permissao);
        Assert.Equal(Lone.Contracts.Seguranca.Permissoes.Pessoas.VisualizarFinanceiro, CatalogoFiltrosPessoas.Obter(CamposFiltroPessoas.LimiteCredito)!.Permissao);
    }

    [Fact]
    public void Telefone_ddd_e_email_aceitam_o_que_o_usuario_digita()
    {
        var telefone = Condicao(CamposFiltroPessoas.Telefone, OperadorFiltro.Contem, "(38) 99988");
        var ddd = Condicao(CamposFiltroPessoas.Ddd, OperadorFiltro.Igual, " 38 ");
        var email = Condicao(CamposFiltroPessoas.Email, OperadorFiltro.Contem, "@gmail.com");
        Assert.Empty(CatalogoFiltrosPessoas.Normalizar([telefone, ddd, email]));
        Assert.Equal("3899988", telefone.Valores[0]);
        Assert.Equal("38", ddd.Valores[0]);
        Assert.Equal("@gmail.com", email.Valores[0]);

        Assert.NotEmpty(CatalogoFiltrosPessoas.Normalizar([Condicao(CamposFiltroPessoas.Ddd, OperadorFiltro.Igual, "381")]));
    }
}
