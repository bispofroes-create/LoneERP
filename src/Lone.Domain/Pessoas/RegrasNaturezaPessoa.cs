using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Validacao;

namespace Lone.Domain.Pessoas;

/// <summary>
/// Troca do tipo de pessoa (natureza) num cadastro gravado. Pessoa jurídica gravada NÃO vira pessoa física nem
/// estrangeiro: o normalizador apagaria CNPJ, nome fantasia e dados da empresa, e o grupo empresarial e os vínculos
/// societários ficariam incoerentes. Nada é removido em silêncio: a gravação é recusada, com a lista do que seria perdido.
/// Se o tipo estava errado, o caminho é cadastrar a pessoa correta e desativar este cadastro.
/// </summary>
public static class RegrasNaturezaPessoa
{
    /// <param name="vinculosSocietariosComoEmpresa">
    /// Vínculos "sócio de"/"administrador de" em aberto em que esta pessoa é a empresa (o destino).
    /// </param>
    public static string? ValidarTroca(Pessoa? anterior, Pessoa dados, int vinculosSocietariosComoEmpresa)
    {
        if (anterior is null || anterior.Natureza != NaturezaPessoa.Juridica || dados.Natureza == NaturezaPessoa.Juridica)
            return null;

        var perdas = new List<string>();
        var cnpjs = anterior.Estabelecimentos.Where(e => e.Cnpj is { Length: > 0 }).Select(e => Documento.Formatar(e.Cnpj)).ToList();
        if (cnpjs.Count == 1) perdas.Add($"o CNPJ {cnpjs[0]}");
        else if (cnpjs.Count > 1) perdas.Add($"{cnpjs.Count} estabelecimentos (matriz e filiais: {string.Join(", ", cnpjs)})");
        if (anterior.Estabelecimentos.Any(e => !string.IsNullOrWhiteSpace(e.NomeFantasia))) perdas.Add("o nome fantasia");
        if (anterior.GrupoEmpresarialId is not null) perdas.Add("a participação no grupo empresarial");
        if (vinculosSocietariosComoEmpresa > 0)
            perdas.Add($"{vinculosSocietariosComoEmpresa} vínculo(s) de sócio/administrador em que ela é a empresa");
        if (anterior.Socios.Count > 0) perdas.Add("o quadro de sócios da Receita");
        if (anterior.DataAbertura is not null || anterior.Porte is not null || anterior.CapitalSocial is not null)
            perdas.Add("os dados da empresa (abertura, porte, capital social)");

        var natureza = dados.Natureza == NaturezaPessoa.Fisica ? "pessoa física" : "estrangeiro";
        return $"Uma pessoa jurídica gravada não pode virar {natureza}" +
               (perdas.Count > 0 ? $": seriam perdidos {string.Join("; ", perdas)}" : string.Empty) +
               ". Nada foi alterado. Se o tipo de pessoa está errado, cadastre a pessoa correta e desative este cadastro.";
    }

    // ---- Bloco G (P1-2): pessoa física que vira pessoa jurídica ou estrangeiro ----

    /// <summary>
    /// Ao sair de pessoa física, o normalizador apaga o que só existe nela (CPF, nome social, apelido, nascimento, dados
    /// pessoais, profissão, naturalidade, cor/raça) e, no estrangeiro, a inscrição estadual. A troca é permitida, mas nunca
    /// em silêncio: a ficha avisa antes e a API só aceita com a confirmação explícita, feita para exatamente esta troca e
    /// estes dados (<see cref="ValidarSaidaDaPessoaFisica"/>). PJ → PF continua recusada (<see cref="ValidarTroca"/>).
    /// </summary>
    public static IReadOnlyList<PerdaNaTroca> PerdasAoSairDaPessoaFisica(DadosSoDaPessoaFisica dados, NaturezaPessoa nova)
    {
        var perdas = new List<PerdaNaTroca>();
        if (nova == NaturezaPessoa.Fisica) return perdas;

        void Se(bool tem, string campo, string descricao)
        {
            if (tem) perdas.Add(new PerdaNaTroca(campo, descricao));
        }

        Se(dados.Cpf, CamposFichaPessoa.Documento, "o CPF");
        Se(dados.NomeSocial, CamposFichaPessoa.NomeSocial, "o nome social");
        Se(dados.Apelido, CamposFichaPessoa.Apelido, "o apelido");
        Se(dados.DataNascimento, CamposFichaPessoa.DataNascimento, "a data de nascimento");
        Se(dados.Sexo, CamposFichaPessoa.Sexo, "o sexo");
        Se(dados.IdentidadeGenero, CamposFichaPessoa.IdentidadeGenero, "a identidade de gênero");
        Se(dados.EstadoCivil, CamposFichaPessoa.EstadoCivil, "o estado civil");
        Se(dados.Escolaridade, CamposFichaPessoa.Escolaridade, "a escolaridade");
        Se(dados.NomeMae, CamposFichaPessoa.NomeMae, "o nome da mãe");
        Se(dados.NomePai, CamposFichaPessoa.NomePai, "o nome do pai");
        Se(dados.Profissao, CamposFichaPessoa.Profissao, "a profissão");
        Se(dados.Naturalidade, CamposFichaPessoa.Naturalidade, "a naturalidade");
        Se(dados.CorRaca, CamposFichaPessoa.CorRaca, "a cor/raça");
        Se(dados.InscricaoEstadual && nova == NaturezaPessoa.Estrangeiro, CamposFichaPessoa.InscricaoEstadual, "a inscrição estadual");
        return perdas;
    }

