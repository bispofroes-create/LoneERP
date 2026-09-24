using Lone.Contracts.Integracoes;

namespace Lone.Application.Integracoes;

/// <summary>
/// Consulta de dados cadastrais por CNPJ. A implementação (BrasilAPI, Serpro...) pode ser trocada
/// sem mexer no cadastro. (Etapa 6: vira um orquestrador de provedores.)
/// </summary>
public interface ICnpjConsulta
{
    /// <summary>Retorna null se o CNPJ não existir. Lança ServicoExternoException em falha de comunicação.</summary>
    Task<DadosCnpj?> ConsultarAsync(string cnpj, CancellationToken ct = default);
}
