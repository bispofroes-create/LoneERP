using Lone.Domain.Enderecos.ConferenciaCep;
using Lone.Domain.Validacao;
using CepValor = Lone.Domain.ObjetosDeValor.Cep;

namespace Lone.Application.Integracoes.ConferenciaCep;

/// <summary>Confere o CEP de um endereço: fontes externas → <see cref="MotorCep"/> (F1). Não grava nada.</summary>
public interface IServicoConferenciaCep
{
    /// <summary>A decisão do motor e os avisos do serviço. Falha técnica não é exceção: vira <see cref="ResultadoDecisaoCep.FonteIndisponivel"/>.</summary>
    Task<ResultadoConferenciaCep> ConferirAsync(EnderecoConferenciaCep endereco, CancellationToken ct = default);

    /// <summary>
    /// Busca de CEP pelo endereço sem CEP (Checkpoint D): candidatos para o usuário escolher, nunca "o CEP certo". Dados
    /// insuficientes = <see cref="ValidacaoException"/>; falha técnica = <see cref="ResultadoDecisaoCep.FonteIndisponivel"/>.
    /// </summary>
    Task<ResultadoConferenciaCep> BuscarPorEnderecoAsync(EnderecoConferenciaCep endereco, CancellationToken ct = default);

    /// <summary>
    /// F6: a mesma conferência (mesmas fontes, cache e motor), registrada no histórico como reconferência em lote. Não
    /// registra conferência para o Salvar da ficha: quem grava o estado é a própria reconferência, com proteção própria.
    /// </summary>
    Task<ResultadoConferenciaCep> ReconferirAsync(EnderecoConferenciaCep endereco, CancellationToken ct = default);

    /// <summary>
    /// Checkpoint G, segunda opinião (só por pedido explícito do usuário): a resposta principal (o mesmo caminho da
    /// conferência: cache, fonte principal, reserva) e a da <b>outra</b> fonte que consulta por CEP, comparadas pelo
    /// <see cref="ComparadorFontesCep"/>. Não escolhe fonte, não altera endereço, não grava estado, não registra sugestão.
    /// </summary>
    Task<ComparacaoFontesCep> ConsultarOutraFonteAsync(string cep, CancellationToken ct = default);
}

/// <summary>
/// A decisão do motor (F1, inalterada) e os avisos que só o serviço conhece, sobre a consulta externa (ex.: a busca bateu
/// no limite da fonte e a lista pode estar incompleta, D-F2-1).
/// </summary>
public sealed record ResultadoConferenciaCep(DecisaoCep Decisao, IReadOnlyList<string> Avisos)
{
    /// <summary>
    /// F3, offline: a fonte não respondeu e havia uma informação anterior guardada para o CEP. A decisão continua
    /// <see cref="ResultadoDecisaoCep.FonteIndisponivel"/> (nada foi confirmado agora); isto é só o que se sabia antes.
    /// </summary>
    public InformacaoAnteriorCep? Anterior { get; init; }
}

/// <summary>
/// A última informação guardada sobre o CEP (cache postal persistente), com a fonte original e a data da consulta, e o que
/// o motor diz do endereço com base nela. Nunca é confirmação atual nem atualiza a data da conferência do endereço.
/// </summary>
public sealed record InformacaoAnteriorCep(RegistroCep Registro, CepFonte Fonte, DateTime ConsultadoEm, DecisaoCep DecisaoComEla);

