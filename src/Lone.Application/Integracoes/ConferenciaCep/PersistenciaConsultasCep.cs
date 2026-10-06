using System.Collections.Concurrent;
using Lone.Domain.Enderecos.ConferenciaCep;

namespace Lone.Application.Integracoes.ConferenciaCep;

/// <summary>
/// Política do cache postal persistente (F3). As validades de resposta atual são as do cache em memória (DM2, sem
/// mudança): encontrado 24 h, inexistente 1 h, busca 1 h. A única novidade é o uso offline: um CEP <b>encontrado</b> pode
/// ser mostrado como "última informação disponível" por até 30 dias quando a fonte estiver fora do ar. Informação
/// negativa (inexistente) e busca (lista possivelmente limitada) nunca são usadas offline.
/// </summary>
public static class PoliticaCachePostalCep
{
    public static readonly TimeSpan ValidadeEncontrado = CacheConferenciaCep.ValidadeEncontrado;
    public static readonly TimeSpan ValidadeInexistente = CacheConferenciaCep.ValidadeInexistente;
    public static readonly TimeSpan ValidadeBusca = CacheConferenciaCep.ValidadeBusca;

    /// <summary>Janela em que um CEP encontrado pode ser mostrado, identificado como anterior, com a fonte fora do ar.</summary>
    public static readonly TimeSpan UsoOfflineEncontrado = TimeSpan.FromDays(30);

    /// <summary>Retenção recomendada do histórico técnico (a limpeza fica para quando houver rotina; não é decisão legal).</summary>
    public static readonly TimeSpan RetencaoRecomendadaHistorico = TimeSpan.FromDays(90);
}

/// <summary>Uma resposta lida do cache postal persistente, com a fonte original e a hora da consulta.</summary>
public sealed record RespostaGuardadaCep(SituacaoCachePostal Situacao, CepFonte Fonte, IReadOnlyList<RegistroCep> Registros,
                                         bool LimiteAtingido, DateTime ConsultadoEm, DateTime ExpiraEm, DateTime? UtilizavelAte)
{
    public bool Valida(DateTime agora) => ExpiraEm > agora;
    public bool UtilizavelOffline(DateTime agora) => Situacao == SituacaoCachePostal.Encontrado && UtilizavelAte > agora;
}

/// <summary>
/// Cache postal persistente (F3; tabela CacheCep). Nunca lança: falha do banco não pode virar "fonte indisponível" nem
/// derrubar uma consulta válida (quem implementa registra no log e devolve nulo / segue).
/// </summary>
public interface ICachePostalCep
{
    Task<RespostaGuardadaCep?> ObterAsync(string chave, CancellationToken ct = default);

    /// <summary>Guarda (ou substitui) a resposta funcional da chave. Falha técnica nunca chega aqui.</summary>
    Task GuardarAsync(string chave, TipoConsultaCep tipo, string? cep, RespostaGuardadaCep resposta, CancellationToken ct = default);
}

/// <summary>Uma linha do histórico técnico (sem dados da pessoa).</summary>
public sealed record ConsultaCepOcorrida(OperacaoConsultaCep Operacao, TipoConsultaCep Tipo, string Chave, string? Cep, CepFonte? Fonte,
                                         DateTime OcorridoEm, TimeSpan Duracao, ResultadoConsultaCep Resultado, OrigemRespostaCep Origem,
                                         bool? LimiteAtingido = null);

/// <summary>Histórico técnico das consultas (F3; tabela ConsultasCep). Nunca lança: falhar ao registrar não muda a consulta.</summary>
public interface IHistoricoConsultasCep
{
    Task RegistrarAsync(ConsultaCepOcorrida consulta);
}

/// <summary>Sem persistência (testes e uso sem banco): nada é lido nem gravado.</summary>
public sealed class SemCachePostalCep : ICachePostalCep, IHistoricoConsultasCep
{
    public static readonly SemCachePostalCep Instancia = new();
    public Task<RespostaGuardadaCep?> ObterAsync(string chave, CancellationToken ct = default) => Task.FromResult<RespostaGuardadaCep?>(null);
    public Task GuardarAsync(string chave, TipoConsultaCep tipo, string? cep, RespostaGuardadaCep resposta, CancellationToken ct = default) =>
        Task.CompletedTask;
    public Task RegistrarAsync(ConsultaCepOcorrida consulta) => Task.CompletedTask;
}

/// <summary>
/// Conferências que esta API fez (F3), por 1 h, só em memória, pela assinatura dos dados conferidos
/// (<see cref="EstadoConferenciaCep.Assinatura(EnderecoConferenciaCep)"/>). No Salvar, o estado do endereço só vira
/// "conferido / divergente / não encontrado" se a conferência saiu daqui para exatamente os dados gravados: o aplicativo
/// não consegue inventar uma conferência. Fonte indisponível não é registrada (não é conclusão). Reiniciou a API ou passou
/// a hora: o endereço grava normalmente, sem atualizar o estado (honesto: não houve conferência que o sustente).
/// </summary>
public sealed class ConferenciasCepEmitidas
{
    public static readonly TimeSpan Validade = TimeSpan.FromHours(1);

    private readonly TimeProvider _relogio;
    private readonly ConcurrentDictionary<string, (DateTimeOffset Vence, ConferenciaCepRealizada Conferencia)> _emitidas =
        new(StringComparer.Ordinal);

    public ConferenciasCepEmitidas(TimeProvider relogio) => _relogio = relogio;

    /// <summary>Registra a conferência (se o resultado tem conclusão). Não altera nada.</summary>
    public void Registrar(EnderecoConferenciaCep endereco, DecisaoCep decisao)
    {
        if (EstadoConferenciaCep.SituacaoPersistida(decisao.Resultado) is not { } situacao || decisao.Fonte is not { } fonte) return;
        var agora = _relogio.GetUtcNow();
        _emitidas[EstadoConferenciaCep.Assinatura(endereco)] = (agora + Validade, new ConferenciaCepRealizada(situacao, fonte, agora.UtcDateTime));
        if (_emitidas.Count > CacheConferenciaCep.MaximoItens)
            foreach (var vencida in _emitidas.Where(e => e.Value.Vence <= agora).ToList())
                _emitidas.TryRemove(vencida);
    }

    /// <summary>A conferência feita para exatamente estes dados, ainda válida; nula se não houve.</summary>
    public ConferenciaCepRealizada? Obter(string? assinatura) =>
        assinatura is not null && _emitidas.TryGetValue(assinatura, out var e) && e.Vence > _relogio.GetUtcNow() ? e.Conferencia : null;
}
