namespace Lone.Application.Infraestrutura;

public interface IBancoDeDados
{
    /// <summary>Cria o banco, se não existir, e aplica as migrações pendentes.</summary>
    Task PrepararAsync(CancellationToken ct = default);

    /// <summary>Migrações que existem no código e ainda não foram aplicadas no banco (não altera nada).</summary>
    Task<IReadOnlyList<string>> MigracoesPendentesAsync(CancellationToken ct = default);

    /// <summary>O banco responde? (verificação de saúde da API).</summary>
    Task<bool> DisponivelAsync(CancellationToken ct = default);
}
