using Lone.Cliente.Plataforma;
using Lone.Contracts.Empresas;
using Lone.Contracts.Seguranca;

namespace Lone.Cliente.Sessao;

/// <summary>
/// Sessão deste aparelho: tokens e dados do usuário logado. Só o token de renovação é guardado no cofre
/// do aparelho (para reabrir o app sem pedir senha); o de acesso fica só na memória.
/// As permissões aqui servem para mostrar ou esconder opções — quem decide de verdade é a API.
/// </summary>
public sealed class SessaoCliente
{
    internal const string ChaveRenovacao = "lone.token-renovacao";

    private readonly IArmazenamentoSeguro _cofre;

    public SessaoCliente(IArmazenamentoSeguro cofre)
    {
        _cofre = cofre;
    }

    public SessaoDto? Atual { get; private set; }

    /// <summary>Disparado ao entrar, renovar, trocar de empresa, trocar a senha ou sair.</summary>
    public event EventHandler? Alterada;

    /// <summary>A sessão acabou sem o usuário pedir (token revogado ou vencido). Traz a mensagem para o login.</summary>
    public event EventHandler<string>? Expirou;

    /// <summary>A API passou a exigir a troca de senha no meio do uso (senha marcada pelo administrador).</summary>
    public event EventHandler? TrocaDeSenhaExigida;

    public bool Autenticada => Atual is not null;
    public string? TokenAcesso => Atual?.TokenAcesso;
    public string NomeUsuario => Atual?.Nome ?? string.Empty;
    public EmpresaAtiva? EmpresaAtiva => Atual?.EmpresaAtiva;
    public IReadOnlyList<EmpresaAtiva> EmpresasDisponiveis => Atual?.EmpresasDisponiveis ?? [];
    public bool DeveTrocarSenha => Atual?.DeveTrocarSenha == true;

    public bool Possui(string permissao) => Atual?.Possui(permissao) == true;

    public async Task DefinirAsync(SessaoDto sessao)
    {
        Atual = sessao;
        await _cofre.GravarAsync(ChaveRenovacao, sessao.TokenRenovacao);
        Alterada?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Token de renovação da sessão atual ou, com o app recém-aberto, o guardado no cofre.</summary>
    public async Task<string?> TokenRenovacaoAsync() =>
        Atual?.TokenRenovacao is { Length: > 0 } atual ? atual : await _cofre.LerAsync(ChaveRenovacao);

    public void MarcarSenhaTrocada()
    {
        if (Atual is null) return;
        Atual.DeveTrocarSenha = false;
        Alterada?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Saída pedida pelo usuário (ou antes de entrar de novo).</summary>
    public void Encerrar()
    {
        Atual = null;
        _cofre.Remover(ChaveRenovacao);
        Alterada?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Encerra por decisão do servidor e avisa o aplicativo, que volta ao login com a mensagem.</summary>
    public void Expirar(string mensagem)
    {
        Encerrar();
        Expirou?.Invoke(this, mensagem);
    }

    public void ExigirTrocaDeSenha()
    {
        if (Atual is null) return;
        Atual.DeveTrocarSenha = true;
        Alterada?.Invoke(this, EventArgs.Empty);
        TrocaDeSenhaExigida?.Invoke(this, EventArgs.Empty);
    }
}