/// <summary>
/// Orquestra as fontes de CEP (DM2):
/// <list type="bullet">
/// <item>cache transitório primeiro (só resultados funcionais);</item>
/// <item>fontes na ordem de registro (ViaCEP, depois BrasilAPI); passa à próxima <b>só em falha técnica</b>. CEP
/// inexistente encerra: a reserva não "desmente" a resposta;</item>
/// <item>CEP inexistente: busca pelo endereço (só fontes que buscam), com o logradouro normalizado pelo domínio;</item>
/// <item>monta as respostas e chama o <see cref="MotorCep"/>; registra a sugestão e os candidatos emitidos (casos 4
/// e 5) para a gravação poder conferir a procedência (DM3).</item>
/// </list>
/// Não decide regra de CEP (é do motor), não altera endereço, não grava nada. Timeout, nova tentativa e disjuntor são do
/// pipeline HTTP de cada fonte; aqui só o teto total da conferência, para a chamada nunca ficar sem limite.
/// </summary>
public sealed class ServicoConferenciaCep : IServicoConferenciaCep
{
    /// <summary>
    /// Teto da conferência inteira (consulta + reserva + busca), abaixo dos 30 s que o aplicativo espera a API. Estourou:
    /// o que faltava vira "indisponível".
    /// </summary>
    public static readonly TimeSpan TetoTotal = TimeSpan.FromSeconds(25);

    /// <summary>D-F2-1 (parte C): a busca devolveu o máximo da fonte; o CEP certo pode ter ficado de fora.</summary>
    public const string AvisoListaIncompleta =
        "A lista de candidatos pode estar incompleta devido ao limite da consulta externa; informe o número e o bairro.";

    /// <summary>Busca sem CEP com a lista no limite da fonte (D-1: a mesma busca pode devolver outro recorte de 50).</summary>
    public const string AvisoBuscaListaIncompleta =
        "A fonte retornou o limite de resultados: a lista pode estar incompleta. Informe o logradouro completo, o número e o bairro para refinar a busca.";

    /// <summary>Busca sem CEP, sem candidato e com a lista no limite: não achar entre os retornados não prova que não exista.</summary>
    public const string AvisoBuscaSemCandidatoNoLimite =
        "Não encontramos um CEP compatível entre os resultados retornados. A fonte devolveu uma lista limitada, então isso não prova que o CEP não exista: refine o endereço ou tente de novo mais tarde.";

    /// <summary>Nenhuma fonte configurada faz busca por endereço (a BrasilAPI só consulta pelo CEP).</summary>
    public const string AvisoSemFonteDeBusca = "Nenhuma fonte de CEP configurada faz busca pelo endereço.";

    /// <summary>Dados mínimos da busca (<see cref="BuscaEnderecoCep.De"/>, a mesma regra da busca da conferência).</summary>
    public const string MensagemDadosInsuficientes =
        "Para encontrar o CEP pelo endereço, informe a UF, o município e o logradouro (pelo menos 3 letras).";

    private readonly IReadOnlyList<IProvedorCep> _provedores;
    private readonly CacheConferenciaCep _cache;
    private readonly SugestoesCepEmitidas _sugestoes;
    private readonly ConferenciasCepEmitidas _conferencias;
    private readonly TimeProvider _relogio;
    private readonly ICachePostalCep _postal;
    private readonly IHistoricoConsultasCep _historico;

    /// <summary>Sem persistência (F2): só o cache em memória; nada é lido nem gravado no banco.</summary>
    public ServicoConferenciaCep(IEnumerable<IProvedorCep> provedores, CacheConferenciaCep cache, SugestoesCepEmitidas sugestoes,
                                 TimeProvider relogio)
        : this(provedores, cache, sugestoes, new ConferenciasCepEmitidas(relogio), relogio, SemCachePostalCep.Instancia,
               SemCachePostalCep.Instancia)
    {
    }

    /// <summary>
    /// F3: com o cache postal persistente (resposta atual dentro da validade e última informação para o offline), o
    /// histórico técnico e o registro das conferências feitas (base do estado gravado no endereço).
    /// </summary>
    public ServicoConferenciaCep(IEnumerable<IProvedorCep> provedores, CacheConferenciaCep cache, SugestoesCepEmitidas sugestoes,
                                 ConferenciasCepEmitidas conferencias, TimeProvider relogio, ICachePostalCep postal,
                                 IHistoricoConsultasCep historico)
    {
        _provedores = provedores.ToList();
        _cache = cache;
        _sugestoes = sugestoes;
        _conferencias = conferencias;
        _relogio = relogio;
        _postal = postal;
        _historico = historico;
    }

