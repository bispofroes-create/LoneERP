using System.Text.Json;
using Lone.Application.Integracoes;
using Lone.Contracts.Integracoes;
using Microsoft.Extensions.Logging;

namespace Lone.Infrastructure.Integracoes.Cnpj;

/// <summary>
/// Inscrições estaduais pela API pública do CNPJ.ws (gratuita, até 3 consultas por minuto, parte dos estados).
/// Qualquer falha (limite atingido, fora do ar, formato inesperado) resulta em lista vazia: a consulta de CNPJ
/// continua valendo e o usuário digita a inscrição, que o sistema confere pela regra da UF.
/// </summary>
public class CnpjWsInscricaoConsulta : IInscricaoEstadualConsulta
{
    private readonly HttpClient _http;
    private readonly ILogger<CnpjWsInscricaoConsulta> _log;

    public CnpjWsInscricaoConsulta(HttpClient http, ILogger<CnpjWsInscricaoConsulta> log)
    {
        _http = http;
        _log = log;
    }

    public async Task<List<InscricaoEstadualEncontrada>> ConsultarAsync(string cnpj, CancellationToken ct = default)
    {
        try
        {
            using var resposta = await _http.GetAsync($"cnpj/{cnpj}", ct);
            if (!resposta.IsSuccessStatusCode)
            {
                _log.LogInformation("CNPJ.ws respondeu {Status} para as inscrições estaduais.", (int)resposta.StatusCode);
                return [];
            }

            await using var fluxo = await resposta.Content.ReadAsStreamAsync(ct);
            using var json = await JsonDocument.ParseAsync(fluxo, cancellationToken: ct);
            return Ler(json.RootElement);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException
                                   && !ct.IsCancellationRequested)
        {
            _log.LogInformation(ex, "Inscrições estaduais indisponíveis no CNPJ.ws.");
            return [];
        }
    }

    /// <summary>estabelecimento.inscricoes_estaduais[] { inscricao_estadual, ativo, estado { sigla } }.</summary>
    internal static List<InscricaoEstadualEncontrada> Ler(JsonElement raiz)
    {
        if (raiz.ValueKind != JsonValueKind.Object ||
            !raiz.TryGetProperty("estabelecimento", out var estabelecimento) ||
            estabelecimento.ValueKind != JsonValueKind.Object ||
            !estabelecimento.TryGetProperty("inscricoes_estaduais", out var lista) ||
            lista.ValueKind != JsonValueKind.Array)
            return [];

        var encontradas = new List<InscricaoEstadualEncontrada>();
        foreach (var item in lista.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var numero = LeitorJson.Texto(item, "inscricao_estadual");
            var uf = item.TryGetProperty("estado", out var estado) && estado.ValueKind == JsonValueKind.Object
                ? LeitorJson.Texto(estado, "sigla")
                : null;
            if (numero is null || uf is null) continue;

            var ativa = item.TryGetProperty("ativo", out var ativo) && ativo.ValueKind == JsonValueKind.True;
            encontradas.Add(new InscricaoEstadualEncontrada(uf.ToUpperInvariant(), numero, ativa));
        }
        return encontradas;
    }
}
