namespace Lone.Domain.Enderecos.ConferenciaCep;

/// <summary>
/// Decide o CEP de um endereço em seis casos (plano v1.3, §7; arquitetura §4.1), só com os dados recebidos: sem banco, sem
/// internet, sem provedor. Nunca altera o endereço: devolve uma <see cref="DecisaoCep"/>.
/// <list type="number">
/// <item>Conferido: o CEP existe; UF e município iguais; logradouro equivalente (ou CEP geral da cidade) e número na faixa.</item>
/// <item>Divergente: o CEP existe, mas é de outra UF, município, logradouro ou faixa. Não troca.</item>
/// <item>Não encontrado: o CEP não existe; pede a busca pelo endereço.</item>
/// <item>Um candidato: a busca achou um único CEP compatível. Sugestão, sem alterar o endereço.</item>
/// <item>Vários candidatos: lista para o usuário escolher. Nenhum é escolhido.</item>
/// <item>Nenhum candidato: "não localizado". Nenhum CEP é inventado.</item>
/// </list>
/// Fonte indisponível não é conclusão: não invalida o endereço.
/// </summary>
public static class MotorCep
{
    /// <param name="endereco">O endereço, como cópia de leitura (<see cref="EnderecoConferenciaCep.De"/>). CEP válido obrigatório.</param>
    /// <param name="consulta">A resposta da consulta pelo CEP do endereço.</param>
    /// <param name="busca">A resposta da busca pelo endereço, quando o CEP não existe e a busca já foi feita; nula se não foi.</param>
    public static DecisaoCep Decidir(EnderecoConferenciaCep endereco, RespostaConsultaCep consulta, RespostaBuscaEndereco? busca = null)
    {
        ArgumentNullException.ThrowIfNull(endereco);
        ArgumentNullException.ThrowIfNull(consulta);
        if (!ObjetosDeValor.Cep.TentarCriar(endereco.Cep, out var cep))
            throw new ArgumentException("O endereço precisa de um CEP válido (8 dígitos) para ser conferido.", nameof(endereco));
        var cepInformado = cep!.Valor;

        switch (consulta.Situacao)
        {
            case SituacaoRespostaFonte.Indisponivel:
                return new DecisaoCep(ResultadoDecisaoCep.FonteIndisponivel, cepInformado, consulta.Fonte, null, null, [],
                    ["Não foi possível conferir o CEP agora (serviço indisponível). Você pode salvar e conferir depois."], false,
                    SemRegistro(SituacaoComponenteCep.NaoValidavel, "A fonte não respondeu: o CEP não foi conferido.",
                        "Não conferido: a fonte não respondeu."));

            case SituacaoRespostaFonte.Encontrado:
                return Conferir(endereco, cepInformado, consulta);

            default:
                return SemCep(endereco, cepInformado, consulta, busca);
        }
    }

    // Casos 1 e 2.
    private static DecisaoCep Conferir(EnderecoConferenciaCep endereco, string cepInformado, RespostaConsultaCep consulta)
    {
        var registro = consulta.Registro!;
        if (DuplicidadeEndereco.Cep(registro.Cep) != cepInformado)
            throw new ArgumentException("A consulta recebida não é do CEP do endereço.", nameof(consulta));

        var avaliacao = Avaliar(endereco, registro, conferirBairro: false);
        // O componente CEP diz só que o CEP existe na fonte; o resto vem da mesma avaliação que decide o resultado.
        var componentes = new[]
        {
            new ConferenciaComponenteCep(ComponenteCep.Cep, SituacaoComponenteCep.Confirmado,
                "O CEP existe na fonte consultada (isso, sozinho, não confirma o endereço).")
        }.Concat(avaliacao.Componentes).ToArray();
        if (avaliacao.Divergencias.Count > 0)
            return new DecisaoCep(ResultadoDecisaoCep.Divergente, cepInformado, consulta.Fonte, registro, null, [],
                avaliacao.Divergencias.Concat(avaliacao.Observacoes).ToArray(), false, componentes);

        return new DecisaoCep(ResultadoDecisaoCep.Conferido, cepInformado, consulta.Fonte, registro, null, [],
            new[] { "CEP conferido: corresponde ao endereço." }.Concat(avaliacao.Observacoes).ToArray(), false, componentes);
    }

