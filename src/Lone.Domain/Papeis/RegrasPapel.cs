using System.Text;
using Lone.Domain.Entidades;

namespace Lone.Domain.Papeis;

/// <summary>Normalização e regras dos papéis (cadastro e uso na pessoa). Não acessa banco.</summary>
public static class RegrasPapel
{
    public static string? Texto(string? texto) =>
        string.IsNullOrWhiteSpace(texto)
            ? null
            : string.Join(' ', texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Código em maiúsculas, sem acento, só letras, números e "_" ("Cliente VIP" → "CLIENTE_VIP").</summary>
    public static string Codigo(string? texto)
    {
        var decomposto = (texto ?? string.Empty).Trim().Normalize(NormalizationForm.FormD);
        var resultado = new StringBuilder();
        foreach (var c in decomposto)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            if (char.IsAsciiLetterOrDigit(c)) resultado.Append(char.ToUpperInvariant(c));
            else if (resultado.Length > 0 && resultado[^1] != '_') resultado.Append('_');
        }
        return resultado.ToString().Trim('_');
    }

    public static void Normalizar(Papel papel)
    {
        papel.Nome = Texto(papel.Nome) ?? string.Empty;
        papel.Descricao = Texto(papel.Descricao);
        papel.Codigo = Codigo(papel.Codigo.Length > 0 ? papel.Codigo : papel.Nome);
    }

    public static List<string> Validar(Papel papel)
    {
        var erros = new List<string>();
        if (papel.Nome.Length == 0)
            erros.Add("Informe o nome do papel.");
        else if (papel.Nome.Length > Papel.TamanhoMaximoNome)
            erros.Add($"O nome do papel pode ter no máximo {Papel.TamanhoMaximoNome} caracteres.");
        if (papel.Codigo.Length == 0)
            erros.Add("Informe o código do papel (letras e números).");
        else if (papel.Codigo.Length > Papel.TamanhoMaximoCodigo)
            erros.Add($"O código do papel pode ter no máximo {Papel.TamanhoMaximoCodigo} caracteres.");
        if (papel.Descricao is { Length: > Papel.TamanhoMaximoDescricao })
            erros.Add($"A descrição pode ter no máximo {Papel.TamanhoMaximoDescricao} caracteres.");
        return erros;
    }

    /// <summary>Período sem o Id do cadastro mas com o papel de sistema (chamada antiga): usa o Id fixo do papel de sistema.</summary>
    public static void CompletarIds(Pessoa pessoa)
    {
        foreach (var p in pessoa.Papeis.Where(p => p.PapelId == Guid.Empty && p.Papel is not null))
            p.PapelId = PapeisSistema.Id(p.Papel!.Value);
    }

    /// <summary>
    /// Liga cada papel da pessoa ao cadastro de papéis: copia o papel de sistema (usado pelas regras e consultas)
    /// e confere que o papel existe e que um desativado não é começado de novo (quem já tinha continua tendo).
    /// </summary>
    /// <param name="anterioresAtivos">Ids de papel que a pessoa tinha ativos (vazio para pessoa nova).</param>
    public static List<string> Aplicar(Pessoa pessoa, IReadOnlySet<Guid> anterioresAtivos, IReadOnlyDictionary<Guid, Papel> cadastro)
    {
        var erros = new List<string>();
        foreach (var papelDaPessoa in pessoa.Papeis)
        {
            if (!cadastro.TryGetValue(papelDaPessoa.PapelId, out var papel))
            {
                erros.Add("Um dos papéis marcados não existe mais. Reabra o cadastro e marque de novo.");
                continue;
            }
            papelDaPessoa.Papel = papel.PapelSistema;
            if (papelDaPessoa.Ativo && !papel.Ativo && !anterioresAtivos.Contains(papel.Id))
                erros.Add($"O papel \"{papel.Nome}\" está desativado e não pode ser marcado em novos cadastros.");
        }
        return erros.Distinct().ToList();
    }

    /// <summary>Frases do histórico para os papéis que começaram ou terminaram nesta gravação.</summary>
    public static IEnumerable<string> Mudancas(IReadOnlySet<Guid> anterioresAtivos, IEnumerable<PessoaPapel> atuais, IReadOnlyDictionary<Guid, Papel> cadastro)
    {
        var ativosAgora = atuais.Where(p => p.Ativo).Select(p => p.PapelId).ToHashSet();
        string Nome(Guid id) => cadastro.TryGetValue(id, out var p) ? p.Nome : "(papel)";
        foreach (var id in ativosAgora.Except(anterioresAtivos))
            yield return $"Papel '{Nome(id)}' incluído.";
        foreach (var id in anterioresAtivos.Except(ativosAgora))
            yield return $"Papel '{Nome(id)}' encerrado.";
    }
}
