using System.ComponentModel;
using Lone.Domain.Auditoria;
using Lone.Domain.CamposPersonalizados;

namespace Lone.Domain.Entidades;

/// <summary>
/// Valor de um campo personalizado. Uma coluna por natureza de dado (texto, número, data, sim/não, opção): o valor
/// fica tipado no banco, dá para filtrar e ordenar com índice e sem conversões. Qual coluna cada tipo usa é decidido
/// pela classe do tipo (ITipoCampo). Sem linha = não informado. Base comum dos valores da pessoa e dos documentos
/// (D4: o mesmo motor serve aos dois; cada um tem sua tabela).
/// </summary>
public abstract class ValorPersonalizado : EntidadePessoaFilha, IValorAuditavel
{
    /// <summary>Prefixo gravado em Auditoria.Campo; a consulta do histórico troca pelo nome atual do campo.</summary>
    public const string PrefixoAuditoria = "CampoPersonalizado:";

    public Guid CampoId { get; set; }

    public string? ValorTexto { get; set; }
    public decimal? ValorNumero { get; set; }
    public DateTime? ValorData { get; set; }
    public bool? ValorLogico { get; set; }
    public Guid? OpcaoId { get; set; }

    public bool Vazio => ValorTexto is null && ValorNumero is null && ValorData is null && ValorLogico is null && OpcaoId is null;

    string IValorAuditavel.CampoAuditado => PrefixoAuditoria + CampoId;

    /// <summary>Texto neutro (a consulta do histórico formata e troca o Id da opção pelo texto dela).</summary>
    string? IValorAuditavel.DescreverValor(Func<string, object?> coluna) =>
        ValorCampo.Descrever(
            coluna(nameof(ValorTexto)) as string,
            coluna(nameof(ValorNumero)) as decimal?,
            coluna(nameof(ValorData)) as DateTime?,
            coluna(nameof(ValorLogico)) as bool?,
            coluna(nameof(OpcaoId)) as Guid?);

    /// <summary>Cópia com todos os dados (inclusive os da classe filha, como o documento).</summary>
    public ValorPersonalizado Clonar() => (ValorPersonalizado)MemberwiseClone();
}

/// <summary>Valor de um campo personalizado da pessoa (aba "Informações adicionais").</summary>
[DisplayName("Informação adicional")]
public class PessoaValorPersonalizado : ValorPersonalizado
{
}

/// <summary>Valor de um campo personalizado de um documento da pessoa (ex.: "Categoria" da CNH).</summary>
[DisplayName("Informação do documento")]
public class DocumentoValorPersonalizado : ValorPersonalizado
{
    public Guid PessoaDocumentoId { get; set; }
}
