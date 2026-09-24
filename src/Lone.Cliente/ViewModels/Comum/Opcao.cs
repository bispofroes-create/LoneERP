namespace Lone.Cliente.ViewModels.Comum;

/// <summary>Item de uma lista de escolha: o valor guardado e o texto mostrado (ToString, usado pelo Picker).</summary>
public sealed record Opcao<T>(T Valor, string Texto)
{
    public override string ToString() => Texto;
}

public static class Opcao
{
    /// <summary>A opção da lista com este valor (ou a primeira, se não houver).</summary>
    public static Opcao<T> De<T>(IReadOnlyList<Opcao<T>> lista, T valor) =>
        lista.FirstOrDefault(o => EqualityComparer<T>.Default.Equals(o.Valor, valor)) ?? lista[0];
}
