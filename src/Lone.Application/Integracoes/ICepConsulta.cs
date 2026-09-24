using Lone.Contracts.Integracoes;

namespace Lone.Application.Integracoes;

public interface ICepConsulta
{
    /// <summary>Retorna null se o CEP não existir. Lança ServicoExternoException em falha de comunicação.</summary>
    Task<DadosCep?> ConsultarAsync(string cep, CancellationToken ct = default);
}
