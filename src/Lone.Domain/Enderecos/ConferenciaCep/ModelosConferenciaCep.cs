using Lone.Domain.Entidades;

namespace Lone.Domain.Enderecos.ConferenciaCep;

/// <summary>
/// O endereço a conferir, como cópia de leitura. O motor recebe esta cópia e nunca a entidade: não há como ele alterar o
/// <see cref="PessoaEndereco"/> (CEP, UF, município, bairro, finalidade ou principalidade).
/// </summary>
/// <param name="CodigoMunicipioIbge">Código do município no IBGE (7 dígitos), quando houver.</param>
public sealed record EnderecoConferenciaCep(
    string? Cep,
    string? Logradouro,
    string? Numero,
    string? Bairro,
    string? Cidade,
    string? Uf,
    string? CodigoMunicipioIbge)
{
    /// <summary>Copia os campos do endereço da pessoa (só leitura). Conferência de CEP é só para endereço no Brasil.</summary>
    public static EnderecoConferenciaCep De(PessoaEndereco endereco)
    {
        ArgumentNullException.ThrowIfNull(endereco);
        if (!endereco.EhBrasil)
            throw new ArgumentException("A conferência de CEP vale só para endereço no Brasil.", nameof(endereco));
        return new EnderecoConferenciaCep(endereco.Cep, endereco.Logradouro, endereco.Numero, endereco.Bairro, endereco.Cidade,
            endereco.Uf, endereco.MunicipioId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? endereco.CodigoMunicipioIbge);
    }
}

/// <summary>
/// Um CEP como a fonte o descreve: logradouro (vazio = CEP geral da cidade), texto da faixa de numeração (ex.: "até 999/1000"),
/// bairro, município e UF. Dado de entrada do motor; quem o obtém (provedor) é da F2.
/// </summary>
/// <param name="Complemento">Texto da faixa que acompanha o CEP (lido por <see cref="FaixaNumeracao.Interpretar"/>).</param>
public sealed record RegistroCep(
    string Cep,
    string? Logradouro,
    string? Complemento,
    string? Bairro,
    string? Cidade,
    string? Uf,
    string? CodigoMunicipioIbge = null);

/// <summary>O que a fonte respondeu.</summary>
public enum SituacaoRespostaFonte
{
    Encontrado = 1,
    NaoEncontrado = 2,
    /// <summary>A fonte não respondeu (fora do ar, sem internet, tempo esgotado). Não é conclusão sobre o endereço.</summary>
    Indisponivel = 3
}

/// <summary>Resposta da consulta pelo CEP informado (dado de entrada do motor).</summary>
public sealed record RespostaConsultaCep
{
    private RespostaConsultaCep(SituacaoRespostaFonte situacao, RegistroCep? registro, CepFonte? fonte)
    {
        Situacao = situacao;
        Registro = registro;
        Fonte = fonte;
    }

    public SituacaoRespostaFonte Situacao { get; }

    /// <summary>O CEP encontrado (só quando <see cref="SituacaoRespostaFonte.Encontrado"/>).</summary>
    public RegistroCep? Registro { get; }

    public CepFonte? Fonte { get; }

    public static RespostaConsultaCep Encontrado(RegistroCep registro, CepFonte fonte)
    {
        ArgumentNullException.ThrowIfNull(registro);
        return new RespostaConsultaCep(SituacaoRespostaFonte.Encontrado, registro, fonte);
    }

    public static RespostaConsultaCep NaoEncontrado(CepFonte fonte) => new(SituacaoRespostaFonte.NaoEncontrado, null, fonte);

    public static RespostaConsultaCep Indisponivel(CepFonte? fonte = null) => new(SituacaoRespostaFonte.Indisponivel, null, fonte);
}

/// <summary>Resposta da busca de CEPs pelo endereço (UF + município + logradouro), feita fora do domínio.</summary>
public sealed record RespostaBuscaEndereco
{
    private RespostaBuscaEndereco(SituacaoRespostaFonte situacao, IReadOnlyList<RegistroCep> registros, CepFonte? fonte)
    {
        Situacao = situacao;
        Registros = registros;
        Fonte = fonte;
    }

    public SituacaoRespostaFonte Situacao { get; }

