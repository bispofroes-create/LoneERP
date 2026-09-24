using Lone.Cliente.Api;
using Lone.Cliente.Sessao;

namespace Lone.Cliente.Navegacao;

/// <summary>Telas "de entrada" do aplicativo (antes do menu principal).</summary>
public enum Tela
{
    Login,
    PrimeiroAcesso,
    TrocaDeSenhaObrigatoria,
    EscolherEmpresa,
    Sistema
}

/// <summary>Troca a tela principal. Implementado pelo aplicativo (MAUI); os ViewModels só pedem.</summary>
public interface INavegacao
{
    Task IrParaAsync(Tela tela, string? mensagem = null);

    /// <summary>Abre a troca de senha por cima da tela atual (opcional, pelo menu).</summary>
    Task AbrirTrocaDeSenhaAsync();

    /// <summary>Fecha a tela aberta por cima (troca de senha opcional).</summary>
    Task FecharAsync();
}

/// <summary>
/// Decide para onde o usuário vai: na abertura do app e depois de cada passo da entrada
/// (login → troca de senha obrigatória → escolha de empresa → sistema).
/// </summary>
public sealed class FluxoDeEntrada
{
    private readonly SessaoCliente _sessao;
    private readonly ServicoAutenticacao _autenticacao;

    public FluxoDeEntrada(SessaoCliente sessao, ServicoAutenticacao autenticacao)
    {
        _sessao = sessao;
        _autenticacao = autenticacao;
    }

    /// <summary>Próxima tela para quem já está autenticado.</summary>
    public Tela Proxima()
    {
        if (!_sessao.Autenticada) return Tela.Login;
        if (_sessao.DeveTrocarSenha) return Tela.TrocaDeSenhaObrigatoria;
        if (_sessao.EmpresaAtiva is null && _sessao.EmpresasDisponiveis.Count > 1) return Tela.EscolherEmpresa;
        return Tela.Sistema;
    }

    /// <summary>
    /// Na abertura do app: sessão guardada válida → segue a entrada; senão, login ou primeiro acesso.
    /// Servidor fora do ar → login (a própria tela mostra o erro e deixa trocar o endereço).
    /// </summary>
    public async Task<(Tela Tela, string? Mensagem)> InicialAsync(CancellationToken ct = default)
    {
        try
        {
            if (await _autenticacao.RestaurarSessaoAsync(ct))
                return (Proxima(), null);

            return await _autenticacao.ExisteUsuarioAsync(ct) ? (Tela.Login, null) : (Tela.PrimeiroAcesso, null);
        }
        catch (ServidorIndisponivelException ex)
        {
            return (Tela.Login, ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Ex.: endereço salvo aponta para outro site. O login mostra o erro e deixa corrigir o endereço.
            return (Tela.Login, $"O servidor respondeu de forma inesperada: {ex.Message}");
        }
    }
}
