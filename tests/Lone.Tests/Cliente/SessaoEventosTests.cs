using System.Net;
using Lone.Cliente.Api;
using Lone.Contracts.Comum;

namespace Lone.Tests.Cliente;

public class SessaoEventosTests
{
    private sealed record Resposta(int Valor);

    [Fact]
    public async Task Sessao_recusada_pelo_servidor_avisa_o_aplicativo_com_a_mensagem()
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao());
        string? aviso = null;
        ambiente.Sessao.Expirou += (_, mensagem) => aviso = mensagem;
        ambiente.Servidor
            .Problema(HttpStatusCode.Unauthorized, ErrosApi.NaoAutenticado, "Token vencido.")
            .Problema(HttpStatusCode.Unauthorized, ErrosApi.NaoAutenticado, "Sua sessão foi encerrada por segurança.");

        await Assert.ThrowsAsync<SessaoExpiradaException>(() => ambiente.Api.GetAsync<Resposta>("api/v1/teste"));

        Assert.Equal("Sua sessão foi encerrada por segurança.", aviso);
    }

    [Fact]
    public async Task Sair_pelo_menu_nao_e_tratado_como_sessao_expirada()
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao());
        var avisou = false;
        ambiente.Sessao.Expirou += (_, _) => avisou = true;
        ambiente.Servidor.Responder(HttpStatusCode.NoContent);

        await ambiente.Autenticacao.SairAsync();

        Assert.False(avisou);
        Assert.False(ambiente.Sessao.Autenticada);
    }

    [Fact]
    public async Task Troca_de_senha_exigida_no_meio_do_uso_marca_a_sessao_e_avisa()
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao());
        var avisou = false;
        ambiente.Sessao.TrocaDeSenhaExigida += (_, _) => avisou = true;
        ambiente.Servidor.Problema(HttpStatusCode.Forbidden, ErrosApi.TrocaDeSenhaObrigatoria, "Troque a senha para continuar.");

        await Assert.ThrowsAsync<TrocaDeSenhaObrigatoriaException>(() => ambiente.Api.GetAsync<Resposta>("api/v1/teste"));

        Assert.True(avisou);
        Assert.True(ambiente.Sessao.DeveTrocarSenha);
    }
}