    public Task<ResultadoConferenciaCep> ConferirAsync(EnderecoConferenciaCep endereco, CancellationToken ct = default) =>
        ConferirAsync(endereco, OperacaoConsultaCep.Conferencia, registrarParaOSalvar: true, ct);

    public Task<ResultadoConferenciaCep> ReconferirAsync(EnderecoConferenciaCep endereco, CancellationToken ct = default) =>
        ConferirAsync(endereco, OperacaoConsultaCep.ReconferenciaLote, registrarParaOSalvar: false, ct);

    private async Task<ResultadoConferenciaCep> ConferirAsync(EnderecoConferenciaCep endereco, OperacaoConsultaCep operacao,
                                                              bool registrarParaOSalvar, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(endereco);
        if (!CepValor.TentarCriar(endereco.Cep, out var cep))
            throw new ValidacaoException(["O CEP deve ter 8 dígitos."]);

        using var limite = new CancellationTokenSource(TetoTotal, _relogio);
        using var ligado = CancellationTokenSource.CreateLinkedTokenSource(ct, limite.Token);
        var execucao = new Execucao(ligado.Token, ct, operacao);

        var consulta = await ConsultarAsync(cep!.Valor, execucao);
        RespostaBuscaEndereco? busca = null;
        if (consulta.Situacao == SituacaoRespostaFonte.NaoEncontrado)
            busca = await BuscarAsync(endereco, execucao);

        var decisao = MotorCep.Decidir(endereco, consulta, busca);
        // Sugestão (caso 4) e candidatos para escolha (caso 5) emitidos por esta API: a gravação só aceita a procedência
        // destes (DM3). Registrar não altera nada.
        if (decisao is { Resultado: ResultadoDecisaoCep.UmCandidato or ResultadoDecisaoCep.VariosCandidatos, Fonte: { } fonte })
            foreach (var candidato in decisao.Candidatos)
                _sugestoes.Registrar(decisao.CepInformado, candidato.Cep, fonte);

        // F3: a conferência feita (com conclusão) fica registrada para o Salvar poder gravar o estado do endereço.
        if (registrarParaOSalvar) _conferencias.Registrar(endereco, decisao);

        var avisos = new List<string>();
        if (execucao.BuscaNoLimite && decisao.Resultado is ResultadoDecisaoCep.UmCandidato or ResultadoDecisaoCep.VariosCandidatos
                or ResultadoDecisaoCep.NenhumCandidato)
            avisos.Add(AvisoListaIncompleta);

        // Offline (F3): a decisão continua "fonte indisponível"; a informação anterior vai à parte, com fonte e data originais.
        var anterior = decisao.Resultado == ResultadoDecisaoCep.FonteIndisponivel && execucao.Anterior is { } guardada
            ? new InformacaoAnteriorCep(guardada.Registros[0], guardada.Fonte, guardada.ConsultadoEm,
                MotorCep.Decidir(endereco, RespostaConsultaCep.Encontrado(guardada.Registros[0], guardada.Fonte)))
            : null;
        return new ResultadoConferenciaCep(decisao, avisos) { Anterior = anterior };
    }

