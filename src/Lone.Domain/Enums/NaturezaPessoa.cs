namespace Lone.Domain.Enums;

/// <summary>Natureza jurídica da pessoa. Não confundir com papel (cliente, fornecedor...).</summary>
public enum NaturezaPessoa : byte
{
    Fisica = 0,
    Juridica = 1,
    Estrangeiro = 2
}
