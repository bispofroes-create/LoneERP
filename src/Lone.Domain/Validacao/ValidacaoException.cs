namespace Lone.Domain.Validacao;

/// <summary>
/// Erro de regra de negócio, com mensagens prontas para mostrar ao usuário. <see cref="Erros"/> (só os textos) é o contrato
/// de sempre; <see cref="Itens"/> traz, além do texto, o campo da ficha a que o erro se refere, quando a regra sabe
/// (Lone Contextual, Fase 1: o erro leva ao campo). Erro sem campo continua sendo um erro geral.
/// </summary>
public class ValidacaoException : Exception
{
    public IReadOnlyList<string> Erros { get; }

    /// <summary>Os mesmos erros, com o campo de cada um (nulo = erro geral). Na mesma ordem de <see cref="Erros"/>.</summary>
    public IReadOnlyList<ErroValidacao> Itens { get; }

    public ValidacaoException(IReadOnlyList<string> erros)
        : base(string.Join(Environment.NewLine, erros))
    {
        Erros = erros;
        Itens = [.. erros.Select(e => new ErroValidacao(e))];
    }

    public ValidacaoException(IReadOnlyList<ErroValidacao> itens)
        : base(string.Join(Environment.NewLine, itens.Select(i => i.Mensagem)))
    {
        Itens = [.. itens];
        Erros = [.. itens.Select(i => i.Mensagem)];
    }
}
