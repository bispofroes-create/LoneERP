namespace Lone.Domain.Enderecos.ConferenciaCep;

/// <summary>Um dado que duas fontes de CEP podem informar para o mesmo CEP (Checkpoint G, segunda opinião).</summary>
public enum ComponenteComparacaoFontes : byte
{
    /// <summary>O CEP existe na fonte (encontrado × não encontrado).</summary>
    Existencia = 1,
    Uf = 2,
    /// <summary>Pelo nome (as duas fontes informam o nome; o código IBGE é comparado à parte).</summary>
    Municipio = 3,
    CodigoIbge = 4,
    Logradouro = 5,
    /// <summary>Só informativo na comparação entre fontes: não vira critério da conferência do endereço.</summary>
    Bairro = 6,
    /// <summary>O texto da faixa como a fonte informa (sem interpretar: a regra de número é só do <see cref="MotorCep"/>).</summary>
    Faixa = 7
}

/// <summary>Como ficou um dado na comparação entre duas fontes.</summary>
public enum SituacaoComparacaoFontes : byte
{
    Concordam = 1,
    Divergem = 2,
    /// <summary>Uma das fontes (ou as duas) não informa o dado: ausência não é divergência.</summary>
    NaoComparavel = 3
}

/// <summary>Resultado da segunda opinião. Nenhum deles escolhe fonte, altera endereço ou muda a conferência.</summary>
public enum ResultadoSegundaOpiniaoCep : byte
{
    /// <summary>As duas fontes responderam e concordam nos dados principais que as duas informam.</summary>
    Concordam = 1,
    /// <summary>As duas responderam e algum dado comparável difere (ou só uma achou o CEP). O Lone não escolhe quem está certa.</summary>
    Divergem = 2,
    /// <summary>As duas responderam, mas não há dado principal informado pelas duas para comparar.</summary>
    Inconclusiva = 3,
    /// <summary>A segunda fonte não respondeu: não é conflito; a conferência com a primeira continua valendo.</summary>
    SegundaFonteIndisponivel = 4,
    /// <summary>Nenhuma fonte respondeu agora: não há o que comparar.</summary>
    FontePrincipalIndisponivel = 5,
    /// <summary>Só há uma fonte configurada que consulta por CEP.</summary>
    SemOutraFonte = 6
}

/// <summary>Um dado comparado: o que cada fonte informa (como veio, sem normalizar) e o motivo da situação.</summary>
public sealed record ComparacaoComponenteFontes(ComponenteComparacaoFontes Componente, SituacaoComparacaoFontes Situacao,
                                                string? ValorPrincipal, string? ValorSegunda, string Motivo);

/// <summary>
/// A segunda opinião sobre um CEP: as duas fontes, o resultado, cada dado comparado e uma frase para a tela. Transitória:
/// não é gravada no endereço e não muda a conferência (a fonte da conferência é só informada, nunca "vencedora").
/// </summary>
public sealed record ComparacaoFontesCep(string Cep, ResultadoSegundaOpiniaoCep Resultado, CepFonte? FontePrincipal, CepFonte? SegundaFonte,
                                         IReadOnlyList<ComparacaoComponenteFontes> Componentes, string Mensagem);

