namespace Lone.Core.Enums;

/// <summary>Papéis que uma pessoa pode exercer. Os valores são gravados no banco: nunca renumere.</summary>
public enum TipoPapel : byte
{
    Cliente = 1,
    Fornecedor = 2,
    /// <summary>Empresa do próprio grupo que usa o Lone (as filiais são os estabelecimentos dela).</summary>
    EmpresaDoGrupo = 3,
    Vendedor = 4,
    Funcionario = 5,
    Transportadora = 6,
    Representante = 7,
    PrestadorServico = 8
}
