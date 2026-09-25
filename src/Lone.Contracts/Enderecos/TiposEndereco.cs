namespace Lone.Contracts.Enderecos;

/// <summary>Tipo (classificação) de endereço do cadastro de tipos.</summary>
public sealed class TipoEnderecoDto
{
    public Guid Id { get; set; }
    public byte[]? Versao { get; set; }
    public string Nome { get; set; } = string.Empty;
    public int Ordem { get; set; }
    public bool Ativo { get; set; } = true;

    /// <summary>Somente leitura: quantos endereços ativos usam este tipo (só para quem gerencia os tipos).</summary>
    public int QuantidadeUsos { get; set; }
}
