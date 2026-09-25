using Lone.Domain.Entidades;

namespace Lone.Domain.Profissoes;

/// <summary>Normalização e regras das profissões (cadastro e escolha na pessoa). Não acessa banco.</summary>
public static class RegrasProfissao
{
    /// <summary>Espaços nas pontas e repetidos saem ("  Advogado   tributarista " → "Advogado tributarista").</summary>
    public static string? Texto(string? texto) =>
        string.IsNullOrWhiteSpace(texto)
            ? null
            : string.Join(' ', texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static void Normalizar(Profissao profissao)
    {
        profissao.Nome = Texto(profissao.Nome) ?? string.Empty;
        profissao.Descricao = Texto(profissao.Descricao);
    }

    /// <param name="ocupacao">A ocupação CBO escolhida, se houver (nula se o código não existir na tabela).</param>
    public static List<string> Validar(Profissao profissao, OcupacaoCbo? ocupacao)
    {
        var erros = new List<string>();
        if (profissao.Nome.Length == 0)
            erros.Add("Informe o nome da profissão.");
        else if (profissao.Nome.Length > Profissao.TamanhoMaximoNome)
            erros.Add($"O nome da profissão pode ter no máximo {Profissao.TamanhoMaximoNome} caracteres.");
        if (profissao.Descricao is { Length: > Profissao.TamanhoMaximoDescricao })
            erros.Add($"A descrição pode ter no máximo {Profissao.TamanhoMaximoDescricao} caracteres.");
        if (profissao.OcupacaoCboId is { } codigo && ocupacao is null)
            erros.Add($"A ocupação CBO {OcupacaoCbo.Formatar(codigo)} não existe na tabela importada.");
        return erros;
    }

    /// <summary>
    /// Confere a profissão escolhida na pessoa: precisa existir; uma desativada só vale se já era a da pessoa
    /// (não é oferecida para escolhas novas, mas quem tem continua tendo).
    /// </summary>
    public static string? ValidarEscolhida(Guid? escolhida, Guid? anterior, Profissao? profissao)
    {
        if (escolhida is null) return null;
        if (profissao is null) return "A profissão escolhida não existe mais. Reabra o cadastro e escolha de novo.";
        if (!profissao.Ativo && escolhida != anterior)
            return $"A profissão \"{profissao.Nome}\" está desativada e não pode ser escolhida em novos cadastros.";
        return null;
    }
}
