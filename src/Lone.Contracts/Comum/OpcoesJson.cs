using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lone.Contracts.Comum;

/// <summary>
/// Formato do JSON trocado entre o aplicativo e a API: nomes em camelCase e enums por nome
/// ("Cliente", e não 1), para as mensagens serem legíveis e resistentes a mudanças de numeração.
/// A API e o aplicativo chamam o mesmo método, então os dois lados sempre concordam.
/// </summary>
public static class OpcoesJson
{
    public static JsonSerializerOptions Configurar(JsonSerializerOptions opcoes)
    {
        opcoes.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        opcoes.PropertyNameCaseInsensitive = true;
        opcoes.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        if (!opcoes.Converters.OfType<JsonStringEnumConverter>().Any())
            opcoes.Converters.Add(new JsonStringEnumConverter());
        return opcoes;
    }

    /// <summary>Instância pronta para uso fora da API (aplicativo, testes).</summary>
    public static JsonSerializerOptions Padrao { get; } = Configurar(new JsonSerializerOptions(JsonSerializerDefaults.Web));
}
