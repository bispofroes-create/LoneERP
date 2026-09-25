using Lone.Domain.Auditoria;
using Lone.Domain.Enums;

namespace Lone.Contracts.Auditoria;

/// <summary>Linha de histórico pronta para exibir: nomes técnicos já traduzidos. DataHora em UTC.</summary>
public sealed class RegistroHistorico
{
    /// <summary>Posição no histórico: a próxima página começa antes deste Id.</summary>
    public long Id { get; set; }
    public DateTime DataHora { get; set; }
    public string Usuario { get; set; } = string.Empty;
    public OrigemAlteracao Origem { get; set; }
    public AcaoAuditoria Acao { get; set; }
    public string Entidade { get; set; } = string.Empty;
    public string? Campo { get; set; }
    public string? ValorAnterior { get; set; }
    public string? ValorNovo { get; set; }

    /// <summary>Texto do evento (Acao = Evento), ex.: "Cliente João da Silva foi desativado."</summary>
    public string? Descricao { get; set; }

    /// <summary>Motivo informado pelo usuário na operação (nulo = não informado).</summary>
    public string? Motivo { get; set; }

    public string EntidadeDescricao => DescritorCampos.Entidade(Entidade);
    public string? CampoDescricao => Campo is null ? null : DescritorCampos.Campo(Entidade, Campo);

    /// <summary>Uma frase para a tela, ex.: "Endereço · Cidade: "Curvelo" → "Corinto"".</summary>
    public string Resumo => Acao switch
    {
        AcaoAuditoria.Evento => Descricao ?? string.Empty,
        AcaoAuditoria.Inclusao when Campo is null => $"{EntidadeDescricao}: incluído",
        AcaoAuditoria.Exclusao when Campo is null => $"{EntidadeDescricao}: removido",
        _ => $"{EntidadeDescricao} · {CampoDescricao}: \"{ValorAnterior ?? "vazio"}\" → \"{ValorNovo ?? "vazio"}\""
    };
}
