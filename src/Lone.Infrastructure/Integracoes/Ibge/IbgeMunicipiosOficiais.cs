using System.Text.Json;
using Lone.Application.Municipios;

namespace Lone.Infrastructure.Integracoes.Ibge;

/// <summary>
/// Lista oficial de municípios pelo serviço de localidades do IBGE (gratuito, sem chave).
/// Lê só o código e o nome: a UF sai dos 2 primeiros dígitos do código, o que não depende da estrutura de
/// regiões do JSON (que já mudou e já veio nula para municípios novos).
/// </summary>
public class IbgeMunicipiosOficiais : IMunicipiosOficiais
{
    private const string Servico = "localidades do IBGE";
    private readonly HttpClient _http;

    public IbgeMunicipiosOficiais(HttpClient http)
    {
        _http = http;
    }

    public async Task<IReadOnlyList<MunicipioOficial>> ListarAsync(CancellationToken ct)
    {
        HttpResponseMessage resposta;
        try
        {
            resposta = await _http.GetAsync("api/v1/localidades/municipios", ct);
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

    internal static List<MunicipioOficial> Ler(JsonElement raiz)
    {
        var lista = new List<MunicipioOficial>();
        if (raiz.ValueKind != JsonValueKind.Array) return lista;

        foreach (var item in raiz.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var codigo = LeitorJson.Inteiro(item, "id");
            var nome = LeitorJson.Texto(item, "nome");
            if (codigo is { } c && nome is not null)
                lista.Add(new MunicipioOficial(c, nome));
        }
        return lista;
    }
}
