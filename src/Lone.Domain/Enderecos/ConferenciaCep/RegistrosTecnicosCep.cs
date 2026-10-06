using Lone.Domain.Auditoria;

namespace Lone.Domain.Enderecos.ConferenciaCep;

/// <summary>O que foi consultado na fonte: um CEP ou uma busca pelo endereço (UF + cidade + logradouro).</summary>
public enum TipoConsultaCep : byte
{
    PorCep = 1,
    PorEndereco = 2
}

/// <summary>Resultado de uma resposta guardada no cache postal: só resultados funcionais (falha técnica nunca entra).</summary>
public enum SituacaoCachePostal : byte
{
    Encontrado = 1,
    NaoEncontrado = 2
}

/// <summary>
/// Cache postal persistente (F3): a resposta de uma fonte para um CEP ou para uma busca pelo endereço, com a fonte
/// <b>original</b> e a hora da consulta. Não é fonte da verdade nem terceira fonte: é informação obtida antes. Não guarda
/// nada da pessoa (nome, documento, número da casa, complemento, observações): a chave é o CEP ou UF + cidade +
/// logradouro normalizados, os mesmos da consulta externa. Fora da auditoria de cadastro (controle técnico).
/// </summary>
[NaoAuditar]
public class CacheCep
{
    /// <summary>"cep:01310100" ou "busca:SP|SAO PAULO|AVENIDA PAULISTA" (a mesma chave do cache em memória).</summary>
    public string Chave { get; set; } = string.Empty;

    public TipoConsultaCep Tipo { get; set; }

    /// <summary>O CEP consultado (só na consulta por CEP).</summary>
    public string? Cep { get; set; }

    public SituacaoCachePostal Situacao { get; set; }

    /// <summary>Fonte original da resposta (ViaCEP, BrasilAPI). Nunca "cache".</summary>
    public CepFonte Fonte { get; set; }

    /// <summary>Quando a fonte respondeu (UTC). Ler o cache não muda.</summary>
    public DateTime ConsultadoEm { get; set; }

    /// <summary>Até quando vale como resposta atual (mesma política do cache em memória).</summary>
    public DateTime ExpiraEm { get; set; }

    /// <summary>
    /// Até quando pode ser mostrada como "última informação disponível" se a fonte estiver fora do ar. Só CEP encontrado;
    /// nulo para inexistente e para busca (informação negativa ou lista limitada nunca vira resposta offline).
    /// </summary>
    public DateTime? UtilizavelAte { get; set; }

    /// <summary>A busca veio no limite da fonte (ViaCEP: 50): a lista guardada continua possivelmente incompleta.</summary>
    public bool LimiteAtingido { get; set; }

    /// <summary>Os CEPs da resposta (dados postais, com <c>unidade</c> quando houver), em JSON.</summary>
    public string Registros { get; set; } = "[]";
}

/// <summary>Operação que gerou a consulta registrada no histórico técnico.</summary>
public enum OperacaoConsultaCep : byte
{
    /// <summary>Consulta antiga ao digitar o CEP (preenche o endereço).</summary>
    ConsultaDireta = 1,
    Conferencia = 2,
    BuscaPorEndereco = 3,
    /// <summary>F6: reconferência em lote (manual, controlada).</summary>
    ReconferenciaLote = 4,
    /// <summary>G: segunda opinião pedida pelo usuário (a consulta principal e a da outra fonte).</summary>
    SegundaOpiniao = 5
}

/// <summary>
/// F6: o que aconteceu com um endereço na reconferência em lote. Só os três primeiros gravam estado (situação, fonte e
/// data); nenhum altera o endereço.
/// </summary>
public enum ResultadoItemReconferencia : byte
{
    Conferido = 1,
    Divergente = 2,
    /// <summary>O CEP gravado não existe na fonte (com ou sem candidatos, que só são mostrados).</summary>
    NaoEncontrado = 3,
    /// <summary>A fonte não respondeu: a situação gravada fica como estava.</summary>
    Indisponivel = 4,
    /// <summary>O endereço mudou durante a reconferência: o resultado (de outros dados) não foi gravado.</summary>
    AlteradoDuranteAReconferencia = 5,
    /// <summary>Não processado: cancelado antes de começar, removido, inativo, no exterior ou sem CEP válido.</summary>
    NaoProcessado = 6,
    /// <summary>Estava em andamento quando o lote foi cancelado: nada foi gravado.</summary>
    Cancelado = 7,
    /// <summary>O tempo da chamada acabou antes de consultar (ou durante a consulta): nada foi gravado; vai no próximo bloco.</summary>
    Adiado = 8
}

/// <summary>Resultado técnico de uma consulta (falha categorizada; sem texto de exceção).</summary>
public enum ResultadoConsultaCep : byte
{
    Encontrado = 1,
    NaoEncontrado = 2,
    /// <summary>Fonte fora do ar, tempo esgotado, rede, disjuntor aberto.</summary>
    Indisponivel = 3,
    /// <summary>A fonte respondeu fora do contrato.</summary>
    RespostaInvalida = 4,
    /// <summary>Quem pediu cancelou (não é falha da fonte).</summary>
    Cancelado = 5
}

/// <summary>De onde saiu a resposta registrada.</summary>
public enum OrigemRespostaCep : byte
{
    /// <summary>Chamada à fonte externa.</summary>
    Fonte = 1,
    CacheMemoria = 2,
    CachePersistente = 3,
    /// <summary>Fonte fora do ar; mostrada a última informação guardada (identificada como anterior).</summary>
    InformacaoAnterior = 4,
    /// <summary>G: chamada à outra fonte para a segunda opinião (não vai para o cache).</summary>
    SegundaFonte = 5
}

/// <summary>
/// Histórico técnico das consultas de CEP (F3): diagnóstico, suporte e métricas. Mínimo de dados: operação, chave postal
/// (CEP ou UF + cidade + logradouro normalizados), fonte, hora, duração, resultado técnico, de onde veio a resposta e o
/// limite. Sem pessoa, sem número da casa, sem bairro, sem complemento, sem texto de exceção. Feito para retenção
/// (índice por data); a limpeza não existe ainda (recomendação: 90 dias). Fora da auditoria de cadastro.
/// </summary>
[NaoAuditar]
public class ConsultaCep
{
    public Guid Id { get; set; }
    public OperacaoConsultaCep Operacao { get; set; }
    public TipoConsultaCep Tipo { get; set; }

    /// <summary>A mesma chave do cache ("cep:..." ou "busca:UF|CIDADE|LOGRADOURO").</summary>
    public string Chave { get; set; } = string.Empty;

    public string? Cep { get; set; }

    /// <summary>Fonte chamada (ou a original, quando veio de cache). Nula se nenhuma fonte foi usada.</summary>
    public CepFonte? Fonte { get; set; }

    public DateTime OcorridoEm { get; set; }
    public int DuracaoMs { get; set; }
    public ResultadoConsultaCep Resultado { get; set; }
    public OrigemRespostaCep Origem { get; set; }

    /// <summary>Só na busca: a lista veio no limite da fonte.</summary>
    public bool? LimiteAtingido { get; set; }
}
