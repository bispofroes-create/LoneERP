namespace Lone.Contracts.Documentos;

/// <summary>Dados de um arquivo anexado a um documento da pessoa (sem o conteúdo).</summary>
public sealed class AnexoDto
{
    public Guid Id { get; set; }
    public Guid PessoaDocumentoId { get; set; }
    public string NomeArquivo { get; set; } = string.Empty;
    public string TipoConteudo { get; set; } = string.Empty;
    public long Tamanho { get; set; }
    public DateTime EnviadoEm { get; set; }
    public string EnviadoPor { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;
}

/// <summary>Envio de um arquivo (conteúdo em base64 no JSON; o limite de tamanho é conferido pela API).</summary>
public sealed class EnviarAnexoRequisicao
{
    public string NomeArquivo { get; set; } = string.Empty;
    public byte[] Conteudo { get; set; } = [];
}

/// <summary>Arquivo baixado.</summary>
public sealed class AnexoConteudoDto
{
    public string NomeArquivo { get; set; } = string.Empty;
    public string TipoConteudo { get; set; } = string.Empty;
    public byte[] Conteudo { get; set; } = [];
}
