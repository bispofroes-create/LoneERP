namespace Lone.Aplicacao.Seguranca;

public class AcessoNegadoException : Exception
{
    public AcessoNegadoException(string permissao)
        : base($"Você não tem permissão para: {Permissoes.Obter(permissao)?.Descricao ?? permissao}.")
    {
        Permissao = permissao;
    }

    public string Permissao { get; }
}
