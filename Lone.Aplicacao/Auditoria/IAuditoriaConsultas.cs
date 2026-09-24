namespace Lone.Aplicacao.Auditoria;

/// <summary>Leitura da auditoria do ERP. Implementado em Lone.Data; serve qualquer módulo.</summary>
public interface IAuditoriaConsultas
{
    /// <summary>Histórico de um agregado (ex.: "Pessoa", 125), do mais recente ao mais antigo.</summary>
    Task<List<RegistroHistorico>> ListarPorRaizAsync(string raizEntidade, int raizId, int limite, CancellationToken ct);
}
