using Lone.Domain.Enderecos.ConferenciaCep;
using CepValor = Lone.Domain.ObjetosDeValor.Cep;

namespace Lone.Application.Integracoes.ConferenciaCep;

/// <summary>
/// Política da reconferência em lote (F6), separada do TTL do cache. Os valores de ritmo são política do Lone,
/// conservadora: o ViaCEP e a BrasilAPI não publicam limite de requisições, e nenhum limite é atribuído a eles.
/// </summary>
public static class PoliticaReconferenciaCep
{
    /// <summary>"Conferido" mais antigo que isto merece reconferência. Idade não é divergência: só entra na seleção.</summary>
    public static readonly TimeSpan IdadeParaReconferir = TimeSpan.FromDays(180);

    /// <summary>No máximo duas consultas ao mesmo tempo.</summary>
    public const int Paralelismo = 2;

    /// <summary>Intervalo entre o início de duas consultas do mesmo bloco.</summary>
    public static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(1);

    /// <summary>Com a fonte fora do ar o intervalo dobra a cada falha até este teto; volta ao normal no primeiro sucesso.</summary>
    public static readonly TimeSpan IntervaloMaximo = TimeSpan.FromSeconds(16);

    /// <summary>Endereços por chamada à API (cada chamada termina bem antes do tempo que o aplicativo espera).</summary>
    public const int ItensPorChamada = 10;

    /// <summary>Máximo de endereços numa seleção (lotes maiores: rodar de novo, a seleção pega os que faltam).</summary>
    public const int LimiteSelecao = 1000;

    /// <summary>Acima disto a tela pede confirmação explícita antes de começar.</summary>
    public const int ConfirmarAcimaDe = 50;

    /// <summary>
    /// Tempo máximo de uma chamada de processamento no servidor (o aplicativo espera até 30 s). Ao fim dele nada mais começa e
    /// a consulta em andamento é interrompida sem gravar; esses endereços voltam como "adiados" e a tela os reenvia.
    /// </summary>
    public static readonly TimeSpan OrcamentoPorChamada = TimeSpan.FromSeconds(20);

    /// <summary>Quantas vezes a tela reenvia um endereço adiado antes de dá-lo como não processado.</summary>
    public const int MaximoAdiamentos = 2;
}

/// <summary>
/// G (ajuste da F6): o rastro de uma execução da reconferência na Auditoria, um evento por execução, só com metadados
/// operacionais (quem, quando, filtro geral, quantidades, duração). Sem lista de pessoas ou endereços; não repete ConsultasCep.
/// </summary>
public sealed record ExecucaoReconferenciaCep(Guid ExecucaoId, string Usuario, DateTime Em, string Descricao);

public interface IRegistroExecucoesReconferenciaCep
{
    /// <summary>Grava o evento da execução. Falso = já havia evento desta execução (repetir não duplica).</summary>
    Task<bool> RegistrarAsync(ExecucaoReconferenciaCep execucao, CancellationToken ct = default);
}

/// <summary>O texto do evento da execução (até 500 caracteres, o tamanho da descrição na Auditoria).</summary>
public static class EventoReconferenciaCep
{
    /// <summary>Entidade e raiz do evento na Auditoria (o histórico da execução fica sob este nome e o id da execução).</summary>
    public const string Entidade = "ReconferenciaCep";

    public const int MaximoQuantidade = 1_000_000;

