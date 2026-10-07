using System.Net;
using System.Net.Http.Json;
using Lone.Cliente.Api;
using Lone.Cliente.Navegacao;
using Lone.Cliente.Plataforma;
using Lone.Cliente.Sessao;
using Lone.Contracts.Comum;
using Lone.Contracts.Empresas;
using Lone.Contracts.Seguranca;

namespace Lone.Tests.Cliente;

/// <summary>Servidor falso: responde a cada chamada com a próxima resposta da fila e guarda o que recebeu.</summary>
internal sealed class ServidorFalso : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _respostas = new();

    public List<(HttpMethod Metodo, string Caminho, string? Token, string Corpo)> Recebidas { get; } = new();

    public ServidorFalso Responder(HttpStatusCode status, object? corpo = null)
    {
        _respostas.Enqueue(_ => Resposta(status, corpo));
        return this;
    }

    public ServidorFalso Problema(HttpStatusCode status, string codigo, string detalhe, params (string Campo, object Valor)[] extras)
    {
        var corpo = new Dictionary<string, object> { ["status"] = (int)status, ["detail"] = detalhe, [ErrosApi.CampoCodigo] = codigo };
        foreach (var (campo, valor) in extras) corpo[campo] = valor;
        return Responder(status, corpo);
    }

    private readonly Dictionary<string, Queue<Func<HttpRequestMessage, HttpResponseMessage>>> _porCaminho = new();

    /// <summary>
    /// Resposta para um caminho ("/api/..."), atendida antes da fila geral: para chamadas disparadas sozinhas (conferência
    /// do documento, consulta automática) cuja ordem em relação às outras não importa ao teste. Pode ser chamado várias
    /// vezes para o mesmo caminho (atende na ordem).
    /// </summary>
    public ServidorFalso ResponderEm(string caminho, HttpStatusCode status, object? corpo = null, int vezes = 1)
    {
        if (!_porCaminho.TryGetValue(caminho, out var fila)) _porCaminho[caminho] = fila = new();
        for (var i = 0; i < vezes; i++) fila.Enqueue(_ => Resposta(status, corpo));
        return this;
    }

    /// <summary>A chamada não termina a tempo (o HttpClient desiste): o mesmo que o cliente vê num tempo esgotado.</summary>
    public ServidorFalso TempoEsgotado()
    {
        _respostas.Enqueue(_ => throw new TaskCanceledException("Tempo esgotado.", new TimeoutException()));
        return this;
    }

    /// <summary>Resposta 200 com um corpo que não é o JSON esperado.</summary>
    public ServidorFalso RespostaInvalida(string corpo = "{ isto não é json")
    {
        _respostas.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(corpo, System.Text.Encoding.UTF8, "application/json")
        });
        return this;
    }

    /// <summary>
    /// Quando definido, cada chamada fica parada (depois de registrada) até a tarefa terminar ou o token da chamada ser
    /// cancelado: para ver o estado da tela no meio de uma consulta e testar o cancelamento.
    /// </summary>
    public TaskCompletionSource? Segurar { get; set; }

    public ServidorFalso ForaDoAr()
    {
        _respostas.Enqueue(_ => throw new HttpRequestException("Conexão recusada."));
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage requisicao, CancellationToken ct)
    {
        var corpo = requisicao.Content is null ? string.Empty : await requisicao.Content.ReadAsStringAsync(ct);
        Recebidas.Add((requisicao.Method, requisicao.RequestUri!.AbsolutePath, requisicao.Headers.Authorization?.Parameter, corpo));
        if (Segurar is { } segurar) await segurar.Task.WaitAsync(ct);
        if (_porCaminho.TryGetValue(requisicao.RequestUri!.AbsolutePath, out var doCaminho) && doCaminho.Count > 0)
            return doCaminho.Dequeue()(requisicao);
        if (_respostas.Count == 0)
            throw new InvalidOperationException($"Chamada inesperada: {requisicao.Method} {requisicao.RequestUri}");
        return _respostas.Dequeue()(requisicao);
    }

    private static HttpResponseMessage Resposta(HttpStatusCode status, object? corpo) => new(status)
    {
        Content = corpo is null ? new StringContent(string.Empty) : JsonContent.Create(corpo, corpo.GetType(), options: OpcoesJson.Padrao)
    };
}

internal sealed class CofreEmMemoria : IArmazenamentoSeguro
{
    public Dictionary<string, string> Itens { get; } = new();

    public Task<string?> LerAsync(string chave) => Task.FromResult(Itens.GetValueOrDefault(chave));

    public Task GravarAsync(string chave, string valor)
    {
        Itens[chave] = valor;
        return Task.CompletedTask;
    }

    public void Remover(string chave) => Itens.Remove(chave);
}

internal sealed class PreferenciasEmMemoria : IPreferencias
{
    private readonly Dictionary<string, string> _itens = new();

    public string? Ler(string chave) => _itens.GetValueOrDefault(chave);
    public void Gravar(string chave, string valor) => _itens[chave] = valor;
    public void Remover(string chave) => _itens.Remove(chave);
}

