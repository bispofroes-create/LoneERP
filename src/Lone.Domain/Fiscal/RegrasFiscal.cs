using Lone.Domain.Comum;
using Lone.Domain.Entidades;

namespace Lone.Domain.Fiscal;

/// <summary>Histórico fiscal com vigência e CNAEs em tabela, mantidos a partir dos dados do estabelecimento. Não acessa banco.</summary>
public static class RegrasFiscal
{
    /// <summary>CNAEs (principal primeiro) a partir dos campos de texto do estabelecimento.</summary>
    public static List<(int Codigo, bool Principal)> Codigos(Estabelecimento e)
    {
        var lista = new List<(int, bool)>();
        if (Cnae.Codigo(e.CnaePrincipal) is { } principal) lista.Add((principal, true));
        foreach (var parte in (e.CnaesSecundarios ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (Cnae.Codigo(parte) is { } codigo && lista.All(x => x.Item1 != codigo))
                lista.Add((codigo, false));
        return lista;
    }

    /// <summary>Refaz a tabela de CNAEs da pessoa (reaproveitando os Ids gravados de cada estabelecimento + código).</summary>
    public static void SincronizarCnaes(Pessoa dados, IEnumerable<EstabelecimentoCnae> gravados)
    {
        var porChave = gravados.GroupBy(g => (g.EstabelecimentoId, g.Codigo)).ToDictionary(g => g.Key, g => g.First().Id);
        dados.Cnaes = dados.Estabelecimentos.SelectMany(e => Codigos(e).Select(c => new EstabelecimentoCnae
        {
            Id = porChave.GetValueOrDefault((e.Id, c.Codigo), IdSequencial.Novo()),
            PessoaId = dados.Id,
            EstabelecimentoId = e.Id,
            Codigo = c.Codigo,
            Principal = c.Principal
        })).ToList();
    }

    private static HistoricoFiscal Situacao(Pessoa p, Estabelecimento e, DateOnly inicio) => new()
    {
        Id = IdSequencial.Novo(),
        PessoaId = p.Id,
        EstabelecimentoId = e.Id,
        InicioEm = inicio,
        RegimeTributario = e.RegimeTributario,
        IndicadorIE = e.IndicadorIE,
        InscricaoEstadual = e.InscricaoEstadual,
        SituacaoReceita = e.SituacaoReceita,
        ProdutorRural = e.ProdutorRural
    };

    /// <summary>
    /// Mantém o histórico: parte do gravado; para cada estabelecimento, se a situação mudou, encerra o período aberto na
    /// véspera e abre outro a partir de hoje (mudou duas vezes no mesmo dia: corrige o período de hoje).
    /// Estabelecimento sem histórico ganha o primeiro período. Nada é apagado.
    /// </summary>
    public static void AtualizarHistorico(Pessoa dados, IEnumerable<HistoricoFiscal> gravado, DateOnly hoje)
    {
        var historico = gravado.Select(h => h.Copia()).ToList();
        foreach (var e in dados.Estabelecimentos)
        {
            var atual = Situacao(dados, e, hoje);
            var aberto = historico.Where(h => h.EstabelecimentoId == e.Id && h.FimEm is null).MaxBy(h => h.InicioEm);
            if (aberto is null)
            {
                historico.Add(atual);
                continue;
            }
            if (aberto.MesmaSituacao(atual)) continue;

            if (aberto.InicioEm >= hoje)
            {
                aberto.RegimeTributario = atual.RegimeTributario;
                aberto.IndicadorIE = atual.IndicadorIE;
                aberto.InscricaoEstadual = atual.InscricaoEstadual;
                aberto.SituacaoReceita = atual.SituacaoReceita;
                aberto.ProdutorRural = atual.ProdutorRural;
            }
            else
            {
                aberto.FimEm = hoje.AddDays(-1);
                historico.Add(atual);
            }
        }
        dados.HistoricoFiscal = historico;
    }

    /// <summary>Situação fiscal vigente numa data (para notas e apurações de períodos passados).</summary>
    public static HistoricoFiscal? Vigente(IEnumerable<HistoricoFiscal> historico, Guid estabelecimentoId, DateOnly data) =>
        historico.Where(h => h.EstabelecimentoId == estabelecimentoId && h.InicioEm <= data && (h.FimEm is null || h.FimEm >= data))
            .MaxBy(h => h.InicioEm);
}
