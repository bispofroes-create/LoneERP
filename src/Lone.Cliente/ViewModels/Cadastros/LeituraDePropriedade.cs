using System.Collections.Concurrent;
using System.Reflection;

namespace Lone.Cliente.ViewModels.Cadastros;

/// <summary>
/// Lê o Id (Guid) e o nome de exibição de fichas e linhas de lista sem que cada cadastro precise declarar — base da
/// navegação por registros em todas as telas de uma vez. Só propriedades públicas simples; cache por tipo.
/// </summary>
internal static class LeituraDePropriedade
{
    private static readonly ConcurrentDictionary<(Type, string), PropertyInfo?> Cache = new();

    public static Guid? Id(object? objeto) => objeto is not null && Propriedade(objeto.GetType(), "Id") is { } p
        ? p.GetValue(objeto) switch { Guid g when g != Guid.Empty => g, _ => null }
        : null;

    public static string? Texto(object? objeto, string nome) => objeto is not null && Propriedade(objeto.GetType(), nome) is { } p
        && p.GetValue(objeto) is string texto && !string.IsNullOrWhiteSpace(texto)
        ? texto.Trim()
        : null;

    private static PropertyInfo? Propriedade(Type tipo, string nome) => Cache.GetOrAdd((tipo, nome), static chave =>
    {
        try
        {
            var p = chave.Item1.GetProperty(chave.Item2, BindingFlags.Instance | BindingFlags.Public);
            return p is { CanRead: true } && p.GetIndexParameters().Length == 0
                   && (p.PropertyType == typeof(Guid) || p.PropertyType == typeof(Guid?) || p.PropertyType == typeof(string))
                ? p
                : null;
        }
        catch (AmbiguousMatchException)
        {
            return null;
        }
    });
}
