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
            AcaoAuditoria.Inclusao when r.Entidade == "Pessoa" && r.Campo is null => "Cadastro criado",
            AcaoAuditoria.Inclusao when r.Campo is null => $"{parte}: incluído",
            AcaoAuditoria.Exclusao when r.Campo is null => $"{parte}: removido",
            // Foto do conteúdo (P0, D3-a): uma linha por campo, junto da linha "incluído"/"removido" da mesma operação.
            AcaoAuditoria.Inclusao => $"{parte}: incluído · {r.CampoDescricao} = \"{r.ValorNovo}\"",
            AcaoAuditoria.Exclusao => $"{parte}: removido · {r.CampoDescricao} era \"{r.ValorAnterior}\"",
            // Inativar não é excluir (D6): o registro continua gravado.
            AcaoAuditoria.Inativacao => string.IsNullOrWhiteSpace(r.Descricao) ? $"{parte}: inativado" : $"{parte}: inativado — {r.Descricao}",
            AcaoAuditoria.Reativacao => string.IsNullOrWhiteSpace(r.Descricao) ? $"{parte}: reativado" : $"{parte}: reativado — {r.Descricao}",
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