    /// <summary>Os CEPs que a fonte devolveu, antes do filtro do motor.</summary>
    public IReadOnlyList<RegistroCep> Registros { get; }

    public CepFonte? Fonte { get; }

    /// <summary>A busca foi feita; nenhum registro = <see cref="SituacaoRespostaFonte.NaoEncontrado"/>.</summary>
    public static RespostaBuscaEndereco Realizada(IEnumerable<RegistroCep> registros, CepFonte fonte)
    {
        ArgumentNullException.ThrowIfNull(registros);
        var lista = registros.ToArray();
        if (lista.Any(r => r is null)) throw new ArgumentException("A busca não pode ter registro vazio.", nameof(registros));
        return new RespostaBuscaEndereco(lista.Length > 0 ? SituacaoRespostaFonte.Encontrado : SituacaoRespostaFonte.NaoEncontrado,
            Array.AsReadOnly(lista), fonte);
    }

    public static RespostaBuscaEndereco Indisponivel(CepFonte? fonte = null) =>
        new(SituacaoRespostaFonte.Indisponivel, Array.Empty<RegistroCep>(), fonte);
}

/// <summary>
/// O que o motor decidiu. É só resultado: situação, candidatos, sugestão e motivos. Nada é gravado nem trocado; aplicar a
/// sugestão é do usuário, na ficha (F2 em diante).
/// </summary>
public sealed record DecisaoCep
{
    internal DecisaoCep(ResultadoDecisaoCep resultado, string cepInformado, CepFonte? fonte, RegistroCep? registroConsultado,
                        string? cepSugerido, IReadOnlyList<RegistroCep> candidatos, IReadOnlyList<string> motivos,
                        bool deveBuscarPorEndereco)
    {
        Resultado = resultado;
        CepInformado = cepInformado;
        Fonte = fonte;
        RegistroConsultado = registroConsultado;
        CepSugerido = cepSugerido;
        Candidatos = candidatos;
        Motivos = motivos;
        DeveBuscarPorEndereco = deveBuscarPorEndereco;
    }

    /// <summary>Qual dos casos (1 a 6, ou fonte indisponível).</summary>
    public ResultadoDecisaoCep Resultado { get; }

    /// <summary>A situação correspondente (o que seria gravado na F6). Um candidato = pendente de decisão, nunca "corrigido".</summary>
    public CepSituacao Situacao => Resultado switch
    {
        ResultadoDecisaoCep.Conferido => CepSituacao.Conferido,
        ResultadoDecisaoCep.Divergente => CepSituacao.Divergente,
        ResultadoDecisaoCep.NaoEncontrado => CepSituacao.NaoEncontrado,
        ResultadoDecisaoCep.UmCandidato => CepSituacao.PendenteDeDecisao,
        ResultadoDecisaoCep.VariosCandidatos => CepSituacao.MultiplosCandidatos,
        ResultadoDecisaoCep.NenhumCandidato => CepSituacao.NaoEncontrado,
        ResultadoDecisaoCep.FonteIndisponivel => CepSituacao.FonteIndisponivel,
        _ => CepSituacao.NaoConferido
    };

    /// <summary>O CEP do endereço, só dígitos (o que foi conferido; nunca muda).</summary>
    public string CepInformado { get; }

    /// <summary>De onde veio a informação que levou à decisão (a busca, nos casos 4 a 6).</summary>
    public CepFonte? Fonte { get; }

    /// <summary>O que a fonte disse do CEP informado (evidência nos casos 1 e 2).</summary>
    public RegistroCep? RegistroConsultado { get; }

    /// <summary>Só no caso 4 (um candidato): o CEP sugerido, só dígitos. Nos demais, nulo: o motor não escolhe nem inventa.</summary>
    public string? CepSugerido { get; }

    /// <summary>Os CEPs compatíveis achados pela busca (casos 4 e 5), em ordem de CEP.</summary>
    public IReadOnlyList<RegistroCep> Candidatos { get; }

    /// <summary>Por que o motor decidiu assim, em texto para o usuário.</summary>
    public IReadOnlyList<string> Motivos { get; }

    /// <summary>Caso 3: o CEP não existe e a busca pelo endereço ainda não foi feita (ou não pôde ser feita).</summary>
    public bool DeveBuscarPorEndereco { get; }
}
