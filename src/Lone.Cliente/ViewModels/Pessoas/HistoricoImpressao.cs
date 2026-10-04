using System.Net;
using System.Text;
using Lone.Cliente.ViewModels.Comum;

namespace Lone.Cliente.ViewModels.Pessoas;

/// <summary>
/// Histórico de um cadastro para imprimir (03/10/2026): página HTML simples, aberta no navegador padrão, que já chama a
/// impressão (o navegador também salva em PDF). Sem biblioteca de PDF; o texto é escapado.
/// </summary>
public static class HistoricoImpressao
{
    public static string Html(string pessoa, string filtro, IReadOnlyList<HistoricoItem> itens, bool cortado, DateTime geradoEm)
    {
        static string E(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);
        var sb = new StringBuilder();
        sb.Append("<!doctype html><html lang=\"pt-BR\"><head><meta charset=\"utf-8\"><title>Histórico · ")
          .Append(E(pessoa)).Append("</title><style>")
          .Append("body{font-family:Segoe UI,Arial,sans-serif;color:#1b1f24;margin:32px;font-size:13px}")
          .Append("h1{font-size:20px;margin:0 0 4px}p.sub{color:#5b6470;margin:0 0 16px}")
          .Append("table{border-collapse:collapse;width:100%}th,td{text-align:left;vertical-align:top;padding:6px 8px;border-bottom:1px solid #dde1e6}")
          .Append("th{font-size:12px;color:#5b6470;text-transform:uppercase}td.q{white-space:nowrap;width:1%}em{color:#5b6470}")
          .Append("@media print{body{margin:12mm}}</style></head><body>")
          .Append("<h1>Histórico · ").Append(E(pessoa)).Append("</h1><p class=\"sub\">")
          .Append(itens.Count == 1 ? "1 registro" : $"{itens.Count.ToString("N0", TextoTela.Brasil)} registros")
          .Append(filtro.Length > 0 ? " · " + E(filtro) : " · sem filtro")
          .Append(" · gerado em ").Append(E(geradoEm.ToString("dd/MM/yyyy HH:mm", TextoTela.Brasil))).Append("</p>");
        if (cortado)
            sb.Append("<p class=\"sub\"><strong>Mostrando só os registros mais recentes; use o filtro para ver um período menor.</strong></p>");
        sb.Append("<table><thead><tr><th>Quando · quem</th><th>O que mudou</th></tr></thead><tbody>");
        foreach (var i in itens)
        {
            sb.Append("<tr><td class=\"q\">").Append(E(i.Quando)).Append("</td><td>").Append(E(i.Descricao));
            if (i.TemMotivo) sb.Append("<br><em>").Append(E(i.Motivo)).Append("</em>");
            sb.Append("</td></tr>");
        }
        if (itens.Count == 0) sb.Append("<tr><td colspan=\"2\"><em>Sem alterações registradas.</em></td></tr>");
        sb.Append("</tbody></table><script>window.onload=function(){window.print();};</script></body></html>");
        return sb.ToString();
    }
}
