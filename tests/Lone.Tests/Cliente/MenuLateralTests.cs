using System.Net;
using Lone.Cliente.Api;
using Lone.Cliente.ViewModels;
using Lone.Contracts.Comum;
using Lone.Contracts.Menu;
using Lone.Contracts.Seguranca;

namespace Lone.Tests.Cliente;

/// <summary>
/// Menu lateral em dois níveis: busca · Início · Favoritos · Recentes · módulos que abrem e fecham (Pessoas, Organização,
/// Metas) · Configurações do sistema. Favoritos e recentes guardados na API, por usuário.
/// </summary>
public class MenuLateralTests
{
    private static readonly Func<string, bool> Tudo = _ => true;

    private static async Task<(AmbienteCliente Ambiente, MenuViewModel Menu)> CriarMenuAsync()
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao()); // administrador: vê tudo
        var menu = new MenuViewModel(ambiente.Sessao, ambiente.Autenticacao, new NavegacaoGravada(), new MenuUsuarioApi(ambiente.Api));
        return (ambiente, menu);
    }

    /// <summary>Navega como o Shell faz; a tela aberta vai para os recentes e a API recebe o acesso.</summary>
    private static async Task AbrirAsync(AmbienteCliente ambiente, MenuViewModel menu, string rota)
    {
        ambiente.Servidor.Responder(HttpStatusCode.NoContent);
        menu.DefinirRotaAtual("//" + rota);
        await menu.UltimoRegistroDeAcesso;
    }

    private static ItemMenu Item(MenuViewModel menu, string rota) =>
        menu.Secoes.SelectMany(s => s.Itens).Single(i => i.Rota == rota);

    private static SecaoMenu Secao(MenuViewModel menu, string titulo) => menu.Secoes.Single(s => s.Titulo == titulo);

    [Fact]
    public void Administrador_ve_os_modulos_com_itens_curtos_e_o_nome_completo_na_descricao()
    {
        var secoes = MenuViewModel.CriarSecoes(Tudo);

        Assert.Equal(new string?[] { "Pessoas", "Organização", "Metas", null }, secoes.Select(s => s.Titulo).ToArray());
        Assert.Equal(new[] { "pessoas", "consulta-pessoas", "configuracoes-pessoas" }, secoes[0].Itens.Select(i => i.Rota).ToArray());
        // Dentro do módulo, sem repetir o nome dele; fora (atalhos, busca, leitor de tela), o nome completo.
        Assert.Equal(new[] { "Cadastro", "Consulta avançada", "⚙  Configurações" }, secoes[0].Itens.Select(i => i.TextoExibido).ToArray());
        Assert.Equal(new[] { "Cadastro de pessoas", "Consulta avançada de pessoas", "Configurações de Pessoas" },
            secoes[0].Itens.Select(i => i.Descricao).ToArray());
        Assert.Equal(new[] { "grupos-empresariais", "configuracoes-organizacao" }, secoes[1].Itens.Select(i => i.Rota).ToArray());
        Assert.Equal(new[] { "Painel", "⚙  Configurações" }, secoes[2].Itens.Select(i => i.TextoExibido).ToArray());
        Assert.Equal("Painel de metas", secoes[2].Itens[0].Descricao);
        Assert.Equal("Configurações de Metas", secoes[2].Itens[1].Descricao);
        var sistema = Assert.Single(secoes[3].Itens);
        Assert.Equal("configuracoes-sistema", sistema.Rota);
        Assert.Equal("⚙  Configurações do sistema", sistema.TextoExibido);
        Assert.True(secoes[3].MostrarItens); // entrada solta: sem cabeçalho, sempre visível
    }

    [Fact]
    public void Sem_permissao_o_item_e_o_modulo_nao_aparecem()
    {
        var secoes = MenuViewModel.CriarSecoes(p => p == Permissoes.Pessoas.Visualizar);

        var pessoas = Assert.Single(secoes);
        Assert.Equal(new[] { "pessoas", "consulta-pessoas" }, pessoas.Itens.Select(i => i.Rota).ToArray()); // sem configurações

        var soConfiguracao = MenuViewModel.CriarSecoes(p => p == Permissoes.Cadastros.Etiquetas);
        Assert.Equal(new[] { "configuracoes-pessoas" }, Assert.Single(soConfiguracao).Itens.Select(i => i.Rota).ToArray());
    }

    [Fact]
    public void Catalogo_tem_inicio_as_telas_dos_modulos_e_os_cadastros_de_configuracao_uma_vez_cada()
    {
        var inicio = new ItemMenu("Início", ItemMenu.RotaInicio);
        var catalogo = MenuViewModel.CriarCatalogo(inicio, MenuViewModel.CriarSecoes(Tudo), Tudo);

        Assert.Same(inicio, catalogo[0]);
        Assert.Equal(catalogo.Count, catalogo.Select(i => i.Rota).Distinct().Count());
        Assert.Equal("Pessoas › Configurações", catalogo.Single(i => i.Rota == "papeis").Caminho);
        Assert.Equal("Metas › Configurações", catalogo.Single(i => i.Rota == "equipes").Caminho);
        Assert.Equal("Configurações do sistema", catalogo.Single(i => i.Rota == "usuarios").Caminho);
        Assert.Equal("Pessoas", catalogo.Single(i => i.Rota == "pessoas").Caminho);
        Assert.False(inicio.PodeFavoritar);

        var semEtiquetas = MenuViewModel.CriarCatalogo(inicio, MenuViewModel.CriarSecoes(p => p != Permissoes.Cadastros.Etiquetas),
            p => p != Permissoes.Cadastros.Etiquetas);
        Assert.DoesNotContain(semEtiquetas, i => i.Rota == "etiquetas"); // sem permissão, nem a busca encontra
    }

    [Fact]
    public void Busca_ignora_acentos_e_maiusculas_exige_todas_as_palavras_e_poe_primeiro_o_que_comeca_pelo_texto()
    {
        var catalogo = MenuViewModel.CriarCatalogo(new ItemMenu("Início", ItemMenu.RotaInicio), MenuViewModel.CriarSecoes(Tudo), Tudo);

        Assert.Equal("papeis", Assert.Single(MenuViewModel.Pesquisar(catalogo, "PAPEIS")).Rota);
        Assert.Equal("consulta-pessoas", MenuViewModel.Pesquisar(catalogo, "consulta")[0].Rota);
        Assert.Equal("configuracoes-pessoas", MenuViewModel.Pesquisar(catalogo, "config pessoas")[0].Rota);
        // "tipos" aparece em vários cadastros; pelo caminho, a busca também acha tudo que fica em Pessoas › Configurações.
        Assert.Contains(MenuViewModel.Pesquisar(catalogo, "tipos pessoas"), i => i.Rota == "tipos-documento");
        Assert.Empty(MenuViewModel.Pesquisar(catalogo, "   "));
        Assert.Empty(MenuViewModel.Pesquisar(catalogo, "estoque"));
    }

    [Fact]
    public async Task Modulos_comecam_fechados_abrem_ao_tocar_e_o_da_tela_aberta_abre_sozinho()
    {
        var (ambiente, menu) = await CriarMenuAsync();
        var pessoas = Secao(menu, "Pessoas");
        var metas = Secao(menu, "Metas");
        Assert.False(pessoas.Expandida);
        Assert.False(pessoas.MostrarItens);

        metas.AlternarCommand.Execute(null);
        Assert.True(metas.MostrarItens);
        Assert.Equal(90d, metas.RotacaoSeta);

        await AbrirAsync(ambiente, menu, "papeis"); // cadastro aberto a partir de Configurações de Pessoas
        Assert.True(pessoas.Expandida);
        Assert.True(pessoas.ContemAtivo);
        Assert.True(Item(menu, "configuracoes-pessoas").Ativo);
        Assert.True(metas.Expandida); // os outros ficam como o usuário deixou

        pessoas.AlternarCommand.Execute(null); // fechar com a tela aberta dentro: o cabeçalho continua destacado
        Assert.False(pessoas.MostrarItens);
        Assert.True(pessoas.ContemAtivo);

        menu.MontarMenu(); // sessão relida (permissões): o que estava aberto continua aberto
        Assert.True(Secao(menu, "Metas").Expandida);
        Assert.False(Secao(menu, "Pessoas").Expandida);
    }

    [Fact]
    public async Task Tela_aberta_vai_para_o_topo_dos_recentes_e_a_api_recebe_o_acesso()
    {
        var (ambiente, menu) = await CriarMenuAsync();

        await AbrirAsync(ambiente, menu, "pessoas");
        await AbrirAsync(ambiente, menu, "papeis");
        await AbrirAsync(ambiente, menu, "pessoas");
        menu.DefinirRotaAtual("//inicio"); // Início não é recente (nenhuma chamada à API)

        Assert.Equal(new[] { "pessoas", "papeis" }, menu.Recentes.Itens.Select(i => i.Rota).ToArray());
        Assert.Equal("Cadastro de pessoas", menu.Recentes.Itens[0].Descricao);
        Assert.Equal(3, ambiente.Servidor.Recebidas.Count);
        Assert.All(ambiente.Servidor.Recebidas, r =>
        {
            Assert.Equal(HttpMethod.Post, r.Metodo);
            Assert.EndsWith(Rotas.Menu.Acessos, r.Caminho);
        });
        Assert.Contains("papeis", ambiente.Servidor.Recebidas[1].Corpo);
    }

    [Fact]
    public async Task Recente_sem_resposta_da_api_continua_na_lista()
    {
        var (ambiente, menu) = await CriarMenuAsync();
        ambiente.Servidor.ForaDoAr();

        menu.DefinirRotaAtual("//metas");
        await menu.UltimoRegistroDeAcesso; // a falha não sobe

        Assert.Equal("metas", Assert.Single(menu.Recentes.Itens).Rota);
        Assert.False(menu.TemAviso);
    }

    [Fact]
    public async Task Preferencias_da_api_viram_favoritos_e_recentes_so_com_telas_permitidas()
    {
        var (ambiente, menu) = await CriarMenuAsync();
        ambiente.Servidor.Responder(HttpStatusCode.OK, new PreferenciasMenuDto
        {
            Favoritos = ["metas", "tela-que-nao-existe", "papeis"],
            Recentes = ["consulta-pessoas", "tela-que-nao-existe", "pessoas"]
        });

        await menu.CarregarPreferenciasCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "metas", "papeis" }, menu.Favoritos.Itens.Select(i => i.Rota).ToArray());
        Assert.True(Item(menu, "metas").Favorito);
        Assert.Equal("★", Item(menu, "metas").Estrela);
        Assert.False(Item(menu, "pessoas").Favorito);
        Assert.Equal(new[] { "consulta-pessoas", "pessoas" }, menu.Recentes.Itens.Select(i => i.Rota).ToArray());
        Assert.True(menu.Favoritos.TemItens);
    }

    [Fact]
    public async Task Sem_resposta_da_api_o_menu_funciona_sem_favoritos_e_avisa()
    {
        var (ambiente, menu) = await CriarMenuAsync();
        ambiente.Servidor.ForaDoAr();

        await menu.CarregarPreferenciasCommand.ExecuteAsync(null);

        Assert.False(menu.Favoritos.TemItens);
        Assert.True(menu.TemAviso);
        Assert.NotEmpty(menu.Secoes);
    }

    [Fact]
    public async Task Estrela_marca_e_desmarca_o_favorito_e_grava_na_api()
    {
        var (ambiente, menu) = await CriarMenuAsync();
        var consulta = Item(menu, "consulta-pessoas");

        ambiente.Servidor.Responder(HttpStatusCode.NoContent);
        await menu.AlternarFavoritoCommand.ExecuteAsync(consulta);

        Assert.True(consulta.Favorito);
        Assert.Same(consulta, Assert.Single(menu.Favoritos.Itens));
        var marcar = Assert.Single(ambiente.Servidor.Recebidas);
        Assert.Equal(HttpMethod.Put, marcar.Metodo);
        Assert.EndsWith(Rotas.Menu.Favoritos, marcar.Caminho);
        Assert.Contains("consulta-pessoas", marcar.Corpo);
        Assert.Contains("true", marcar.Corpo);

        ambiente.Servidor.Responder(HttpStatusCode.NoContent);
        await menu.AlternarFavoritoCommand.ExecuteAsync(consulta);

        Assert.False(consulta.Favorito);
        Assert.False(menu.Favoritos.TemItens);
        Assert.Contains("false", ambiente.Servidor.Recebidas[1].Corpo);
    }

    [Fact]
    public async Task Favorito_recusado_pela_api_volta_como_estava_e_mostra_o_motivo()
    {
        var (ambiente, menu) = await CriarMenuAsync();
        var metas = Item(menu, "metas");

        ambiente.Servidor.Problema(HttpStatusCode.BadRequest, ErrosApi.Validacao, "Regra",
            (ErrosApi.CampoErros, new[] { "Você já tem 20 favoritos. Remova um para marcar outro." }));
        await menu.AlternarFavoritoCommand.ExecuteAsync(metas);

        Assert.False(metas.Favorito);
        Assert.False(menu.Favoritos.TemItens);
        Assert.Contains("20 favoritos", menu.Aviso);

        ambiente.Servidor.ForaDoAr();
        await menu.AlternarFavoritoCommand.ExecuteAsync(metas);
        Assert.False(metas.Favorito);
        Assert.Contains("Tente de novo", menu.Aviso);
    }

    [Fact]
    public async Task No_limite_de_favoritos_nem_chama_a_api_e_inicio_nao_vira_favorito()
    {
        var (ambiente, menu) = await CriarMenuAsync();
        ambiente.Servidor.Responder(HttpStatusCode.OK, new PreferenciasMenuDto
        {
            Favoritos = Enumerable.Range(0, LimitesMenu.MaximoFavoritos).Select(i => $"tela-{i}").ToList() // contam mesmo sem permissão
        });
        await menu.CarregarPreferenciasCommand.ExecuteAsync(null);
        var chamadas = ambiente.Servidor.Recebidas.Count;

        await menu.AlternarFavoritoCommand.ExecuteAsync(Item(menu, "pessoas"));
        await menu.AlternarFavoritoCommand.ExecuteAsync(menu.Inicio);

        Assert.Equal(chamadas, ambiente.Servidor.Recebidas.Count);
        Assert.False(Item(menu, "pessoas").Favorito);
        Assert.Contains($"{LimitesMenu.MaximoFavoritos} favoritos", menu.Aviso);
        Assert.False(menu.Inicio.Favorito);
    }

    [Fact]
    public async Task Busca_mostra_as_telas_encontradas_no_lugar_do_menu_e_enter_abre_a_primeira()
    {
        var (_, menu) = await CriarMenuAsync();
        string? destino = null;
        menu.Navegar = rota => { destino = rota; return Task.CompletedTask; };

        menu.Busca = "etiq";

        Assert.True(menu.Buscando);
        Assert.False(menu.MostrarMenu);
        Assert.Equal("etiquetas", Assert.Single(menu.Resultados).Rota);

        await menu.AbrirPrimeiroResultadoCommand.ExecuteAsync(null);

        Assert.Equal("etiquetas", destino);
        Assert.Equal(string.Empty, menu.Busca); // escolheu: o menu volta ao normal
        Assert.True(menu.MostrarMenu);

        menu.Busca = "nada disso";
        Assert.True(menu.SemResultados);
        await menu.AbrirPrimeiroResultadoCommand.ExecuteAsync(null);
        Assert.Equal("etiquetas", destino); // nada encontrado: não navega
    }

    [Fact]
    public async Task Tocar_num_item_navega_para_a_rota()
    {
        var (_, menu) = await CriarMenuAsync();
        string? destino = null;
        menu.Navegar = rota => { destino = rota; return Task.CompletedTask; };

        await menu.IrCommand.ExecuteAsync(Item(menu, "metas"));

        Assert.Equal("metas", destino);
    }
}