internal sealed class DispositivoDeTeste : IDispositivo
{
    public string Descricao => "Teste";
    public string ServidorPadrao => "https://servidor.teste";
}

internal sealed class NavegacaoGravada : INavegacao
{
    public List<Tela> Telas { get; } = new();

    public Task IrParaAsync(Tela tela, string? mensagem = null)
    {
        Telas.Add(tela);
        return Task.CompletedTask;
    }

    public int TrocasDeSenhaAbertas { get; private set; }
    public bool Encerrou { get; private set; }

    /// <summary>Segura a abertura da troca de senha (o modal ainda abrindo) até o teste liberar.</summary>
    public TaskCompletionSource? SegurarTrocaDeSenha { get; set; }

    public Task AbrirTrocaDeSenhaAsync()
    {
        TrocasDeSenhaAbertas++;
        return SegurarTrocaDeSenha?.Task ?? Task.CompletedTask;
    }

    public Task FecharAsync() => Task.CompletedTask;

    public Task EncerrarAplicativoAsync()
    {
        Encerrou = true;
        return Task.CompletedTask;
    }
}

/// <summary>Diálogos com respostas programadas (o que o "usuário" escolhe) e registro das perguntas feitas.</summary>
internal sealed class DialogosFalsos : IDialogos
{
    public bool RespostaConfirmacao { get; set; } = true;
    public string? RespostaPergunta { get; set; } = string.Empty;
    public List<string> Perguntas { get; } = new();

    public Task<bool> ConfirmarAsync(string titulo, string mensagem, string aceitar, string cancelar)
    {
        Perguntas.Add(mensagem);
        return Task.FromResult(RespostaConfirmacao);
    }

    public Task<string?> PerguntarAsync(string titulo, string mensagem, string aceitar, string cancelar, string? dica = null, int tamanhoMaximo = 200)
    {
        Perguntas.Add(mensagem);
        return Task.FromResult(RespostaPergunta);
    }

    /// <summary>Opção devolvida pelo menu de ações (nulo = cancelou); as opções oferecidas ficam em <see cref="OpcoesOferecidas"/>.</summary>
    public string? RespostaEscolha { get; set; }
    public List<string> OpcoesOferecidas { get; } = new();

    public Task<string?> EscolherAsync(string titulo, string cancelar, IReadOnlyList<string> opcoes)
    {
        OpcoesOferecidas.Clear();
        OpcoesOferecidas.AddRange(opcoes);
        return Task.FromResult(RespostaEscolha);
    }
}

/// <summary>Arquivos do aparelho: devolve o arquivo configurado e guarda o que foi aberto.</summary>
internal sealed class ArquivosFalsos : IArquivos
{
    public ArquivoEscolhido? Escolhido { get; set; }
    public List<(string Nome, byte[] Conteudo)> Abertos { get; } = new();

    public Task<ArquivoEscolhido?> EscolherAsync(string titulo) => Task.FromResult(Escolhido);

    public Task AbrirAsync(string nome, byte[] conteudo)
    {
        Abertos.Add((nome, conteudo));
        return Task.CompletedTask;
    }
}

/// <summary>Monta o cliente completo (API, sessão, autenticação, fluxo) sobre o servidor falso.</summary>
internal sealed class AmbienteCliente
{
    public AmbienteCliente()
    {
        Servidor = new ServidorFalso();
        Cofre = new CofreEmMemoria();
        Sessao = new SessaoCliente(Cofre);
        Endereco = new ConfiguracaoServidor(new PreferenciasEmMemoria(), new DispositivoDeTeste());
        Api = new ClienteApi(new HttpClient(Servidor), Sessao, Endereco);
        Autenticacao = new ServicoAutenticacao(Api, Sessao, new DispositivoDeTeste());
        Fluxo = new FluxoDeEntrada(Sessao, Autenticacao);
        Dialogos = new DialogosFalsos();
    }

    public DialogosFalsos Dialogos { get; }
    public ArquivosFalsos Arquivos { get; } = new();

    public ServidorFalso Servidor { get; }
    public CofreEmMemoria Cofre { get; }
    public SessaoCliente Sessao { get; }
    public ConfiguracaoServidor Endereco { get; }
    public ClienteApi Api { get; }
    public ServicoAutenticacao Autenticacao { get; }
    public FluxoDeEntrada Fluxo { get; }

    public static SessaoDto NovaSessao(string acesso = "acesso-1", string renovacao = "renovacao-1", int empresas = 1,
                                       bool deveTrocarSenha = false) => new()
    {
        TokenAcesso = acesso,
        TokenRenovacao = renovacao,
        Nome = "Administrador",
        Login = "admin",
        DeveTrocarSenha = deveTrocarSenha,
        Administrador = true,
        EmpresasDisponiveis = Enumerable.Range(1, empresas)
            .Select(i => new EmpresaAtiva(Guid.NewGuid(), Guid.NewGuid(), $"Empresa {i}", null, i == 1))
            .ToList(),
        EmpresaAtiva = null
    };
}
