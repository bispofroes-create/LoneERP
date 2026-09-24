using Lone.Aplicacao.Auditoria;
using Lone.Core.Enums;

namespace Lone.ViewModels.Pessoas
{
    /// <summary>Linha do histórico já formatada para a tela.</summary>
    public class HistoricoItem
    {
        public string Quando { get; init; } = string.Empty;
        public string Descricao { get; init; } = string.Empty;

        public static HistoricoItem De(RegistroHistorico r)
        {
            var parte = r.EntidadeDescricao;
            var descricao = r.Acao switch
            {
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

            return new HistoricoItem
            {
                Quando = $"{r.DataHora:dd/MM/yyyy HH:mm} · {r.Usuario}{origem}",
                Descricao = descricao
            };
        }
    }
}
