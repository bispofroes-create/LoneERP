namespace Lone.Core.Enums;

/// <summary>
/// Situação do cadastro. Bloqueio e suspensão NÃO são situações: são registros de Bloqueio (com histórico).
/// </summary>
public enum SituacaoPessoa : byte
{
    /// <summary>Pode ser usada em todas as operações.</summary>
    Ativo = 0,
    /// <summary>Cadastro incompleto ou aguardando aprovação: não movimenta.</summary>
    EmAnalise = 1,
    /// <summary>Fora de uso; some da busca padrão; pode ser reativada.</summary>
    Inativo = 2,
    /// <summary>Somente leitura, definitivo (ex.: cadastro duplicado mesclado em outro).</summary>
    Arquivado = 3
}
