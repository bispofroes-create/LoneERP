using System.Net;
using System.Text;
using Lone.Cliente.ViewModels.Comum;

namespace Lone.Cliente.ViewModels.Comercial;

/// <summary>
/// Carteira vencendo para imprimir (03/10/2026, como o histórico de Pessoas): página HTML simples aberta no navegador,
/// que já chama a impressão (o navegador também salva em PDF). O texto é escapado.
/// </summary>
public static class CarteiraVencendoImpressao
{
    public static string Html(string filtro, IReadOnlyList<LinhaVencendo> itens, string resumo, DateTime geradoEm)
    {
        static string E(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);
        var sb = new StringBuilder();
        sb.Append("<!doctype html><html lang=\"pt-BR\"><head><meta charset=\"utf-8\"><title>Carteira vencendo</title><style>")
          .Append("body{font-family:Segoe UI,Arial,sans-serif;color:#1b1f24;margin:32px;font-size:13px}")
          .Append("h1{font-size:20px;margin:0 0 4px}p.sub{color:#5b6470;margin:0 0 16px}")
          .Append("table{border-collapse:collapse;width:100%}th,td{text-align:left;vertical-align:top;padding:6px 8px;border-bottom:1px solid #dde1e6}")
          .Append("th{font-size:12px;color:#5b6470;text-transform:uppercase}td.n{white-space:nowrap}")
          .Append("@media print{body{margin:12mm}}</style></head><body>")
          .Append("<h1>Carteira vencendo</h1><p class=\"sub\">").Append(E(resumo))
          .Append(filtro.Length > 0 ? " · " + E(filtro) : " · sem filtro")
          .Append(" · gerado em ").Append(E(geradoEm.ToString("dd/MM/yyyy HH:mm", TextoTela.Brasil))).Append("</p>")
          .Append("<table><thead><tr><th>Cliente</th><th>Papel</th><th>Quem atende</th><th>Empresa</th><th>Início</th><th>Fim</th><th>Faltam</th></tr></thead><tbody>");
        foreach (var l in itens)
            sb.Append("<tr><td>").Append(E(l.Cliente)).Append("</td><td>").Append(E(l.Papel)).Append("</td><td>").Append(E(l.Pessoa))
              .Append("</td><td>").Append(E(l.Empresa)).Append("</td><td class=\"n\">").Append(E(l.Inicio)).Append("</td><td class=\"n\">")
              .Append(E(l.Fim)).Append("</td><td class=\"n\">").Append(E(l.Faltam)).Append("</td></tr>");
        if (itens.Count == 0) sb.Append("<tr><td colspan=\"7\"><em>Nenhum vínculo no prazo.</em></td></tr>");
        sb.Append("</tbody></table><script>window.onload=function(){window.print();};</script></body></html>");
        return sb.ToString();
    }
}
