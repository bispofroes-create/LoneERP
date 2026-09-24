using System.Net;
using Lone.Cliente.Navegacao;
using Lone.Cliente.ViewModels;
using Lone.Contracts.Comum;
using Lone.Contracts.Seguranca;

namespace Lone.Tests.Cliente;

public class LoginViewModelTests
{
    private static (LoginViewModel Tela, AmbienteCliente Ambiente, NavegacaoGravada Navegacao) Criar()
    {
        var ambiente = new AmbienteCliente();
        var navegacao = new NavegacaoGravada();
        var tela = new LoginViewModel(ambiente.Autenticacao, ambiente.Fluxo, navegacao, ambiente.Endereco);
        return (tela, ambiente, navegacao);
    }

    [Fact]
    public async Task Sem_login_ou_senha_avisa_e_nao_chama_o_servidor()
    {
        var (tela, ambiente, navegacao) = Criar();

        await tela.EntrarCommand.ExecuteAsync(null);

        Assert.Equal(TipoMensagem.Aviso, tela.TipoMensagem);
        Assert.Empty(ambiente.Servidor.Recebidas);
        Assert.Empty(navegacao.Telas);
    }

    [Fact]
    public async Task Login_certo_limpa_a_senha_e_segue_para_a_proxima_tela()
    {
        var (tela, ambiente, navegacao) = Criar();
        ambiente.Servidor.Responder(HttpStatusCode.OK, AmbienteCliente.NovaSessao());
        tela.Login = "admin";
        tela.Senha = "Lone2026erp";

        await tela.EntrarCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, tela.Senha);
        Assert.Equal(new[] { Tela.Sistema }, navegacao.Telas);
        Assert.False(tela.TemMensagem);
    }

    [Fact]
    public async Task Login_recusado_mostra_a_mensagem_da_API_e_fica_na_tela()
    {
        var (tela, ambiente, navegacao) = Criar();
        ambiente.Servidor.Problema(HttpStatusCode.Unauthorized, ErrosApi.LoginRecusado, "Login ou senha inválidos.",
            (ErrosApi.CampoSituacaoLogin, nameof(SituacaoLogin.CredenciaisInvalidas)));
        tela.Login = "admin";
        tela.Senha = "errada";

        await tela.EntrarCommand.ExecuteAsync(null);

        Assert.Equal("Login ou senha inválidos.", tela.Mensagem);
        Assert.Equal(TipoMensagem.Erro, tela.TipoMensagem);
        Assert.Empty(navegacao.Telas);
        Assert.True(tela.Livre);
    }

    [Fact]
    public async Task Endereco_de_servidor_invalido_abre_o_campo_do_servidor()
    {
        var (tela, ambiente, _) = Criar();
        tela.Login = "admin";
        tela.Senha = "x";
        tela.EnderecoServidor = "sem protocolo";

        await tela.EntrarCommand.ExecuteAsync(null);

        Assert.True(tela.MostrarServidor);
        Assert.Equal(TipoMensagem.Erro, tela.TipoMensagem);
        Assert.Empty(ambiente.Servidor.Recebidas);
    }
}
