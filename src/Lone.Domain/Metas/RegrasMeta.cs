using Lone.Domain.Entidades;
using Lone.Domain.Enums;

namespace Lone.Domain.Metas;

/// <summary>Os indicadores de sistema (fontes do cadastro), com Ids fixos. Outros (informados) o usuário cria.</summary>
public static class IndicadoresSistema
{
    public static IReadOnlyList<(Guid Id, string Codigo, string Nome, FonteIndicador Fonte)> Todos { get; } =
    [
        (new Guid("7a9e1c06-0000-0000-0000-000000000001"), "NOVOS_CLIENTES", "Novos clientes", FonteIndicador.NovosClientes),
        (new Guid("7a9e1c06-0000-0000-0000-000000000002"), "CLIENTES_ATIVOS", "Clientes ativos", FonteIndicador.ClientesAtivos),
        (new Guid("7a9e1c06-0000-0000-0000-000000000003"), "CLIENTES_REATIVADOS", "Clientes reativados", FonteIndicador.ClientesReativados),
        (new Guid("7a9e1c06-0000-0000-0000-000000000004"), "INTERACOES", "Interações com clientes", FonteIndicador.InteracoesRegistradas)
    ];
}

/// <summary>Resultado de um participante: nota ponderada, faixa e prêmio, e o atingimento de cada item.</summary>
public sealed record ResultadoParticipante(Guid ParticipanteId, decimal Nota, MetaFaixa? Faixa, IReadOnlyDictionary<Guid, decimal?> AtingimentoPorItem);

/// <summary>Regras das metas: validação, fluxo (rascunho → publicada → em apuração → fechada) e cálculo. Não acessa banco.</summary>
public static class RegrasMeta
{
    public const decimal Tolerancia = 0.01m;