    public static string Descricao(bool cancelada, int total, int conferidos, int divergentes, int naoEncontrados, int indisponiveis,
                                   int alterados, int naoProcessados, int duracaoSegundos, bool naoConferidos, bool divergentesFiltro,
                                   bool naoEncontradosFiltro, bool conferidosAntigos, string? uf, int? municipioId)
    {
        static int Q(int n) => Math.Clamp(n, 0, MaximoQuantidade);
        var situacoes = new List<string>();
        if (naoConferidos) situacoes.Add("não conferidos");
        if (divergentesFiltro) situacoes.Add("divergentes");
        if (naoEncontradosFiltro) situacoes.Add("não encontrados");
        if (conferidosAntigos) situacoes.Add($"conferidos há mais de {PoliticaReconferenciaCep.IdadeParaReconferir.TotalDays:0} dias");
        var filtro = (situacoes.Count == 0 ? "nenhuma situação" : string.Join(", ", situacoes))
                     + (string.IsNullOrWhiteSpace(uf) ? string.Empty : $"; UF {uf.Trim().ToUpperInvariant()}")
                     + (municipioId is { } m ? $"; município {m}" : string.Empty);
        var d = TimeSpan.FromSeconds(Q(duracaoSegundos));
        var texto = $"Reconferência de CEPs em lote {(cancelada ? "cancelada pelo usuário" : "concluída")}: {Q(total)} endereço(s); "
                    + $"{Q(conferidos)} conferido(s), {Q(divergentes)} com divergência, {Q(naoEncontrados)} não encontrado(s), "
                    + $"{Q(indisponiveis)} não consultado(s) agora, {Q(alterados)} alterado(s) durante, {Q(naoProcessados)} não processado(s). "
                    + $"Filtro: {filtro}. Duração: {(int)d.TotalMinutes} min {d.Seconds:00} s. Quantidades somadas pela tela a partir dos blocos.";
        return texto.Length <= 500 ? texto : texto[..500];
    }
}

/// <summary>Quais endereços reconferir (só ativos, no Brasil, de cadastros não arquivados; nunca consolidados).</summary>
public sealed record CriterioReconferenciaCep(bool NaoConferidos = true, bool Divergentes = true, bool NaoEncontrados = true,
                                              bool ConferidosAntigos = true, string? Uf = null, int? MunicipioId = null,
                                              int Limite = PoliticaReconferenciaCep.LimiteSelecao);

/// <summary>A seleção: quantos atendem, os ids (até o limite, em ordem previsível) e quantos ficaram de fora sem CEP válido.</summary>
public sealed record SelecaoReconferenciaCep(int Total, IReadOnlyList<Guid> Enderecos, int SemCepValido)
{
    public bool Truncada => Total > Enderecos.Count;
}

/// <summary>
/// Um endereço como está gravado agora (lido no momento do processamento). Os valores exatos servem de trava: a
/// gravação só acontece se o endereço continuar exatamente assim.
/// </summary>
public sealed record EnderecoReconferivel(Guid EnderecoId, Guid PessoaId, int PessoaCodigo, string PessoaNome, string? Cep,
                                          string Logradouro, string? Numero, string? Bairro, string Cidade, string? Uf,
                                          int? MunicipioId, string? CodigoMunicipioIbge, bool Ativo, bool EhBrasil, CepSituacao Situacao)
{
    /// <summary>Os dados da conferência (o município pelo Id, como na ficha e no Salvar).</summary>
    public EnderecoConferenciaCep ParaConferencia() => new(Cep, Logradouro, Numero, Bairro, Cidade, Uf,
        MunicipioId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? CodigoMunicipioIbge);

    public string Resumo => string.Join(", ", new[] { Logradouro, Numero }.Where(s => !string.IsNullOrWhiteSpace(s)))
                            + (string.IsNullOrWhiteSpace(Cidade) ? string.Empty : $" - {Cidade}/{Uf}");
}

/// <summary>Leitura e gravação da reconferência (F6). Gravar mexe só nas três colunas de conferência.</summary>
public interface IReconferenciaCepRepositorio
{
    Task<SelecaoReconferenciaCep> SelecionarAsync(CriterioReconferenciaCep criterio, DateTime agora, CancellationToken ct = default);

    /// <summary>Os endereços como estão agora (os que não existem mais não voltam).</summary>
    Task<IReadOnlyList<EnderecoReconferivel>> ObterAsync(IReadOnlyCollection<Guid> enderecos, CancellationToken ct = default);

    /// <summary>
    /// Grava situação, fonte e data só se o endereço continua exatamente como <paramref name="lido"/> (uma instrução, sem
    /// transação longa). Falso = mudou no meio: nada é gravado.
    /// </summary>
    Task<bool> GravarAsync(EnderecoReconferivel lido, ConferenciaCepRealizada conferencia, CancellationToken ct = default);
}

