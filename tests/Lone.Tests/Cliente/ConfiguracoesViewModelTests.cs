using Lone.Cliente.ViewModels;
using Lone.Contracts.Seguranca;

namespace Lone.Tests.Cliente;

/// <summary>
/// Configurações por módulo (decisão de UX): cada módulo tem as suas; a página do sistema fica só com o transversal.
/// Cada item continua com a mesma permissão do cadastro.
/// </summary>
public class ConfiguracoesViewModelTests
{
    [Fact]
    public void Configuracoes_de_Pessoas_em_grupos_na_ordem_e_itens_em_ordem_alfabetica()
    {
        var grupos = ConfiguracoesViewModel.Montar(_ => true, ModulosConfiguracao.Pessoas);

        // F6 do motor de CEP (classe C, autorizada): + "Manutenção" › Reconferência de CEPs.
        Assert.Equal(new[] { "Cadastros auxiliares", "Personalização", "Privacidade", "Manutenção" }, grupos.Select(g => g.Titulo).ToArray());
        Assert.Equal(new[] { "Papéis", "Profissões", "Tipos de documento", "Tipos de endereço", "Tipos de telefone e e-mail" },
            grupos[0].Itens.Select(i => i.Titulo).ToArray());
        Assert.Equal(new[] { "Campos personalizados", "Etiquetas" }, grupos[1].Itens.Select(i => i.Titulo).ToArray());
    }

    [Fact]
    public void Todos_os_cadastros_continuam_acessiveis_em_algum_modulo_uma_vez_so()
    {
        var rotas = ModulosConfiguracao.Todos.SelectMany(m => ConfiguracoesViewModel.Montar(_ => true, m))
            .SelectMany(g => g.Itens).Select(i => i.Rota).ToList();
        Assert.Equal(27, rotas.Count); // 25 cadastros (2 de territórios na Fase 2b-1a, +1 na 2b-1b, + Prazos de período) + "Trocar senha" (Minha conta) + Reconferência de CEPs (F6)
        Assert.Equal(rotas.Count, rotas.Distinct().Count());
    }

    [Fact]
    public void Sistema_so_tem_o_que_e_transversal()
    {
        var sistema = ConfiguracoesViewModel.Montar(_ => true, ModulosConfiguracao.Sistema);
        Assert.Equal(new[] { "Minha conta", "Usuários e permissões", "Parâmetros" }, sistema.Select(g => g.Titulo).ToArray());
        Assert.Equal(new[] { ModulosConfiguracao.RotaTrocarSenha }, sistema[0].Itens.Select(i => i.Rota).ToArray());
        Assert.Equal(new[] { "perfis", "usuarios" }, sistema[1].Itens.Select(i => i.Rota).ToArray());
        Assert.Equal(new[] { "prazos-periodo" }, sistema[2].Itens.Select(i => i.Rota).ToArray()); // menu do usuário › Administração
        Assert.Equal(new[] { "cargos", "centros-custo", "departamentos", "setores" },
            Assert.Single(ConfiguracoesViewModel.Montar(_ => true, ModulosConfiguracao.Organizacao)).Itens.Select(i => i.Rota).ToArray());
        Assert.Equal(new[] { "equipes", "indicadores" },
            Assert.Single(ConfiguracoesViewModel.Montar(_ => true, ModulosConfiguracao.Metas)).Itens.Select(i => i.Rota).ToArray());
        // Comercial (Fase 1c): papéis comerciais, condições e perfis saíram de Pessoas.
        var comercial = ConfiguracoesViewModel.Montar(_ => true, ModulosConfiguracao.Comercial);
        Assert.Equal(new[] { "Carteira de clientes", "Condições de venda", "Territórios" }, comercial.Select(g => g.Titulo).ToArray());
        Assert.Equal(new[] { "tipos-carteira", "parametros-comerciais", "tipos-ausencia" }, comercial[0].Itens.Select(i => i.Rota).ToArray());
        Assert.Equal(new[] { "condicoes-pagamento", "perfis-comerciais" }, comercial[1].Itens.Select(i => i.Rota).ToArray());
        Assert.Equal(new[] { "mapas-territoriais", "parametros-territoriais", "tipos-territorio" },
            comercial[2].Itens.Select(i => i.Rota).ToArray()); // Fase 2b-1a; parâmetros na 2b-1b
        Assert.Equal(ModulosConfiguracao.Comercial, ModulosConfiguracao.DaRota("//configuracoes-comercial"));
    }

