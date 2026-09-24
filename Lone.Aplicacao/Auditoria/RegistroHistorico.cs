using Lone.Core.Auditoria;
using Lone.Core.Enums;

namespace Lone.Aplicacao.Auditoria;

/// <summary>Linha de histórico pronta para exibir: nomes técnicos já traduzidos.</summary>
public sealed class RegistroHistorico
{
    public DateTime DataHora { get; init; }
    public string Usuario { get; init; } = string.Empty;
    public OrigemAlteracao Origem { get; init; }
    public AcaoAuditoria Acao { get; init; }
    public string Entidade { get; init; } = string.Empty;
    public string? Campo { get; init; }
    public string? ValorAnterior { get; init; }
    public string? ValorNovo { get; init; }

    public string EntidadeDescricao => DescritorCampos.Entidade(Entidade);
    public string? CampoDescricao => Campo is null ? null : DescritorCampos.Campo(Entidade, Campo);
}
