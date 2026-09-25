using Lone.Domain.Enums;

namespace Lone.Contracts.Papeis;

/// <summary>Papel do cadastro de papéis (Cliente, Fornecedor... e os criados pelo usuário).</summary>
public sealed class PapelCadastroDto
{
    public Guid Id { get; set; }
    public byte[]? Versao { get; set; }

    /// <summary>Código estável (ex.: "CLIENTE"); só é aceito na criação, depois não muda.</summary>
    public string Codigo { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public int Ordem { get; set; }
    public bool Ativo { get; set; } = true;

    /// <summary>Somente leitura: papel de sistema (com regras no código); nulo nos criados pelo usuário.</summary>
    public TipoPapel? PapelSistema { get; set; }

    /// <summary>Somente leitura: o papel de sistema tem regra e não pode ser desativado.</summary>
    public bool Obrigatorio { get; set; }

    /// <summary>Somente leitura: quantos cadastros têm este papel ativo (só para quem gerencia papéis).</summary>
    public int QuantidadePessoas { get; set; }
}
