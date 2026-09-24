namespace Lone.Aplicacao.Integracoes;

/// <summary>
/// Consulta de dados cadastrais por CNPJ. A implementação (BrasilAPI, Serpro...) pode ser trocada
/// sem mexer no cadastro. (Etapa 6: vira ICnpjConsultaProvider com orquestrador de provedores.)
/// </summary>
public interface ICnpjConsulta
{
    /// <summary>Retorna null se o CNPJ não existir. Lança InvalidOperationException em falha de comunicação.</summary>
    Task<DadosCnpj?> ConsultarAsync(string cnpj, CancellationToken ct = default);
}
