namespace Lone.Core.Auditoria;

/// <summary>
/// Marca um campo com dado pessoal que não deve aparecer por inteiro no histórico (LGPD).
/// A auditoria guarda o valor mascarado (ex.: "•••••••2-25").
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class DadoSensivelAttribute : Attribute
{
}