    public static string? Texto(string? s) => string.IsNullOrWhiteSpace(s) ? null : string.Join(' ', s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Estrutura da meta. <paramref name="completa"/> = exigências para publicar (alvos de todos os participantes em todos os itens).</summary>
    public static List<string> Validar(Meta m, IReadOnlyDictionary<Guid, Indicador> indicadores, bool completa)
    {
        var erros = new List<string>();
        if (string.IsNullOrWhiteSpace(m.Nome)) erros.Add("Informe o nome da meta.");
        else if (m.Nome.Length > Meta.TamanhoMaximoNome) erros.Add($"O nome pode ter no máximo {Meta.TamanhoMaximoNome} caracteres.");
        if (m.InicioEm == default || m.FimEm == default) erros.Add("Informe o início e o fim do período.");
        else if (m.FimEm < m.InicioEm) erros.Add("O fim do período é anterior ao início.");
        if (m.LimiteAtingimento is < 100 or > 1000) erros.Add("O limite de atingimento por item deve ficar entre 100% e 1.000%.");

        if (m.Itens.Count == 0) erros.Add("Inclua ao menos um indicador.");
        foreach (var item in m.Itens)
        {
            if (!indicadores.TryGetValue(item.IndicadorId, out var indicador)) erros.Add("Um dos indicadores não existe mais.");
            if (item.Peso <= 0) erros.Add($"O peso de \"{indicador?.Nome}\" precisa ser maior que zero.");
        }
        if (m.Itens.GroupBy(i => i.IndicadorId).Any(g => g.Count() > 1)) erros.Add("Um indicador aparece duas vezes na meta.");
        if (m.Itens.Count > 0 && Math.Abs(m.Itens.Sum(i => i.Peso) - 100) > Tolerancia)
            erros.Add($"A soma dos pesos precisa ser 100% (está em {m.Itens.Sum(i => i.Peso):0.##}%).");

        if (m.Faixas.Count == 0) erros.Add("Inclua ao menos uma faixa de desempenho (ex.: a partir de 100%: Atingida).");
        if (m.Faixas.Any(f => string.IsNullOrWhiteSpace(f.Nome))) erros.Add("Dê um nome a cada faixa.");
        if (m.Faixas.Any(f => f.InicioPercentual < 0)) erros.Add("As faixas começam em 0% ou mais.");
        if (m.Faixas.Any(f => f.PercentualPremio is < 0 or > 1000)) erros.Add("O prêmio de cada faixa fica entre 0% e 1.000%.");
        if (m.Faixas.GroupBy(f => f.InicioPercentual).Any(g => g.Count() > 1)) erros.Add("Duas faixas começam no mesmo percentual.");

        if (m.Participantes.GroupBy(p => (p.Nivel, p.ReferenciaId)).Any(g => g.Count() > 1)) erros.Add("Um participante aparece duas vezes.");
        if (m.Alvos.Any(a => a.Alvo < 0)) erros.Add("Os alvos não podem ser negativos.");

        if (completa)
        {
            if (m.Participantes.Count == 0) erros.Add("Inclua ao menos um participante.");
            var faltando = m.Participantes.SelectMany(p => m.Itens.Select(i => (p.Id, i.Id)))
                .Count(par => !m.Alvos.Any(a => a.ParticipanteId == par.Item1 && a.ItemId == par.Item2));
            if (faltando > 0) erros.Add($"Faltam {faltando} alvo(s): cada participante precisa de um alvo em cada indicador.");
            foreach (var a in m.Alvos)
                if (a.Alvo == 0 && m.Itens.FirstOrDefault(i => i.Id == a.ItemId) is { } item &&
                    indicadores.TryGetValue(item.IndicadorId, out var ind) && ind.Sentido == SentidoIndicador.MaiorMelhor)
                {
                    erros.Add($"Alvo zero em \"{ind.Nome}\": use um alvo maior que zero (ou retire o participante).");
                    break;
                }
        }
        return erros.Distinct().ToList();
    }

    // ---------------------------------------------------------------- Fluxo

    public static bool PodeEditarEstrutura(Meta m) => m.Situacao == SituacaoMeta.Rascunho;

    public static bool PodeLancarRealizado(Meta m) => m.Situacao is SituacaoMeta.Publicada or SituacaoMeta.EmApuracao;

    public static string? ErroTransicao(SituacaoMeta de, SituacaoMeta para) => (de, para) switch
    {
        (SituacaoMeta.Rascunho, SituacaoMeta.Publicada) => null,
        (SituacaoMeta.Publicada, SituacaoMeta.EmApuracao) => null,
        (SituacaoMeta.Publicada, SituacaoMeta.Rascunho) => null, // publicada por engano: volta a rascunho (fica no histórico)
        (SituacaoMeta.EmApuracao, SituacaoMeta.Fechada) => null,
        (SituacaoMeta.EmApuracao, SituacaoMeta.Publicada) => null,
        (SituacaoMeta.Fechada, SituacaoMeta.EmApuracao) => null, // reabrir (permissão e motivo)
        _ => $"Não é possível passar de \"{Nome(de)}\" para \"{Nome(para)}\"."
    };

    public static string Nome(SituacaoMeta s) => s switch
    {
        SituacaoMeta.Rascunho => "Rascunho",
        SituacaoMeta.Publicada => "Publicada",
        SituacaoMeta.EmApuracao => "Em apuração",
        SituacaoMeta.Fechada => "Fechada",
        _ => s.ToString()
    };

    // ---------------------------------------------------------------- Cálculo

    /// <summary>Quanto do alvo foi atingido (%). Maior-melhor: realizado ÷ alvo. Menor-melhor: alvo ÷ realizado (realizado zero = 100% do teto).</summary>
    public static decimal? Atingimento(decimal alvo, decimal? realizado, SentidoIndicador sentido, decimal limite)
    {
        if (realizado is not { } r) return null;
        decimal valor;
        if (sentido == SentidoIndicador.MaiorMelhor)
            valor = alvo == 0 ? (r > 0 ? limite : 100) : r / alvo * 100;
        else
            valor = r == 0 ? limite : alvo / r * 100;
        return Math.Round(Math.Min(Math.Max(valor, 0), limite), 2);
    }

    /// <summary>Faixa alcançada: a de maior início que a nota atinge (nenhuma = abaixo da primeira faixa).</summary>
    public static MetaFaixa? Faixa(IEnumerable<MetaFaixa> faixas, decimal nota) =>
        faixas.Where(f => nota + Tolerancia >= f.InicioPercentual).MaxBy(f => f.InicioPercentual);

    /// <summary>
    /// Nota de cada participante: soma de peso × atingimento (com o teto por item). Item sem realizado conta zero
    /// (a apuração avisa quais faltam). <paramref name="realizados"/>: (participante, item) → realizado.
    /// </summary>
    public static List<ResultadoParticipante> Apurar(Meta m, IReadOnlyDictionary<Guid, Indicador> indicadores,
                                                     IReadOnlyDictionary<(Guid Participante, Guid Item), decimal?> realizados)
    {
        var resultado = new List<ResultadoParticipante>();
        foreach (var p in m.Participantes)
        {
            var porItem = new Dictionary<Guid, decimal?>();
            decimal nota = 0;
            foreach (var item in m.Itens)
            {
                var alvo = m.Alvos.FirstOrDefault(a => a.ParticipanteId == p.Id && a.ItemId == item.Id);
                var sentido = indicadores.TryGetValue(item.IndicadorId, out var ind) ? ind.Sentido : SentidoIndicador.MaiorMelhor;
                var atingimento = alvo is null ? null
                    : Atingimento(alvo.Alvo, realizados.GetValueOrDefault((p.Id, item.Id)), sentido, m.LimiteAtingimento);
                porItem[item.Id] = atingimento;
                nota += (atingimento ?? 0) * item.Peso / 100;
            }
            nota = Math.Round(nota, 2);
            resultado.Add(new ResultadoParticipante(p.Id, nota, Faixa(m.Faixas, nota), porItem));
        }
        return resultado;
    }
}
