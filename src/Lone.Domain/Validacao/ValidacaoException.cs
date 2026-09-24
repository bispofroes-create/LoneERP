namespace Lone.Domain.Validacao;

/// <summary>Erro de regra de negócio, com mensagens prontas para mostrar ao usuário.</summary>
public class ValidacaoException : Exception
{
    public IReadOnlyList<string> Erros { get; }

    public ValidacaoException(IReadOnlyList<string> erros)
        : base(string.Join(Environment.NewLine, erros))
    {
        Erros = erros;
    }
}
