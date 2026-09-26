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

        Assert.Equal(new[] { "Cadastros auxiliares", "Personalização", "Privacidade", "Comercial (cliente e fornecedor)" },
            grupos.Select(g => g.Titulo).ToArray());
        Assert.Equal(new[] { "Papéis", "Profissões", "Tipos de documento", "Tipos de endereço", "Tipos de telefone e e-mail" },
            grupos[0].Itens.Select(i => i.Titulo).ToArray());
        Assert.Equal(new[] { "Campos personalizados", "Etiquetas" }, grupos[1].Itens.Select(i => i.Titulo).ToArray());
    }

    [Fact]
    public void Todos_os_cadastros_continuam_acessiveis_em_algum_modulo_uma_vez_so()
    {
        var rotas = ModulosConfiguracao.Todos.SelectMany(m => ConfiguracoesViewModel.Montar(_ => true, m))
            .SelectMany(g => g.Itens).Select(i => i.Rota).ToList();
        Assert.Equal(19, rotas.Count);
        Assert.Equal(rotas.Count, rotas.Distinct().Count());
    }

    [Fact]
    public void Sistema_so_tem_o_que_e_transversal()
    {
        var sistema = Assert.Single(ConfiguracoesViewModel.Montar(_ => true, ModulosConfiguracao.Sistema));
        Assert.Equal(new[] { "perfis", "usuarios" }, sistema.Itens.Select(i => i.Rota).ToArray());
        Assert.Equal(new[] { "cargos", "centros-custo", "departamentos", "setores" },
            Assert.Single(ConfiguracoesViewModel.Montar(_ => true, ModulosConfiguracao.Organizacao)).Itens.Select(i => i.Rota).ToArray());
        Assert.Equal(new[] { "equipes", "indicadores" },
            Assert.Single(ConfiguracoesViewModel.Montar(_ => true, ModulosConfiguracao.Metas)).Itens.Select(i => i.Rota).ToArray());
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
        var tela = new ConfiguracoesViewModel(ambiente.Sessao) { Modulo = ModulosConfiguracao.Pessoas };
        string? destino = null;
        tela.Navegar = rota => { destino = rota; return Task.CompletedTask; };

        tela.AtualizarCommand.Execute(null);
        Assert.False(tela.Vazio);
        Assert.Equal("Configurações de Pessoas", tela.Titulo);
        var etiquetas = tela.Grupos.SelectMany(g => g.Itens).Single(i => i.Titulo == "Etiquetas");
        await tela.AbrirCommand.ExecuteAsync(etiquetas);

        Assert.Equal("etiquetas", destino);
    }
}
