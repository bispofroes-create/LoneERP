namespace Lone.Domain.Fiscal;

/// <summary>
/// Tabela de natureza jurídica (CONCLA/IBGE, usada pela Receita no CNPJ). O código tem 3 dígitos mais o verificador
/// ("204-6"); a Receita manda os 4 juntos ("2046"), que é o que o estabelecimento grava. Só descreve para a tela:
/// o campo continua sendo o código. Código fora da tabela aparece só formatado.
/// </summary>
public static class NaturezasJuridicas
{
    private static readonly Dictionary<int, string> Nomes = new()
    {
        // 1 — Administração pública
        [101] = "Órgão Público do Poder Executivo Federal",
        [102] = "Órgão Público do Poder Executivo Estadual ou do Distrito Federal",
        [103] = "Órgão Público do Poder Executivo Municipal",
        [104] = "Órgão Público do Poder Legislativo Federal",
        [105] = "Órgão Público do Poder Legislativo Estadual ou do Distrito Federal",
        [106] = "Órgão Público do Poder Legislativo Municipal",
        [107] = "Órgão Público do Poder Judiciário Federal",
        [108] = "Órgão Público do Poder Judiciário Estadual",
        [110] = "Autarquia Federal",
        [111] = "Autarquia Estadual ou do Distrito Federal",
        [112] = "Autarquia Municipal",
        [113] = "Fundação Pública de Direito Público Federal",
        [114] = "Fundação Pública de Direito Público Estadual ou do Distrito Federal",
        [115] = "Fundação Pública de Direito Público Municipal",
        [116] = "Órgão Público Autônomo Federal",
        [117] = "Órgão Público Autônomo Estadual ou do Distrito Federal",
        [118] = "Órgão Público Autônomo Municipal",
        [119] = "Comissão Polinacional",
        [121] = "Consórcio Público de Direito Público (Associação Pública)",
        [122] = "Consórcio Público de Direito Privado",
        [123] = "Estado ou Distrito Federal",
        [124] = "Município",
        [125] = "Fundação Pública de Direito Privado Federal",
        [126] = "Fundação Pública de Direito Privado Estadual ou do Distrito Federal",
        [127] = "Fundação Pública de Direito Privado Municipal",
        [128] = "Fundo Público da Administração Indireta Federal",
        [129] = "Fundo Público da Administração Indireta Estadual ou do Distrito Federal",
        [130] = "Fundo Público da Administração Indireta Municipal",
        [131] = "Fundo Público da Administração Direta Federal",
        [132] = "Fundo Público da Administração Direta Estadual ou do Distrito Federal",
        [133] = "Fundo Público da Administração Direta Municipal",

        // 2 — Entidades empresariais
        [201] = "Empresa Pública",
        [203] = "Sociedade de Economia Mista",
        [204] = "Sociedade Anônima Aberta",
        [205] = "Sociedade Anônima Fechada",
        [206] = "Sociedade Empresária Limitada",
        [207] = "Sociedade Empresária em Nome Coletivo",
        [208] = "Sociedade Empresária em Comandita Simples",
        [209] = "Sociedade Empresária em Comandita por Ações",
        [212] = "Sociedade em Conta de Participação",
        [213] = "Empresário (Individual)",
        [214] = "Cooperativa",
        [215] = "Consórcio de Sociedades",
        [216] = "Grupo de Sociedades",
        [217] = "Estabelecimento, no Brasil, de Sociedade Estrangeira",
        [219] = "Estabelecimento, no Brasil, de Empresa Binacional Argentino-Brasileira",
        [221] = "Empresa Domiciliada no Exterior",
        [222] = "Clube/Fundo de Investimento",
        [223] = "Sociedade Simples Pura",
        [224] = "Sociedade Simples Limitada",
        [225] = "Sociedade Simples em Nome Coletivo",
        [226] = "Sociedade Simples em Comandita Simples",
        [227] = "Empresa Binacional",
        [228] = "Consórcio de Empregadores",
        [229] = "Consórcio Simples",
        [230] = "Empresa Individual de Responsabilidade Limitada (de Natureza Empresária)",
        [231] = "Empresa Individual de Responsabilidade Limitada (de Natureza Simples)",
        [232] = "Sociedade Unipessoal de Advocacia",
        [233] = "Cooperativas de Consumo",
        [234] = "Empresa Simples de Inovação - Inova Simples",
        [235] = "Investidor Não Residente",

        // 3 — Entidades sem fins lucrativos
        [303] = "Serviço Notarial e Registral (Cartório)",
        [306] = "Fundação Privada",
        [307] = "Serviço Social Autônomo",
        [308] = "Condomínio Edilício",
        [310] = "Comissão de Conciliação Prévia",
        [311] = "Entidade de Mediação e Arbitragem",
        [313] = "Entidade Sindical",
        [320] = "Estabelecimento, no Brasil, de Fundação ou Associação Estrangeiras",
        [321] = "Fundação ou Associação Domiciliada no Exterior",
        [322] = "Organização Religiosa",
        [323] = "Comunidade Indígena",
        [324] = "Fundo Privado",
        [325] = "Órgão de Direção Nacional de Partido Político",
        [326] = "Órgão de Direção Regional de Partido Político",
        [327] = "Órgão de Direção Local de Partido Político",
        [328] = "Comitê Financeiro de Partido Político",
        [329] = "Frente Plebiscitária ou Referendária",
        [330] = "Organização Social (OS)",
        [399] = "Associação Privada",

        // 4 — Pessoas físicas (com CNPJ)
        [401] = "Empresa Individual Imobiliária",
        [402] = "Segurado Especial",
        [408] = "Contribuinte Individual",
        [409] = "Candidato a Cargo Político Eletivo",
        [411] = "Leiloeiro",
        [412] = "Produtor Rural (Pessoa Física)",

        // 5 — Organizações internacionais e outras instituições extraterritoriais
        [501] = "Organização Internacional",
        [502] = "Representação Diplomática Estrangeira",
        [503] = "Outras Instituições Extraterritoriais"
    };

    /// <summary>Toda a tabela, na ordem dos códigos: ("2054", "205-4 · Sociedade Anônima Fechada").</summary>
    public static IReadOnlyList<(string Codigo, string Texto)> Todas { get; } =
        Nomes.OrderBy(n => n.Key)
            .Select(n => ($"{n.Key:000}{Digito(n.Key)}", $"{n.Key:000}-{Digito(n.Key)} · {n.Value}"))
            .ToList();

    /// <summary>Dígito verificador (módulo 11, pesos 4-3-2): "204" → 6.</summary>
    public static int Digito(int codigo)
    {
        var resto = (codigo / 100 * 4 + codigo / 10 % 10 * 3 + codigo % 10 * 2) % 11;
        return resto < 2 ? 0 : 11 - resto;
    }

    /// <summary>"2046", "204-6" ou "204" → "204-6 · Sociedade Anônima Aberta"; vazio se não houver código.</summary>
    public static string Descrever(string? texto)
    {
        var digitos = new string((texto ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        if (digitos.Length is not (3 or 4)) return string.Empty;
        var codigo = int.Parse(digitos[..3], System.Globalization.CultureInfo.InvariantCulture);
        var formatado = $"{digitos[..3]}-{Digito(codigo)}";
        return Nomes.TryGetValue(codigo, out var nome) ? $"{formatado} · {nome}" : formatado;
    }
}
