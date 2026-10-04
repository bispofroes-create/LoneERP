using System.Net;
using System.Text;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Territorios;
using Lone.Domain.Enums;

namespace Lone.Cliente.ViewModels.Territorios;

/// <summary>
/// Textos e exportação da tela Divergências territoriais (padrão de tela de consulta, 04/10/2026): título com o contador,
/// contagem por efeito, cor do selo, lista vazia, CSV para o Excel e página de impressão.
/// </summary>
public static class DivergenciasTerritoriais
{
    public const string TituloAntesDeConferir = "Escolha o mapa e clique em Conferir";
    public const string TextoAntesDeConferir = "Aparece quem as regras e exceções de hoje colocariam num território diferente do gravado.";
    public const string TituloSemMapas = "Nenhum mapa territorial ativo";
    public const string TextoSemMapas = "As divergências são conferidas por mapa: cadastre ou reative um mapa territorial.";

    /// <summary>"Divergências" antes de conferir; "Divergências em 04/10/2026 (37)" depois.</summary>
    public static string TituloLista(DateOnly? data, int total) =>
        data is { } d ? $"Divergências em {TextoTela.Data(d)} ({total.ToString("N0", TextoTela.Brasil)})" : "Divergências";

    /// <summary>Quantos o mapa tem com o efeito (contagem da API, antes do alcance).</summary>
    public static int Quantidade(DivergenciasTerritoriaisDto d, EfeitoNoCliente efeito) => efeito switch
    {
        EfeitoNoCliente.Entra => d.Entram,
        EfeitoNoCliente.Sai => d.Saem,
        EfeitoNoCliente.Muda => d.Mudam,
        EfeitoNoCliente.OrigemAtualizada => d.OrigemAtualizada,
        EfeitoNoCliente.Bloqueado => d.Inconsistencias,
        _ => 0
    };

    /// <summary>Cor do selo do efeito: inconsistência em vermelho, saída em amarelo, entrada em verde, o resto neutro/azul.</summary>
    public static string Tom(EfeitoNoCliente efeito) => efeito switch
    {
        EfeitoNoCliente.Bloqueado => "Erro",
        EfeitoNoCliente.Sai => "Aviso",
        EfeitoNoCliente.Entra => "Sucesso",
        EfeitoNoCliente.Muda => "Informacao",
        _ => "Neutro"
    };

    public static string Territorio(string? nome) => string.IsNullOrWhiteSpace(nome) ? "Sem território" : nome;

    public static string AvisoInconsistencias(int quantas) => quantas == 1
        ? "1 cliente com inconsistência (conflito de fixação ou fixação inválida): ela bloqueia a aplicação até ser resolvida. Veja em \"Inconsistências\"."
        : $"{quantas.ToString("N0", TextoTela.Brasil)} clientes com inconsistência (conflito de fixação ou fixação inválida): elas bloqueiam a aplicação até serem resolvidas. Veja em \"Inconsistências\".";

    /// <summary>"Em 04/10/2026: 12 entrariam, 5 sairiam, 20 mudariam de território..." (só o que tem).</summary>
    public static string Resumo(DivergenciasTerritoriaisDto d)
    {
        static string? P(int n, string texto) => n > 0 ? $"{n.ToString("N0", TextoTela.Brasil)} {texto}" : null;
        var partes = new[]
        {
            P(d.Entram, "entrariam"), P(d.Saem, "sairiam"), P(d.Mudam, "mudariam de território"), P(d.OrigemAtualizada, "mudariam só a origem"),
            P(d.EmConflito, "em conflito"), P(d.Inconsistencias, "com inconsistência")
        }.Where(x => x is not null).ToList();
        return $"Em {TextoTela.Data(d.Data)}: " + (partes.Count == 0 ? "nenhuma divergência" : string.Join(", ", partes)) +
               (d.Itens.FiltradoPeloAlcance ? " (lista só com os clientes do seu alcance)" : string.Empty);
    }

    /// <summary>CSV para o Excel (separador ";", como o Excel em português abre direto; texto entre aspas quando precisa).</summary>
    public static string Csv(IEnumerable<LinhaItemTerritorial> linhas)
    {
        static string C(string? s)
        {
            s ??= string.Empty;
            return s.IndexOfAny([';', '"', '\n', '\r']) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
        }
        var sb = new StringBuilder("Código;Cliente;Efeito;Território atual;Território certo;Resultado;Motivo;Por quê\r\n");
        foreach (var l in linhas)
            sb.Append(l.Item.Codigo?.ToString("000000", System.Globalization.CultureInfo.InvariantCulture)).Append(';')
              .Append(C(l.Item.Pessoa)).Append(';').Append(C(l.Item.EfeitoNome)).Append(';')
              .Append(C(Territorio(l.Item.TerritorioAtual))).Append(';').Append(C(Territorio(l.Item.TerritorioProposto))).Append(';')
              .Append(C(l.Item.ResultadoNome)).Append(';').Append(C(l.Item.Motivo)).Append(';').Append(C(l.PorQue)).Append("\r\n");
        return sb.ToString();
    }

    /// <summary>Página para imprimir ou salvar em PDF (abre no navegador e chama a impressão).</summary>
    public static string Html(string mapa, string filtro, IReadOnlyList<LinhaItemTerritorial> itens, DivergenciasTerritoriaisDto d, DateTime geradoEm)
    {
        static string E(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);
        var sb = new StringBuilder();
        sb.Append("<!doctype html><html lang=\"pt-BR\"><head><meta charset=\"utf-8\"><title>Divergências territoriais</title><style>")
          .Append("body{font-family:Segoe UI,Arial,sans-serif;color:#1b1f24;margin:32px;font-size:13px}")
          .Append("h1{font-size:20px;margin:0 0 4px}p.sub{color:#5b6470;margin:0 0 16px}")
          .Append("table{border-collapse:collapse;width:100%}th,td{text-align:left;vertical-align:top;padding:6px 8px;border-bottom:1px solid #dde1e6}")
          .Append("th{font-size:12px;color:#5b6470;text-transform:uppercase}td.n{white-space:nowrap}td.p{color:#5b6470;font-size:12px}")
          .Append("@media print{body{margin:12mm}}</style></head><body>")
          .Append("<h1>Divergências territoriais · ").Append(E(mapa)).Append("</h1><p class=\"sub\">").Append(E(Resumo(d)))
          .Append(filtro.Length > 0 ? " · " + E(filtro) : string.Empty)
          .Append(" · gerado em ").Append(E(geradoEm.ToString("dd/MM/yyyy HH:mm", TextoTela.Brasil))).Append("</p>")
          .Append("<table><thead><tr><th>Cliente</th><th>Efeito</th><th>Território atual</th><th>Território certo</th><th>Motivo</th><th>Por quê</th></tr></thead><tbody>");
        foreach (var l in itens)
            sb.Append("<tr><td>").Append(E(l.Titulo)).Append("</td><td class=\"n\">").Append(E(l.Item.EfeitoNome))
              .Append("</td><td>").Append(E(Territorio(l.Item.TerritorioAtual))).Append("</td><td>").Append(E(Territorio(l.Item.TerritorioProposto)))
              .Append("</td><td>").Append(E(l.Item.Motivo)).Append("</td><td class=\"p\">").Append(E(l.PorQue)).Append("</td></tr>");
        if (itens.Count == 0) sb.Append("<tr><td colspan=\"6\"><em>Nenhuma divergência.</em></td></tr>");
        sb.Append("</tbody></table><script>window.onload=function(){window.print();};</script></body></html>");
        return sb.ToString();
    }
}
