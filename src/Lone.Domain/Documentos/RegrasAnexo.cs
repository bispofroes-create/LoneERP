namespace Lone.Domain.Documentos;

/// <summary>
/// Regras dos anexos de documentos: só formatos de documento/imagem conhecidos, conferidos pelo conteúdo (a
/// "assinatura" dos primeiros bytes), não pela extensão — um executável renomeado para .pdf é recusado.
/// </summary>
public static class RegrasAnexo
{
    /// <summary>Limite padrão por arquivo (a API pode configurar outro em "Anexos:TamanhoMaximoMb").</summary>
    public const int TamanhoMaximoMbPadrao = 10;

    private static readonly (string Tipo, string[] Extensoes, byte[] Assinatura)[] Formatos =
    [
        ("application/pdf", [".pdf"], "%PDF"u8.ToArray()),
        ("image/png", [".png"], [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
        ("image/jpeg", [".jpg", ".jpeg"], [0xFF, 0xD8, 0xFF])
    ];

    public static string ExtensoesAceitas => string.Join(", ", Formatos.SelectMany(f => f.Extensoes));

    /// <summary>Tipo do conteúdo (MIME) pelo conteúdo; nulo = formato não aceito.</summary>
    public static string? IdentificarTipo(ReadOnlySpan<byte> inicio)
    {
        foreach (var (tipo, _, assinatura) in Formatos)
            if (inicio.StartsWith(assinatura)) return tipo;
        return null;
    }

    /// <summary>Extensão do nome coerente com o conteúdo (evita "foto.pdf" que na verdade é imagem).</summary>
    public static bool ExtensaoConfere(string nomeArquivo, string tipo)
    {
        var extensao = Path.GetExtension(nomeArquivo).ToLowerInvariant();
        return Formatos.Any(f => f.Tipo == tipo && f.Extensoes.Contains(extensao));
    }

    /// <summary>Só o nome (sem pastas), sem caracteres de controle, com no máximo o tamanho da coluna.</summary>
    public static string NomeSeguro(string? nomeArquivo)
    {
        var nome = Path.GetFileName((nomeArquivo ?? string.Empty).Replace('\\', '/'));
        nome = new string(nome.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (nome.Length == 0) nome = "arquivo";
        if (nome.Length <= Entidades.AnexoDocumento.TamanhoMaximoNome) return nome;
        var extensao = Path.GetExtension(nome);
        return nome[..(Entidades.AnexoDocumento.TamanhoMaximoNome - extensao.Length)] + extensao;
    }

    public static List<string> Validar(string nomeArquivo, byte[] conteudo, int tamanhoMaximoMb, out string? tipo)
    {
        var erros = new List<string>();
        tipo = null;
        if (conteudo.Length == 0)
        {
            erros.Add("O arquivo está vazio.");
            return erros;
        }
        if (conteudo.LongLength > tamanhoMaximoMb * 1024L * 1024L)
            erros.Add($"O arquivo passa do limite de {tamanhoMaximoMb} MB.");

        tipo = IdentificarTipo(conteudo);
        if (tipo is null)
            erros.Add($"Formato não aceito. Envie {ExtensoesAceitas}.");
        else if (!ExtensaoConfere(nomeArquivo, tipo))
            erros.Add("A extensão do nome não confere com o conteúdo do arquivo.");
        return erros;
    }
}
