using System.ComponentModel;

namespace Lone.Domain.Entidades;

/// <summary>
/// Arquivo anexado a um documento da pessoa (foto da CNH, PDF do alvará...). O conteúdo fica numa pasta do servidor
/// da API (D7), fora do banco (o SQL Server Express tem limite de 10 GB); aqui ficam só os dados do arquivo.
/// Nunca é apagado: removido na ficha, fica inativo — o arquivo continua guardado (histórico e auditoria).
/// Fica no histórico da pessoa (parte do cadastro dela), mas é gravado à parte da ficha: cada envio é imediato.
/// </summary>
[DisplayName("Anexo")]
public class AnexoDocumento : EntidadePessoaFilha
{
    public const int TamanhoMaximoNome = 200;

    /// <summary>Documento da pessoa a que o arquivo pertence.</summary>
    [DisplayName("Documento")]
    public Guid PessoaDocumentoId { get; set; }

    /// <summary>Nome do arquivo como veio do aparelho (só para exibir; nunca usado como caminho).</summary>
    [DisplayName("Arquivo")]
    public string NomeArquivo { get; set; } = string.Empty;

    [DisplayName("Tipo do arquivo")]
    public string TipoConteudo { get; set; } = string.Empty;

    [DisplayName("Tamanho (bytes)")]
    public long Tamanho { get; set; }

    /// <summary>SHA-256 do conteúdo (hexadecimal): confere a integridade ao baixar e aponta envios repetidos.</summary>
    [DisplayName("Hash")]
    public string Hash { get; set; } = string.Empty;

    /// <summary>Chave do arquivo no armazenamento (gerada pelo servidor a partir do Id; nunca vem do usuário).</summary>
    [DisplayName("Local")]
    public string Caminho { get; set; } = string.Empty;

    [DisplayName("Enviado por")]
    public string EnviadoPor { get; set; } = string.Empty;

    [DisplayName("Ativo")]
    public bool Ativo { get; set; } = true;
}
