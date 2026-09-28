using System.Net;
using Lone.Cliente.ViewModels;
using Lone.Cliente.ViewModels.Comum;
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
        await tela.EscolherFiltroRapidoCommand.ExecuteAsync(tela.FiltrosRapidos.Single(f => f.Chave == FiltroRapido.Clientes));
        var filtro = tela.FiltroAtual();
        Assert.Equal(Lone.Domain.Papeis.PapeisSistema.Id(TipoPapel.Cliente), filtro.PapelId);
        Assert.Null(filtro.Natureza); // atalhos não se somam
        Assert.False(filtro.SomenteInativos);
    }

    [Fact]
    public async Task Abas_sao_os_atalhos_sem_ativos_e_inativos_e_mostram_a_contagem_da_API()
    {
        var (tela, ambiente) = await AbrirAsync();
        Assert.Equal(new[] { FiltroRapido.Todos, FiltroRapido.Fisicas, FiltroRapido.Juridicas, FiltroRapido.Clientes, FiltroRapido.Fornecedores },
            tela.FiltrosRapidos.Select(f => f.Chave).ToArray());
        Assert.All(tela.FiltrosRapidos, f => Assert.False(f.TemQuantidade)); // a primeira página do teste veio sem contagens

        var pagina = Pagina(total: 1248, quantos: 2);
        pagina.Atalhos = new ContagensAtalhosPessoas
        {
            Todos = 1248,
            Naturezas = { [nameof(NaturezaPessoa.Fisica)] = 1000, [nameof(NaturezaPessoa.Juridica)] = 248 },
            Papeis =
            {
                [Lone.Domain.Papeis.PapeisSistema.Id(TipoPapel.Cliente)] = 900,
                [Lone.Domain.Papeis.PapeisSistema.Id(TipoPapel.Fornecedor)] = 120
            }
        };
        ambiente.Servidor.Responder(HttpStatusCode.OK, pagina);
        await tela.EscolherFiltroRapidoCommand.ExecuteAsync(tela.FiltrosRapidos.Single(f => f.Chave == FiltroRapido.Fisicas));

        Assert.Equal("1.248", tela.FiltrosRapidos.Single(f => f.Chave == FiltroRapido.Todos).TextoQuantidade);
        Assert.Equal("Clientes, 900", tela.FiltrosRapidos.Single(f => f.Chave == FiltroRapido.Clientes).Descricao);

        // Página 2 vem sem contagens: as abas continuam com as da página 1 (mesmos filtros).
        ambiente.Servidor.Responder(HttpStatusCode.OK, Pagina(total: 1248, pagina: 2, quantos: 2));
        await tela.ProximaPaginaCommand.ExecuteAsync(null);
        Assert.Equal(248, tela.FiltrosRapidos.Single(f => f.Chave == FiltroRapido.Juridicas).Quantidade);
    }

    [Fact]
    public async Task Atalho_entra_nos_criterios_da_visao_e_da_exportacao_como_condicao()
    {
        var (tela, ambiente) = await AbrirAsync();
        Assert.Empty(tela.CriteriosDaTela().Condicoes);
        Assert.Null(tela.CriteriosDaTela().Texto);

        ambiente.Servidor.Responder(HttpStatusCode.OK, Pagina(0, quantos: 0));
        await tela.EscolherFiltroRapidoCommand.ExecuteAsync(tela.FiltrosRapidos.Single(f => f.Chave == FiltroRapido.Juridicas));
        var natureza = Assert.Single(tela.CriteriosDaTela().Condicoes);
        Assert.Equal(CamposFiltroPessoas.Natureza, natureza.Campo);
        Assert.Equal(OperadorFiltro.UmDestes, natureza.Operador);
        Assert.Equal(new[] { nameof(NaturezaPessoa.Juridica) }, natureza.Valores);

        ambiente.Servidor.Responder(HttpStatusCode.OK, Pagina(0, quantos: 0));
        await tela.EscolherFiltroRapidoCommand.ExecuteAsync(tela.FiltrosRapidos.Single(f => f.Chave == FiltroRapido.Clientes));
        var papel = Assert.Single(tela.CriteriosDaTela().Condicoes);
        Assert.Equal(CamposFiltroPessoas.Papeis, papel.Campo);
        Assert.Equal(new[] { Lone.Domain.Papeis.PapeisSistema.Id(TipoPapel.Cliente).ToString("D") }, papel.Valores);
    }

    [Fact]
    public async Task Visao_volta_o_atalho_para_todos_rele_a_lista_e_avisa_o_que_nao_deu_para_aplicar()
    {
        var (tela, ambiente) = await AbrirAsync();
        ambiente.Servidor.Responder(HttpStatusCode.OK, Pagina(0, quantos: 0));
        await tela.EscolherFiltroRapidoCommand.ExecuteAsync(tela.FiltrosRapidos.Single(f => f.Chave == FiltroRapido.Juridicas));
        var antes = ambiente.Servidor.Recebidas.Count;

        // O catálogo desta tela veio vazio: o campo da visão não existe para o usuário (ex.: sem permissão).
        var visao = new FiltroSalvoDto
        {
            Id = Guid.NewGuid(), Nome = "Clientes de MG", Proprio = true,
            Criterios = new CriteriosPessoas
            {
                Condicoes = [new() { Campo = CamposFiltroPessoas.Uf, Operador = OperadorFiltro.UmDestes, Valores = ["MG"] }]
            }
        };
        ambiente.Servidor.Responder(HttpStatusCode.OK, Pagina(total: 3, quantos: 3));
        await tela.AplicarVisaoAsync(visao);

        Assert.True(tela.FiltrosRapidos.Single(f => f.Chave == FiltroRapido.Todos).Selecionado);
        Assert.Null(tela.FiltroAtual().Natureza);
        Assert.Equal(antes + 1, ambiente.Servidor.Recebidas.Count); // releu uma vez, na hora
        Assert.Equal(3, tela.TotalRegistros);
        Assert.Same(visao, tela.VisaoAtual);
        Assert.Equal("Visão: Clientes de MG", tela.TextoBotaoVisoes);
        Assert.Equal(TipoMensagem.Aviso, tela.TipoMensagem);
        Assert.Contains("Parte da visão", tela.Mensagem);

        ambiente.Servidor.Responder(HttpStatusCode.OK, Pagina(0, quantos: 0));
        await tela.EscolherFiltroRapidoCommand.ExecuteAsync(tela.FiltrosRapidos.Single(f => f.Chave == FiltroRapido.Clientes));
        Assert.Equal("Visão: Clientes de MG (alterada)", tela.TextoBotaoVisoes);
    }

    [Fact]
    public void Query_string_so_leva_os_filtros_novos_quando_informados()
    {
        Assert.Equal(string.Empty, Lone.Cliente.Api.PessoasApi.Consulta(new FiltroPessoas()));
        Assert.Equal("?natureza=Juridica&somenteInativos=true",
            Lone.Cliente.Api.PessoasApi.Consulta(new FiltroPessoas { Natureza = NaturezaPessoa.Juridica, SomenteInativos = true }));
    }

    [Fact]
    public async Task Tela_estreita_mostra_so_o_nome_com_o_documento_embaixo()
    {
        var (tela, _) = await AbrirAsync();
        tela.DefinirLarguraDaLista(1400);
        Assert.True(tela.Grade.MostrarColunas);
        var linha = Assert.Single(tela.Linhas);
        Assert.Equal(5, linha.Celulas.Count); // sem catálogo de colunas: as de sempre
        Assert.StartsWith("Cód.", linha.Subtitulo);

        tela.DefinirLarguraDaLista(500);
        Assert.False(tela.Grade.MostrarColunas);
        linha = Assert.Single(tela.Linhas);
        Assert.Empty(linha.Celulas);
        Assert.Equal(tela.Itens[0].DocumentoFormatado, linha.Subtitulo);
    }

    [Fact]
    public async Task Ordenar_pelo_titulo_rele_da_pagina_1_e_guarda_a_escolha_do_usuario()
    {
        var (tela, ambiente) = await AbrirAsync();
        tela.EsperaParaSalvarColunas = TimeSpan.FromMilliseconds(150); // a releitura sai antes da gravação
        var antes = ambiente.Servidor.Recebidas.Count;

        ambiente.Servidor.Responder(HttpStatusCode.OK, Pagina(total: 2));   // releitura ordenada
        ambiente.Servidor.Responder(HttpStatusCode.NoContent);              // preferência da tela
        tela.Grade.Visiveis.Single(c => c.Id == CamposFiltroPessoas.Cidade).OrdenarCommand.Execute(null);
        await tela.SalvandoColunas;
        for (var i = 0; i < 50 && ambiente.Servidor.Recebidas.Count < antes + 2; i++) await Task.Delay(10);

        var novas = ambiente.Servidor.Recebidas.Skip(antes).ToList();
        var lista = Assert.Single(novas, r => r.Caminho == "/" + Rotas.Pessoas.Pagina);
        Assert.Equal(HttpMethod.Post, lista.Metodo); // com ordenação, vai no corpo
        Assert.Contains(CamposFiltroPessoas.Cidade, lista.Corpo);
        var preferencia = Assert.Single(novas, r => r.Caminho == "/" + Rotas.Menu.Tela(ColunasPessoas.TelaLista));
        Assert.Equal(HttpMethod.Put, preferencia.Metodo);
        Assert.Contains(CamposFiltroPessoas.Cidade, preferencia.Corpo);
        Assert.Equal(2, tela.TotalRegistros);
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
    [Fact]
    public void Telefone_vira_endereco_de_ligacao_e_de_whatsapp_com_o_55_do_brasil()
    {
        Assert.Equal("tel:+5538999887766", PessoasViewModel.EnderecoDoTelefone("(38) 99988-7766", whatsApp: false));
        Assert.Equal("https://wa.me/553837210001", PessoasViewModel.EnderecoDoTelefone("38 3721-0001", whatsApp: true));
        Assert.Equal("tel:+14155550100", PessoasViewModel.EnderecoDoTelefone("+1 415 555 0100", whatsApp: false)); // já com o país
        Assert.Equal("tel:+5538999887766", PessoasViewModel.EnderecoDoTelefone("038 99988-7766", whatsApp: false)); // 0 de longa distância
        Assert.Null(PessoasViewModel.EnderecoDoTelefone("ramal 12", whatsApp: false));
        Assert.Null(PessoasViewModel.EnderecoDoTelefone(null, whatsApp: true));
    }

    [Fact]
    public async Task Acoes_rapidas_abrem_o_endereco_do_contato_principal()
    {
        var (tela, _) = await AbrirAsync();
        var abertos = new List<string>();
        tela.AbrirEndereco = e => { abertos.Add(e); return Task.CompletedTask; };
        var linha = new LinhaPessoa(new PessoaResumo
        {
            Id = Guid.NewGuid(), Nome = "Froés", Natureza = NaturezaPessoa.Juridica,
            Valores = new() { [CamposFiltroPessoas.Telefone] = "3837210001", [CamposFiltroPessoas.Email] = "financeiro@froes.com.br" }
        }, "Cód. 000001", []);

        await tela.LigarCommand.ExecuteAsync(linha);
        await tela.WhatsAppCommand.ExecuteAsync(linha);
        await tela.EnviarEmailCommand.ExecuteAsync(linha);
        Assert.Equal(new[] { "tel:+553837210001", "https://wa.me/553837210001", "mailto:financeiro@froes.com.br" }, abertos);

        tela.AbrirEndereco = _ => throw new InvalidOperationException("sem aplicativo");
        await tela.LigarCommand.ExecuteAsync(linha);
        Assert.Equal(TipoMensagem.Aviso, tela.TipoMensagem);
    }

    [Fact]
    public async Task Sucesso_na_lista_flutua_e_some_sozinho_e_erro_fica_na_barra()
    {
        var (tela, _) = await AbrirAsync();
        tela.TempoAvisoFlutuante = TimeSpan.FromMilliseconds(50);
        var linha = new LinhaPessoa(new PessoaResumo { Id = Guid.NewGuid(), Nome = "Ana", Natureza = NaturezaPessoa.Fisica,
            Valores = new() { [CamposFiltroPessoas.Telefone] = "38999887766" } }, "", []);

        // Erro: barra (fica até a próxima ação).
        tela.AbrirEndereco = _ => throw new InvalidOperationException();
        await tela.LigarCommand.ExecuteAsync(linha);
        Assert.True(tela.MostrarBarraDaLista);
        Assert.False(tela.MostrarAvisoFlutuante);

        // Sucesso (ex.: "Visão removida."): flutua e some sozinho.
        tela.FecharAvisoFlutuanteCommand.Execute(null);
        Assert.False(tela.TemMensagem);
        tela.TipoMensagem = TipoMensagem.Sucesso;
        tela.Mensagem = "Visão removida.";
        Assert.True(tela.MostrarAvisoFlutuante);
        Assert.False(tela.MostrarBarraDaLista);
        for (var i = 0; i < 40 && tela.TemMensagem; i++) await Task.Delay(25);
        Assert.False(tela.TemMensagem);
    }

    [Fact]
    public async Task Configuracoes_da_tela_abrem_a_pagina_de_configuracoes_de_pessoas_em_cartoes()
    {
        var (tela, _) = await AbrirAsync();
        Assert.True(tela.PodeConfigurar);
        var abertas = new List<string>();
        tela.AbrirTela = rota => { abertas.Add(rota); return Task.CompletedTask; };

        await tela.ConfiguracoesCommand.ExecuteAsync(null);
        Assert.Equal(new[] { "configuracoes-pessoas" }, abertas);
    }
    [Fact]
    public async Task Usuario_poe_a_aba_Transportadoras_com_contador_e_a_escolha_fica_guardada()
    {
        var (tela, ambiente) = await AbrirAsync();
        tela.EsperaParaSalvarColunas = TimeSpan.Zero;
        var transportadora = Lone.Domain.Papeis.PapeisSistema.Id(TipoPapel.Transportadora);
        var idAba = AbasPessoas.Papel(transportadora);

        ambiente.Servidor.Responder(HttpStatusCode.OK, new OpcoesConsultaPessoasDto()); // visões, lidas ao abrir o editor
        await tela.EditarAbasCommand.ExecuteAsync(null);
        Assert.True(tela.Abas.Aberto);
        var item = tela.Abas.Grupos.SelectMany(g => g.Itens).Single(i => i.Id == idAba);
        Assert.Equal("Transportadoras", item.Nome);
        Assert.False(item.Marcado);

        ambiente.Servidor.Responder(HttpStatusCode.NoContent); // preferência da tela
        item.Marcado = true;
        await tela.SalvandoColunas;
        Assert.Equal("Transportadoras", tela.FiltrosRapidos[^1].Texto);
        var preferencia = ambiente.Servidor.Recebidas.Last();
        Assert.EndsWith(Rotas.Menu.Tela(ColunasPessoas.TelaLista), preferencia.Caminho);
        Assert.Contains(idAba, preferencia.Corpo);

        var pagina = Pagina(total: 2, quantos: 2);
        pagina.Atalhos = new ContagensAtalhosPessoas { Todos = 6, Papeis = { [transportadora] = 2 } };
        ambiente.Servidor.Responder(HttpStatusCode.OK, pagina);
        await tela.EscolherFiltroRapidoCommand.ExecuteAsync(tela.FiltrosRapidos[^1]);
        Assert.Equal(transportadora, tela.FiltroAtual().PapelId);
        Assert.Equal(2, tela.FiltrosRapidos[^1].Quantidade);
        Assert.Equal(0, tela.FiltrosRapidos.Single(f => f.Chave == FiltroRapido.Clientes).Quantidade); // papel sem ninguém = 0

        // Tirar a aba marcada: volta para "Todos" e relê (a gravação da preferência sai depois).
        tela.EsperaParaSalvarColunas = TimeSpan.FromMilliseconds(100);
        ambiente.Servidor.Responder(HttpStatusCode.OK, Pagina(total: 6, quantos: 2)); // releitura em "Todos"
        ambiente.Servidor.Responder(HttpStatusCode.NoContent);                         // preferência
        tela.Abas.TirarCommand.Execute(tela.Abas.NaLista.Single(i => i.Id == idAba));
        await tela.SalvandoColunas;
        Assert.DoesNotContain(tela.FiltrosRapidos, f => f.Chave == idAba);
        Assert.True(tela.FiltrosRapidos[0].Selecionado);
        Assert.Null(tela.FiltroAtual().PapelId);
    }

    [Fact]
    public async Task Editor_de_abas_respeita_o_maximo_e_restaura_o_padrao()
    {
        var (tela, ambiente) = await AbrirAsync();
        tela.EsperaParaSalvarColunas = TimeSpan.FromHours(1); // a gravação não sai neste teste
        ambiente.Servidor.Responder(HttpStatusCode.OK, new OpcoesConsultaPessoasDto());
        await tela.EditarAbasCommand.ExecuteAsync(null);

        foreach (var item in tela.Abas.Grupos.SelectMany(g => g.Itens).Where(i => !i.Marcado).ToList()) item.Marcado = true;
        Assert.Equal(AbasPessoas.Maximo, tela.Abas.Ids.Count); // 3 tipos + 8 papéis de sistema = 11 possíveis; para em 10
        Assert.True(tela.Abas.TemAviso);
        Assert.Equal(AbasPessoas.Maximo + 1, tela.FiltrosRapidos.Count); // + "Todos"

        tela.Abas.RestaurarPadraoCommand.Execute(null);
        Assert.Equal(FiltroRapido.Padrao, tela.Abas.Ids);
        Assert.False(tela.Abas.TemAviso);
    }

    [Fact]
    public async Task Aba_de_visao_mostra_quantos_a_visao_traz_aplica_a_visao_e_ao_sair_limpa_os_filtros_dela()
    {
        var (tela, ambiente) = await AbrirAsync();
        tela.EsperaParaSalvarColunas = TimeSpan.Zero;
        var visao = new FiltroSalvoDto { Id = Guid.NewGuid(), Nome = "Clientes de Curvelo", Proprio = true, Criterios = new CriteriosPessoas() };
        var idAba = AbasPessoas.Visao(visao.Id);

        ambiente.Servidor.Responder(HttpStatusCode.OK, new OpcoesConsultaPessoasDto { Filtros = [visao] });
        await tela.EditarAbasCommand.ExecuteAsync(null);
        tela.EsperaParaSalvarColunas = TimeSpan.FromMilliseconds(100); // o contador sai antes da gravação
        ambiente.Servidor.Responder(HttpStatusCode.OK, new Dictionary<Guid, int> { [visao.Id] = 7 }); // contador da visão
        ambiente.Servidor.Responder(HttpStatusCode.NoContent);                                   // preferência
        tela.Abas.Grupos.SelectMany(g => g.Itens).Single(i => i.Id == idAba).Marcado = true;
        await tela.SalvandoColunas;
        var aba = tela.FiltrosRapidos.Single(f => f.Chave == idAba);
        for (var i = 0; i < 100 && aba.Quantidade is null; i++) await Task.Delay(10);
        Assert.Equal(7, aba.Quantidade);
        Assert.Equal("★ Clientes de Curvelo", aba.TextoExibido);
        Assert.Contains(ambiente.Servidor.Recebidas, r => r.Caminho == "/" + Rotas.Pessoas.ContagemFiltros);

        // A página da visão traz contagens com os filtros dela: as abas de natureza/papel ficam sem número (tocar nelas
        // limpa esses filtros, e o número não corresponderia ao que a aba traz).
        var paginaDaVisao = Pagina(total: 7, quantos: 2);
        paginaDaVisao.Atalhos = new ContagensAtalhosPessoas { Todos = 7, Naturezas = { ["Fisica"] = 5 } };
        ambiente.Servidor.Responder(HttpStatusCode.OK, paginaDaVisao);
        await tela.EscolherFiltroRapidoCommand.ExecuteAsync(aba);
        Assert.True(aba.Selecionado);
        Assert.Equal(visao.Id, tela.VisaoAtual?.Id);
        Assert.Null(tela.FiltroAtual().PapelId);
        Assert.All(tela.FiltrosRapidos.Where(f => !f.EhVisao), f => Assert.Null(f.Quantidade));

        ambiente.Servidor.Responder(HttpStatusCode.OK, Pagina(total: 6, quantos: 2));
        await tela.EscolherFiltroRapidoCommand.ExecuteAsync(tela.FiltrosRapidos[0]);
        Assert.Null(tela.VisaoAtual); // saiu da aba da visão: os filtros eram dela
        Assert.True(tela.FiltrosRapidos[0].Selecionado);
    }

    // ---- Prévia ao lado da lista (Etapa 3) ----

    private static async Task<(PessoasViewModel Tela, AmbienteCliente Ambiente)> AbrirComTresPessoasAsync()
    {
        var (tela, ambiente) = await AbrirAsync();
        tela.Previa.EsperaParaLer = TimeSpan.Zero;
        tela.DefinirLarguraDaLista(1600); // janela larga: a prévia cabe ao lado
        ambiente.Servidor.Responder(HttpStatusCode.OK, Pagina(total: 3, quantos: 3));
        await tela.EscolherFiltroRapidoCommand.ExecuteAsync(tela.FiltrosRapidos.Single(f => f.Chave == FiltroRapido.Fisicas));
        return (tela, ambiente);
    }

    private static PessoaDto Ficha(LinhaPessoa linha) =>
        new() { Id = linha.Pessoa.Id, Codigo = linha.Pessoa.Codigo, Nome = linha.Nome, Natureza = NaturezaPessoa.Fisica };

    [Fact]
    public async Task Clique_na_linha_mostra_a_previa_com_o_resumo_sem_abrir_a_ficha()
    {
        var (tela, ambiente) = await AbrirComTresPessoasAsync();
        Assert.True(tela.Previa.Cabe);
        var primeira = tela.Linhas[0];

        ambiente.Servidor.Responder(HttpStatusCode.OK, Ficha(primeira));
        tela.AbrirLinhaCommand.Execute(primeira);
        await tela.Previa.Lendo;

        Assert.False(tela.Editando); // a ficha não abriu
        Assert.True(tela.Previa.Visivel);
        Assert.True(primeira.NaPrevia);
        Assert.Equal("Pessoa 1", tela.Previa.Nome);
        Assert.Equal("1 de 3", tela.Previa.Posicao);
        Assert.False(tela.Previa.TemAnterior);
        Assert.True(tela.Previa.TemProxima);
        Assert.False(tela.Previa.Carregando);
        Assert.Contains(tela.Previa.Resumo.Blocos, b => b.Titulo == "Cadastro");
        Assert.Contains(tela.Previa.Resumo.Blocos.SelectMany(b => b.Itens), i => i.Texto == "CPF não informado");
        Assert.Equal("/" + Rotas.Pessoas.PorId(primeira.Pessoa.Id), ambiente.Servidor.Recebidas.Last().Caminho);

        // A mesma pessoa de novo: não lê outra vez.
        var lidas = ambiente.Servidor.Recebidas.Count;
        tela.AbrirLinhaCommand.Execute(primeira);
        await tela.Previa.Lendo;
        Assert.Equal(lidas, ambiente.Servidor.Recebidas.Count);

        // Próxima: troca a pessoa e a linha marcada.
        ambiente.Servidor.Responder(HttpStatusCode.OK, Ficha(tela.Linhas[1]));
        tela.Previa.ProximaCommand.Execute(null);
        await tela.Previa.Lendo;
        Assert.Equal("Pessoa 2", tela.Previa.Nome);
        Assert.Equal("2 de 3", tela.Previa.Posicao);
        Assert.False(primeira.NaPrevia);
        Assert.True(tela.Linhas[1].NaPrevia);

        tela.Previa.FecharCommand.Execute(null);
        Assert.False(tela.Previa.Visivel);
        Assert.False(tela.Linhas[1].NaPrevia);
    }

    [Fact]
    public async Task Previa_continua_na_pessoa_quando_a_lista_e_relida()
    {
        var (tela, ambiente) = await AbrirComTresPessoasAsync();
        ambiente.Servidor.Responder(HttpStatusCode.OK, Ficha(tela.Linhas[0]));
        tela.AbrirLinhaCommand.Execute(tela.Linhas[0]);
        await tela.Previa.Lendo;
        var lidas = ambiente.Servidor.Recebidas.Count;
        tela.EsperaParaSalvarColunas = TimeSpan.FromHours(1); // a gravação da densidade não sai neste teste

        tela.Grade.AlternarDensidadeCommand.Execute(null); // as linhas são refeitas
        Assert.Equal(lidas, ambiente.Servidor.Recebidas.Count); // sem ler a pessoa de novo
        Assert.Same(tela.Linhas[0], tela.Previa.Linha);
        Assert.True(tela.Linhas[0].NaPrevia);
    }

    [Fact]
    public async Task Previa_de_cadastro_que_nao_existe_mais_avisa()
    {
        var (tela, ambiente) = await AbrirComTresPessoasAsync();
        ambiente.Servidor.Responder(HttpStatusCode.NotFound);
        tela.AbrirLinhaCommand.Execute(tela.Linhas[2]);
        await tela.Previa.Lendo;

        Assert.True(tela.Previa.TemErro);
        Assert.Equal("Este cadastro não existe mais.", tela.Previa.Erro);
        Assert.False(tela.Previa.MostrarResumo);
        Assert.False(tela.Editando);
    }

    [Fact]
    public async Task Escolha_do_clique_abrir_a_ficha_fica_guardada_e_sem_espaco_o_clique_abre_a_ficha()
    {
        var (tela, ambiente) = await AbrirComTresPessoasAsync();
        tela.EsperaParaSalvarColunas = TimeSpan.Zero;
        ambiente.Servidor.Responder(HttpStatusCode.NoContent); // preferência
        tela.AlternarCliqueNaLinhaCommand.Execute(null);
        await tela.SalvandoColunas;

        Assert.True(tela.CliqueAbreFicha);
        Assert.Equal("Clique: abre a ficha", tela.TextoCliqueNaLinha);
        var corpo = ambiente.Servidor.Recebidas.Last(r => r.Caminho == "/" + Rotas.Menu.Tela(ColunasPessoas.TelaLista)).Corpo;
        var preferencia = System.Text.Json.JsonSerializer.Deserialize<Lone.Contracts.Menu.PreferenciaTelaDto>(corpo,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        Assert.True(System.Text.Json.JsonSerializer.Deserialize<LayoutListaPessoas>(preferencia.Conteudo)!.CliqueAbreFicha);

        // Com "abre a ficha", o clique abre a ficha e a prévia não aparece.
        var linha = tela.Linhas[0];
        ambiente.Servidor.Responder(HttpStatusCode.OK, Ficha(linha));
        tela.AbrirLinhaCommand.Execute(linha);
        for (var i = 0; i < 100 && tela.Formulario?.Id != linha.Pessoa.Id; i++) await Task.Delay(10);
        Assert.Equal(linha.Pessoa.Id, tela.Formulario?.Id);
        Assert.False(tela.Previa.Visivel);
    }

    [Fact]
    public async Task Sem_espaco_ao_lado_o_clique_abre_a_ficha_e_o_menu_da_linha_nao_oferece_previa()
    {
        var (tela, ambiente) = await AbrirAsync(); // sem largura definida (como no celular): a prévia não cabe
        Assert.False(tela.Previa.Cabe);
        var linha = tela.Linhas[0];
        ambiente.Servidor.Responder(HttpStatusCode.OK, Ficha(linha));
        tela.AbrirLinhaCommand.Execute(linha);
        for (var i = 0; i < 100 && tela.Formulario?.Id != linha.Pessoa.Id; i++) await Task.Delay(10);
        Assert.Equal(linha.Pessoa.Id, tela.Formulario?.Id);
        Assert.False(tela.Previa.Aberta);
    }

    [Fact]
    public async Task Previa_aberta_tira_espaco_das_colunas_em_janela_media()
    {
        var (tela, ambiente) = await AbrirComTresPessoasAsync();
        tela.DefinirLarguraDaLista(1100); // cabe a prévia (380 + 16 + 700 = 1096)
        Assert.True(tela.Previa.Cabe);
        Assert.True(tela.Grade.MostrarColunas);

        ambiente.Servidor.Responder(HttpStatusCode.OK, Ficha(tela.Linhas[0]));
        tela.AbrirLinhaCommand.Execute(tela.Linhas[0]);
        await tela.Previa.Lendo;
        Assert.True(tela.Grade.MostrarColunas); // sobram 704: as colunas continuam (rolam para o lado)

        tela.DefinirLarguraDaLista(1000); // não cabe mais: a prévia some e a lista volta a ter a largura toda
        Assert.False(tela.Previa.Visivel);
        Assert.True(tela.Grade.MostrarColunas);
    }

    [Fact]
    public void Nome_do_papel_vira_plural_na_aba()
    {
        Assert.Equal("Transportadoras", CatalogoAbas.Plural("Transportadora"));
        Assert.Equal("Cliente especial", CatalogoAbas.Plural("Cliente especial")); // sem regra segura: fica no singular
        Assert.Equal("Cidadão", CatalogoAbas.Plural("Cidadão"));
        Assert.Equal("Prestadores de serviço", CatalogoAbas.Plural("Prestador de serviço"));
        Assert.Equal("Empresas do grupo", CatalogoAbas.Plural("Empresa do grupo"));
        Assert.Equal("Funcionários", CatalogoAbas.Plural("Funcionário"));
        Assert.Equal("Vendedores", CatalogoAbas.Plural("Vendedor"));
        Assert.Equal("CLIENTES", CatalogoAbas.Plural("CLIENTE"));
        Assert.Equal("Ônibus", CatalogoAbas.Plural("Ônibus"));
    }
}
