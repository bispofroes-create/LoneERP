using Lone.Cliente.Sessao;
using Lone.Contracts.Seguranca;

namespace Lone.Cliente.Navegacao;

/// <summary>
/// "Abrir ficha" a partir de outra tela (transferências, carteira vencendo, carteira em uma data): a tela de origem pede a
/// pessoa e vai para Pessoas; a tela de Pessoas, ao aparecer, retira o pedido e abre a ficha (perguntando antes, como sempre,
/// se houver alterações não salvas em outra). Um pedido de cada vez; o último vale.
/// </summary>
public sealed class AberturaDePessoa
{
    /// <summary>Rota da tela de Pessoas no Shell.</summary>
    public const string RotaPessoas = "pessoas";

    private readonly SessaoCliente _sessao;
    private Guid? _pendente;

    public AberturaDePessoa(SessaoCliente sessao) => _sessao = sessao;

    /// <summary>O usuário pode abrir fichas de pessoas (sem a permissão, o botão não aparece).</summary>
    public bool Permitida => _sessao.Possui(Permissoes.Pessoas.Visualizar);

    public void Pedir(Guid pessoaId) => _pendente = pessoaId;

    /// <summary>O pedido pendente (e o esquece), ou nulo.</summary>
    public Guid? Retirar()
    {
        var id = _pendente;
        _pendente = null;
        return id;
    }
}
