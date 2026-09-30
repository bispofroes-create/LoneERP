namespace Lone.Cliente.Navegacao;

/// <summary>
/// "Criar operação com estas" (Divergências territoriais, DN-14): a tela de divergências cria o rascunho, pede a operação e
/// vai para Operações territoriais; esta, ao aparecer, retira o pedido e abre a ficha (perguntando antes, como sempre, se
/// houver alterações não salvas em outra). Mesmo padrão de <see cref="AberturaDePessoa"/>: um pedido de cada vez, o último vale.
/// </summary>
public sealed class AberturaDeOperacaoTerritorial
{
    /// <summary>Rota da tela de Operações territoriais no Shell.</summary>
    public const string RotaOperacoes = "operacoes-territoriais";

    private Guid? _pendente;

    public void Pedir(Guid operacaoId) => _pendente = operacaoId;

    /// <summary>O pedido pendente (e o esquece), ou nulo.</summary>
    public Guid? Retirar()
    {
        var id = _pendente;
        _pendente = null;
        return id;
    }
}