/// <summary>
/// Compara as respostas de duas fontes à <b>mesma pergunta</b> (consulta pelo CEP). Regras (Checkpoint G):
/// <list type="bullet">
/// <item>só dados que as duas fontes informam são comparados; dado ausente em uma delas é "não comparável", nunca divergência;</item>
/// <item>a comparação usa a normalização que já existe (<see cref="DuplicidadeEndereco"/> e <see cref="NormalizadorLogradouro"/>):
/// nada de aproximação, pontuação, percentual ou maioria;</item>
/// <item>fonte indisponível não é conflito;</item>
/// <item>divergência é mostrada como tal: nenhuma fonte é tratada como certa, nada é escolhido nem alterado.</item>
/// </list>
/// Puro: sem rede, sem banco. A conferência do endereço continua sendo do <see cref="MotorCep"/>.
/// </summary>
public static class ComparadorFontesCep
{
    /// <param name="cep">O CEP perguntado (8 dígitos).</param>
    /// <param name="principal">A resposta usada pela conferência (fonte principal, ou a reserva se a principal falhou).</param>
    /// <param name="segunda">A resposta da outra fonte; nula = não há outra fonte para perguntar.</param>
    public static ComparacaoFontesCep Comparar(string cep, RespostaConsultaCep principal, RespostaConsultaCep? segunda)
    {
        ArgumentNullException.ThrowIfNull(principal);
        var a = principal.Fonte;
        if (principal.Situacao == SituacaoRespostaFonte.Indisponivel)
            return new(cep, ResultadoSegundaOpiniaoCep.FontePrincipalIndisponivel, a, segunda?.Fonte, [],
                "Não foi possível consultar as fontes de CEP agora; não há o que comparar. Tente de novo mais tarde.");
        if (segunda is null)
            return new(cep, ResultadoSegundaOpiniaoCep.SemOutraFonte, a, null, [],
                "Não há outra fonte de CEP configurada para comparar.");
        var b = segunda.Fonte;
        if (segunda.Situacao == SituacaoRespostaFonte.Indisponivel)
            return new(cep, ResultadoSegundaOpiniaoCep.SegundaFonteIndisponivel, a, b, [],
                $"Não foi possível consultar {Nome(b)} agora. Isso não é conflito: a conferência com {Nome(a)} continua valendo.");

        var achouA = principal.Situacao == SituacaoRespostaFonte.Encontrado;
        var achouB = segunda.Situacao == SituacaoRespostaFonte.Encontrado;
        if (!achouA && !achouB)
            return new(cep, ResultadoSegundaOpiniaoCep.Concordam, a, b,
                [new(ComponenteComparacaoFontes.Existencia, SituacaoComparacaoFontes.Concordam, "não encontrado", "não encontrado",
                    "Nenhuma das duas fontes encontrou o CEP.")],
                $"✓ {Nome(a)} e {Nome(b)} concordam: o CEP não foi encontrado em nenhuma das duas.");
        if (achouA != achouB)
            return new(cep, ResultadoSegundaOpiniaoCep.Divergem, a, b,
                [new(ComponenteComparacaoFontes.Existencia, SituacaoComparacaoFontes.Divergem,
                    achouA ? "encontrado" : "não encontrado", achouB ? "encontrado" : "não encontrado",
                    $"{Nome(achouA ? a : b)} encontrou o CEP; {Nome(achouA ? b : a)} não.")],
                Divergencia(a, ["existência do CEP"]));

        var ra = principal.Registro!;
        var rb = segunda.Registro!;
        var componentes = new List<ComparacaoComponenteFontes>
        {
            new(ComponenteComparacaoFontes.Existencia, SituacaoComparacaoFontes.Concordam, "encontrado", "encontrado",
                "As duas fontes encontraram o CEP."),
            Texto(ComponenteComparacaoFontes.Uf, "UF", ra.Uf, rb.Uf, a, b, DuplicidadeEndereco.Texto),
            Texto(ComponenteComparacaoFontes.Municipio, "Município", ra.Cidade, rb.Cidade, a, b, DuplicidadeEndereco.Texto),
            Texto(ComponenteComparacaoFontes.CodigoIbge, "Código IBGE", ra.CodigoMunicipioIbge, rb.CodigoMunicipioIbge, a, b, DuplicidadeEndereco.Cep),
            Texto(ComponenteComparacaoFontes.Logradouro, "Logradouro", ra.Logradouro, rb.Logradouro, a, b, NormalizadorLogradouro.Normalizar),
            Texto(ComponenteComparacaoFontes.Bairro, "Bairro", ra.Bairro, rb.Bairro, a, b, DuplicidadeEndereco.Texto),
            Texto(ComponenteComparacaoFontes.Faixa, "Faixa de numeração", ra.Complemento, rb.Complemento, a, b, DuplicidadeEndereco.Texto)
        };

        var divergentes = componentes.Where(c => c.Situacao == SituacaoComparacaoFontes.Divergem).ToList();
        if (divergentes.Count > 0)
            return new(cep, ResultadoSegundaOpiniaoCep.Divergem, a, b, componentes,
                Divergencia(a, divergentes.Select(c => NomeComponente(c.Componente)).ToList()));

        // Dados principais: UF, município (nome ou IBGE) e logradouro. Bairro e faixa sozinhos não bastam para "concordam".
        var principais = componentes.Where(c => c.Componente is ComponenteComparacaoFontes.Uf or ComponenteComparacaoFontes.Municipio
            or ComponenteComparacaoFontes.CodigoIbge or ComponenteComparacaoFontes.Logradouro).ToList();
        if (!principais.Any(c => c.Situacao == SituacaoComparacaoFontes.Concordam))
            return new(cep, ResultadoSegundaOpiniaoCep.Inconclusiva, a, b, componentes,
                $"{Nome(a)} e {Nome(b)} encontraram o CEP, mas não informam dados principais em comum para comparar.");

        var ressalva = componentes.Any(c => c.Situacao == SituacaoComparacaoFontes.NaoComparavel)
            ? " Alguns dados não puderam ser comparados (uma das fontes não os informa)."
            : string.Empty;
        return new(cep, ResultadoSegundaOpiniaoCep.Concordam, a, b, componentes,
            $"✓ {Nome(a)} e {Nome(b)} concordam nos dados principais.{ressalva}");
    }

