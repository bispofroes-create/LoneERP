using Lone.Domain.Enderecos.ConferenciaCep;

namespace Lone.Contracts.Integracoes;

/// <summary>Pedido de conferência do CEP de um endereço (POST consultas/cep/conferir). Nada é gravado.</summary>
public sealed class ConferirCepRequisicao
{
    public string? Cep { get; set; }
    public string? Logradouro { get; set; }
    public string? Numero { get; set; }
    public string? Bairro { get; set; }

    /// <summary>Nome do município.</summary>
    public string? Cidade { get; set; }
    public string? Uf { get; set; }
    public string? CodigoMunicipioIbge { get; set; }
}

/// <summary>
/// Pedido de busca de CEP pelo endereço quando o usuário não sabe o CEP (POST consultas/cep/buscar-por-endereco). Não tem
/// CEP. Nada é gravado; a resposta é uma <see cref="DecisaoCepDto"/> com <c>CepInformado</c> vazio e os candidatos.
/// </summary>
public sealed class BuscarCepPorEnderecoRequisicao
{
    public string? Logradouro { get; set; }
    public string? Numero { get; set; }
    public string? Bairro { get; set; }

    /// <summary>Nome do município.</summary>
    public string? Cidade { get; set; }
    public string? Uf { get; set; }
    public string? CodigoMunicipioIbge { get; set; }
}

/// <summary>
/// A decisão do motor de CEP, para a tela. É só resultado: o endereço não foi alterado. A sugestão (caso 4) é aplicada
/// pelo usuário na ficha como alteração não salva; vários candidatos são para escolha; nenhum candidato não traz CEP.
/// Fonte indisponível é um resultado (200), não erro: o endereço não fica inválido por isso.
/// </summary>
public sealed class DecisaoCepDto
{
    public ResultadoDecisaoCep Resultado { get; set; }
    public CepSituacao Situacao { get; set; }
    public string CepInformado { get; set; } = string.Empty;

    /// <summary>Só com um único candidato compatível (caso 4).</summary>
    public string? CepSugerido { get; set; }

    /// <summary>De onde veio a informação que levou à decisão.</summary>
    public CepFonte? Fonte { get; set; }

    /// <summary>O que a fonte disse do CEP informado (conferido ou divergente).</summary>
    public CandidatoCepDto? RegistroConsultado { get; set; }

    /// <summary>CEPs compatíveis achados pela busca (casos 4 e 5), em ordem de CEP.</summary>
    public List<CandidatoCepDto> Candidatos { get; set; } = new();
    public List<string> Motivos { get; set; } = new();

    /// <summary>
    /// Avisos sobre a consulta externa, além da decisão (ex.: a busca bateu no limite da fonte e a lista de candidatos pode
    /// estar incompleta).
    /// </summary>
    public List<string> Avisos { get; set; } = new();

    /// <summary>CEP inexistente e a busca pelo endereço não foi feita (faltam dados) ou não pôde ser feita.</summary>
    public bool DeveBuscarPorEndereco { get; set; }

    /// <summary>
    /// O CEP informado componente a componente (CEP, UF, município, logradouro, número), da mesma avaliação que deu o
    /// <see cref="Resultado"/>. Adicional: cliente antigo pode ignorar; resposta antiga chega vazia.
    /// </summary>
    public List<ComponenteCepDto> Componentes { get; set; } = new();

    /// <summary>
    /// F3, offline: a fonte não respondeu (<see cref="Resultado"/> = FonteIndisponivel) e havia informação anterior do CEP.
    /// Não é confirmação atual; mostrada como "última informação disponível", com a fonte e a data originais.
    /// </summary>
    public InformacaoAnteriorCepDto? InformacaoAnterior { get; set; }
}

/// <summary>A última informação guardada sobre o CEP (F3), e o que o motor diz do endereço com base nela.</summary>
public sealed class InformacaoAnteriorCepDto
{
    /// <summary>Fonte original (ex.: ViaCEP), nunca "cache".</summary>
    public CepFonte Fonte { get; set; }

