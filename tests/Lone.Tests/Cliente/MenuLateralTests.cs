using Lone.Cliente.ViewModels;
using Lone.Contracts.Seguranca;

namespace Lone.Tests.Cliente;

/// <summary>Menu lateral por módulo: Início · PESSOAS · ORGANIZAÇÃO · METAS · Configurações do sistema.</summary>
public class MenuLateralTests
{
    [Fact]
    public void Administrador_ve_os_modulos_com_as_suas_configuracoes()
    {
        var secoes = MenuViewModel.CriarSecoes(_ => true);

        Assert.Equal(new string?[] { null, "PESSOAS", "ORGANIZAÇÃO", "METAS", null }, secoes.Select(s => s.Titulo).ToArray());
        Assert.Equal(new[] { "pessoas", "consulta-pessoas", "configuracoes-pessoas" }, secoes[1].Itens.Select(i => i.Rota).ToArray());
        Assert.True(secoes[1].Itens[2].Configuracao);
        Assert.Equal("⚙  Configurações de Pessoas", secoes[1].Itens[2].TextoExibido);
        Assert.Equal(new[] { "grupos-empresariais", "configuracoes-organizacao" }, secoes[2].Itens.Select(i => i.Rota).ToArray());
        Assert.Equal(new[] { "metas", "configuracoes-metas" }, secoes[3].Itens.Select(i => i.Rota).ToArray());
        Assert.Equal("configuracoes-sistema", Assert.Single(secoes[4].Itens).Rota);
    }

    [Fact]
    public void Sem_permissao_o_item_e_a_secao_nao_aparecem()
    {
        var secoes = MenuViewModel.CriarSecoes(p => p == Permissoes.Pessoas.Visualizar);

        Assert.Equal(2, secoes.Count); // Início e Pessoas
        Assert.Equal(new[] { "pessoas", "consulta-pessoas" }, secoes[1].Itens.Select(i => i.Rota).ToArray()); // sem configurações de Pessoas

        var soConfiguracao = MenuViewModel.CriarSecoes(p => p == Permissoes.Cadastros.Etiquetas);
        Assert.Equal(new[] { "configuracoes-pessoas" }, soConfiguracao[1].Itens.Select(i => i.Rota).ToArray());
    }

    [Fact]
    public async Task Item_ativo_acompanha_a_tela_aberta_inclusive_nos_cadastros_do_modulo()
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao());
        var menu = new MenuViewModel(ambiente.Sessao, ambiente.Autenticacao, new NavegacaoGravada());
        var itens = menu.Secoes.SelectMany(s => s.Itens).ToList();

        menu.DefinirRotaAtual("//pessoas");
        Assert.Equal(new[] { "pessoas" }, itens.Where(i => i.Ativo).Select(i => i.Rota).ToArray());

        menu.DefinirRotaAtual("//papeis"); // aberto a partir de Configurações de Pessoas
        Assert.Equal(new[] { "configuracoes-pessoas" }, itens.Where(i => i.Ativo).Select(i => i.Rota).ToArray());

        string? destino = null;
        menu.Navegar = rota => { destino = rota; return Task.CompletedTask; };
        await menu.IrCommand.ExecuteAsync(itens.Single(i => i.Rota == "metas"));
        Assert.Equal("metas", destino);
    }
}
