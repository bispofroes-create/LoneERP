using Lone.Domain.Entidades;

namespace Lone.Domain.Etiquetas;

/// <summary>Normalização e regras das etiquetas (cadastro e uso na pessoa). Não acessa banco.</summary>
public static class RegrasEtiqueta
{
    /// <summary>Limite de etiquetas marcadas numa pessoa.</summary>
    public const int MaximoPorPessoa = 20;

    /// <summary>Espaços nas pontas e repetidos saem ("  Cliente   VIP " → "Cliente VIP").</summary>
    public static string? Texto(string? texto) =>
        string.IsNullOrWhiteSpace(texto)
            ? null
            : string.Join(' ', texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static void Normalizar(Etiqueta etiqueta)
    {
        etiqueta.Nome = Texto(etiqueta.Nome) ?? string.Empty;
        etiqueta.Descricao = Texto(etiqueta.Descricao);
    }

    public static List<string> Validar(Etiqueta etiqueta)
    {
        var erros = new List<string>();
        if (etiqueta.Nome.Length == 0)
            erros.Add("Informe o nome da etiqueta.");
        else if (etiqueta.Nome.Length > Etiqueta.TamanhoMaximoNome)
            erros.Add($"O nome da etiqueta pode ter no máximo {Etiqueta.TamanhoMaximoNome} caracteres.");
        if (etiqueta.Descricao is { Length: > Etiqueta.TamanhoMaximoDescricao })
            erros.Add($"A descrição pode ter no máximo {Etiqueta.TamanhoMaximoDescricao} caracteres.");
        return erros;
    }

    /// <summary>
    /// Confere as etiquetas marcadas na pessoa contra o cadastro: precisam existir; uma desativada só vale se a
    /// pessoa já a tinha (não é oferecida para marcações novas, mas quem tem continua tendo).
    /// </summary>
    /// <param name="marcadas">Etiquetas enviadas para a pessoa (já normalizadas).</param>
    /// <param name="anteriores">Ids que a pessoa tinha gravados (vazio para pessoa nova).</param>
    /// <param name="cadastro">Etiquetas do cadastro com os Ids marcados (as que existirem).</param>
    public static List<string> ValidarMarcadas(
        IEnumerable<PessoaEtiqueta> marcadas, IReadOnlySet<Guid> anteriores, IReadOnlyDictionary<Guid, Etiqueta> cadastro)
    {
        var erros = new List<string>();
        foreach (var marcada in marcadas)
        {
            if (!cadastro.TryGetValue(marcada.EtiquetaId, out var etiqueta))
                erros.Add("Uma das etiquetas marcadas não existe mais. Reabra o cadastro e marque de novo.");
            else if (!etiqueta.Ativo && !anteriores.Contains(etiqueta.Id))
                erros.Add($"A etiqueta \"{etiqueta.Nome}\" está desativada e não pode ser marcada em novos cadastros.");
        }
        return erros.Distinct().ToList();
    }
}
