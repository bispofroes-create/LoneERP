using System.Globalization;
using System.Text.Json;
using Lone.Application.Fiscal;

namespace Lone.Infrastructure.Integracoes.Ibge;

/// <summary>Subclasses da CNAE pelo serviço de dados do IBGE (gratuito, sem chave): código "0111301" e descrição.</summary>
public class IbgeCnaesOficiais : ICnaesOficiais
{
    private const string Servico = "CNAE do IBGE";
    private readonly HttpClient _http;

    public IbgeCnaesOficiais(HttpClient http)
    {
        _http = http;
    }

    public async Task<IReadOnlyList<CnaeOficial>> ListarAsync(CancellationToken ct)
    {
        HttpResponseMessage resposta;
        try
        {
            resposta = await _http.GetAsync("api/v2/cnae/subclasses", ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            throw ErroIntegracao.SemConexao(Servico, ex);
        }

        using (resposta)
        {
            if (!resposta.IsSuccessStatusCode)
                throw ErroIntegracao.RespostaInesperada(Servico, (int)resposta.StatusCode);

            await using var fluxo = await resposta.Content.ReadAsStreamAsync(ct);
            using var json = await JsonDocument.ParseAsync(fluxo, cancellationToken: ct);
            return Ler(json.RootElement);
        }
    }

    internal static List<CnaeOficial> Ler(JsonElement raiz)
    {
        var lista = new List<CnaeOficial>();
        if (raiz.ValueKind != JsonValueKind.Array) return lista;
        foreach (var item in raiz.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var id = LeitorJson.Texto(item, "id");
            var descricao = LeitorJson.Texto(item, "descricao");
            var digitos = new string((id ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
            if (digitos.Length == 7 && descricao is not null)
                lista.Add(new CnaeOficial(int.Parse(digitos, CultureInfo.InvariantCulture), descricao));
        }
        return lista;
    }
}
