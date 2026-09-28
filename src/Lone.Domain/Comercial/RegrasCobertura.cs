using System.Globalization;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;

namespace Lone.Domain.Comercial;

/// <summary>
/// Regras das ausências e coberturas da carteira (Motor Comercial, Fase 1c). Não acessa banco.
/// A cobertura não muda a carteira: vale pelas datas e acaba sozinha no fim. O histórico não é reescrito: depois que a
/// cobertura começou, só o fim (encerrar antes ou prorrogar), a observação e o acesso mudam; cancelar só antes de começar.
/// </summary>
public static class RegrasCobertura
{
    private static string Data(DateOnly d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static string? Texto(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    public static void Normalizar(CoberturaComercial c)
    {
        c.Observacao = Texto(c.Observacao);
        c.MotivoCancelamento = Texto(c.MotivoCancelamento);
        if (c.SubstitutoId == Guid.Empty) c.SubstitutoId = null;
        if (c.EquipeSubstitutaId == Guid.Empty) c.EquipeSubstitutaId = null;
        if (c.TipoCarteiraId == Guid.Empty) c.TipoCarteiraId = null;
        if (c.EmpresaId == Guid.Empty) c.EmpresaId = null;
        if (c.RegraCredito != RegraCreditoAusencia.Dividido) c.PercentualSubstituto = null;
    }

    /// <summary>As duas coberturas valem ao mesmo tempo para os mesmos clientes (escopos que se cruzam).</summary>
    public static bool Sobrepoem(CoberturaComercial a, CoberturaComercial b) =>
        a.Id != b.Id && !a.Cancelada && !b.Cancelada && a.TitularId == b.TitularId &&
        a.InicioEm <= b.FimEm && b.InicioEm <= a.FimEm &&
        (a.TipoCarteiraId is null || b.TipoCarteiraId is null || a.TipoCarteiraId == b.TipoCarteiraId) &&
        (a.EmpresaId is null || b.EmpresaId is null || a.EmpresaId == b.EmpresaId);

    /// <summary>
    /// Campos, datas, quem cobre, crédito, sobreposição com outra cobertura do mesmo titular e o que não muda depois que
    /// começou. <paramref name="outras"/>: as coberturas gravadas do titular (a própria pode vir junto: é ignorada).
    /// </summary>
    public static List<string> Validar(CoberturaComercial c, CoberturaComercial? anterior, IEnumerable<CoberturaComercial> outras, DateOnly hoje)
    {
        var erros = new List<string>();
        if (c.TitularId == Guid.Empty) erros.Add("Escolha quem vai se ausentar.");
        if (c.TipoAusenciaId == Guid.Empty) erros.Add("Escolha o tipo de ausência.");
        if (c.InicioEm == default) erros.Add("Informe o início.");
        if (c.FimEm == default) erros.Add("Informe o fim (ausência sem fim é transferência de carteira).");
        else if (c.InicioEm != default && c.FimEm < c.InicioEm) erros.Add("O fim é anterior ao início.");

        if (c.SubstitutoId is null && c.EquipeSubstitutaId is null) erros.Add("Escolha quem cobre: uma pessoa ou uma equipe.");
        if (c.SubstitutoId is not null && c.EquipeSubstitutaId is not null) erros.Add("Escolha uma pessoa ou uma equipe para cobrir, não as duas.");
        if (c.SubstitutoId is { } s && s == c.TitularId) erros.Add("Quem cobre não pode ser o próprio titular.");

        if (c.RegraCredito == RegraCreditoAusencia.Dividido)
        {
            if (c.PercentualSubstituto is not { } pct) erros.Add("Crédito dividido: informe o percentual de quem cobre.");
            else if (pct is <= 0 or >= 100) erros.Add("Crédito dividido: o percentual de quem cobre fica entre 0% e 100% (sem os extremos).");
            else if (decimal.Round(pct, 2) != pct) erros.Add("Crédito dividido: no máximo 2 casas decimais.");
        }
        if (!Enum.IsDefined(c.RegraCredito)) erros.Add("Regra de crédito inválida.");
        if (c.Observacao is { Length: > CoberturaComercial.TamanhoMaximoTexto })
            erros.Add($"Observação de no máximo {CoberturaComercial.TamanhoMaximoTexto} caracteres.");
        if (c.MotivoCancelamento is { Length: > CoberturaComercial.TamanhoMaximoTexto })
            erros.Add($"Motivo de no máximo {CoberturaComercial.TamanhoMaximoTexto} caracteres.");

        if (anterior is not null) erros.AddRange(ValidarHistorico(c, anterior, hoje));

        if (!c.Cancelada && c.InicioEm != default && c.FimEm >= c.InicioEm &&
            outras.FirstOrDefault(o => Sobrepoem(c, o)) is { } outra)
            erros.Add($"Já existe cobertura para este titular de {Data(outra.InicioEm)} a {Data(outra.FimEm)} no mesmo escopo: " +
                      "ajuste as datas, o papel ou a empresa, ou encerre a outra antes.");
        return erros.Distinct().ToList();
    }

    /// <summary>
    /// Depois que começou (início até hoje), só fim, observação e acesso mudam; o fim não pode ficar antes de hoje menos
    /// um dia (encerrar hoje = fim ontem ou hoje). Cancelar só antes de começar. Cancelada não volta.
    /// </summary>
    private static IEnumerable<string> ValidarHistorico(CoberturaComercial c, CoberturaComercial g, DateOnly hoje)
    {
        if (g.Cancelada)
        {
            if (!c.Cancelada) yield return "Cobertura cancelada não volta: cadastre outra.";
            if (c.InicioEm != g.InicioEm || c.FimEm != g.FimEm || c.TitularId != g.TitularId || c.SubstitutoId != g.SubstitutoId ||
                c.EquipeSubstitutaId != g.EquipeSubstitutaId || c.TipoAusenciaId != g.TipoAusenciaId || c.TipoCarteiraId != g.TipoCarteiraId ||
                c.EmpresaId != g.EmpresaId || c.RegraCredito != g.RegraCredito || c.PercentualSubstituto != g.PercentualSubstituto ||
                c.PermiteAcesso != g.PermiteAcesso)
                yield return "Cobertura cancelada não muda (só a observação).";
            yield break;
        }

        var comecou = g.InicioEm <= hoje;
        if (c.Cancelada && comecou)
            yield return $"A cobertura já começou em {Data(g.InicioEm)} e não pode ser cancelada: encerre-a mudando o fim.";
        if (!comecou) yield break;

        var mudou = new List<string>();
        if (c.TitularId != g.TitularId) mudou.Add("titular");
        if (c.TipoAusenciaId != g.TipoAusenciaId) mudou.Add("tipo");
        if (c.InicioEm != g.InicioEm) mudou.Add("início");
        if (c.SubstitutoId != g.SubstitutoId || c.EquipeSubstitutaId != g.EquipeSubstitutaId) mudou.Add("quem cobre");
        if (c.TipoCarteiraId != g.TipoCarteiraId) mudou.Add("papel");
        if (c.EmpresaId != g.EmpresaId) mudou.Add("empresa");
        if (c.RegraCredito != g.RegraCredito || c.PercentualSubstituto != g.PercentualSubstituto) mudou.Add("crédito");
        if (mudou.Count > 0)
            yield return $"A cobertura já começou em {Data(g.InicioEm)}: {string.Join(", ", mudou)} não muda(m). " +
                         "Mude o fim para encerrá-la e cadastre outra a partir da data desejada.";
        if (c.FimEm != g.FimEm && c.FimEm < hoje.AddDays(-1) && c.FimEm < g.FimEm)
            yield return $"Para encerrar uma cobertura que já começou, use um fim a partir de {Data(hoje.AddDays(-1))} (o passado não é reescrito).";
    }

    /// <summary>"Férias de 01/10/2026 a 15/10/2026 · atendimento por Maria (crédito do titular)".</summary>
    public static string Descrever(CoberturaComercial c, string tipo, string quemCobre)
    {
        var credito = c.RegraCredito switch
        {
            RegraCreditoAusencia.Substituto => "crédito de quem cobre",
            RegraCreditoAusencia.Dividido => $"crédito dividido: {c.PercentualSubstituto?.ToString("0.##", CultureInfo.GetCultureInfo("pt-BR"))}% para quem cobre",
            _ => "crédito do titular"
        };
        return $"{tipo} de {Data(c.InicioEm)} a {Data(c.FimEm)} · atendimento por {quemCobre} ({credito})";
    }
}

/// <summary>Os tipos de ausência iniciais (Ids fixos; o usuário pode criar outros e desativar estes).</summary>
public static class TiposAusenciaIniciais
{
    public static IReadOnlyList<(Guid Id, string Nome, int Ordem)> Todos { get; } =
    [
        (new Guid("7a9e1c05-0000-0000-0000-000000000001"), "Férias", 1),
        (new Guid("7a9e1c05-0000-0000-0000-000000000002"), "Folga", 2),
        (new Guid("7a9e1c05-0000-0000-0000-000000000003"), "Licença", 3),
        (new Guid("7a9e1c05-0000-0000-0000-000000000004"), "Afastamento", 4),
        (new Guid("7a9e1c05-0000-0000-0000-000000000005"), "Treinamento", 5)
    ];

    /// <summary>Nome único (sem diferenciar maiúsculas nem acentos), tamanho e ordem.</summary>
    public static List<string> Validar(TipoAusencia dados, IEnumerable<TipoAusencia> todos)
    {
        var erros = new List<string>();
        if (dados.Nome.Length == 0) erros.Add("Informe o nome do tipo.");
        else if (dados.Nome.Length > TipoAusencia.TamanhoMaximoNome) erros.Add($"O nome pode ter no máximo {TipoAusencia.TamanhoMaximoNome} caracteres.");
        if (dados.Nome.Length > 0 && todos.Any(t => t.Id != dados.Id && TextoBusca.Normalizar(t.Nome) == TextoBusca.Normalizar(dados.Nome)))
            erros.Add($"Já existe \"{dados.Nome}\" (ativo ou desativado; maiúsculas e acentos não contam).");
        return erros;
    }
}

/// <summary>Regras dos parâmetros comerciais.</summary>
public static class RegrasParametrosComerciais
{
    public static List<string> Validar(ParametrosComerciais p)
    {
        var erros = new List<string>();
        if (p.DiasAvisoFimVinculo is < 1 or > ParametrosComerciais.MaximoDiasAviso)
            erros.Add($"Aviso de fim do vínculo: de 1 a {ParametrosComerciais.MaximoDiasAviso} dias.");
        if (!Enum.IsDefined(p.CreditoNaAusencia)) erros.Add("Regra de crédito na ausência inválida.");
        if (p.CreditoNaAusencia == RegraCreditoAusencia.Dividido)
        {
            if (p.PercentualSubstitutoPadrao is not { } pct || pct is <= 0 or >= 100)
                erros.Add("Crédito dividido: informe o percentual de quem cobre (entre 0% e 100%, sem os extremos).");
            else if (decimal.Round(pct, 2) != pct)
                erros.Add("Crédito dividido: no máximo 2 casas decimais.");
        }
        else p.PercentualSubstitutoPadrao = null;
        return erros;
    }
}