    private static string Divergencia(CepFonte? principal, IReadOnlyList<string> dados) =>
        $"⚠ As fontes consultadas apresentam informações diferentes ({string.Join(", ", dados)}). O Lone não escolhe qual está certa: " +
        $"a conferência continua sendo a feita com {Nome(principal)}. Confira o endereço com o cliente ou com os Correios.";

    /// <summary>Compara um texto pela normalização dada; vazio em qualquer lado = não comparável.</summary>
    private static ComparacaoComponenteFontes Texto(ComponenteComparacaoFontes componente, string rotulo, string? va, string? vb,
                                                    CepFonte? a, CepFonte? b, Func<string?, string> normalizar)
    {
        var na = normalizar(va);
        var nb = normalizar(vb);
        var ta = va?.Trim();
        var tb = vb?.Trim();
        if (na.Length == 0 || nb.Length == 0)
        {
            var quem = na.Length == 0 && nb.Length == 0 ? "Nenhuma das fontes informa" : $"{Nome(na.Length == 0 ? a : b)} não informa";
            return new(componente, SituacaoComparacaoFontes.NaoComparavel, ta, tb, $"{rotulo}: {quem} este dado; não comparado.");
        }
        return string.Equals(na, nb, StringComparison.Ordinal)
            ? new(componente, SituacaoComparacaoFontes.Concordam, ta, tb, $"{rotulo}: as duas fontes informam o mesmo ({ta}).")
            : new(componente, SituacaoComparacaoFontes.Divergem, ta, tb, $"{rotulo}: {Nome(a)} informa \"{ta}\"; {Nome(b)} informa \"{tb}\".");
    }

    public static string NomeComponente(ComponenteComparacaoFontes c) => c switch
    {
        ComponenteComparacaoFontes.Existencia => "existência do CEP",
        ComponenteComparacaoFontes.Uf => "UF",
        ComponenteComparacaoFontes.Municipio => "município",
        ComponenteComparacaoFontes.CodigoIbge => "código IBGE",
        ComponenteComparacaoFontes.Logradouro => "logradouro",
        ComponenteComparacaoFontes.Bairro => "bairro",
        _ => "faixa de numeração"
    };

    public static string Nome(CepFonte? fonte) => fonte switch
    {
        CepFonte.ViaCep => "ViaCEP",
        CepFonte.BrasilApi => "BrasilAPI",
        CepFonte.Correios => "Correios",
        null => "a fonte",
        _ => fonte.Value.ToString()
    };
}