    public async Task<ComparacaoFontesCep> ConsultarOutraFonteAsync(string cep, CancellationToken ct = default)
    {
        if (!CepValor.TentarCriar(cep, out var valor))
            throw new ValidacaoException(["O CEP deve ter 8 dígitos."]);
        var numero = valor!.Valor;

        using var limite = new CancellationTokenSource(TetoTotal, _relogio);
        using var ligado = CancellationTokenSource.CreateLinkedTokenSource(ct, limite.Token);
        var execucao = new Execucao(ligado.Token, ct, OperacaoConsultaCep.SegundaOpiniao);

        // A resposta principal: o mesmo caminho da conferência (cache válido, fonte principal, reserva em falha técnica).
        var principal = await ConsultarAsync(numero, execucao);
        if (principal.Situacao == SituacaoRespostaFonte.Indisponivel)
            return ComparadorFontesCep.Comparar(numero, principal, null);

        // A outra fonte: a primeira configurada diferente da que respondeu (a BrasilAPI só consulta por CEP: é a mesma pergunta).
        if (_provedores.FirstOrDefault(p => p.Fonte != principal.Fonte) is not { } outra)
            return ComparadorFontesCep.Comparar(numero, principal, null);

        // A outra fonte acabou de falhar nesta mesma operação (a principal respondeu pela reserva): não é chamada de novo.
        if (execucao.FalharamTecnicamente.Contains(outra.Fonte))
            return ComparadorFontesCep.Comparar(numero, principal, RespostaConsultaCep.Indisponivel(outra.Fonte));

        var chave = CacheConferenciaCep.ChaveCep(numero);
        var inicio = _relogio.GetTimestamp();
        ResultadoProvedorCep r;
        try
        {
            r = await ExecutarAsync(execucao, outra.Fonte, () => outra.ConsultarPorCepAsync(numero, execucao.Token),
                detalhe => ResultadoProvedorCep.Indisponivel(outra.Fonte, detalhe));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await RegistrarAsync(execucao, TipoConsultaCep.PorCep, chave, numero, outra.Fonte, inicio, ResultadoConsultaCep.Cancelado,
                OrigemRespostaCep.SegundaFonte);
            throw;
        }
        await RegistrarAsync(execucao, TipoConsultaCep.PorCep, chave, numero, outra.Fonte, inicio, Resultado(r.Situacao), OrigemRespostaCep.SegundaFonte);
        // Não vai para o cache: o cache guarda a resposta da conferência (a principal); a segunda é só para comparar.
        var segunda = r.Situacao.EhFalhaTecnica() ? RespostaConsultaCep.Indisponivel(outra.Fonte) : ParaResposta(r);
        return ComparadorFontesCep.Comparar(numero, principal, segunda);
    }

    public async Task<ResultadoConferenciaCep> BuscarPorEnderecoAsync(EnderecoConferenciaCep endereco, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(endereco);
        // A mesma regra de dados mínimos da busca feita na conferência (UF, cidade e logradouro com 3 ou mais).
        if (BuscaEnderecoCep.De(endereco) is null) throw new ValidacaoException([MensagemDadosInsuficientes]);

        using var limite = new CancellationTokenSource(TetoTotal, _relogio);
        using var ligado = CancellationTokenSource.CreateLinkedTokenSource(ct, limite.Token);
        var execucao = new Execucao(ligado.Token, ct, OperacaoConsultaCep.BuscaPorEndereco);

        var avisos = new List<string>();
        var busca = await BuscarAsync(endereco, execucao);
        if (busca is null)
        {
            // Os dados já foram conferidos acima: nulo aqui é só "nenhuma fonte busca por endereço".
            avisos.Add(AvisoSemFonteDeBusca);
            busca = RespostaBuscaEndereco.Indisponivel();
        }

        var decisao = MotorCep.BuscarPorEndereco(endereco, busca);
        // Candidatos emitidos pela busca (CEP conferido vazio): a gravação só aceita a procedência destes (DM3).
        if (decisao is { Resultado: ResultadoDecisaoCep.UmCandidato or ResultadoDecisaoCep.VariosCandidatos, Fonte: { } fonte })
            foreach (var candidato in decisao.Candidatos)
                _sugestoes.Registrar(decisao.CepInformado, candidato.Cep, fonte);

        if (execucao.BuscaNoLimite)
        {
            if (decisao.Resultado == ResultadoDecisaoCep.NenhumCandidato) avisos.Add(AvisoBuscaSemCandidatoNoLimite);
            else if (decisao.Resultado is ResultadoDecisaoCep.UmCandidato or ResultadoDecisaoCep.VariosCandidatos)
                avisos.Add(AvisoBuscaListaIncompleta);
        }
        return new ResultadoConferenciaCep(decisao, avisos);
    }

    /// <summary>Estado de uma conferência: fontes que falharam tecnicamente e se o teto estourou.</summary>
    private sealed class Execucao(CancellationToken token, CancellationToken doChamador, OperacaoConsultaCep operacao)
    {
        public CancellationToken Token { get; } = token;
        public CancellationToken DoChamador { get; } = doChamador;
        public OperacaoConsultaCep Operacao { get; } = operacao;

