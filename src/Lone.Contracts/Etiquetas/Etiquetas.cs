namespace Lone.Contracts.Etiquetas;

/// <summary>Etiqueta do cadastro, como trafega entre o aplicativo e a API.</summary>
public sealed class EtiquetaDto
{
    public Guid Id { get; set; }
    public byte[]? Versao { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public bool Ativo { get; set; } = true;

    /// <summary>Somente leitura: quantos cadastros têm esta etiqueta (só para quem gerencia etiquetas).</summary>
    public int QuantidadePessoas { get; set; }
}

/// <summary>Mesclar: as pessoas da etiqueta de origem (a da URL) passam para a de destino, e a origem é desativada.</summary>
public sealed class MesclarEtiquetaRequisicao
{
    public Guid DestinoId { get; set; }

    /// <summary>Versão da etiqueta de origem que o usuário via (se mudou, dá conflito).</summary>
    public byte[]? Versao { get; set; }
}

public sealed class ResultadoMesclarEtiqueta
{
    /// <summary>A etiqueta de origem, já desativada.</summary>
    public EtiquetaDto Origem { get; set; } = new();
    public EtiquetaDto Destino { get; set; } = new();

    /// <summary>Cadastros que tinham a etiqueta de origem.</summary>
    public int CadastrosAlterados { get; set; }
}