/// <summary>Manutenção do histórico técnico (ConsultasCep): só ele; nunca CacheCep, endereço ou auditoria.</summary>
public interface IManutencaoConsultasCep
{
    /// <summary>Apaga, em lotes, as linhas anteriores a <paramref name="limite"/>. Idempotente e cancelável.</summary>
    Task<int> LimparAsync(DateTime limite, int tamanhoLote, CancellationToken ct = default);
}

/// <summary>O resultado de um endereço do lote (com o que explica a revisão humana).</summary>
public sealed record ItemReconferenciaCep(Guid EnderecoId, EnderecoReconferivel? Endereco, ResultadoItemReconferencia Resultado,
                                          DecisaoCep? Decisao, string Detalhe);

/// <summary>O bloco processado: um resultado por endereço pedido, na ordem pedida, e a duração.</summary>
public sealed record ResumoReconferenciaCep(IReadOnlyList<ItemReconferenciaCep> Itens, TimeSpan Duracao)
{
    public int Total => Itens.Count;
    public int Quantos(ResultadoItemReconferencia r) => Itens.Count(i => i.Resultado == r);
}

/// <summary>
/// Reconferência em lote (F6): detecção e acompanhamento, nunca correção. Orquestra o mesmo serviço de conferência (mesmas
/// fontes, cache e <see cref="MotorCep"/>); não tem regra própria de endereço. Para cada endereço:
/// <list type="number">
/// <item>lê como está gravado agora;</item>
/// <item>confere (paralelismo e ritmo da <see cref="PoliticaReconferenciaCep"/>, com espera maior quando a fonte cai);</item>
/// <item>resultado com conclusão: grava situação, fonte e data <b>só se o endereço não mudou</b>; fonte indisponível
/// (com ou sem informação anterior) não grava nada;</item>
/// <item>nunca altera CEP, UF, município, logradouro, bairro, número ou complemento; candidatos só são mostrados.</item>
/// </list>
/// Cada endereço tem resultado próprio: a falha de um não derruba o bloco. Cancelado: não começa os seguintes, os que
/// estavam em andamento terminam sem gravar e os já concluídos ficam.
/// </summary>
public sealed class ServicoReconferenciaCep
{
    private readonly IReconferenciaCepRepositorio _repositorio;
    private readonly IServicoConferenciaCep _conferencia;
    private readonly IManutencaoConsultasCep _manutencao;
    private readonly TimeProvider _relogio;
    private readonly Func<TimeSpan, CancellationToken, Task> _esperar;
    private readonly TimeSpan _intervalo;
    private readonly int _paralelismo;

    public ServicoReconferenciaCep(IReconferenciaCepRepositorio repositorio, IServicoConferenciaCep conferencia,
                                   IManutencaoConsultasCep manutencao, TimeProvider relogio)
        : this(repositorio, conferencia, manutencao, relogio, esperar: null, PoliticaReconferenciaCep.Intervalo,
               PoliticaReconferenciaCep.Paralelismo)
    {
    }

    /// <summary>Para testes: a espera entre consultas, o intervalo e o paralelismo podem ser trocados.</summary>
    public ServicoReconferenciaCep(IReconferenciaCepRepositorio repositorio, IServicoConferenciaCep conferencia,
                                   IManutencaoConsultasCep manutencao, TimeProvider relogio,
                                   Func<TimeSpan, CancellationToken, Task>? esperar, TimeSpan intervalo, int paralelismo)
    {
        _repositorio = repositorio;
        _conferencia = conferencia;
        _manutencao = manutencao;
        _relogio = relogio;
        _esperar = esperar ?? ((t, ct) => Task.Delay(t, relogio, ct));
        _intervalo = intervalo;
        _paralelismo = Math.Max(1, paralelismo);
    }

    public Task<SelecaoReconferenciaCep> SelecionarAsync(CriterioReconferenciaCep criterio, CancellationToken ct = default) =>
        _repositorio.SelecionarAsync(criterio with { Limite = Math.Clamp(criterio.Limite, 0, PoliticaReconferenciaCep.LimiteSelecao) },
            _relogio.GetUtcNow().UtcDateTime, ct);

