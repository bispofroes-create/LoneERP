namespace Lone.Domain.Auditoria;

/// <summary>
/// A auditoria registra que o campo mudou, mas nunca o valor (ex.: hash de senha).
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class NaoAuditarValorAttribute : Attribute
{
}