    // Casos 3 a 6.
    private static DecisaoCep SemCep(EnderecoConferenciaCep endereco, string cepInformado, RespostaConsultaCep consulta,
                                     RespostaBuscaEndereco? busca)
    {
        const string naoEncontrado = "CEP informado não encontrado.";
        // O CEP informado não existe na fonte (resposta dela, não falta de resposta); sem registro, o resto não é comparável.
        var componentes = SemRegistro(SituacaoComponenteCep.Divergente, "O CEP informado não existe na fonte consultada.",
            "Não conferido: não há registro do CEP informado para comparar.");
        if (busca is null)
            return new DecisaoCep(ResultadoDecisaoCep.NaoEncontrado, cepInformado, consulta.Fonte, null, null, [],
                [naoEncontrado, "Falta buscar o CEP pelo endereço."], true, componentes);

        if (busca.Situacao == SituacaoRespostaFonte.Indisponivel)
            return new DecisaoCep(ResultadoDecisaoCep.NaoEncontrado, cepInformado, consulta.Fonte, null, null, [],
                [naoEncontrado, "Não foi possível buscar o CEP pelo endereço agora (serviço indisponível)."], true, componentes);

        var candidatos = Filtrar(endereco, busca.Registros, cepExcluido: cepInformado);

        switch (candidatos.Length)
        {
            case 0:
                return new DecisaoCep(ResultadoDecisaoCep.NenhumCandidato, cepInformado, busca.Fonte, null, null, [],
                    ["CEP não localizado: nenhum CEP compatível com o endereço foi encontrado."], false, componentes);
            case 1:
                var sugerido = DuplicidadeEndereco.Cep(candidatos[0].Registro.Cep);
                return new DecisaoCep(ResultadoDecisaoCep.UmCandidato, cepInformado, busca.Fonte, null, sugerido, candidatos,
                    ["CEP informado não encontrado; foi localizado um único CEP compatível.",
                     $"Sugestão: {Formatar(cepInformado)} → {Formatar(sugerido)}."], false, componentes);
            default:
                return new DecisaoCep(ResultadoDecisaoCep.VariosCandidatos, cepInformado, busca.Fonte, null, null, candidatos,
                    [$"CEP informado não encontrado; foram localizados {candidatos.Length} CEPs compatíveis. Escolha um."], false,
                    componentes);
        }
    }

