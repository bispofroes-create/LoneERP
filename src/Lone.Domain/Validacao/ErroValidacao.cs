namespace Lone.Domain.Validacao;

/// <summary>
/// Um erro de validação: o texto para o usuário e, quando a regra sabe, o campo da ficha (id estável de
/// <see cref="Lone.Domain.Pessoas.CamposFichaPessoa"/>, nunca o rótulo da tela) e o registro da lista a que ele pertence
/// (o <c>Id</c> do endereço, documento, telefone...; nunca a posição, que muda quando a lista muda).
/// </summary>
public sealed record ErroValidacao(string Mensagem, string? Campo = null, Guid? Item = null)
{
    public bool TemCampo => Campo is not null;
}

/// <summary>
/// Lista de erros que aceita texto puro (regras que ainda não dizem o campo) e erros com campo. Usada pelas regras que
/// juntam erros de várias fontes, para que cada uma diga o campo quando souber, sem mudar as outras.
/// </summary>
public sealed class ListaErros : IReadOnlyList<ErroValidacao>
{
    private readonly List<ErroValidacao> _itens = new();

    public ListaErros() { }

    public ListaErros(IEnumerable<ErroValidacao> itens) => _itens.AddRange(itens);

    public int Count => _itens.Count;
    public ErroValidacao this[int indice] => _itens[indice];

    public void Add(string mensagem) => _itens.Add(new ErroValidacao(mensagem));
    public void Add(string mensagem, string campo, Guid? item = null) => _itens.Add(new ErroValidacao(mensagem, campo, item));
    public void Add(ErroValidacao erro) => _itens.Add(erro);
    public void AddRange(IEnumerable<string> mensagens) => _itens.AddRange(mensagens.Select(m => new ErroValidacao(m)));
    public void AddRange(IEnumerable<ErroValidacao> erros) => _itens.AddRange(erros);

    /// <summary>Só os textos (o contrato antigo).</summary>
    public List<string> Mensagens() => [.. _itens.Select(i => i.Mensagem)];

    public IEnumerator<ErroValidacao> GetEnumerator() => _itens.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
