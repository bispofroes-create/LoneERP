using System.ComponentModel;
using Lone.Domain.Auditoria;

namespace Lone.Domain.Entidades;

/// <summary>
/// País do catálogo oficial da NF-e (cPais), tabela de sistema preenchida só pela migration a partir de
/// <see cref="Lone.Domain.Enderecos.PaisesNFeOficiais"/>, nunca pelo cadastro. O código é o da NF-e (4 dígitos, texto); não é o
/// código RFB de 3 dígitos nem o ISO, e nunca é convertido de um para outro. Códigos encerrados ficam (histórico).
/// </summary>
[DisplayName("País (NF-e)"), NaoAuditar]
public class Pais
{
    public const int TamanhoCodigo = 4;
    public const int TamanhoMaximoNome = 60;
    public const int TamanhoMaximoSituacao = 40;
    public const int TamanhoMaximoFonte = 100;
    public const int TamanhoMaximoVersao = 20;
    public const int TamanhoHash = 64;

    /// <summary>cPais da NF-e: exatamente 4 dígitos, com o zero à esquerda (Argentina = "0639").</summary>
    public string CodigoPaisNFe { get; set; } = string.Empty;

    /// <summary>Nome atual na tabela oficial, como está na fonte.</summary>
    public string NomeFiscal { get; set; } = string.Empty;

    /// <summary>Coluna SITUAÇÃO da fonte (INCLUÍDO, EXCLUÍDO, ALTERADO...); nula quando vazia.</summary>
    public string? SituacaoFonte { get; set; }

    public DateOnly VigenciaInicio { get; set; }

    /// <summary>Nula enquanto o código está em vigor.</summary>
    public DateOnly? VigenciaFim { get; set; }

    /// <summary>
    /// Código que substituiu este, só quando a fonte mostra a troca de forma inequívoca. Serve para sugerir; nunca para
    /// converter dados gravados.
    /// </summary>
    public string? CodigoSucessor { get; set; }

    public string Fonte { get; set; } = string.Empty;

    public string VersaoFonte { get; set; } = string.Empty;

    /// <summary>SHA-256 (hexadecimal minúsculo) do arquivo oficial de onde a linha veio.</summary>
    public string HashFonte { get; set; } = string.Empty;

    public bool VigenteEm(DateOnly data) => VigenciaInicio <= data && (VigenciaFim is null || data <= VigenciaFim);

    /// <summary>Exatamente 4 dígitos ASCII. Não completa zeros nem converte: "639" e "249" são inválidos.</summary>
    public static bool CodigoValido(string? codigo) =>
        codigo is { Length: TamanhoCodigo } && codigo.All(char.IsAsciiDigit);
}
