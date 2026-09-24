namespace Lone.Core.Auditoria;

/// <summary>
/// Implementado por entidades que pertencem a um agregado (ex.: endereço pertence à Pessoa).
/// A auditoria usa isso para montar o histórico completo do agregado.
/// Implemente de forma explícita, para o EF não criar colunas para estas propriedades.
/// </summary>
public interface IParteDeAgregado
{
    string RaizEntidade { get; }
    int RaizId { get; }
}
