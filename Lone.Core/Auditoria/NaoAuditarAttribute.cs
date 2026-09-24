namespace Lone.Core.Auditoria;

/// <summary>
/// A entidade inteira fica fora da auditoria de cadastro (ex.: controle técnico de acesso,
/// que muda a cada login e só geraria ruído no histórico).
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class NaoAuditarAttribute : Attribute
{
}