    /// <summary>"o CPF; o nome social; a data de nascimento".</summary>
    public static string Lista(IEnumerable<PerdaNaTroca> perdas) => string.Join("; ", perdas.Select(p => p.Descricao));

    /// <summary>"pessoa jurídica" / "estrangeiro" / "pessoa física".</summary>
    public static string Nome(NaturezaPessoa natureza) => natureza switch
    {
        NaturezaPessoa.Juridica => "pessoa jurídica",
        NaturezaPessoa.Estrangeiro => "estrangeiro",
        _ => "pessoa física"
    };

    /// <summary>
    /// Na gravação (API): pessoa física GRAVADA que vira outra natureza, com dados que seriam apagados, só passa com a
    /// confirmação desta troca (de pessoa física para <paramref name="nova"/>) cobrindo todos esses dados. Os dados vêm
    /// do gravado (não do que o aplicativo mandou), então chamada direta da API sem a confirmação é recusada.
    /// </summary>
    /// <returns>A mensagem do erro, ou nulo se a troca pode seguir.</returns>
    public static string? ValidarSaidaDaPessoaFisica(Pessoa? anterior, NaturezaPessoa nova, NaturezaPessoa? confirmadaDe,
                                                     NaturezaPessoa? confirmadaPara, IReadOnlyCollection<string>? camposConfirmados)
    {
        if (anterior is not { Natureza: NaturezaPessoa.Fisica } || nova == NaturezaPessoa.Fisica) return null;
        var perdas = PerdasAoSairDaPessoaFisica(DadosSoDaPessoaFisica.De(anterior), nova);
        if (perdas.Count == 0) return null;

        var confirmada = confirmadaDe == NaturezaPessoa.Fisica && confirmadaPara == nova && camposConfirmados is not null &&
                         perdas.All(p => camposConfirmados.Contains(p.Campo));
        return confirmada
            ? null
            : $"Ao trocar de pessoa física para {Nome(nova)}, seriam apagados: {Lista(perdas)}. Nada foi alterado. " +
              "Para continuar, escolha de novo o tipo de pessoa na ficha e confirme a troca.";
    }

    /// <summary>Frase do histórico quando a troca confirmada é gravada (os valores apagados ficam na auditoria campo a campo).</summary>
    public static string? EventoSaidaDaPessoaFisica(Pessoa? anterior, NaturezaPessoa nova)
    {
        if (anterior is not { Natureza: NaturezaPessoa.Fisica } || nova == NaturezaPessoa.Fisica) return null;
        var perdas = PerdasAoSairDaPessoaFisica(DadosSoDaPessoaFisica.De(anterior), nova);
        return $"Tipo de pessoa trocado de pessoa física para {Nome(nova)}" +
               (perdas.Count > 0 ? $", com confirmação; dados apagados: {Lista(perdas)}." : ".");
    }
}

/// <summary>Um dado que a troca de natureza apagaria: o campo da ficha (<see cref="CamposFichaPessoa"/>) e o texto para o usuário.</summary>
public sealed record PerdaNaTroca(string Campo, string Descricao);

/// <summary>
/// Quais dados só da pessoa física estão preenchidos (só "tem ou não tem": nunca os valores). A API monta do gravado; a
/// ficha monta do que carregou e do que está na tela, com a mesma regra.
/// </summary>
public sealed record DadosSoDaPessoaFisica(
    bool Cpf, bool NomeSocial, bool Apelido, bool DataNascimento, bool Sexo, bool IdentidadeGenero, bool EstadoCivil,
    bool Escolaridade, bool NomeMae, bool NomePai, bool Profissao, bool Naturalidade, bool CorRaca, bool InscricaoEstadual)
{
    public static DadosSoDaPessoaFisica De(Pessoa p) => new(
        Cpf: !string.IsNullOrWhiteSpace(p.DocumentoPrincipal),
        NomeSocial: !string.IsNullOrWhiteSpace(p.NomeSocial),
        Apelido: !string.IsNullOrWhiteSpace(p.Apelido),
        DataNascimento: p.DataNascimento is not null,
        Sexo: p.Sexo != SexoRegistro.NaoInformado,
        IdentidadeGenero: p.IdentidadeGenero != Enums.IdentidadeGenero.NaoInformado,
        EstadoCivil: p.EstadoCivil != Enums.EstadoCivil.NaoInformado,
        Escolaridade: p.Escolaridade != Enums.Escolaridade.NaoInformado,
        NomeMae: !string.IsNullOrWhiteSpace(p.NomeMae),
        NomePai: !string.IsNullOrWhiteSpace(p.NomePai),
        Profissao: p.ProfissaoId is not null,
        Naturalidade: p.NaturalidadeMunicipioId is not null,
        CorRaca: p.CorRaca != Enums.CorRaca.NaoInformado,
        InscricaoEstadual: p.EstabelecimentoPrincipal()?.InscricaoEstadual is { Length: > 0 });

    /// <summary>O que alguém tem preenchido em pelo menos um dos dois (usado pela ficha: carregado ∪ tela).</summary>
    public DadosSoDaPessoaFisica Mais(DadosSoDaPessoaFisica outro) => new(
        Cpf || outro.Cpf, NomeSocial || outro.NomeSocial, Apelido || outro.Apelido, DataNascimento || outro.DataNascimento,
        Sexo || outro.Sexo, IdentidadeGenero || outro.IdentidadeGenero, EstadoCivil || outro.EstadoCivil,
        Escolaridade || outro.Escolaridade, NomeMae || outro.NomeMae, NomePai || outro.NomePai, Profissao || outro.Profissao,
        Naturalidade || outro.Naturalidade, CorRaca || outro.CorRaca, InscricaoEstadual || outro.InscricaoEstadual);
}
