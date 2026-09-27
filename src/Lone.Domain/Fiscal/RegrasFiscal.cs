using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;

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

            // Campo vazio ("não informado", sem IE, sem situação) é falta de dado, não situação fiscal: quando a mudança
            // só preenche o que estava vazio (nada foi alterado), corrige o período aberto em vez de abrir outro.
            if (SoPreencheuVazios(aberto, atual))
            {
                aberto.RegimeTributario = atual.RegimeTributario;
                aberto.IndicadorIE = atual.IndicadorIE;
                aberto.InscricaoEstadual = atual.InscricaoEstadual;
                aberto.SituacaoReceita = atual.SituacaoReceita;
                continue;
            }

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

    /// <summary>
    /// Cada dado ficou igual ou saiu do vazio (regime e indicador "não informado", IE e situação sem valor).
    /// Produtor rural é sim/não (não tem vazio): mudar é sempre situação nova.
    /// </summary>
    private static bool SoPreencheuVazios(HistoricoFiscal aberto, HistoricoFiscal atual) =>
        aberto.ProdutorRural == atual.ProdutorRural &&
        (aberto.RegimeTributario == atual.RegimeTributario || aberto.RegimeTributario == RegimeTributario.NaoInformado) &&
        (aberto.IndicadorIE == atual.IndicadorIE || aberto.IndicadorIE == IndicadorIE.NaoInformado) &&
        (aberto.InscricaoEstadual == atual.InscricaoEstadual || aberto.InscricaoEstadual is null) &&
        (aberto.SituacaoReceita == atual.SituacaoReceita || aberto.SituacaoReceita is null);

    /// <summary>
    /// Regime tributário, CNAE e SUFRAMA são da empresa: pessoa física e estrangeiro não os têm. Cadastro antigo que já
    /// tinha algum deles pode manter o valor gravado ou apagá-lo (nada se perde sem ação do usuário); incluir ou alterar, não.
    /// </summary>
    public static List<string> ValidarCamposDeEmpresa(Pessoa dados, Pessoa? anterior)
    {
        var erros = new List<string>();
        if (dados.Natureza == NaturezaPessoa.Juridica) return erros;

        var quem = dados.Natureza == NaturezaPessoa.Fisica ? "Pessoa física" : "Estrangeiro";
        var gravados = (anterior?.Estabelecimentos ?? []).ToDictionary(e => e.Id);
        foreach (var e in dados.Estabelecimentos)
        {
            var gravado = gravados.GetValueOrDefault(e.Id);
            if (e.RegimeTributario != RegimeTributario.NaoInformado && e.RegimeTributario != gravado?.RegimeTributario)
                erros.Add($"{quem} não tem regime tributário (é dado de empresa).");
            if (e.CnaePrincipal is not null && e.CnaePrincipal != gravado?.CnaePrincipal)
                erros.Add($"{quem} não tem CNAE (é dado de empresa).");
            if (e.InscricaoSuframa is not null && e.InscricaoSuframa != gravado?.InscricaoSuframa)
                erros.Add($"{quem} não tem inscrição SUFRAMA (é dado de empresa).");
        }
        return erros;
    }

    /// <summary>Situação fiscal vigente numa data (para notas e apurações de períodos passados).</summary>
    public static HistoricoFiscal? Vigente(IEnumerable<HistoricoFiscal> historico, Guid estabelecimentoId, DateOnly data) =>
        historico.Where(h => h.EstabelecimentoId == estabelecimentoId && h.InicioEm <= data && (h.FimEm is null || h.FimEm >= data))
            .MaxBy(h => h.InicioEm);
}
