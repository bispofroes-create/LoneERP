using System.ComponentModel;
using Lone.Domain.Auditoria;
using Lone.Domain.CamposPersonalizados;

namespace Lone.Domain.Entidades;

/// <summary>
/// Valor de um campo personalizado para uma pessoa. Uma coluna por natureza de dado (texto, número, data,
/// sim/não, opção): o valor fica tipado no banco, dá para filtrar e ordenar com índice e sem conversões.
/// Qual coluna cada tipo usa é decidido pela classe do tipo (ITipoCampo). Sem linha = não informado.
/// </summary>
[DisplayName("Informação adicional")]
public class PessoaValorPersonalizado : EntidadePessoaFilha, IValorAuditavel
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
}
