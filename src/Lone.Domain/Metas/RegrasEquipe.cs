using System.Globalization;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;

namespace Lone.Domain.Metas;

/// <summary>
/// Regras das equipes (metas e Motor Comercial, Fase 2a): nome único, membros com vigência, liderança com vigência (um líder
/// por vez; decisão F3) e hierarquia sem ciclo. Não acessa banco. O histórico não é reescrito: num membro que já entrou, a
/// pessoa e o papel não mudam (sai e entra de novo); só a saída muda.
/// </summary>
public static class RegrasEquipe
{
    /// <summary>Profundidade máxima da árvore de equipes (proteção contra dados circulares vindos de fora).</summary>
    public const int ProfundidadeMaxima = 20;

    private static string Data(DateOnly d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    /// <summary>
    /// Conferência completa. <paramref name="todas"/>: as equipes gravadas (com as desativadas; a própria pode vir junto).
    /// <paramref name="anterior"/>: como a equipe estava gravada (nula se nova).
    /// </summary>
    public static List<string> Validar(Equipe dados, IReadOnlyCollection<Equipe> todas, Equipe? anterior, DateOnly hoje)
    {
        var erros = new List<string>();
        if (dados.Nome.Length == 0) erros.Add("Informe o nome da equipe.");
        else if (dados.Nome.Length > Equipe.TamanhoMaximoNome) erros.Add($"O nome pode ter no máximo {Equipe.TamanhoMaximoNome} caracteres.");
        if (dados.Nome.Length > 0 && todas.Any(t => t.Id != dados.Id && TextoBusca.Normalizar(t.Nome) == TextoBusca.Normalizar(dados.Nome)))
            erros.Add($"Já existe a equipe \"{dados.Nome}\".");

        foreach (var m in dados.Membros)
        {
            if (m.PessoaId == Guid.Empty || m.InicioEm == default) erros.Add("Cada membro precisa da pessoa e da data de entrada.");
            if (m.FimEm is { } fim && fim < m.InicioEm) erros.Add("A saída de um membro é anterior à entrada.");
            if (!Enum.IsDefined(m.Papel)) erros.Add("Papel de membro inválido.");
        }
        foreach (var grupo in dados.Membros.GroupBy(m => m.PessoaId))
            if (Sobrepostos(grupo) is { } data)
                erros.Add($"A mesma pessoa aparece duas vezes na equipe no mesmo período ({Data(data)}): encerre a participação anterior antes.");
        if (Sobrepostos(dados.Membros.Where(m => m.Papel == PapelNaEquipe.Lider)) is { } dataLider)
            erros.Add($"A equipe teria dois líderes ao mesmo tempo ({Data(dataLider)}): encerre a liderança anterior antes (saída na véspera).");

        erros.AddRange(ValidarHistorico(dados, anterior, hoje));
        erros.AddRange(ValidarHierarquia(dados, todas, anterior));
        return erros.Distinct().ToList();
    }

    /// <summary>A primeira data em que dois dos períodos se cruzam (nula se nenhum se cruza).</summary>
    private static DateOnly? Sobrepostos(IEnumerable<MembroEquipe> membros)
    {
        var lista = membros.Where(m => m.InicioEm != default).OrderBy(m => m.InicioEm).ToList();
        for (var i = 1; i < lista.Count; i++)
            if ((lista[i - 1].FimEm ?? DateOnly.MaxValue) >= lista[i].InicioEm) return lista[i].InicioEm;
        return null;
    }

    /// <summary>Membro gravado que já entrou: pessoa, entrada e papel não mudam (sai e entra de novo). Planejado muda à vontade.</summary>
    private static IEnumerable<string> ValidarHistorico(Equipe dados, Equipe? anterior, DateOnly hoje)
    {
        if (anterior is null) yield break;
        var antes = anterior.Membros.ToDictionary(m => m.Id);
        foreach (var m in dados.Membros)
        {
            if (!antes.TryGetValue(m.Id, out var g) || g.InicioEm > hoje) continue;
            if (g.PessoaId != m.PessoaId || g.InicioEm != m.InicioEm || g.Papel != m.Papel)
                yield return $"Um membro que entrou em {Data(g.InicioEm)} não muda de pessoa, entrada nem papel (o histórico não é reescrito): " +
                             "informe a saída e inclua de novo a partir da data desejada.";
        }
    }

    /// <summary>A equipe acima existe, não é a própria nem uma de baixo (sem ciclo); desativada só se já era a gravada.</summary>
    private static IEnumerable<string> ValidarHierarquia(Equipe dados, IReadOnlyCollection<Equipe> todas, Equipe? anterior)
    {
        if (dados.EquipePaiId is not { } pai) yield break;
        if (pai == dados.Id)
        {
            yield return "A equipe não pode estar acima dela mesma.";
            yield break;
        }
        var porId = todas.Where(t => t.Id != dados.Id).ToDictionary(t => t.Id);
        if (!porId.TryGetValue(pai, out var equipePai))
        {
            yield return "A equipe acima escolhida não existe mais.";
            yield break;
        }
        if (!equipePai.Ativo && anterior?.EquipePaiId != pai)
            yield return $"A equipe \"{equipePai.Nome}\" está desativada e não pode ser escolhida como equipe acima.";
        if (Descendentes(todas, dados.Id).Contains(pai))
            yield return $"\"{equipePai.Nome}\" está abaixo desta equipe: escolher como equipe acima formaria um ciclo.";
    }

    /// <summary>As equipes abaixo de <paramref name="equipeId"/> (filhas, netas...), sem a própria. Resiste a ciclo gravado.</summary>
    public static HashSet<Guid> Descendentes(IEnumerable<Equipe> todas, Guid equipeId)
    {
        var filhas = todas.Where(e => e.EquipePaiId is not null).ToLookup(e => e.EquipePaiId!.Value, e => e.Id);
        var resultado = new HashSet<Guid>();
        var fila = new Queue<(Guid Id, int Nivel)>();
        fila.Enqueue((equipeId, 0));
        while (fila.Count > 0)
        {
            var (id, nivel) = fila.Dequeue();
            if (nivel >= ProfundidadeMaxima) continue;
            foreach (var filha in filhas[id])
                if (filha != equipeId && resultado.Add(filha)) fila.Enqueue((filha, nivel + 1));
        }
        return resultado;
    }

    /// <summary>Quem liderava a equipe na data (o membro com papel Líder vigente), ou nulo.</summary>
    public static Guid? LiderEm(Equipe equipe, DateOnly data) =>
        equipe.Membros.Where(m => m.Papel == PapelNaEquipe.Lider && m.InicioEm <= data && (m.FimEm is null || m.FimEm >= data))
            .OrderByDescending(m => m.InicioEm).Select(m => (Guid?)m.PessoaId).FirstOrDefault();

    /// <summary>As equipes que a pessoa lidera na data (base do alcance "Minha equipe").</summary>
    public static IEnumerable<Guid> LideradasPor(IEnumerable<Equipe> todas, Guid pessoaId, DateOnly data) =>
        todas.Where(e => e.Ativo && LiderEm(e, data) == pessoaId).Select(e => e.Id);
}
