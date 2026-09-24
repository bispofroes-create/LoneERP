using Lone.Contracts.Integracoes;

namespace Lone.Application.Integracoes;

/// <summary>
/// Busca as inscrições estaduais de um CNPJ numa fonte pública complementar. Melhor esforço: a fonte
/// gratuita cobre parte dos estados e aceita poucas consultas por minuto, então "não achou" é normal.
/// </summary>
public interface IInscricaoEstadualConsulta
{
    /// <summary>Lista vazia quando não encontra ou a fonte não responde (nunca lança por falha da fonte).</summary>
    Task<List<InscricaoEstadualEncontrada>> ConsultarAsync(string cnpj, CancellationToken ct = default);
}
