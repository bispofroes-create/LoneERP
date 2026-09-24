namespace Lone.Contracts.Seguranca;

/// <summary>O usuário não tem a permissão exigida. Na API vira HTTP 403; no aplicativo volta a ser esta exceção.</summary>
public class AcessoNegadoException : Exception
{
    public AcessoNegadoException(string permissao)
        : this(permissao, $"Você não tem permissão para: {Permissoes.Obter(permissao)?.Descricao ?? permissao}.")
    {
    }

    public AcessoNegadoException(string permissao, string mensagem) : base(mensagem)
    {
        Permissao = permissao;
    }

    public string Permissao { get; }
}