        /// <summary>F3: CEP encontrado guardado antes, utilizável se a fonte estiver fora do ar (offline).</summary>
        public RespostaGuardadaCep? Anterior { get; set; }
        public HashSet<CepFonte> FalharamTecnicamente { get; } = [];
        public bool TetoEstourado { get; set; }

        /// <summary>A busca usada veio no limite da fonte (lista possivelmente cortada).</summary>
        public bool BuscaNoLimite { get; set; }
    }

    private async Task<RespostaConsultaCep> ConsultarAsync(string cep, Execucao e)
    {
        var chave = CacheConferenciaCep.ChaveCep(cep);
        var inicio = _relogio.GetTimestamp();
        if (_cache.ObterConsulta(cep) is { } guardado)
        {
            await RegistrarAsync(e, TipoConsultaCep.PorCep, chave, cep, guardado.Fonte, inicio, Resultado(guardado.Situacao), OrigemRespostaCep.CacheMemoria);
            return ParaResposta(guardado);
        }

        // F3: resposta guardada no banco ainda dentro da validade = resposta atual (a mesma política da memória).
        var agora = _relogio.GetUtcNow().UtcDateTime;
        var persistida = await _postal.ObterAsync(chave, e.DoChamador);
        if (persistida is { } valida && valida.Valida(agora) && ParaResposta(valida) is { } daGuardada)
        {
            await RegistrarAsync(e, TipoConsultaCep.PorCep, chave, cep, valida.Fonte, inicio, Resultado(valida.Situacao), OrigemRespostaCep.CachePersistente);
            return daGuardada;
        }

        foreach (var provedor in _provedores)
        {
            if (e.TetoEstourado) break;
            inicio = _relogio.GetTimestamp();
            ResultadoProvedorCep r;
            try
            {
                r = await ExecutarAsync(e, provedor.Fonte, () => provedor.ConsultarPorCepAsync(cep, e.Token),
                    detalhe => ResultadoProvedorCep.Indisponivel(provedor.Fonte, detalhe));
            }
            catch (OperationCanceledException) when (e.DoChamador.IsCancellationRequested)
            {
                await RegistrarAsync(e, TipoConsultaCep.PorCep, chave, cep, provedor.Fonte, inicio, ResultadoConsultaCep.Cancelado, OrigemRespostaCep.Fonte);
                throw;
            }
            await RegistrarAsync(e, TipoConsultaCep.PorCep, chave, cep, provedor.Fonte, inicio, Resultado(r.Situacao), OrigemRespostaCep.Fonte);
            if (!r.Situacao.EhFalhaTecnica())
            {
                _cache.GuardarConsulta(cep, r);
                await GuardarAsync(chave, TipoConsultaCep.PorCep, cep, r.Situacao, r.Fonte, r.Registro is { } reg ? new[] { reg } : Array.Empty<RegistroCep>(), limite: false);
                return ParaResposta(r);
            }
            e.FalharamTecnicamente.Add(provedor.Fonte);
        }

        // Todas as fontes falharam tecnicamente: a última informação de CEP encontrado (até 30 dias) fica à parte, como
        // anterior; a resposta continua "indisponível" (nunca inexistente, nunca confirmação atual).
        if (persistida is { } antiga && antiga.UtilizavelOffline(agora) && antiga.Registros.Count > 0)
        {
            e.Anterior = antiga;
            await RegistrarAsync(e, TipoConsultaCep.PorCep, chave, cep, antiga.Fonte, _relogio.GetTimestamp(), ResultadoConsultaCep.Encontrado,
                OrigemRespostaCep.InformacaoAnterior);
        }
        return RespostaConsultaCep.Indisponivel();
    }

