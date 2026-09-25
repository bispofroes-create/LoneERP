using Lone.Contracts.Auditoria;

namespace Lone.Application.Auditoria;

/// <summary>Leitura da auditoria do ERP. Implementado em Lone.Infrastructure; serve qualquer módulo.</summary>
public interface IAuditoriaConsultas
{
    /// <summary>
    /// Histórico de um agregado (ex.: "Pessoa", id), do mais recente ao mais antigo, em páginas. Datas em UTC.
    /// </summary>
    /// <param name="antesDe">Id do último registro da página anterior (nulo = primeira página).</param>
    Task<List<RegistroHistorico>> ListarPorRaizAsync(string raizEntidade, Guid raizId, int limite, long? antesDe, CancellationToken ct);
}