    /// <summary>
    /// Busca de CEP pelo endereço quando o usuário <b>não sabe o CEP</b> (Checkpoint D): o mesmo filtro dos casos 4 a 6
    /// (UF, município, bairro, logradouro, número/faixa/lado/prédio), sem CEP informado. Busca candidatos; não descobre "o
    /// CEP certo": nenhum é escolhido, sugerido como melhor ou aplicado. Um candidato só = um CEP compatível na resposta da
    /// fonte, não certeza (a fonte pode ter cortado a lista). Nenhum candidato = nenhum CEP é inventado. Fonte fora do ar =
    /// <see cref="ResultadoDecisaoCep.FonteIndisponivel"/>, nunca "nenhum candidato".
    /// </summary>
    /// <param name="endereco">O endereço como digitado; o CEP dele é ignorado (pode estar vazio).</param>
    /// <param name="busca">A resposta da busca pelo endereço (feita fora do domínio).</param>
    public static DecisaoCep BuscarPorEndereco(EnderecoConferenciaCep endereco, RespostaBuscaEndereco busca)
    {
        ArgumentNullException.ThrowIfNull(endereco);
        ArgumentNullException.ThrowIfNull(busca);
        const string semCep = "";

        if (busca.Situacao == SituacaoRespostaFonte.Indisponivel)
            return new DecisaoCep(ResultadoDecisaoCep.FonteIndisponivel, semCep, busca.Fonte, null, null, [],
                ["Não foi possível consultar a fonte de CEP agora. Tente de novo mais tarde; o cadastro pode seguir normalmente."],
                false, []);

        var candidatos = Filtrar(endereco, busca.Registros, cepExcluido: null);
        return candidatos.Length switch
        {
            0 => new DecisaoCep(ResultadoDecisaoCep.NenhumCandidato, semCep, busca.Fonte, null, null, [],
                ["Não encontramos um CEP compatível com os dados informados."], false, []),
            1 => new DecisaoCep(ResultadoDecisaoCep.UmCandidato, semCep, busca.Fonte, null, null, candidatos,
                ["Encontramos um CEP compatível com os dados informados. Confira antes de usar."], false, []),
            _ => new DecisaoCep(ResultadoDecisaoCep.VariosCandidatos, semCep, busca.Fonte, null, null, candidatos,
                [$"Encontramos {candidatos.Length} CEPs compatíveis com os dados informados. Confira e escolha, se for o caso."], false, [])
        };
    }

    /// <summary>
    /// O filtro de candidatos (casos 4 a 6 e busca sem CEP): CEP válido, diferente do informado, sem nenhuma divergência na
    /// avaliação com bairro; um por CEP (a fonte pode repetir), em ordem de CEP (a ordem da resposta não muda a decisão).
    /// Cada candidato leva os componentes da mesma avaliação que o manteve.
    /// </summary>
    private static CandidatoCep[] Filtrar(EnderecoConferenciaCep endereco, IEnumerable<RegistroCep> registros, string? cepExcluido) =>
        registros
            .Where(r => ObjetosDeValor.Cep.EhValido(r.Cep) && (cepExcluido is null || DuplicidadeEndereco.Cep(r.Cep) != cepExcluido))
            .Select(r => (Registro: r, Avaliacao: Avaliar(endereco, r, conferirBairro: true)))
            .Where(x => x.Avaliacao.Divergencias.Count == 0)
            .GroupBy(x => DuplicidadeEndereco.Cep(x.Registro.Cep), StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => g.OrderBy(x => Chave(x.Registro), StringComparer.Ordinal).First())
            .Select(x => new CandidatoCep(x.Registro, x.Avaliacao.Componentes))
            .ToArray();

    /// <summary>Componentes de quando não há registro do CEP informado para comparar (fonte sem resposta ou CEP inexistente).</summary>
    private static ConferenciaComponenteCep[] SemRegistro(SituacaoComponenteCep cep, string motivoCep, string motivoDemais) =>
    [
        new(ComponenteCep.Cep, cep, motivoCep),
        new(ComponenteCep.Uf, SituacaoComponenteCep.NaoValidavel, motivoDemais),
        new(ComponenteCep.Municipio, SituacaoComponenteCep.NaoValidavel, motivoDemais),
        new(ComponenteCep.Logradouro, SituacaoComponenteCep.NaoValidavel, motivoDemais),
        new(ComponenteCep.Numero, SituacaoComponenteCep.NaoValidavel, motivoDemais)
    ];

    private sealed record Avaliacao(List<string> Divergencias, List<string> Observacoes, List<ConferenciaComponenteCep> Componentes);

