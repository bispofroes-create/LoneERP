// Regras portadas do Caelum Stella (Apache License 2.0, Copyright Caelum).
// https://github.com/caelum/caelum-stella — pacote br.com.caelum.stella.validation.ie.
// Veja THIRD-PARTY-NOTICES.md na raiz do repositório.

using Lone.Domain.Validacao.InscricoesEstaduais;

namespace Lone.Domain.Validacao;

/// <summary>
/// Normalização e validação da Inscrição Estadual (IE) de contribuintes do ICMS, por UF.
/// A validação confere o formato (quantidade de dígitos e prefixos) e os dígitos verificadores
/// de cada UF; não consulta o Sintegra/SEFAZ.
/// </summary>
public static class InscricaoEstadual
{
    private static readonly IReadOnlyDictionary<string, IRegraInscricaoEstadual> Regras =
        new Dictionary<string, IRegraInscricaoEstadual>(StringComparer.Ordinal)
        {
            ["AC"] = new RegraAcre(),
            ["AL"] = new RegraAlagoas(),
            ["AP"] = new RegraAmapa(),
            ["AM"] = new RegraAmazonas(),
            ["BA"] = new RegraBahia(),
            ["CE"] = new RegraCeara(),
            ["DF"] = new RegraDistritoFederal(),
            ["ES"] = new RegraEspiritoSanto(),
            ["GO"] = new RegraGoias(),
            ["MA"] = new RegraMaranhao(),
            ["MT"] = new RegraMatoGrosso(),
            ["MS"] = new RegraMatoGrossoDoSul(),
            ["MG"] = new RegraMinasGerais(),
            ["PA"] = new RegraPara(),
            ["PB"] = new RegraParaiba(),
            ["PR"] = new RegraParana(),
            ["PE"] = new RegraPernambuco(),
            ["PI"] = new RegraPiaui(),
            ["RJ"] = new RegraRioDeJaneiro(),
            ["RN"] = new RegraRioGrandeDoNorte(),
            ["RS"] = new RegraRioGrandeDoSul(),
            ["RO"] = new RegraRondonia(),
            ["RR"] = new RegraRoraima(),
            ["SC"] = new RegraSantaCatarina(),
            ["SP"] = new RegraSaoPaulo(),
            ["SE"] = new RegraSergipe(),
            ["TO"] = new RegraTocantins(),
        };

    /// <summary>
    /// Remove pontuação e espaços e passa para maiúsculas, mantendo letras e dígitos ASCII
    /// (ex.: "P-01100424.3/002" → "P011004243002"; "110.042.490.114" → "110042490114").
    /// Letras são mantidas para que valores com texto estranho não virem uma IE válida por acaso;
    /// só o 'P' inicial do produtor rural de SP passa na validação.
    /// </summary>
    public static string Normalizar(string? valor) =>
        string.IsNullOrWhiteSpace(valor)
            ? string.Empty
            : new string(valor.ToUpperInvariant().Where(char.IsAsciiLetterOrDigit).ToArray());

    /// <summary>Indica se o valor declara contribuinte isento ("ISENTO" ou "ISENTA", sem diferenciar maiúsculas).</summary>
    public static bool EhIsento(string? valor)
    {
        var texto = valor?.Trim();
        return string.Equals(texto, "ISENTO", StringComparison.OrdinalIgnoreCase)
            || string.Equals(texto, "ISENTA", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Indica se a inscrição é válida para a UF (formato e dígitos verificadores).
    /// Aceita o valor formatado ou não. UF nula, desconhecida ou <see cref="Ufs.Exterior"/> → falso.
    /// "ISENTO" não é considerado válido aqui; use <see cref="EhIsento"/>.
    /// </summary>
    public static bool Valida(string? uf, string? inscricao)
    {
        if (string.IsNullOrWhiteSpace(uf) || !Regras.TryGetValue(uf.Trim().ToUpperInvariant(), out var regra))
            return false;

        var digitos = Normalizar(inscricao);
        return digitos.Length > 0 && regra.Valida(digitos);
    }
}
