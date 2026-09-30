using Lone.Contracts.Territorios;
using Lone.Domain.Entidades;
using Lone.Domain.Territorios;

namespace Lone.Application.Territorios;

// Fase 2b-1b — o que o motor territorial pede ao banco. As regras ficam no domínio (MotorAtribuicao, CenarioTerritorial,
// AplicacaoTerritorial, RegrasOperacaoTerritorial) e no serviço; a Infraestrutura só lê, trava e grava, sempre na ordem
// única de travas: árvore → motor do mapa → operação → território → linhas de fato.

/// <summary>Operações TE- (cabeçalho, mudanças e simulações). Nada é apagado.</summary>
public interface IOperacaoTerritorialRepositorio
{
    /// <summary>Operações (do mapa, ou todas), com as mudanças, sem rastreamento; mais novas primeiro.</summary>
    Task<List<OperacaoTerritorial>> ListarAsync(Guid? mapaId, CancellationToken ct);

    Task<OperacaoTerritorial?> ObterAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Grava a operação nova com o próximo número TE- do <paramref name="ano"/> (incremento atômico na mesma transação:
    /// DN-13). Preenche <see cref="OperacaoTerritorial.Ano"/> e <see cref="OperacaoTerritorial.Sequencia"/> e chama
    /// <paramref name="aoNumerar"/> antes de gravar (o evento de criação já sai com o número).
    /// </summary>
    Task CriarAsync(OperacaoTerritorial operacao, int ano, Action<OperacaoTerritorial> aoNumerar, CancellationToken ct);

    /// <summary>
    /// Grava cabeçalho e mudanças de uma operação em aberto, exigindo a versão que a tela abriu (rowversion): dois usuários
    /// no mesmo rascunho não gravam às cegas. Mudança retirada do rascunho é apagada (ainda não é fato nem evidência).
    /// </summary>
    Task SalvarAsync(OperacaoTerritorial operacao, CancellationToken ct);

    /// <summary>
    /// Guarda a simulação (nunca apaga as anteriores; DN-03) e marca a operação como Simulada apontando para ela, exigindo a
    /// versão da operação — numa transação curta.
    /// </summary>
    Task SalvarSimulacaoAsync(OperacaoTerritorial operacao, OperacaoTerritorialSimulacao simulacao, CancellationToken ct);

    /// <summary>As simulações da operação (sem os itens), mais novas primeiro.</summary>
    Task<List<OperacaoTerritorialSimulacao>> SimulacoesAsync(Guid operacaoId, CancellationToken ct);

    Task<OperacaoTerritorialSimulacao?> ObterSimulacaoAsync(Guid simulacaoId, CancellationToken ct);

    /// <summary>Itens de uma simulação, filtrados e paginados (<paramref name="pessoas"/>: só estes, quando o alcance é restrito).</summary>
    Task<(int Total, List<OperacaoTerritorialSimulacaoItem> Itens)> ItensSimulacaoAsync(Guid simulacaoId, FiltroItensOperacaoTerritorialDto filtro,
                                                                                         IReadOnlySet<Guid>? pessoas, CancellationToken ct);

    /// <summary>Itens aplicados de uma operação, filtrados e paginados.</summary>
    Task<(int Total, List<OperacaoTerritorialItem> Itens)> ItensAplicadosAsync(Guid operacaoId, FiltroItensOperacaoTerritorialDto filtro,
                                                                                IReadOnlySet<Guid>? pessoas, CancellationToken ct);

    /// <summary>Os clientes citados nos itens (para restringir pelo alcance antes de paginar).</summary>
    Task<List<Guid>> PessoasDosItensAsync(Guid? simulacaoId, Guid? operacaoId, CancellationToken ct);

    /// <summary>
    /// Registra um evento na história da operação numa transação própria (ex.: "tentativa de aplicação recusada"): a
    /// transação da aplicação caiu e levaria o registro junto (seção F).
    /// </summary>
    Task RegistrarEventoAsync(Guid operacaoId, string evento, CancellationToken ct);

    /// <summary>Número TE- de cada operação citada.</summary>
    Task<Dictionary<Guid, string>> NumerosAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
}

/// <summary>
/// Leituras do motor. A simulação lê sem travas (confere a versão do motor antes e depois); a aplicação lê pelo mesmo
/// leitor DENTRO da transação travada, então o que ela confere é o que ela grava.
/// </summary>
public interface ILeitorTerritorial
{
    Task<MapaTerritorialMotor?> MotorAsync(Guid mapaId, CancellationToken ct);
    Task<byte[]?> VersaoArvoreAsync(Guid mapaId, CancellationToken ct);
    Task<ParametrosTerritoriais> ParametrosAsync(CancellationToken ct);
    Task<MapaTerritorial?> MapaAsync(Guid mapaId, CancellationToken ct);

    /// <summary>
    /// O estado oficial do mapa para a data de efeito: territórios (com posições), versões da regra, exceções e atribuições
    /// válidas (não anuladas) que valem em <paramref name="efeito"/>. Sem <paramref name="comAtribuicoes"/>, a lista de
    /// atribuições vem vazia (telas que só conferem as bases das mudanças não precisam de milhares de linhas).
    /// </summary>
    Task<EstadoTerritorialEmD> EstadoAsync(MapaTerritorial mapa, DateOnly efeito, bool comAtribuicoes, CancellationToken ct);

    /// <summary>O universo do mapa: pessoas com alguma das classificações do mapa ativa (nenhum critério implícito).</summary>
    Task<IReadOnlySet<Guid>> UniversoAsync(MapaTerritorial mapa, CancellationToken ct);