    /// <summary>
    /// O registro da fonte bate com o endereço? Divergência = dado presente dos dois lados e diferente (ou número fora da
    /// faixa). Dado ausente não diverge: vira observação (não se conclui o que não se sabe). Cada comparação gera, de uma
    /// vez só, o componente (UF, município, logradouro, número) e, quando é o caso, a divergência ou a observação: o
    /// componente divergente é exatamente o que entra nas divergências (uma única fonte de decisão). O bairro (só na busca)
    /// é critério de filtro e não vira componente.
    /// </summary>
    private static Avaliacao Avaliar(EnderecoConferenciaCep endereco, RegistroCep registro, bool conferirBairro)
    {
        var divergencias = new List<string>();
        var observacoes = new List<string>();
        var componentes = new List<ConferenciaComponenteCep>();

        // Registra o componente; divergente entra nas divergências e, quando pedido, o motivo vira observação (os textos dos
        // motivos são os de antes: nenhum motivo novo aparece por causa dos componentes).
        void Componente(ComponenteCep c, SituacaoComponenteCep situacao, string motivo, bool observar = false)
        {
            componentes.Add(new ConferenciaComponenteCep(c, situacao, motivo));
            if (situacao == SituacaoComponenteCep.Divergente) divergencias.Add(motivo);
            else if (observar) observacoes.Add(motivo);
        }

        var ufEndereco = DuplicidadeEndereco.Texto(endereco.Uf);
        var ufRegistro = DuplicidadeEndereco.Texto(registro.Uf);
        if (ufEndereco.Length > 0 && ufRegistro.Length > 0)
            Componente(ComponenteCep.Uf, ufEndereco == ufRegistro ? SituacaoComponenteCep.Confirmado : SituacaoComponenteCep.Divergente,
                ufEndereco == ufRegistro ? $"UF confirmada ({registro.Uf?.Trim()})." : $"O CEP informado é de outra UF ({registro.Uf?.Trim()}).");
        else
            Componente(ComponenteCep.Uf, SituacaoComponenteCep.NaoInformado,
                ufEndereco.Length == 0 ? "O endereço não tem UF para comparar." : "A fonte não informa a UF do CEP.");

        var ibgeEndereco = DuplicidadeEndereco.Cep(endereco.CodigoMunicipioIbge);
        var ibgeRegistro = DuplicidadeEndereco.Cep(registro.CodigoMunicipioIbge);
        var cidadeEndereco = DuplicidadeEndereco.Texto(endereco.Cidade);
        var cidadeRegistro = DuplicidadeEndereco.Texto(registro.Cidade);
        var peloIbge = ibgeEndereco.Length > 0 && ibgeRegistro.Length > 0;
        bool? mesmoMunicipio = peloIbge ? ibgeEndereco == ibgeRegistro
            : cidadeEndereco.Length > 0 && cidadeRegistro.Length > 0 ? cidadeEndereco == cidadeRegistro
            : null;
        if (mesmoMunicipio == false)
            Componente(ComponenteCep.Municipio, SituacaoComponenteCep.Divergente, $"O CEP informado é de outro município ({registro.Cidade?.Trim()}).");
        else if (mesmoMunicipio is null)
            Componente(ComponenteCep.Municipio, SituacaoComponenteCep.NaoInformado, "O município não pôde ser comparado (falta o dado).", observar: true);
        else
            Componente(ComponenteCep.Municipio, SituacaoComponenteCep.Confirmado,
                peloIbge ? $"Município confirmado pelo código IBGE ({registro.Cidade?.Trim()})." : $"Município confirmado pelo nome ({registro.Cidade?.Trim()}).");

        if (conferirBairro)
        {
            var bairroEndereco = DuplicidadeEndereco.Texto(endereco.Bairro);
            var bairroRegistro = DuplicidadeEndereco.Texto(registro.Bairro);
            if (bairroEndereco.Length > 0 && bairroRegistro.Length > 0 && bairroEndereco != bairroRegistro)
                divergencias.Add($"O CEP é de outro bairro ({registro.Bairro?.Trim()}).");
        }

        // Sem logradouro na fonte: CEP geral da cidade (vale para a cidade toda; não há faixa).
        if (NormalizadorLogradouro.Normalizar(registro.Logradouro).Length == 0)
        {
            Componente(ComponenteCep.Logradouro, SituacaoComponenteCep.NaoInformado,
                "CEP geral do município: a fonte não informa logradouro para este CEP.");
            Componente(ComponenteCep.Numero, SituacaoComponenteCep.NaoInformado,
                "CEP geral do município: a fonte não informa faixa de numeração para este CEP.");
            return new Avaliacao(divergencias, observacoes, componentes);
        }

        if (NormalizadorLogradouro.Normalizar(endereco.Logradouro).Length == 0)
            Componente(ComponenteCep.Logradouro, SituacaoComponenteCep.Divergente, "O endereço não tem logradouro para comparar com o do CEP.");
        else if (!NormalizadorLogradouro.Equivalentes(endereco.Logradouro, registro.Logradouro))
            Componente(ComponenteCep.Logradouro, SituacaoComponenteCep.Divergente,
                $"O CEP informado corresponde a outro logradouro ({registro.Logradouro?.Trim()}).");
        else
            Componente(ComponenteCep.Logradouro, SituacaoComponenteCep.Confirmado,
                $"Logradouro equivalente ao do CEP ({registro.Logradouro?.Trim()}).");

        var faixa = FaixaNumeracao.Interpretar(registro.Complemento);
        if (faixa is null)
            Componente(ComponenteCep.Numero, SituacaoComponenteCep.NaoValidavel,
                $"A faixa de numeração do CEP não foi interpretada (\"{registro.Complemento?.Trim()}\"); o número não foi conferido.", observar: true);
        else
            switch (faixa.Contem(endereco.Numero))
            {
                case PertinenciaFaixa.Fora:
                    Componente(ComponenteCep.Numero, SituacaoComponenteCep.Divergente,
                        $"O número informado é incompatível com a faixa do CEP ({registro.Complemento?.Trim()}).");
                    break;
                case PertinenciaFaixa.Indeterminado when faixa != FaixaNumeracao.Todas:
                    // Indeterminado nunca elimina: S/N ou número ilegível = não validável; sem número = não informado.
                    Componente(ComponenteCep.Numero,
                        string.IsNullOrWhiteSpace(endereco.Numero) ? SituacaoComponenteCep.NaoInformado : SituacaoComponenteCep.NaoValidavel,
                        "O endereço não tem número legível; a faixa de numeração do CEP não foi conferida.", observar: true);
                    break;
                case PertinenciaFaixa.Dentro when faixa != FaixaNumeracao.Todas:
                    Componente(ComponenteCep.Numero, SituacaoComponenteCep.Confirmado,
                        faixa.Inicio is { } predio && faixa.Fim == predio
                            ? $"O número é o do prédio do CEP ({registro.Complemento?.Trim()})."
                            : $"O número está na faixa do CEP ({registro.Complemento?.Trim()}).");
                    break;
                default:
                    // CEP sem faixa de numeração (vale para a rua toda): a fonte não diz nada sobre o número.
                    Componente(ComponenteCep.Numero, SituacaoComponenteCep.NaoInformado,
                        "A fonte não informa faixa de numeração para este CEP; o número não é conferido.");
                    break;
            }

        return new Avaliacao(divergencias, observacoes, componentes);
    }

    // Desempate estável entre registros repetidos do mesmo CEP.
    private static string Chave(RegistroCep r) => string.Join("|", NormalizadorLogradouro.Normalizar(r.Logradouro),
        DuplicidadeEndereco.Texto(r.Complemento), DuplicidadeEndereco.Texto(r.Bairro), DuplicidadeEndereco.Texto(r.Cidade),
        DuplicidadeEndereco.Texto(r.Uf), DuplicidadeEndereco.Cep(r.CodigoMunicipioIbge));

    private static string Formatar(string cep) => ObjetosDeValor.Cep.TentarCriar(cep, out var c) ? c!.Formatado : cep;
}
