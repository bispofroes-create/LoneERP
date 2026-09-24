using System.Net;
using Lone.Cliente.Api;
using Lone.Contracts.Comum;
using Lone.Contracts.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Validacao;

namespace Lone.Tests.Cliente;

public class ClienteApiTests
{
    private sealed record Resposta(int Valor);

    [Fact]
    public async Task Envia_o_token_de_acesso_e_le_a_resposta()
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao());
        ambiente.Servidor.Responder(HttpStatusCode.OK, new Resposta(7));

        var resposta = await ambiente.Api.GetAsync<Resposta>("api/v1/teste");

        Assert.Equal(7, resposta.Valor);
        Assert.Equal("acesso-1", ambiente.Servidor.Recebidas[0].Token);
        Assert.Equal("/api/v1/teste", ambiente.Servidor.Recebidas[0].Caminho);
    }

    [Fact]
    public async Task Erro_de_validacao_volta_como_ValidacaoException_com_as_mensagens()
    {
        var ambiente = new AmbienteCliente();
        ambiente.Servidor.Problema(HttpStatusCode.BadRequest, ErrosApi.Validacao, "Dados inválidos.",
            (ErrosApi.CampoErros, new[] { "Informe o nome.", "CPF inválido." }));

        var erro = await Assert.ThrowsAsync<ValidacaoException>(() => ambiente.Api.PostAsync("api/v1/teste", new { }));

        Assert.Equal(new[] { "Informe o nome.", "CPF inválido." }, erro.Erros);
    }

    [Fact]
    public async Task Conflito_volta_como_ConflitoDeEdicaoException()
    {
        var ambiente = new AmbienteCliente();
        ambiente.Servidor.Problema(HttpStatusCode.Conflict, ErrosApi.Conflito, "Outro usuário alterou o cadastro.");

        var erro = await Assert.ThrowsAsync<ConflitoDeEdicaoException>(() => ambiente.Api.PutAsync<Resposta>("api/v1/teste", new { }));

        Assert.Equal("Outro usuário alterou o cadastro.", erro.Message);
    }

    [Fact]
    public async Task Acesso_negado_traz_a_permissao_que_faltou()
    {
        var ambiente = new AmbienteCliente();
        ambiente.Servidor.Problema(HttpStatusCode.Forbidden, ErrosApi.AcessoNegado, "Sem permissão.",
            (ErrosApi.CampoPermissao, "pessoas.editar"));

        var erro = await Assert.ThrowsAsync<AcessoNegadoException>(() => ambiente.Api.GetAsync<Resposta>("api/v1/teste"));

        Assert.Equal("pessoas.editar", erro.Permissao);
    }

    [Fact]
    public async Task Login_recusado_traz_a_situacao()
    {
        var ambiente = new AmbienteCliente();
        ambiente.Servidor.Problema(HttpStatusCode.Unauthorized, ErrosApi.LoginRecusado, "Usuário bloqueado por 15 minutos.",
            (ErrosApi.CampoSituacaoLogin, nameof(SituacaoLogin.Bloqueado)));

        var erro = await Assert.ThrowsAsync<LoginRecusadoException>(() => ambiente.Autenticacao.EntrarAsync("admin", "errada"));

        Assert.Equal(SituacaoLogin.Bloqueado, erro.Situacao);
        Assert.False(ambiente.Sessao.Autenticada);
    }

    [Fact]
    public async Task Token_vencido_renova_a_sessao_e_repete_a_chamada()
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao());
        ambiente.Servidor
            .Problema(HttpStatusCode.Unauthorized, ErrosApi.NaoAutenticado, "Token vencido.")
            .Responder(HttpStatusCode.OK, AmbienteCliente.NovaSessao("acesso-2", "renovacao-2"))
            .Responder(HttpStatusCode.OK, new Resposta(1));

        var resposta = await ambiente.Api.GetAsync<Resposta>("api/v1/teste");

        Assert.Equal(1, resposta.Valor);
        Assert.Equal("/" + Rotas.Autenticacao.Renovar, ambiente.Servidor.Recebidas[1].Caminho);
        Assert.Null(ambiente.Servidor.Recebidas[1].Token);
        Assert.Equal("acesso-2", ambiente.Servidor.Recebidas[2].Token);
        Assert.Equal("renovacao-2", ambiente.Cofre.Itens[Lone.Cliente.Sessao.SessaoCliente.ChaveRenovacao]);
    }

    [Fact]
    public async Task Renovacao_recusada_encerra_a_sessao_e_apaga_o_token_guardado()
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao());
        ambiente.Servidor
            .Problema(HttpStatusCode.Unauthorized, ErrosApi.NaoAutenticado, "Token vencido.")
            .Problema(HttpStatusCode.Unauthorized, ErrosApi.NaoAutenticado, "Sessão revogada.");

        var erro = await Assert.ThrowsAsync<SessaoExpiradaException>(() => ambiente.Api.GetAsync<Resposta>("api/v1/teste"));

        Assert.Equal("Sessão revogada.", erro.Message);
        Assert.False(ambiente.Sessao.Autenticada);
        Assert.Empty(ambiente.Cofre.Itens);
    }

    [Fact]
    public async Task Renovacao_com_servidor_instavel_mantem_o_token_guardado()
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao());
        ambiente.Servidor
            .Problema(HttpStatusCode.Unauthorized, ErrosApi.NaoAutenticado, "Token vencido.")
            .Responder(HttpStatusCode.ServiceUnavailable);

        await Assert.ThrowsAsync<ErroDaApiException>(() => ambiente.Api.GetAsync<Resposta>("api/v1/teste"));

        Assert.Equal("renovacao-1", ambiente.Cofre.Itens[Lone.Cliente.Sessao.SessaoCliente.ChaveRenovacao]);
    }

    [Fact]
    public async Task Recusa_depois_de_renovar_encerra_a_sessao()
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao());
        ambiente.Servidor
            .Problema(HttpStatusCode.Unauthorized, ErrosApi.NaoAutenticado, "Token vencido.")
            .Responder(HttpStatusCode.OK, AmbienteCliente.NovaSessao("acesso-2", "renovacao-2"))
            .Problema(HttpStatusCode.Unauthorized, ErrosApi.NaoAutenticado, "Sessão revogada.");

        await Assert.ThrowsAsync<SessaoExpiradaException>(() => ambiente.Api.GetAsync<Resposta>("api/v1/teste"));

        Assert.False(ambiente.Sessao.Autenticada);
    }

    [Fact]
    public async Task Troca_de_empresa_repetida_apos_renovar_envia_o_token_de_renovacao_novo()
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao(empresas: 2));
        ambiente.Servidor
            .Problema(HttpStatusCode.Unauthorized, ErrosApi.NaoAutenticado, "Token vencido.")
            .Responder(HttpStatusCode.OK, AmbienteCliente.NovaSessao("acesso-2", "renovacao-2", empresas: 2))
            .Responder(HttpStatusCode.OK, AmbienteCliente.NovaSessao("acesso-3", "renovacao-3", empresas: 2));

        await ambiente.Autenticacao.SelecionarEmpresaAsync(Guid.NewGuid());

        Assert.Contains("renovacao-1", ambiente.Servidor.Recebidas[0].Corpo);
        Assert.Contains("renovacao-2", ambiente.Servidor.Recebidas[2].Corpo);
        Assert.Equal("acesso-3", ambiente.Sessao.TokenAcesso);
    }

    [Fact]
    public async Task Troca_de_senha_obrigatoria_volta_como_excecao_propria()
    {
        var ambiente = new AmbienteCliente();
        ambiente.Servidor.Problema(HttpStatusCode.Forbidden, ErrosApi.TrocaDeSenhaObrigatoria, "Troque a senha para continuar.");

        await Assert.ThrowsAsync<TrocaDeSenhaObrigatoriaException>(() => ambiente.Api.GetAsync<Resposta>("api/v1/teste"));
    }

    [Fact]
    public async Task Duas_chamadas_com_o_mesmo_token_vencido_renovam_uma_vez_so()
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao());
        ambiente.Servidor.Responder(HttpStatusCode.OK, AmbienteCliente.NovaSessao("acesso-2", "renovacao-2"));

        await ambiente.Api.RenovarAsync("acesso-1", CancellationToken.None);
        await ambiente.Api.RenovarAsync("acesso-1", CancellationToken.None); // a segunda vê que já renovou

        Assert.Single(ambiente.Servidor.Recebidas);
        Assert.Equal("acesso-2", ambiente.Sessao.TokenAcesso);
    }

    [Fact]
    public async Task Servidor_fora_do_ar_vira_ServidorIndisponivelException()
    {
        var ambiente = new AmbienteCliente();
        ambiente.Servidor.ForaDoAr();

        var erro = await Assert.ThrowsAsync<ServidorIndisponivelException>(() => ambiente.Api.GetAsync<Resposta>("api/v1/teste"));

        Assert.Contains("https://servidor.teste", erro.Message);
    }

    [Fact]
    public async Task Resposta_de_erro_sem_ProblemDetails_vira_ErroDaApiException_com_o_status()
    {
        var ambiente = new AmbienteCliente();
        ambiente.Servidor.Responder(HttpStatusCode.InternalServerError);

        var erro = await Assert.ThrowsAsync<ErroDaApiException>(() => ambiente.Api.GetAsync<Resposta>("api/v1/teste"));

        Assert.Equal(500, erro.Status);
    }

    [Theory]
    [InlineData("servidor sem protocolo")]
    [InlineData("ftp://servidor")]
    public void Endereco_do_servidor_invalido_e_recusado(string endereco)
    {
        var ambiente = new AmbienteCliente();

        Assert.NotNull(ambiente.Endereco.Definir(endereco));
        Assert.Equal("https://servidor.teste", ambiente.Endereco.Endereco);
    }

    [Fact]
    public void Endereco_do_servidor_com_barra_final_monta_a_rota_certa()
    {
        var ambiente = new AmbienteCliente();

        Assert.Null(ambiente.Endereco.Definir("https://erp.empresa.com.br:8443/lone/"));

        Assert.Equal("https://erp.empresa.com.br:8443/lone/api/v1/saude", ambiente.Endereco.Montar("api/v1/saude").ToString());
    }
}