    private async Task<RespostaBuscaEndereco?> BuscarAsync(EnderecoConferenciaCep endereco, Execucao e)
    {
        // Sem UF, cidade ou logradouro suficientes não há busca (caso 3: "falta buscar pelo endereço").
        if (BuscaEnderecoCep.De(endereco) is not { } dados) return null;
        var buscadores = _provedores.Where(p => p.BuscaPorEndereco).ToList();
        if (buscadores.Count == 0) return null;
        var chave = CacheConferenciaCep.ChaveBusca(dados);
        var inicio = _relogio.GetTimestamp();
        if (_cache.ObterBusca(dados) is { } guardada)
        {
            e.BuscaNoLimite = guardada.LimiteAtingido;
            await RegistrarAsync(e, TipoConsultaCep.PorEndereco, chave, null, guardada.Fonte, inicio, Resultado(guardada.Situacao),
                OrigemRespostaCep.CacheMemoria, guardada.LimiteAtingido);
            return RespostaBuscaEndereco.Realizada(guardada.Registros, guardada.Fonte);
        }

        // F3: busca guardada no banco dentro da validade (1 h); o sinal de lista limitada vem junto (50 ≠ lista completa).
        // Busca nunca é usada offline.
        if (await _postal.ObterAsync(chave, e.DoChamador) is { } persistida && persistida.Valida(_relogio.GetUtcNow().UtcDateTime))
        {
            e.BuscaNoLimite = persistida.LimiteAtingido;
            await RegistrarAsync(e, TipoConsultaCep.PorEndereco, chave, null, persistida.Fonte, inicio, Resultado(persistida.Situacao),
                OrigemRespostaCep.CachePersistente, persistida.LimiteAtingido);
            return RespostaBuscaEndereco.Realizada(persistida.Registros, persistida.Fonte);
        }

        CepFonte? ultimaFalha = null;
        foreach (var provedor in buscadores)
        {
            // A fonte que acabou de falhar na consulta não é chamada de novo nesta conferência (não estoura o teto).
            if (e.TetoEstourado || e.FalharamTecnicamente.Contains(provedor.Fonte))
            {
                ultimaFalha = provedor.Fonte;
                continue;
            }
            inicio = _relogio.GetTimestamp();
            ResultadoBuscaProvedorCep r;
            try
            {
                r = await ExecutarAsync(e, provedor.Fonte, () => provedor.BuscarPorEnderecoAsync(dados, e.Token),
                    detalhe => ResultadoBuscaProvedorCep.Indisponivel(provedor.Fonte, detalhe));
            }
            catch (OperationCanceledException) when (e.DoChamador.IsCancellationRequested)
            {
                await RegistrarAsync(e, TipoConsultaCep.PorEndereco, chave, null, provedor.Fonte, inicio, ResultadoConsultaCep.Cancelado,
                    OrigemRespostaCep.Fonte);
                throw;
            }
            await RegistrarAsync(e, TipoConsultaCep.PorEndereco, chave, null, provedor.Fonte, inicio, Resultado(r.Situacao), OrigemRespostaCep.Fonte,
                r.Situacao.EhFalhaTecnica() ? null : r.LimiteAtingido);
            if (!r.Situacao.EhFalhaTecnica())
            {
                _cache.GuardarBusca(dados, r);
                await GuardarAsync(chave, TipoConsultaCep.PorEndereco, null, r.Situacao, r.Fonte, r.Registros, r.LimiteAtingido);
                e.BuscaNoLimite = r.LimiteAtingido;
                return RespostaBuscaEndereco.Realizada(r.Registros, r.Fonte);
            }
            e.FalharamTecnicamente.Add(provedor.Fonte);
            ultimaFalha = provedor.Fonte;
        }
        return RespostaBuscaEndereco.Indisponivel(ultimaFalha);
    }

