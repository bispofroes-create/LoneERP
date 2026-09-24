using Lone.Cliente.Plataforma;
using Lone.Cliente.Sessao;
using Lone.Contracts.Comum;
using Lone.Contracts.Seguranca;

namespace Lone.Cliente.Api;

/// <summary>Login, primeiro acesso, empresa ativa, troca de senha e saída — sempre mantendo a SessaoCliente em dia.</summary>
public sealed class ServicoAutenticacao
{
    private readonly ClienteApi _api;
    private readonly SessaoCliente _sessao;
    private readonly IDispositivo _dispositivo;

    public ServicoAutenticacao(ClienteApi api, SessaoCliente sessao, IDispositivo dispositivo)
    {
        _api = api;
        _sessao = sessao;
        _dispositivo = dispositivo;
    }

    /// <summary>O servidor já tem usuários? (sem usuários, o aplicativo abre o primeiro acesso)</summary>
    public async Task<bool> ExisteUsuarioAsync(CancellationToken ct = default)
    {
        using var resposta = await _api.EnviarSemSessaoAsync(HttpMethod.Get, Rotas.Autenticacao.Situacao, null, ct);
        return (await ClienteApi.LerAsync<SituacaoSistema>(resposta, ct)).ExisteUsuario;
    }

    public async Task EntrarAsync(string login, string senha, CancellationToken ct = default)
    {
        using var resposta = await _api.EnviarSemSessaoAsync(HttpMethod.Post, Rotas.Autenticacao.Entrar,
            new EntrarRequisicao(login, senha, _dispositivo.Descricao), ct);
        await _sessao.DefinirAsync(await ClienteApi.LerAsync<SessaoDto>(resposta, ct));
    }

    public async Task CriarPrimeiroAdministradorAsync(string nome, string login, string senha, CancellationToken ct = default)
    {
        using var resposta = await _api.EnviarSemSessaoAsync(HttpMethod.Post, Rotas.Autenticacao.PrimeiroAcesso,
            new PrimeiroAcessoRequisicao(nome, login, senha, _dispositivo.Descricao), ct);
        await _sessao.DefinirAsync(await ClienteApi.LerAsync<SessaoDto>(resposta, ct));
    }

    /// <summary>
    /// Reabre a sessão guardada no aparelho (app reaberto). Falso se não havia sessão ou ela expirou;
    /// lança ServidorIndisponivelException se o servidor não respondeu (a sessão guardada é mantida).
    /// </summary>
    public async Task<bool> RestaurarSessaoAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(await _sessao.TokenRenovacaoAsync()))
            return false;

        try
        {
            await _api.RenovarAsync(tokenQueFalhou: null, ct);
            return _sessao.Autenticada;
        }
        catch (SessaoExpiradaException)
        {
            return false;
        }
    }

    public async Task SelecionarEmpresaAsync(Guid estabelecimentoId, CancellationToken ct = default)
    {
        var tokenRenovacao = await _sessao.TokenRenovacaoAsync()
                             ?? throw new SessaoExpiradaException();
        // O corpo é remontado se a sessão for renovada no meio: reenviar o token antigo seria tratado
        // pelo servidor como roubo de token e encerraria todas as sessões do usuário.
        var sessao = await _api.PostComCorpoAtualAsync<SessaoDto>(Rotas.Autenticacao.Empresa,
            () => new SelecionarEmpresaRequisicao(estabelecimentoId, _sessao.Atual?.TokenRenovacao ?? tokenRenovacao), ct);
        await _sessao.DefinirAsync(sessao);
    }

    /// <summary>
    /// Relê a sessão no servidor (empresas disponíveis e permissões), mantendo a empresa ativa.
    /// Usado depois de mudar as empresas do grupo.
    /// </summary>
    public Task AtualizarSessaoAsync(CancellationToken ct = default) =>
        _api.RenovarAsync(_sessao.TokenAcesso, ct);

    public async Task TrocarSenhaAsync(string senhaAtual, string novaSenha, CancellationToken ct = default)
    {
        await _api.PostAsync(Rotas.Autenticacao.Senha, new TrocarSenhaRequisicao(senhaAtual, novaSenha), ct: ct);
        _sessao.MarcarSenhaTrocada();
    }

    /// <summary>Encerra a sessão no servidor (se ele responder) e sempre no aparelho.</summary>
    public async Task SairAsync()
    {
        var tokenRenovacao = await _sessao.TokenRenovacaoAsync();
        _sessao.Encerrar();

        if (string.IsNullOrEmpty(tokenRenovacao)) return;
        try
        {
            using var resposta = await _api.EnviarSemSessaoAsync(HttpMethod.Post, Rotas.Autenticacao.Sair,
                new SairRequisicao(tokenRenovacao), CancellationToken.None);
        }
        catch (ServidorIndisponivelException)
        {
            // Sem servidor: o token some do aparelho e vence sozinho no servidor.
        }
    }
}
