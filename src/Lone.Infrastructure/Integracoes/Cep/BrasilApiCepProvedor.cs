using System.Net;
using System.Text.Json;
using Lone.Application.Integracoes.ConferenciaCep;
using Lone.Domain.Enderecos.ConferenciaCep;
using Lone.Domain.Validacao;
using Microsoft.Extensions.Logging;

namespace Lone.Infrastructure.Integracoes.Cep;

/// <summary>
/// BrasilAPI (GET cep/v1/{cep}), fonte <b>reserva</b>: só consulta por CEP e só é chamada pelo serviço quando o ViaCEP
/// falha tecnicamente (DM2). Particularidades: 404 = CEP não encontrado (resultado funcional); não traz o código IBGE
/// nem a faixa de numeração (o município é comparado pelo nome; sem faixa, o número não é conferido).
/// </summary>
public sealed class BrasilApiCepProvedor : IProvedorCep
{
    private readonly HttpClient _http;
    private readonly ILogger<BrasilApiCepProvedor> _log;

    public BrasilApiCepProvedor(HttpClient http, ILogger<BrasilApiCepProvedor> log)
    {
        _http = http;
        _log = log;
    }

    public CepFonte Fonte => CepFonte.BrasilApi;
    public bool BuscaPorEndereco => false;

    public async Task<ResultadoProvedorCep> ConsultarPorCepAsync(string cep, CancellationToken ct = default)
    {
        var numero = Documento.SomenteDigitos(cep);
        if (numero.Length != 8) return ResultadoProvedorCep.NaoEncontrado(Fonte);

        var r = await ChamadaCep.GetAsync(_http, $"cep/v1/{numero}", ct);
        if (r.Tecnica) return ChamadaCep.FalhaConsulta(_log, Fonte, r, numero);
        if (r.Status is HttpStatusCode.NotFound or HttpStatusCode.BadRequest) return ResultadoProvedorCep.NaoEncontrado(Fonte);
        if (r.Json is null) return ChamadaCep.InvalidaConsulta(_log, Fonte, numero, $"HTTP {(int)r.Status}");

        using var json = r.Json;
        var raiz = json.RootElement;
        if (raiz.ValueKind != JsonValueKind.Object) return ChamadaCep.InvalidaConsulta(_log, Fonte, numero, "não é objeto");
        var registro = Ler(raiz);
        if (registro is null || registro.Cep != numero)
            return ChamadaCep.InvalidaConsulta(_log, Fonte, numero, "sem o CEP pedido");
        return ResultadoProvedorCep.Encontrado(Fonte, registro);
    }

    /// <summary>A BrasilAPI não busca por endereço (o serviço nunca chama: <see cref="BuscaPorEndereco"/> é falso).</summary>
    public Task<ResultadoBuscaProvedorCep> BuscarPorEnderecoAsync(BuscaEnderecoCep busca, CancellationToken ct = default) =>
        throw new NotSupportedException("A BrasilAPI não busca CEP por endereço.");

    /// <summary>{ cep, state, city, neighborhood, street }. Sem IBGE e sem faixa: ficam nulos (nada é inventado).</summary>
    internal static RegistroCep? Ler(JsonElement e)
    {
        var cep = Documento.SomenteDigitos(LeitorJson.Texto(e, "cep"));
        if (cep.Length != 8) return null;
        return new RegistroCep(cep, LeitorJson.Texto(e, "street"), null, LeitorJson.Texto(e, "neighborhood"), LeitorJson.Texto(e, "city"),
            LeitorJson.Texto(e, "state"));
    }
}
