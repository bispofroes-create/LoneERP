namespace Lone.Aplicacao.Integracoes;

public interface ICepConsulta
{
    /// <summary>Retorna null se o CEP não existir. Lança InvalidOperationException em falha de comunicação.</summary>
    Task<DadosCep?> ConsultarAsync(string cep, CancellationToken ct = default);
}
