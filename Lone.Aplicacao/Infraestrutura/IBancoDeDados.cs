namespace Lone.Aplicacao.Infraestrutura;

public interface IBancoDeDados
{
    /// <summary>Cria o banco, se não existir, e aplica as migrações pendentes.</summary>
    Task PrepararAsync(CancellationToken ct = default);
}
