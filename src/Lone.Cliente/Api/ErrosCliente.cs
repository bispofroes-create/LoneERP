using Lone.Contracts.Seguranca;

namespace Lone.Cliente.Api;

/// <summary>A sessão acabou (token vencido ou revogado) e não pôde ser renovada: é preciso entrar de novo.</summary>
public sealed class SessaoExpiradaException : Exception
{
    public SessaoExpiradaException(string mensagem = "Sua sessão expirou. Entre novamente.") : base(mensagem) { }
}

/// <summary>O administrador redefiniu a senha durante a sessão: é preciso trocá-la antes de continuar.</summary>
public sealed class TrocaDeSenhaObrigatoriaException : Exception
{
    public TrocaDeSenhaObrigatoriaException(string mensagem) : base(mensagem) { }
}

/// <summary>Login recusado (senha errada, usuário bloqueado ou inativo). A mensagem vem pronta da API.</summary>
public sealed class LoginRecusadoException : Exception
{
    public LoginRecusadoException(string mensagem, SituacaoLogin situacao) : base(mensagem)
    {
        Situacao = situacao;
    }

    public SituacaoLogin Situacao { get; }
}

/// <summary>O servidor não respondeu (fora do ar, sem rede ou endereço errado).</summary>
public sealed class ServidorIndisponivelException : Exception
{
    public ServidorIndisponivelException(string endereco, Exception? interna = null)
        : base($"Não foi possível falar com o servidor ({endereco}). Verifique a conexão e o endereço do servidor.", interna)
    {
    }
}

/// <summary>A API respondeu com um erro sem tratamento específico (ex.: erro interno, serviço externo fora do ar).</summary>
public sealed class ErroDaApiException : Exception
{
    public ErroDaApiException(int status, string mensagem, string? codigo) : base(mensagem)
    {
        Status = status;
        Codigo = codigo;
    }

    public int Status { get; }
    public string? Codigo { get; }
}
