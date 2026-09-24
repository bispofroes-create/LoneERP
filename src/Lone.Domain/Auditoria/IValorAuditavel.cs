namespace Lone.Domain.Auditoria;

/// <summary>
/// Entidade cujo conteúdo é um único valor lógico espalhado em várias colunas (ex.: valor de campo personalizado,
/// com uma coluna por tipo). A auditoria registra uma linha só, com o nome do campo e o valor legível, em vez de
/// uma linha por coluna.
/// </summary>
public interface IValorAuditavel
{
    /// <summary>Nome gravado em Auditoria.Campo (até 60 caracteres). Traduzido para o nome da tela na consulta.</summary>
    string CampoAuditado { get; }

    /// <summary>Valor legível a partir das colunas (a auditoria passa os valores antigos ou os novos).</summary>
    string? DescreverValor(Func<string, object?> coluna);
}
