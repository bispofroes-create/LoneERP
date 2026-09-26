using Lone.Cliente.ViewModels;
using Lone.Contracts.Seguranca;

namespace Lone.Tests.Cliente;

/// <summary>Página Configurações: cadastros de apoio agrupados, com as mesmas permissões do menu antigo.</summary>
public class ConfiguracoesViewModelTests
{
    [Fact]
    public void Administrador_ve_os_cinco_grupos_na_ordem_e_itens_em_ordem_alfabetica()
    {
        var grupos = ConfiguracoesViewModel.Montar(_ => true);

        Assert.Equal(new[] { "Pessoas", "Comercial", "Organização", "Metas", "Sistema" }, grupos.Select(g => g.Titulo).ToArray());
        foreach (var grupo in grupos)
        {
            var titulos = grupo.Itens.Select(i => i.Titulo).ToArray();
            Assert.Equal(titulos.OrderBy(t => t, StringComparer.Create(new System.Globalization.CultureInfo("pt-BR"), true)).ToArray(), titulos);
        }
        Assert.Equal(18, grupos.Sum(g => g.Itens.Count));
        Assert.Equal(18, grupos.SelectMany(g => g.Itens).Select(i => i.Rota).Distinct().Count()); // uma rota por tela
    }

    [Fact]
    public void Cada_item_segue_a_propria_permissao_e_grupo_vazio_nao_aparece()
    {
        var grupos = ConfiguracoesViewModel.Montar(p => p == Permissoes.Cadastros.Tipos);

        var grupo = Assert.Single(grupos);
        Assert.Equal("Pessoas", grupo.Titulo);
        Assert.Equal(new[] { "tipos-documento", "tipos-endereco", "tipos-meio-contato" }, grupo.Itens.Select(i => i.Rota).ToArray());

        Assert.Empty(ConfiguracoesViewModel.Montar(p => p == Permissoes.Pessoas.Visualizar));
        Assert.False(ConfiguracoesViewModel.AlgumaPermitida(_ => false));
        Assert.True(ConfiguracoesViewModel.AlgumaPermitida(p => p == Permissoes.Seguranca.GerenciarUsuarios));
    }

    [Fact]
    public async Task Abrir_navega_para_a_rota_do_item()
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao()); // administrador: pode tudo
        var tela = new ConfiguracoesViewModel(ambiente.Sessao);
        string? destino = null;
        tela.Navegar = rota => { destino = rota; return Task.CompletedTask; };

        tela.AtualizarCommand.Execute(null);
        Assert.False(tela.Vazio);
        var etiquetas = tela.Grupos.SelectMany(g => g.Itens).Single(i => i.Titulo == "Etiquetas");
        await tela.AbrirCommand.ExecuteAsync(etiquetas);

        Assert.Equal("etiquetas", destino);
    }
}