    /// <summary>Processa um bloco (até <see cref="PoliticaReconferenciaCep.ItensPorChamada"/> endereços). Cancelar devolve o parcial.</summary>
    public async Task<ResumoReconferenciaCep> ProcessarAsync(IReadOnlyList<Guid> enderecos, CancellationToken ct = default)
    {
        var inicio = _relogio.GetTimestamp();
        var pedidos = enderecos.Distinct().Take(PoliticaReconferenciaCep.ItensPorChamada).ToList();
        var resultados = new ItemReconferenciaCep?[pedidos.Count];
        IReadOnlyList<EnderecoReconferivel> lidos;
        try
        {
            lidos = await _repositorio.ObterAsync(pedidos, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return Resumo(pedidos, resultados, inicio, adiar: false);
        }
        var porId = lidos.ToDictionary(e => e.EnderecoId);

        // Orçamento da chamada: ao fim dele não começa mais nada e o que estiver consultando é interrompido (sem gravar) e
        // volta como "adiado" para o próximo bloco. Assim a chamada termina antes do tempo que o aplicativo espera, mesmo
        // com a fonte lenta (a resiliência pode levar até 12 s por fonte) e o backoff no teto.
        using var prazo = new CancellationTokenSource(PoliticaReconferenciaCep.OrcamentoPorChamada, _relogio);
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct, prazo.Token);
        var token = limite.Token;

        using var vagas = new SemaphoreSlim(_paralelismo);
        var andamento = new List<Task>();
        var intervalo = _intervalo;
        var trava = new object();
        for (var i = 0; i < pedidos.Count; i++)
        {
            var indice = i;
            if (!porId.TryGetValue(pedidos[i], out var lido))
            {
                resultados[i] = new(pedidos[i], null, ResultadoItemReconferencia.NaoProcessado, null, "O endereço não existe mais.");
                continue;
            }
            if (Motivo(lido) is { } motivo)
            {
                resultados[i] = new(pedidos[i], lido, ResultadoItemReconferencia.NaoProcessado, null, motivo);
                continue;
            }
            try
            {
                // No máximo N ao mesmo tempo; depois de conseguir a vaga, espera o intervalo entre os inícios. O intervalo é lido
                // só depois da vaga para já refletir quem acabou de terminar (maior enquanto a fonte estiver fora do ar).
                await vagas.WaitAsync(token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break; // cancelado ou sem tempo: os seguintes não começam
            }
            try
            {
                if (andamento.Count > 0)
                {
                    TimeSpan espera;
                    lock (trava) espera = intervalo;
                    await _esperar(espera, token);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                vagas.Release();
                break;
            }
            // A vaga pode ser entregue no mesmo instante do cancelamento (SemaphoreSlim não garante a ordem): conferir antes
            // de começar, para nenhum endereço novo começar depois de "Cancelar" ou do fim do orçamento.
            if (token.IsCancellationRequested)
            {
                vagas.Release();
                break;
            }
            andamento.Add(Task.Run(async () =>
            {
                try
                {
                    var item = await ProcessarUmAsync(lido, token, ct);
                    resultados[indice] = item;
                    lock (trava)
                        intervalo = item.Resultado == ResultadoItemReconferencia.Indisponivel
                            ? TimeSpan.FromTicks(Math.Min(PoliticaReconferenciaCep.IntervaloMaximo.Ticks, Math.Max(intervalo.Ticks, 1) * 2))
                            : _intervalo;
                }
                finally
                {
                    vagas.Release();
                }
            }, CancellationToken.None));
        }
        await Task.WhenAll(andamento);
        // Os que não começaram: cancelados pelo usuário → não processados; sem tempo → adiados para o próximo bloco.
        return Resumo(pedidos, resultados, inicio, adiar: !ct.IsCancellationRequested);
    }

    /// <summary>
    /// Um endereço: conferir, decidir se grava, gravar com a trava. Nunca lança: tudo vira resultado. <paramref name="token"/>
    /// junta o cancelamento do usuário (<paramref name="usuario"/>) e o fim do orçamento da chamada.
    /// </summary>
    private async Task<ItemReconferenciaCep> ProcessarUmAsync(EnderecoReconferivel lido, CancellationToken token, CancellationToken usuario)
    {
        try
        {
            var r = await _conferencia.ReconferirAsync(lido.ParaConferencia(), token);
            var d = r.Decisao;
            // Fonte indisponível (com ou sem informação anterior) não é conferência: nada é gravado.
            if (EstadoConferenciaCep.SituacaoPersistida(d.Resultado) is not { } situacao || d.Fonte is not { } fonte)
                return new(lido.EnderecoId, lido, ResultadoItemReconferencia.Indisponivel, d,
                    r.Anterior is null ? "Não foi possível consultar a fonte agora; a situação gravada continua."
                        : "Não foi possível consultar a fonte agora (havia informação anterior, que não conta como conferência); a situação gravada continua.");

            var conferencia = new ConferenciaCepRealizada(situacao, fonte, _relogio.GetUtcNow().UtcDateTime);
            if (!await _repositorio.GravarAsync(lido, conferencia, CancellationToken.None))
                return new(lido.EnderecoId, lido, ResultadoItemReconferencia.AlteradoDuranteAReconferencia, d,
                    "O endereço foi alterado durante a reconferência; o resultado não foi gravado.");

            return new(lido.EnderecoId, lido, situacao switch
            {
                CepSituacao.Conferido => ResultadoItemReconferencia.Conferido,
                CepSituacao.Divergente => ResultadoItemReconferencia.Divergente,
                _ => ResultadoItemReconferencia.NaoEncontrado
            }, d, string.Join(" ", d.Motivos));
        }
        catch (OperationCanceledException) when (usuario.IsCancellationRequested)
        {
            return new(lido.EnderecoId, lido, ResultadoItemReconferencia.Cancelado, null, "Cancelado em andamento; nada foi gravado.");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return new(lido.EnderecoId, lido, ResultadoItemReconferencia.Adiado, null, TextoAdiado);
        }
        catch (Exception)
        {
            // Falha de um endereço não derruba o bloco nem vira conclusão postal (as consultas já ficam no histórico técnico).
            return new(lido.EnderecoId, lido, ResultadoItemReconferencia.Indisponivel, null, "Falha técnica; a situação gravada continua.");
        }
    }

    private const string TextoAdiado = "Adiado: o tempo desta chamada acabou antes da consulta; fica para o próximo bloco. Nada foi gravado.";

    /// <summary>Não processável: inativo, exterior ou sem CEP válido (a conferência exige CEP de 8 dígitos).</summary>
    private static string? Motivo(EnderecoReconferivel e) =>
        !e.Ativo ? "Endereço inativo."
        : !e.EhBrasil ? "Endereço no exterior."
        : !CepValor.EhValido(e.Cep) ? "Sem CEP válido (8 dígitos)."
        : null;

    private ResumoReconferenciaCep Resumo(List<Guid> pedidos, ItemReconferenciaCep?[] resultados, long inicio, bool adiar) =>
        new(pedidos.Select((id, i) => resultados[i] ?? (adiar
                ? new ItemReconferenciaCep(id, null, ResultadoItemReconferencia.Adiado, null, TextoAdiado)
                : new ItemReconferenciaCep(id, null, ResultadoItemReconferencia.NaoProcessado, null,
                    "Não processado (lote cancelado antes de começar)."))).ToList(),
            _relogio.GetElapsedTime(inicio));

    /// <summary>Limpa o histórico técnico mais antigo que a retenção (padrão 90 dias), em lotes.</summary>
    public Task<int> LimparHistoricoAsync(CancellationToken ct = default) =>
        _manutencao.LimparAsync(_relogio.GetUtcNow().UtcDateTime - PoliticaCachePostalCep.RetencaoRecomendadaHistorico,
            TamanhoLoteLimpeza, ct);

    public const int TamanhoLoteLimpeza = 1000;
}
