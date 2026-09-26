using System.Net;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Comum;
using Lone.Contracts.Pessoas;
using Lone.Contracts.Seguranca;
using Lone.Domain.Enums;

namespace Lone.Tests.Cliente;

/// <summary>Tela de Pessoas redesenhada: tabela paginada, atalhos de filtro, colunas por largura e menu de ações.</summary>
public class PessoasListaTests
{
    private static PaginaListaPessoas Pagina(int total, int pagina = 1, int quantos = 2) => new()
    {
        Total = total,
        Pagina = pagina,
        TamanhoPagina = 50,
        Itens = Enumerable.Range(1, quantos).Select(i => new PessoaResumo
        {
            Id = Guid.NewGuid(), Codigo = i, Nome = "Pessoa " + i, Natureza = NaturezaPessoa.Fisica, Situacao = SituacaoPessoa.Ativo
        }).ToList()
    };

    private static async Task<(PessoasViewModel Tela, AmbienteCliente Ambiente)> AbrirAsync()
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao()); // administrador
        return await PessoasViewModelTests.AbrirTelaComAsync(ambiente);
    }

    [Fact]
    public void Paginacao_resumo_e_janela_de_paginas()
    {
        Assert.Equal("Mostrando 1–50 de 1.248", Paginacao.Resumo(1, 50, 1248, 50));
        Assert.Equal("Mostrando 1.201–1.248 de 1.248", Paginacao.Resumo(25, 50, 1248, 48));
        Assert.Equal("Nenhum registro", Paginacao.Resumo(1, 50, 0, 0));
        Assert.Equal(25, Paginacao.Paginas(1248, 50));
        Assert.Equal(1, Paginacao.Paginas(0, 50));

        Assert.Equal(new[] { "1", "…", "8", "9", "10", "11", "12", "…", "25" }, Paginacao.Janela(10, 25).Select(p => p.Texto).ToArray());
        Assert.True(Paginacao.Janela(10, 25).Single(p => p.Numero == 10).Atual);
        Assert.False(Paginacao.Janela(10, 25).Single(p => p.Numero == 10).Clicavel);
        Assert.Equal(new[] { "1", "2", "3" }, Paginacao.Janela(1, 3).Select(p => p.Texto).ToArray());
    }

    [Fact]
    public async Task Lista_usa_a_pagina_com_total_e_navega_entre_paginas()
    {
        var (tela, ambiente) = await AbrirAsync();
        Assert.Equal("/" + Rotas.Pessoas.Pagina, ambiente.Servidor.Recebidas.Last().Caminho);
        Assert.Equal(1, tela.TotalRegistros);
        Assert.Equal("Mostrando 1–1 de 1", tela.ResumoPaginacao);
        Assert.False(tela.TemVariasPaginas);
        Assert.False(tela.MostrarEstadoVazio);

        ambiente.Servidor.Responder(HttpStatusCode.OK, Pagina(total: 120, pagina: 1, quantos: 50));
        await tela.EscolherFiltroRapidoCommand.ExecuteAsync(tela.FiltrosRapidos.Single(f => f.Chave == FiltroRapido.Fisicas));
        Assert.Equal(3, tela.TotalPaginas);
        Assert.True(tela.PodeAvancarPagina);
        Assert.Equal("Mostrando 1–50 de 120", tela.ResumoPaginacao);

        ambiente.Servidor.Responder(HttpStatusCode.OK, Pagina(total: 120, pagina: 2, quantos: 50));
        await tela.ProximaPaginaCommand.ExecuteAsync(null);
        Assert.Equal(2, tela.PaginaAtual);
        Assert.Equal("Mostrando 51–100 de 120", tela.ResumoPaginacao);
        Assert.True(tela.PodeVoltarPagina);
    }

    [Fact]
    public async Task Atalho_e_um_so_e_vira_filtro_da_API()
    {
        var (tela, ambiente) = await AbrirAsync();
        Assert.Single(tela.FiltrosRapidos, f => f.Selecionado);
        Assert.Null(tela.FiltroAtual().Natureza);

        ambiente.Servidor.Responder(HttpStatusCode.OK, Pagina(0, quantos: 0));
        await tela.EscolherFiltroRapidoCommand.ExecuteAsync(tela.FiltrosRapidos.Single(f => f.Chave == FiltroRapido.Juridicas));
        Assert.Equal(NaturezaPessoa.Juridica, tela.FiltroAtual().Natureza);
        Assert.Single(tela.FiltrosRapidos, f => f.Selecionado);
        Assert.True(tela.MostrarEstadoVazio); // lista vazia: estado vazio com "Nova pessoa"

        ambiente.Servidor.Responder(HttpStatusCode.OK, Pagina(0, quantos: 0));
        await tela.EscolherFiltroRapidoCommand.ExecuteAsync(tela.FiltrosRapidos.Single(f => f.Chave == FiltroRapido.Inativos));
        var filtro = tela.FiltroAtual();
        Assert.True(filtro.SomenteInativos);
        Assert.Null(filtro.Natureza); // atalhos não se somam

        ambiente.Servidor.Responder(HttpStatusCode.OK, Pagina(0, quantos: 0));
        await tela.EscolherFiltroRapidoCommand.ExecuteAsync(tela.FiltrosRapidos.Single(f => f.Chave == FiltroRapido.Clientes));
        Assert.Equal(Lone.Domain.Papeis.PapeisSistema.Id(TipoPapel.Cliente), tela.FiltroAtual().PapelId);
    }

    [Fact]
    public void Query_string_so_leva_os_filtros_novos_quando_informados()
    {
        Assert.Equal(string.Empty, Lone.Cliente.Api.PessoasApi.Consulta(new FiltroPessoas()));
        Assert.Equal("?natureza=Juridica&somenteInativos=true",
            Lone.Cliente.Api.PessoasApi.Consulta(new FiltroPessoas { Natureza = NaturezaPessoa.Juridica, SomenteInativos = true }));
    }

    [Fact]
    public async Task Colunas_somem_por_largura_na_ordem_de_importancia()
    {
        var (tela, _) = await AbrirAsync();
        tela.DefinirLarguraDaLista(1400);
        Assert.True(tela.MostrarColunaCidade && tela.MostrarColunaPapeis && tela.MostrarColunaTipo && tela.MostrarColunaDocumento);
        tela.DefinirLarguraDaLista(1000);
        Assert.False(tela.MostrarColunaCidade);
        Assert.True(tela.MostrarColunaPapeis);
        tela.DefinirLarguraDaLista(500);
        Assert.False(tela.MostrarColunaDocumento);
        Assert.True(tela.MostrarDocumentoNoNome); // o documento vai para baixo do nome
    }

    [Fact]
    public async Task Menu_da_linha_so_oferece_o_que_o_usuario_pode_fazer()
    {
        var ambiente = new AmbienteCliente();
        var sessao = AmbienteCliente.NovaSessao();
        sessao.Administrador = false;
        sessao.Permissoes = [Permissoes.Pessoas.Visualizar];
        await ambiente.Sessao.DefinirAsync(sessao);
        var (tela, _) = await PessoasViewModelTests.AbrirTelaComAsync(ambiente);
        ambiente.Dialogos.RespostaEscolha = null; // cancela

        await tela.AcoesDaLinhaCommand.ExecuteAsync(tela.Itens[0]);

        Assert.Equal(new[] { "Abrir ficha", "Ver histórico" }, ambiente.Dialogos.OpcoesOferecidas.ToArray()); // sem desativar
        Assert.False(tela.Editando);
    }
}
