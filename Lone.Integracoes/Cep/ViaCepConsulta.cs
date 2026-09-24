using System.Text.Json;
using Lone.Aplicacao.Integracoes;
using Lone.Core.Validacao;

namespace Lone.Integracoes.Cep;

/// <summary>Consulta de CEP pelo ViaCEP (gratuito; devolve também o código IBGE do município).</summary>
public class ViaCepConsulta : ICepConsulta
{
    private const string Servico = "consulta de CEP";
    private readonly HttpClient _http;

    public ViaCepConsulta(HttpClient http)
    {
        _http = http;
    }

    public async Task<DadosCep?> ConsultarAsync(string cep, CancellationToken ct = default)
    {
        var numero = Documento.SomenteDigitos(cep);
        if (numero.Length != 8)
            return null;

        HttpResponseMessage resposta;
        try
        {
            resposta = await _http.GetAsync($"{numero}/json/", ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw ErroIntegracao.SemConexao(Servico, ex);
        }

        using (resposta)
        {
            if (resposta.StatusCode == System.Net.HttpStatusCode.BadRequest)
                return null;
            if (!resposta.IsSuccessStatusCode)
                throw ErroIntegracao.RespostaInesperada(Servico, (int)resposta.StatusCode);

            await using var fluxo = await resposta.Content.ReadAsStreamAsync(ct);
            using var json = await JsonDocument.ParseAsync(fluxo, cancellationToken: ct);
            var r = json.RootElement;

            // CEP inexistente: o ViaCEP responde 200 com { "erro": true } (ou "true").
            if (LeitorJson.Texto(r, "erro") == "true")
                return null;

            return new DadosCep
            {
                Cep = numero,
                Logradouro = LeitorJson.Texto(r, "logradouro"),
                Complemento = LeitorJson.Texto(r, "complemento"),
                Bairro = LeitorJson.Texto(r, "bairro"),
                Cidade = LeitorJson.Texto(r, "localidade"),
                Uf = LeitorJson.Texto(r, "uf"),
                CodigoMunicipioIbge = LeitorJson.Texto(r, "ibge")
            };
        }
    }
}
