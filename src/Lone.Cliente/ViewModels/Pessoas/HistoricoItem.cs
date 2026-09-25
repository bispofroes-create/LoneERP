using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Auditoria;
using Lone.Domain.Enums;

namespace Lone.Cliente.ViewModels.Pessoas;

/// <summary>Linha do histórico do cadastro, já em texto: quando, quem e o que mudou.</summary>
public sealed record HistoricoItem(string Quando, string Descricao, string Motivo = "")
{
    public bool TemMotivo => Motivo.Length > 0;

    public static HistoricoItem De(RegistroHistorico r)
    {
        var parte = r.EntidadeDescricao;
        var descricao = r.Acao switch
        {
            AcaoAuditoria.Evento => r.Descricao ?? string.Empty,
            AcaoAuditoria.Inclusao when r.Entidade == "Pessoa" => "Cadastro criado",
            AcaoAuditoria.Inclusao => $"{parte}: incluído",
            AcaoAuditoria.Exclusao => $"{parte}: removido",
            _ => $"{parte} · {r.CampoDescricao}: \"{r.ValorAnterior ?? "vazio"}\" → \"{r.ValorNovo ?? "vazio"}\""
        };

        var origem = r.Origem switch
        {
            OrigemAlteracao.ConsultaExterna => " · consulta externa",
            OrigemAlteracao.Importacao => " · importação",
            OrigemAlteracao.Sistema => " · sistema",
            _ => string.Empty
        };

        var quando = DateTime.SpecifyKind(r.DataHora, DateTimeKind.Utc).ToLocalTime().ToString("dd/MM/yyyy HH:mm", TextoTela.Brasil);
        return new HistoricoItem($"{quando} · {r.Usuario}{origem}", descricao,
            string.IsNullOrWhiteSpace(r.Motivo) ? string.Empty : "Motivo: " + r.Motivo);
    }
}