    /// <summary>
    /// Candidatos de uma regra (T6), calculados por conjunto no banco: pessoas do universo que atendem a algum grupo de
    /// inclusão e a nenhum grupo de exclusão, com as condições de endereço no endereço de referência do mapa (T16).
    /// </summary>
    Task<IReadOnlySet<Guid>> CandidatosAsync(MapaTerritorial mapa, GruposRegraTerritorioDto grupos, DateOnly hoje, CancellationToken ct);

    /// <summary>Maior número de versão da regra por território do mapa (anuladas contam: o número nunca se repete).</summary>
    Task<Dictionary<Guid, int>> UltimaVersaoDasRegrasAsync(Guid mapaId, CancellationToken ct);

    /// <summary>Responsáveis válidos dos territórios que ainda valem em <paramref name="efeito"/> ou depois.</summary>
    Task<List<TerritorioResponsavel>> ResponsaveisAsync(IReadOnlyCollection<Guid> territorios, DateOnly efeito, CancellationToken ct);

    /// <summary>
    /// Os valores dos atributos que as regras avaliaram, em texto, por cliente ("UF de referência = MG; Etiquetas = VIP"):
    /// o cadastro de Pessoas não é versionado, então a explicação guarda o que foi lido (seção L).
    /// </summary>
    Task<Dictionary<Guid, string>> AtributosAsync(MapaTerritorial mapa, IReadOnlyCollection<Guid> pessoas, IReadOnlySet<string> campos, CancellationToken ct);

    /// <summary>Número TE- de uma operação (lido na mesma conexão: dentro da aplicação, nada de outra conexão).</summary>
    Task<string?> NumeroOperacaoAsync(Guid operacaoId, CancellationToken ct);

    /// <summary>A versão atual (rowversion) de regras e exceções citadas como base das mudanças.</summary>
    Task<Dictionary<Guid, byte[]>> VersoesDasBasesAsync(IReadOnlyCollection<Guid> regras, IReadOnlyCollection<Guid> excecoes, CancellationToken ct);
}

/// <summary>O pedido de aplicação: o que a tela conferiu (versões) e o que precisa ser travado antes de ler.</summary>
public sealed record PedidoAplicacaoTerritorial(
    Guid OperacaoId,
    Guid MapaId,
    byte[] VersaoOperacao,
    byte[] VersaoMotor,
    byte[]? VersaoArvore,
    IReadOnlyCollection<Guid> TerritoriosEstruturais);

/// <summary>O resultado do cálculo feito dentro da transação: o plano de gravação e a operação já marcada como Aplicada.</summary>
public sealed record AplicacaoCalculada(PlanoAplicacaoTerritorial Plano, OperacaoTerritorial Operacao);

/// <summary>Leitura, aplicação e desfazer das operações: transação, travas e gravação.</summary>
public interface IMotorTerritorialDados
{
    /// <summary>Lê fora de transação (simulação, telas).</summary>
    Task<T> LerAsync<T>(Func<ILeitorTerritorial, Task<T>> ler, CancellationToken ct);

    /// <summary>
    /// Aplica numa transação só (T11): trava árvore (só com mudança estrutural) → motor (versão simulada) → operação (versão
    /// e Simulada) → territórios da estrutura; chama <paramref name="calcular"/> com um leitor da mesma transação (T8,
    /// bases, re-simulação e assinatura) e grava o plano na ordem fecha → anula → abre. Qualquer recusa ou erro: nada fica.
    /// </summary>
    Task AplicarAsync(PedidoAplicacaoTerritorial pedido, Func<ILeitorTerritorial, Task<AplicacaoCalculada>> calcular, CancellationToken ct);

    /// <summary>
    /// Desfaz (DN-05) numa transação: trava o motor exigindo que a operação ainda seja a última do mapa, trava a operação
    /// (versão e Aplicada), anula as linhas que ela abriu, reabre as que ela fechou com o fim exato de antes, devolve ao motor
    /// a última operação e o último efeito anteriores e grava a operação como Desfeita.
    /// </summary>
    Task DesfazerAsync(OperacaoTerritorial operacao, byte[] versaoOperacao, CancellationToken ct);
}

/// <summary>Consultas de leitura das regras, exceções e atribuições (telas e contrato para documentos).</summary>
public interface IConsultasTerritoriais
{
    Task<List<RegraTerritorio>> RegrasDoTerritorioAsync(Guid territorioId, CancellationToken ct);
    Task<List<ExcecaoTerritorio>> ExcecoesDoTerritorioAsync(Guid territorioId, CancellationToken ct);

    /// <summary>Atribuições válidas do território na data (até <paramref name="limite"/>) e o total.</summary>
    Task<(int Total, List<AtribuicaoTerritorio> Itens)> AtribuicoesDoTerritorioAsync(Guid territorioId, DateOnly data, IReadOnlySet<Guid>? pessoas,
                                                                                       int limite, CancellationToken ct);

    /// <summary>Atribuições válidas do cliente na data, em todos os mapas.</summary>
    Task<List<AtribuicaoTerritorio>> AtribuicoesDoClienteAsync(Guid pessoaId, DateOnly data, CancellationToken ct);

    /// <summary>Nome para exibir de cada pessoa citada.</summary>
    Task<Dictionary<Guid, string>> NomesDePessoasAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);

    /// <summary>Código de cada pessoa (para as listas).</summary>
    Task<Dictionary<Guid, long>> CodigosDePessoasAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
}

/// <summary>O registro único dos parâmetros territoriais (DN-08).</summary>
public interface IParametrosTerritoriaisRepositorio
{
    Task<ParametrosTerritoriais> ObterAsync(CancellationToken ct);
    Task SalvarAsync(ParametrosTerritoriais parametros, CancellationToken ct);
}
