using Lone.Contracts.Comum;
using Lone.Contracts.Integracoes;

namespace Lone.Cliente.Api;

/// <summary>Consultas externas feitas pela API (o aparelho nunca fala direto com Receita ou Correios).</summary>
public sealed class ConsultasApi
{
    private readonly ClienteApi _api;

    public ConsultasApi(ClienteApi api)
    {
        _api = api;
    }

    /// <summary>Nulo quando o CEP não existe.</summary>
    public Task<DadosCep?> ConsultarCepAsync(string cep, CancellationToken ct = default) =>
        _api.GetOuNuloAsync<DadosCep>(Rotas.Consultas.Cep(cep), ct);

    /// <summary>Nulo quando o CNPJ não existe.</summary>
    public Task<DadosCnpj?> ConsultarCnpjAsync(string cnpj, CancellationToken ct = default) =>
        _api.GetOuNuloAsync<DadosCnpj>(Rotas.Consultas.Cnpj(cnpj), ct);
}
