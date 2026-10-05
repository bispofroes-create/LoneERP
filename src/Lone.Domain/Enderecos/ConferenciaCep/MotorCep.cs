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
                    ["Não foi possível conferir o CEP agora (serviço indisponível). Você pode salvar e conferir depois."], false);

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
        if (avaliacao.Divergencias.Count > 0)
            return new DecisaoCep(ResultadoDecisaoCep.Divergente, cepInformado, consulta.Fonte, registro, null, [],
                avaliacao.Divergencias.Concat(avaliacao.Observacoes).ToArray(), false);

        return new DecisaoCep(ResultadoDecisaoCep.Conferido, cepInformado, consulta.Fonte, registro, null, [],
            new[] { "CEP conferido: corresponde ao endereço." }.Concat(avaliacao.Observacoes).ToArray(), false);
    }

    // Casos 3 a 6.
    private static DecisaoCep SemCep(EnderecoConferenciaCep endereco, string cepInformado, RespostaConsultaCep consulta,
                                     RespostaBuscaEndereco? busca)
    {
        const string naoEncontrado = "CEP informado não encontrado.";
        if (busca is null)
            return new DecisaoCep(ResultadoDecisaoCep.NaoEncontrado, cepInformado, consulta.Fonte, null, null, [],
                [naoEncontrado, "Falta buscar o CEP pelo endereço."], true);

        if (busca.Situacao == SituacaoRespostaFonte.Indisponivel)
            return new DecisaoCep(ResultadoDecisaoCep.NaoEncontrado, cepInformado, consulta.Fonte, null, null, [],
                [naoEncontrado, "Não foi possível buscar o CEP pelo endereço agora (serviço indisponível)."], true);

        // Um CEP por candidato (a fonte pode repetir), em ordem de CEP: a ordem da resposta não muda a decisão.
        var candidatos = busca.Registros
            .Where(r => ObjetosDeValor.Cep.EhValido(r.Cep) && DuplicidadeEndereco.Cep(r.Cep) != cepInformado)
            .Where(r => Avaliar(endereco, r, conferirBairro: true).Divergencias.Count == 0)
            .GroupBy(r => DuplicidadeEndereco.Cep(r.Cep), StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => g.OrderBy(Chave, StringComparer.Ordinal).First())
            .ToArray();

        switch (candidatos.Length)
        {
            case 0:
                return new DecisaoCep(ResultadoDecisaoCep.NenhumCandidato, cepInformado, busca.Fonte, null, null, [],
                    ["CEP não localizado: nenhum CEP compatível com o endereço foi encontrado."], false);
            case 1:
                var sugerido = DuplicidadeEndereco.Cep(candidatos[0].Cep);
                return new DecisaoCep(ResultadoDecisaoCep.UmCandidato, cepInformado, busca.Fonte, null, sugerido,
                    Array.AsReadOnly(candidatos),
                    ["CEP informado não encontrado; foi localizado um único CEP compatível.",
                     $"Sugestão: {Formatar(cepInformado)} → {Formatar(sugerido)}."], false);
            default:
                return new DecisaoCep(ResultadoDecisaoCep.VariosCandidatos, cepInformado, busca.Fonte, null, null,
                    Array.AsReadOnly(candidatos),
                    [$"CEP informado não encontrado; foram localizados {candidatos.Length} CEPs compatíveis. Escolha um."], false);
        }
    }

    private sealed record Avaliacao(List<string> Divergencias, List<string> Observacoes);

    /// <summary>
    /// O registro da fonte bate com o endereço? Divergência = dado presente dos dois lados e diferente (ou número fora da
    /// faixa). Dado ausente não diverge: vira observação (não se conclui o que não se sabe).
    /// </summary>
    private static Avaliacao Avaliar(EnderecoConferenciaCep endereco, RegistroCep registro, bool conferirBairro)
    {
        var divergencias = new List<string>();
        var observacoes = new List<string>();

        var ufEndereco = DuplicidadeEndereco.Texto(endereco.Uf);
        var ufRegistro = DuplicidadeEndereco.Texto(registro.Uf);
        if (ufEndereco.Length > 0 && ufRegistro.Length > 0 && ufEndereco != ufRegistro)
            divergencias.Add($"O CEP informado é de outra UF ({registro.Uf?.Trim()}).");

        var ibgeEndereco = DuplicidadeEndereco.Cep(endereco.CodigoMunicipioIbge);
        var ibgeRegistro = DuplicidadeEndereco.Cep(registro.CodigoMunicipioIbge);
        var cidadeEndereco = DuplicidadeEndereco.Texto(endereco.Cidade);
        var cidadeRegistro = DuplicidadeEndereco.Texto(registro.Cidade);
        bool? mesmoMunicipio = ibgeEndereco.Length > 0 && ibgeRegistro.Length > 0 ? ibgeEndereco == ibgeRegistro
            : cidadeEndereco.Length > 0 && cidadeRegistro.Length > 0 ? cidadeEndereco == cidadeRegistro
            : null;
        if (mesmoMunicipio == false) divergencias.Add($"O CEP informado é de outro município ({registro.Cidade?.Trim()}).");
        else if (mesmoMunicipio is null) observacoes.Add("O município não pôde ser comparado (falta o dado).");

        if (conferirBairro)
        {
            var bairroEndereco = DuplicidadeEndereco.Texto(endereco.Bairro);
            var bairroRegistro = DuplicidadeEndereco.Texto(registro.Bairro);
            if (bairroEndereco.Length > 0 && bairroRegistro.Length > 0 && bairroEndereco != bairroRegistro)
                divergencias.Add($"O CEP é de outro bairro ({registro.Bairro?.Trim()}).");
        }

        // Sem logradouro na fonte: CEP geral da cidade (vale para a cidade toda; não há faixa).
        if (NormalizadorLogradouro.Normalizar(registro.Logradouro).Length == 0) return new Avaliacao(divergencias, observacoes);

        if (NormalizadorLogradouro.Normalizar(endereco.Logradouro).Length == 0)
            divergencias.Add("O endereço não tem logradouro para comparar com o do CEP.");
        else if (!NormalizadorLogradouro.Equivalentes(endereco.Logradouro, registro.Logradouro))
            divergencias.Add($"O CEP informado corresponde a outro logradouro ({registro.Logradouro?.Trim()}).");

        var faixa = FaixaNumeracao.Interpretar(registro.Complemento);
        if (faixa is null)
            observacoes.Add($"A faixa de numeração do CEP não foi interpretada (\"{registro.Complemento?.Trim()}\"); o número não foi conferido.");
        else
            switch (faixa.Contem(endereco.Numero))
            {
                case PertinenciaFaixa.Fora:
                    divergencias.Add($"O número informado é incompatível com a faixa do CEP ({registro.Complemento?.Trim()}).");
                    break;
                case PertinenciaFaixa.Indeterminado when faixa != FaixaNumeracao.Todas:
                    observacoes.Add("O endereço não tem número legível; a faixa de numeração do CEP não foi conferida.");
                    break;
            }

        return new Avaliacao(divergencias, observacoes);
    }

    // Desempate estável entre registros repetidos do mesmo CEP.
    private static string Chave(RegistroCep r) => string.Join("|", NormalizadorLogradouro.Normalizar(r.Logradouro),
        DuplicidadeEndereco.Texto(r.Complemento), DuplicidadeEndereco.Texto(r.Bairro), DuplicidadeEndereco.Texto(r.Cidade),
        DuplicidadeEndereco.Texto(r.Uf), DuplicidadeEndereco.Cep(r.CodigoMunicipioIbge));

    private static string Formatar(string cep) => ObjetosDeValor.Cep.TentarCriar(cep, out var c) ? c!.Formatado : cep;
}
