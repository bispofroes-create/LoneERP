using Lone.Domain.Enums;

namespace Lone.Contracts.Contatos;

/// <summary>Tipo (classificação) de telefone ou e-mail do cadastro de tipos.</summary>
public sealed class TipoMeioContatoDto
{
    public Guid Id { get; set; }
    public byte[]? Versao { get; set; }
    public string Nome { get; set; } = string.Empty;

    /// <summary>Telefone ou e-mail; só é aceito na criação.</summary>
    public CategoriaMeioContato Categoria { get; set; }
    public int Ordem { get; set; }
    public bool Ativo { get; set; } = true;

    /// <summary>Somente leitura: quantos telefones/e-mails ativos usam este tipo (só para quem gerencia os tipos).</summary>
    public int QuantidadeUsos { get; set; }
}
