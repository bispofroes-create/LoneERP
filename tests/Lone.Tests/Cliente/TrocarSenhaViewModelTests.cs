using System.Net;
using Lone.Cliente.Mensagens;
using Lone.Cliente.ViewModels;
using Microsoft.Extensions.Time.Testing;

namespace Lone.Tests.Cliente;

public class TrocarSenhaViewModelTests
{
    [Fact]
    public async Task Troca_pelo_menu_mostra_a_confirmacao_e_troca_Cancelar_por_Fechar()
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao());
        var navegacao = new NavegacaoGravada();
        var tela = new TrocarSenhaViewModel(ambiente.Autenticacao, ambiente.Sessao, ambiente.Fluxo, navegacao)
        {
            SenhaAtual = "Antiga123",
            NovaSenha = "Nova12345",
            Confirmacao = "Nova12345",
            Mensagens = new ServicoMensagens(new FakeTimeProvider())
        };
        ambiente.Servidor.Responder(HttpStatusCode.NoContent);

        await tela.SalvarCommand.ExecuteAsync(null);

        Assert.Equal("Senha alterada. Use a nova senha no próximo acesso.", Assert.Single(tela.Mensagens.Visiveis).Texto);
        Assert.False(tela.TemMensagem); // confirmação no toast; a tela fica concluída (só "Fechar")
        Assert.True(tela.Concluida);
        Assert.False(tela.PodeSalvar);
        Assert.Equal("Fechar", tela.TextoCancelar);
        Assert.Empty(navegacao.Telas);
    }
}
