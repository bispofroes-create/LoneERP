namespace Lone.Domain.Enums;

/// <summary>Situação de um documento pela data de validade (calculada; não é gravada).</summary>
public enum SituacaoValidade : byte
{
    /// <summary>Sem data de validade.</summary>
    SemValidade = 0,
    Valido = 1,

    /// <summary>Dentro da antecedência de aviso do tipo de documento.</summary>
    VenceEmBreve = 2,
    Vencido = 3
}