    [Fact]
    public void Cada_item_segue_a_propria_permissao_e_grupo_vazio_nao_aparece()
    {
        var grupos = ConfiguracoesViewModel.Montar(p => p == Permissoes.Cadastros.Tipos, ModulosConfiguracao.Pessoas);

        Assert.Equal(new[] { "Cadastros auxiliares", "Privacidade" }, grupos.Select(g => g.Titulo).ToArray());
        Assert.Equal(new[] { "tipos-documento", "tipos-endereco", "tipos-meio-contato" }, grupos[0].Itens.Select(i => i.Rota).ToArray());

        Assert.Empty(ConfiguracoesViewModel.Montar(p => p == Permissoes.Pessoas.Visualizar, ModulosConfiguracao.Pessoas));
        Assert.False(ConfiguracoesViewModel.AlgumaPermitida(_ => false, ModulosConfiguracao.Pessoas));
        Assert.True(ConfiguracoesViewModel.AlgumaPermitida(p => p == Permissoes.Seguranca.GerenciarUsuarios, ModulosConfiguracao.Sistema));
        Assert.False(ConfiguracoesViewModel.AlgumaPermitida(p => p == Permissoes.Seguranca.GerenciarUsuarios, ModulosConfiguracao.Pessoas));

        // Sem nenhuma permissão, o sistema ainda mostra "Minha conta › Trocar senha" (a própria senha é de todos).
        var semPermissao = Assert.Single(ConfiguracoesViewModel.Montar(_ => false, ModulosConfiguracao.Sistema));
        Assert.Equal("Minha conta", semPermissao.Titulo);
        Assert.True(ConfiguracoesViewModel.AlgumaPermitida(_ => false, ModulosConfiguracao.Sistema));
    }

    [Fact]
    public void Modulo_vem_da_rota_do_Shell()
    {
        Assert.Equal(ModulosConfiguracao.Pessoas, ModulosConfiguracao.DaRota("//configuracoes-pessoas"));
        Assert.Equal(ModulosConfiguracao.Sistema, ModulosConfiguracao.DaRota("//configuracoes-sistema/"));
        Assert.Null(ModulosConfiguracao.DaRota("//pessoas"));
        Assert.Null(ModulosConfiguracao.DaRota("//configuracoes-inexistente"));
        Assert.Equal("Configurações de Pessoas", ModulosConfiguracao.Titulo(ModulosConfiguracao.Pessoas));
    }

    [Fact]
    public async Task Abrir_navega_para_a_rota_do_item()
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao()); // administrador: pode tudo
        var tela = new ConfiguracoesViewModel(ambiente.Sessao, new NavegacaoGravada()) { Modulo = ModulosConfiguracao.Pessoas };
        string? destino = null;
        tela.Navegar = rota => { destino = rota; return Task.CompletedTask; };

        tela.AtualizarCommand.Execute(null);
        Assert.False(tela.Vazio);
        Assert.Equal("Configurações de Pessoas", tela.Titulo);
        var etiquetas = tela.Grupos.SelectMany(g => g.Itens).Single(i => i.Titulo == "Etiquetas");
        await tela.AbrirCommand.ExecuteAsync(etiquetas);

        Assert.Equal("etiquetas", destino);
    }

    [Fact]
    public async Task Trocar_senha_abre_a_troca_de_senha_por_cima_sem_navegar()
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao());
        var navegacao = new NavegacaoGravada();
        var tela = new ConfiguracoesViewModel(ambiente.Sessao, navegacao) { Modulo = ModulosConfiguracao.Sistema };
        string? destino = null;
        tela.Navegar = rota => { destino = rota; return Task.CompletedTask; };

        tela.AtualizarCommand.Execute(null);
        await tela.AbrirCommand.ExecuteAsync(tela.Grupos[0].Itens.Single());

        Assert.Equal(1, navegacao.TrocasDeSenhaAbertas);
        Assert.Null(destino);
    }
}
