using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;
using Lone.Core.Entidades;

namespace Lone.Core.Auditoria;

/// <summary>Traduz nomes técnicos (entidade/campo) para os nomes mostrados ao usuário, via [DisplayName].</summary>
public static class DescritorCampos
{
    private static readonly ConcurrentDictionary<string, Type?> Tipos = new();

    public static string Entidade(string entidade) =>
        Tipo(entidade)?.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? entidade;

    public static string Campo(string entidade, string campo) =>
        Tipo(entidade)?.GetProperty(campo)?.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? campo;

    private static Type? Tipo(string nome) => Tipos.GetOrAdd(nome, n =>
        typeof(EntidadeBase).Assembly.GetTypes().FirstOrDefault(t => t.Name == n && t.Namespace == typeof(EntidadeBase).Namespace));
}
