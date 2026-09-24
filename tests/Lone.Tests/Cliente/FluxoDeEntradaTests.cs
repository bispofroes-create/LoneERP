using System.Net;
using Lone.Cliente.Navegacao;
using Lone.Cliente.Sessao;
using Lone.Contracts.Seguranca;

namespace Lone.Tests.Cliente;

public class FluxoDeEntradaTests
{
    [Fact]
    public async Task Sem_sessao_guardada_e_servidor_vazio_abre_o_primeiro_acesso()
    {
        var ambiente = new AmbienteCliente();
        ambiente.Servidor.Responder(HttpStatusCode.OK, new SituacaoSistema(ExisteUsuario: false));

        var (tela, _) = await ambiente.Fluxo.InicialAsync();

        Assert.Equal(Tela.PrimeiroAcesso, tela);
    }

    [Fact]
    public async Task Sem_sessao_guardada_e_servidor_com_usuarios_abre_o_login()
    {
        var ambiente = new AmbienteCliente();
        ambiente.Servidor.Responder(HttpStatusCode.OK, new SituacaoSistema(ExisteUsuario: true));

        var (tela, _) = await ambiente.Fluxo.InicialAsync();

        Assert.Equal(Tela.Login, tela);
    }

    [Fact]
    public async Task Sessao_guardada_valida_entra_direto_no_sistema()
    {
        var ambiente = new AmbienteCliente();
        ambiente.Cofre.Itens[SessaoCliente.ChaveRenovacao] = "renovacao-guardada";
        ambiente.Servidor.Responder(HttpStatusCode.OK, AmbienteCliente.NovaSessao());

        var (tela, _) = await ambiente.Fluxo.InicialAsync();

        Assert.Equal(Tela.Sistema, tela);
    }

    [Fact]
    public async Task Servidor_fora_do_ar_abre_o_login_com_a_mensagem_e_mantem_a_sessao_guardada()
    {
        var ambiente = new AmbienteCliente();
        ambiente.Cofre.Itens[SessaoCliente.ChaveRenovacao] = "renovacao-guardada";
        ambiente.Servidor.ForaDoAr();

        var (tela, mensagem) = await ambiente.Fluxo.InicialAsync();

        Assert.Equal(Tela.Login, tela);
        Assert.NotNull(mensagem);
        Assert.True(ambiente.Cofre.Itens.ContainsKey(SessaoCliente.ChaveRenovacao));
    }

    [Fact]
    public async Task Endereco_que_nao_e_do_Lone_abre_o_login_com_a_mensagem_em_vez_de_fechar_o_app()
    {
        var ambiente = new AmbienteCliente();
        ambiente.Servidor.Responder(HttpStatusCode.NotFound);

        var (tela, mensagem) = await ambiente.Fluxo.InicialAsync();

        Assert.Equal(Tela.Login, tela);
        Assert.NotNull(mensagem);
    }

    [Fact]
    public async Task Senha_provisoria_vai_para_a_troca_obrigatoria_antes_de_tudo()
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao(empresas: 3, deveTrocarSenha: true));

        Assert.Equal(Tela.TrocaDeSenhaObrigatoria, ambiente.Fluxo.Proxima());
    }

    [Fact]
    public async Task Varias_empresas_sem_empresa_ativa_pede_a_escolha()
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao(empresas: 2));

        Assert.Equal(Tela.EscolherEmpresa, ambiente.Fluxo.Proxima());
    }

    [Fact]
    public void Sem_sessao_volta_ao_login()
    {
        Assert.Equal(Tela.Login, new AmbienteCliente().Fluxo.Proxima());
    }
}