    /// <summary>
    /// Guarda a resposta funcional no cache postal (mesmas validades da memória; CEP encontrado utilizável offline por
    /// 30 dias). Nunca lança e nunca recebe falha técnica.
    /// </summary>
    private Task GuardarAsync(string chave, TipoConsultaCep tipo, string? cep, SituacaoProvedorCep situacao, CepFonte fonte,
                              IReadOnlyList<RegistroCep> registros, bool limite)
    {
        var agora = _relogio.GetUtcNow().UtcDateTime;
        var encontrado = situacao == SituacaoProvedorCep.Encontrado;
        var validade = tipo == TipoConsultaCep.PorEndereco ? PoliticaCachePostalCep.ValidadeBusca
            : encontrado ? PoliticaCachePostalCep.ValidadeEncontrado : PoliticaCachePostalCep.ValidadeInexistente;
        DateTime? offline = tipo == TipoConsultaCep.PorCep && encontrado ? agora + PoliticaCachePostalCep.UsoOfflineEncontrado : null;
        var resposta = new RespostaGuardadaCep(encontrado ? SituacaoCachePostal.Encontrado : SituacaoCachePostal.NaoEncontrado, fonte, registros,
            limite, agora, agora + validade, offline);
        return _postal.GuardarAsync(chave, tipo, cep, resposta, CancellationToken.None);
    }

    /// <summary>Uma linha do histórico técnico (sem dados da pessoa). Nunca lança.</summary>
    private Task RegistrarAsync(Execucao e, TipoConsultaCep tipo, string chave, string? cep, CepFonte? fonte, long inicio,
                                ResultadoConsultaCep resultado, OrigemRespostaCep origem, bool? limite = null) =>
        _historico.RegistrarAsync(new ConsultaCepOcorrida(e.Operacao, tipo, chave, cep, fonte, _relogio.GetUtcNow().UtcDateTime,
            _relogio.GetElapsedTime(inicio), resultado, origem, limite));

    private static ResultadoConsultaCep Resultado(SituacaoProvedorCep s) => s switch
    {
        SituacaoProvedorCep.Encontrado => ResultadoConsultaCep.Encontrado,
        SituacaoProvedorCep.NaoEncontrado => ResultadoConsultaCep.NaoEncontrado,
        SituacaoProvedorCep.RespostaInvalida => ResultadoConsultaCep.RespostaInvalida,
        _ => ResultadoConsultaCep.Indisponivel
    };

    private static ResultadoConsultaCep Resultado(SituacaoCachePostal s) =>
        s == SituacaoCachePostal.Encontrado ? ResultadoConsultaCep.Encontrado : ResultadoConsultaCep.NaoEncontrado;

    /// <summary>A resposta guardada no banco como resposta de consulta por CEP (nula se veio sem o registro).</summary>
    private static RespostaConsultaCep? ParaResposta(RespostaGuardadaCep g) => g.Situacao switch
    {
        SituacaoCachePostal.Encontrado when g.Registros.Count > 0 => RespostaConsultaCep.Encontrado(g.Registros[0], g.Fonte),
        SituacaoCachePostal.NaoEncontrado => RespostaConsultaCep.NaoEncontrado(g.Fonte),
        _ => null
    };

    /// <summary>
    /// Chama a fonte. Cancelamento de quem chamou sobe como cancelamento (não é falha técnica); o teto estourado vira
    /// "indisponível" e encerra as próximas chamadas.
    /// </summary>
    private static async Task<T> ExecutarAsync<T>(Execucao e, CepFonte fonte, Func<Task<T>> chamada, Func<string, T> indisponivel)
    {
        if (e.Token.IsCancellationRequested && !e.DoChamador.IsCancellationRequested)
        {
            e.TetoEstourado = true;
            return indisponivel("Teto da conferência esgotado.");
        }
        try
        {
            return await chamada();
        }
        catch (OperationCanceledException) when (!e.DoChamador.IsCancellationRequested)
        {
            e.TetoEstourado = true;
            return indisponivel("Teto da conferência esgotado.");
        }
    }

    private static RespostaConsultaCep ParaResposta(ResultadoProvedorCep r) => r.Situacao switch
    {
        SituacaoProvedorCep.Encontrado => RespostaConsultaCep.Encontrado(r.Registro!, r.Fonte),
        SituacaoProvedorCep.NaoEncontrado => RespostaConsultaCep.NaoEncontrado(r.Fonte),
        _ => RespostaConsultaCep.Indisponivel(r.Fonte)
    };
}
