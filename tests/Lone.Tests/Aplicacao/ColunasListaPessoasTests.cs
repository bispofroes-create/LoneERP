using Lone.Application.Consultas;
using Lone.Contracts.Pessoas;
using Lone.Infrastructure.Persistencia.Consultas;

namespace Lone.Tests.Aplicacao;

/// <summary>Colunas da lista de pessoas: cobertura no banco, filtro de cada coluna no catálogo, conferência do pedido.</summary>
public class ColunasListaPessoasTests
{
    [Fact]
    public void Toda_coluna_tem_valor_e_ordenacao_no_banco_e_vice_versa()
    {
        var colunas = ColunasListaPessoas.Colunas.Select(c => c.Id).Append(ColunasPessoas.Nome).Order().ToList();
        Assert.Equal(colunas, ColunasPessoasSql.Implementadas.Order().ToList());
        Assert.Equal(colunas.Count, colunas.Distinct().Count());
    }

    [Fact]
    public void Filtro_de_cada_coluna_existe_no_catalogo_de_filtros_com_a_mesma_permissao()
    {
        foreach (var coluna in ColunasListaPessoas.Colunas.Where(c => c.CampoFiltro is not null))
        {
            var campo = CatalogoFiltrosPessoas.Obter(coluna.CampoFiltro!);
            Assert.NotNull(campo);
            Assert.Equal(coluna.Permissao, campo!.Permissao);
        }
        Assert.NotNull(CatalogoFiltrosPessoas.Obter(ColunasPessoas.Nome)); // o nome (fixo) também filtra
    }

    [Fact]
    public void Padrao_e_a_lista_de_sempre_sem_consulta_extra()
    {
        var padrao = ColunasListaPessoas.Colunas.Where(c => c.Padrao).ToList();
        Assert.Equal(
            new[] { CamposFiltroPessoas.Documento, CamposFiltroPessoas.Natureza, CamposFiltroPessoas.Papeis, CamposFiltroPessoas.Cidade, CamposFiltroPessoas.Uf, CamposFiltroPessoas.Situacao },
            padrao.Select(c => c.Id).ToArray());
        Assert.All(padrao, c => Assert.True(ColunasListaPessoas.VemNaLinha(c.Tipo)));
    }

    [Fact]
    public void Cidade_e_uf_sao_colunas_separadas_e_a_uf_filtra_pela_lista_de_estados()
    {
        var cidade = ColunasListaPessoas.Obter(CamposFiltroPessoas.Cidade)!;
        var uf = ColunasListaPessoas.Obter(CamposFiltroPessoas.Uf)!;
        Assert.Equal("Cidade", cidade.Nome);
        Assert.Equal(("UF", TipoColunaLista.Uf, CamposFiltroPessoas.Uf), (uf.Nome, uf.Tipo, uf.CampoFiltro));
        Assert.True(ColunasListaPessoas.VemNaLinha(uf.Tipo)); // sem consulta extra
        Assert.True(ColunasListaPessoas.Ordenavel(CamposFiltroPessoas.Uf));
        var ordem = ColunasListaPessoas.Colunas.ToList();
        Assert.Equal(ordem.IndexOf(cidade) + 1, ordem.IndexOf(uf)); // logo depois da cidade
    }

    [Fact]
    public void Pedido_confere_colunas_e_ordenacao_e_so_deixa_as_que_precisam_de_consulta()
    {
        var colunas = new List<string> { CamposFiltroPessoas.Bairro, CamposFiltroPessoas.Cidade, CamposFiltroPessoas.Bairro };
        var erros = ColunasListaPessoas.Normalizar(colunas,
            new OrdenacaoLista { Coluna = ColunasPessoas.Nome, Direcao = DirecaoOrdenacao.Decrescente }, out var definicoes);
        Assert.Empty(erros);
        Assert.Equal(2, definicoes.Count);
        Assert.Equal(new[] { CamposFiltroPessoas.Bairro }, colunas); // a cidade já vem na linha

        erros = ColunasListaPessoas.Normalizar(["coluna.inventada"],
            new OrdenacaoLista { Coluna = "outra.inventada", Direcao = (DirecaoOrdenacao)9 }, out _);
        Assert.Equal(3, erros.Count);
    }

    [Fact]
    public void Layout_guardado_perde_so_o_que_nao_existe_mais()
    {
        var layout = ColunasListaPessoas.Limpar(new LayoutListaPessoas
        {
            Colunas = [CamposFiltroPessoas.Email, "coluna.que.saiu", CamposFiltroPessoas.Email, CamposFiltroPessoas.Cep],
            Ordenacao = new OrdenacaoLista { Coluna = "coluna.que.saiu" },
            FiltroNasColunas = true
        })!;
        Assert.Equal(new[] { CamposFiltroPessoas.Email, CamposFiltroPessoas.Cep }, layout.Colunas);
        Assert.Null(layout.Ordenacao);
        Assert.True(layout.FiltroNasColunas);
        Assert.Null(ColunasListaPessoas.Limpar(null));
    }
}
