namespace Lone.Domain.Enums;

/// <summary>
/// Usos de um endereço. Um mesmo endereço pode ter vários (ex.: principal + cobrança + entrega),
/// por isso é um conjunto de marcações e não um tipo único.
/// </summary>
[Flags]
public enum FinalidadeEndereco : short
{
    Nenhuma = 0,
    Principal = 1,
    Fiscal = 2,
    Cobranca = 4,
    Entrega = 8,
    Correspondencia = 16,
    Residencial = 32,
    Comercial = 64
}