    /// <summary>Quando a fonte respondeu (UTC).</summary>
    public DateTime ConsultadoEm { get; set; }

    public CandidatoCepDto Registro { get; set; } = new();

    /// <summary>A decisão com os dados de então (Conferido ou Divergente). Não vira estado gravado.</summary>
    public ResultadoDecisaoCep Resultado { get; set; }
    public List<string> Motivos { get; set; } = new();
}

/// <summary>Um componente da conferência: o estado (para a tela decidir ícone e cor) e o motivo em texto.</summary>
public sealed class ComponenteCepDto
{
    public ComponenteCep Componente { get; set; }
    public SituacaoComponenteCep Situacao { get; set; }
    public string Motivo { get; set; } = string.Empty;
}

/// <summary>Um CEP como a fonte o descreve (faixa = texto da numeração, ex.: "até 999/1000").</summary>
public sealed class CandidatoCepDto
{
    public string Cep { get; set; } = string.Empty;
    public string? Logradouro { get; set; }
    public string? Faixa { get; set; }
    public string? Bairro { get; set; }
    public string? Cidade { get; set; }
    public string? Uf { get; set; }
    public string? CodigoMunicipioIbge { get; set; }

    /// <summary>Nome do prédio/grande usuário de um CEP específico (ViaCEP <c>unidade</c>). Só explicação.</summary>
    public string? Unidade { get; set; }

    /// <summary>
    /// Por que este CEP ficou na lista (UF, município, logradouro, número), da mesma avaliação do filtro. Nunca há
    /// componente divergente aqui (quem diverge sai) e não há pontuação. Vazio no registro consultado.
    /// </summary>
    public List<ComponenteCepDto> Componentes { get; set; } = new();
}

/// <summary>
/// Marca, no endereço enviado para gravar, de que o CEP veio de uma sugestão da conferência (DM3). É só contexto para a
/// auditoria: a API confere a coerência (CEP salvo = sugerido, e a sugestão foi emitida por ela) e, se não bater, ignora
/// a marca. Nunca autoriza nada: o CEP é gravado pelas regras normais do Salvar.
/// </summary>
public sealed class SugestaoCepAplicadaDto
{
    /// <summary>
    /// O CEP que foi conferido (o que estava no endereço quando a sugestão saiu). Vazio = o CEP veio da busca pelo endereço
    /// sem CEP (Checkpoint D); a API confere que essa busca o emitiu.
    /// </summary>
    public string CepConferido { get; set; } = string.Empty;
    public string CepSugerido { get; set; } = string.Empty;
    public CepFonte Fonte { get; set; }
}

/// <summary>Checkpoint G: pedido de segunda opinião sobre um CEP (POST consultas/cep/segunda-opiniao). Nada é gravado.</summary>
public sealed class SegundaOpiniaoCepRequisicao
{
    public string? Cep { get; set; }
}

/// <summary>
/// A segunda opinião: o que cada fonte informa e se concordam. Transitória (não é gravada no endereço). Nenhuma fonte é
/// tratada como certa; nada é escolhido nem aplicado.
/// </summary>
public sealed class SegundaOpiniaoCepDto
{
    public string Cep { get; set; } = string.Empty;
    public ResultadoSegundaOpiniaoCep Resultado { get; set; }

    /// <summary>A fonte da resposta usada pela conferência (só informada; não é "vencedora").</summary>
    public CepFonte? FontePrincipal { get; set; }
    public CepFonte? SegundaFonte { get; set; }
    public string Mensagem { get; set; } = string.Empty;
    public List<ComparacaoComponenteFontesDto> Componentes { get; set; } = new();
}

public sealed class ComparacaoComponenteFontesDto
{
    public ComponenteComparacaoFontes Componente { get; set; }
    public SituacaoComparacaoFontes Situacao { get; set; }
    public string? ValorPrincipal { get; set; }
    public string? ValorSegunda { get; set; }
    public string Motivo { get; set; } = string.Empty;
}
