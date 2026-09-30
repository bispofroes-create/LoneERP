using System.Diagnostics;
using Lone.Cliente.ViewModels;

namespace Lone.Cliente.Mensagens;

/// <summary>
/// Serviço global de mensagens da interface (Fase 1 da arquitetura de feedback): recebe o resultado das telas, classifica
/// como apresentar e mantém o que está na camada global da janela — fora da rolagem de qualquer página.
/// <para>
/// Só em memória e só enquanto o aplicativo está aberto: nada é gravado, não é central de notificações e não é auditoria.
/// Fase 1: apenas sucesso vira toast (<see cref="Classificar"/>); erro, aviso e informação continuam na barra de cada tela.
/// </para>
/// <para>
/// Regras: no máximo <see cref="MaximoVisiveis"/> na tela; as outras esperam (até <see cref="MaximoEmEspera"/>), a de maior
/// prioridade primeiro. A mesma
/// mensagem (tipo + texto + contexto) enquanto está na tela ou esperando não empilha: soma na <see cref="MensagemUsuario.Contagem"/>
/// e recomeça o tempo. A duração é calculada aqui (<see cref="DuracaoPara"/>), nunca pelas telas. O tempo para enquanto o
/// ponteiro ou o foco do teclado está sobre o toast (<see cref="Pausar"/>/<see cref="Retomar"/>).
/// </para>
/// Os eventos podem chegar fora da thread da interface (o fim do tempo vem de um temporizador): quem desenha repassa à thread
/// da tela.
/// </summary>
public sealed class ServicoMensagens
{
    public const int MaximoVisiveis = 3;

    /// <summary>Esperando vaga, no máximo: numa enxurrada, a de menor prioridade e mais antiga sai da fila (toast é passageiro).</summary>
    public const int MaximoEmEspera = 10;

    /// <summary>Nenhum toast some antes disto (tempo para perceber e ler uma frase curta).</summary>
    public static readonly TimeSpan DuracaoMinima = TimeSpan.FromSeconds(4);

    /// <summary>Teto: frases longas não ficam indefinidamente (o ponteiro sobre o toast segura o tempo).</summary>
    public static readonly TimeSpan DuracaoMaxima = TimeSpan.FromSeconds(12);

    /// <summary>Com ação ("Abrir"): tempo para ler e decidir clicar.</summary>
    public static readonly TimeSpan DuracaoMinimaComAcao = TimeSpan.FromSeconds(8);

    /// <summary>Ao tirar o ponteiro/foco de cima: pelo menos isto, mesmo que o tempo já estivesse no fim.</summary>
    public static readonly TimeSpan DuracaoMinimaAoRetomar = TimeSpan.FromSeconds(2);

    private static readonly TimeSpan Base = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan PorCaractere = TimeSpan.FromMilliseconds(70); // ~14 caracteres por segundo

    /// <summary>O serviço do aplicativo (o mesmo registrado na injeção de dependência). As telas usam este.</summary>
    public static ServicoMensagens Padrao { get; } = new();

    private sealed class Exibicao(MensagemUsuario mensagem)
    {
        public MensagemUsuario Mensagem { get; } = mensagem;
        public ITimer? Temporizador { get; set; }
        public DateTimeOffset ExpiraEm { get; set; }
        public TimeSpan? Restante { get; set; } // preenchido enquanto pausada
        public int Versao { get; set; }         // descarta o disparo atrasado de um temporizador já trocado
    }

    private readonly TimeProvider _relogio;
    private readonly object _trava = new();
    private readonly List<Exibicao> _visiveis = [];
    private readonly List<MensagemUsuario> _espera = [];

    public ServicoMensagens() : this(TimeProvider.System) { }

    public ServicoMensagens(TimeProvider relogio) => _relogio = relogio;

    /// <summary>O conjunto visível ou em espera mudou (entrou, saiu, repetiu).</summary>
    public event EventHandler? Mudou;

    /// <summary>Cada publicação, inclusive a repetida (o leitor de tela anuncia de novo: a ação aconteceu de novo).</summary>
    public event EventHandler<MensagemUsuario>? Publicada;

    /// <summary>Na tela agora, da mais antiga para a mais nova.</summary>
    public IReadOnlyList<MensagemUsuario> Visiveis
    {
        get { lock (_trava) return _visiveis.Select(e => e.Mensagem).ToArray(); }
    }

    /// <summary>Esperando vaga, na ordem em que vão aparecer.</summary>
    public IReadOnlyList<MensagemUsuario> EmEspera
    {
        get { lock (_trava) return _espera.ToArray(); }
    }

    /// <summary>
    /// Como cada tipo é apresentado nesta fase. Nulo = continua com o mecanismo atual da tela (barra), sem passar por aqui.
    /// </summary>
    public static ApresentacaoMensagem? Classificar(TipoMensagem tipo) => tipo switch
    {
        TipoMensagem.Sucesso => ApresentacaoMensagem.Toast,
        _ => null // Fase 1: erro, aviso e informação não viram toast
    };

    /// <summary>Tempo na tela: cresce com o texto, entre o mínimo e o teto; com ação, pelo menos o tempo de decidir.</summary>
    public static TimeSpan DuracaoPara(string texto, bool temAcao)
    {
        var porTexto = Base + PorCaractere * texto.Length;
        var duracao = porTexto < DuracaoMinima ? DuracaoMinima : porTexto > DuracaoMaxima ? DuracaoMaxima : porTexto;
        return temAcao && duracao < DuracaoMinimaComAcao ? DuracaoMinimaComAcao : duracao;
    }

    public MensagemUsuario Publicar(string texto, TipoMensagem tipo = TipoMensagem.Sucesso,
        PrioridadeMensagem prioridade = PrioridadeMensagem.Normal, AcaoMensagem? acao = null, string? contexto = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(texto);
        if (Classificar(tipo) is not { } apresentacao)
            throw new ArgumentException($"Mensagens do tipo {tipo} ainda não passam pela camada global (Fase 1: só sucesso).", nameof(tipo));

        MensagemUsuario publicada;
        lock (_trava)
        {
            if (_visiveis.FirstOrDefault(e => e.Mensagem.MesmaQue(texto, tipo, contexto)) is { } repetida)
            {
                repetida.Mensagem.Contagem++;
                if (repetida.Restante is null) Agendar(repetida, repetida.Mensagem.Duracao);
                else repetida.Restante = repetida.Mensagem.Duracao; // pausada: recomeça cheia quando retomar
                publicada = repetida.Mensagem;
            }
            else if (_espera.FirstOrDefault(m => m.MesmaQue(texto, tipo, contexto)) is { } esperando)
            {
                esperando.Contagem++;
                publicada = esperando;
            }
            else
            {
                publicada = new MensagemUsuario(texto, tipo, apresentacao, prioridade, DuracaoPara(texto, acao is not null), acao,
                    contexto, _relogio.GetUtcNow());
                Entrar(publicada);
            }
        }

        Publicada?.Invoke(this, publicada);
        Mudou?.Invoke(this, EventArgs.Empty);
        return publicada;
    }

    /// <summary>Tira da tela (ou da espera). Falso se já tinha saído.</summary>
    public bool Dispensar(Guid id)
    {
        lock (_trava)
        {
            if (!Remover(id)) return false;
            Promover();
        }
        Mudou?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Segura o tempo (ponteiro ou foco sobre o toast).</summary>
    public void Pausar(Guid id)
    {
        lock (_trava)
        {
            if (Visivel(id) is not { Restante: null } exibicao) return;
            var restante = exibicao.ExpiraEm - _relogio.GetUtcNow();
            exibicao.Restante = restante > TimeSpan.Zero ? restante : TimeSpan.Zero;
            exibicao.Versao++;
            exibicao.Temporizador?.Dispose();
            exibicao.Temporizador = null;
        }
    }

    /// <summary>Solta o tempo: continua de onde parou (no mínimo <see cref="DuracaoMinimaAoRetomar"/>).</summary>
    public void Retomar(Guid id)
    {
        lock (_trava)
        {
            if (Visivel(id) is not { Restante: { } restante } exibicao) return;
            Agendar(exibicao, restante < DuracaoMinimaAoRetomar ? DuracaoMinimaAoRetomar : restante);
        }
    }

    /// <summary>Executa a ação do toast (tirando-o da tela antes). Falso se não havia ação ou se ela falhou.</summary>
    public async Task<bool> ExecutarAcaoAsync(Guid id)
    {
        AcaoMensagem? acao;
        lock (_trava) acao = Visivel(id)?.Mensagem.Acao;
        if (acao is null) return false;

        Dispensar(id);
        try
        {
            await acao.Executar();
            return true;
        }
        catch (Exception ex)
        {
            // A ação é um atalho (ex.: abrir uma ficha); se falhar, não derruba o aplicativo.
            Trace.WriteLine($"Falha na ação \"{acao.Texto}\" do aviso: {ex}");
            return false;
        }
    }

    /// <summary>Esvazia tudo (troca de contexto: saída do sistema, outra empresa).</summary>
    public void Limpar()
    {
        lock (_trava)
        {
            if (_visiveis.Count == 0 && _espera.Count == 0) return;
            foreach (var exibicao in _visiveis) exibicao.Temporizador?.Dispose();
            _visiveis.Clear();
            _espera.Clear();
        }
        Mudou?.Invoke(this, EventArgs.Empty);
    }

    // ---- Dentro da trava ----

    private Exibicao? Visivel(Guid id) => _visiveis.FirstOrDefault(e => e.Mensagem.Id == id);

    private void Entrar(MensagemUsuario mensagem)
    {
        if (_visiveis.Count < MaximoVisiveis)
        {
            Exibir(mensagem);
            return;
        }

        // Cheio: a de prioridade maior que a menor visível toma o lugar dela (que volta a esperar, na frente das iguais).
        var menor = _visiveis.MinBy(e => e.Mensagem.Prioridade)!;
        if (mensagem.Prioridade > menor.Mensagem.Prioridade)
        {
            Remover(menor.Mensagem.Id);
            Esperar(menor.Mensagem, naFrenteDasIguais: true);
            Exibir(mensagem);
        }
        else
            Esperar(mensagem, naFrenteDasIguais: false);
    }

    private void Esperar(MensagemUsuario mensagem, bool naFrenteDasIguais)
    {
        var posicao = _espera.FindIndex(m => naFrenteDasIguais ? m.Prioridade <= mensagem.Prioridade : m.Prioridade < mensagem.Prioridade);
        _espera.Insert(posicao < 0 ? _espera.Count : posicao, mensagem);
        if (_espera.Count <= MaximoEmEspera) return;

        // Fila cheia: sai a de menor prioridade que esperou mais (a fila está em ordem de prioridade; dentro dela, de chegada).
        var menor = _espera[^1].Prioridade;
        _espera.RemoveAt(_espera.FindIndex(m => m.Prioridade == menor));
    }

    private void Exibir(MensagemUsuario mensagem)
    {
        var exibicao = new Exibicao(mensagem);
        _visiveis.Add(exibicao);
        Agendar(exibicao, mensagem.Duracao);
    }

    private void Agendar(Exibicao exibicao, TimeSpan tempo)
    {
        exibicao.Temporizador?.Dispose();
        exibicao.Restante = null;
        var versao = ++exibicao.Versao;
        exibicao.ExpiraEm = _relogio.GetUtcNow() + tempo;
        exibicao.Temporizador = _relogio.CreateTimer(_ => Expirar(exibicao, versao), null, tempo, Timeout.InfiniteTimeSpan);
    }

    private bool Remover(Guid id)
    {
        if (Visivel(id) is { } exibicao)
        {
            exibicao.Versao++;
            exibicao.Temporizador?.Dispose();
            _visiveis.Remove(exibicao);
            return true;
        }
        return _espera.RemoveAll(m => m.Id == id) > 0;
    }

    private void Promover()
    {
        while (_visiveis.Count < MaximoVisiveis && _espera.Count > 0)
        {
            var proxima = _espera[0];
            _espera.RemoveAt(0);
            Exibir(proxima);
        }
    }

    // ---- Fim do tempo (thread do temporizador) ----

    private void Expirar(Exibicao exibicao, int versao)
    {
        lock (_trava)
        {
            if (exibicao.Versao != versao || !_visiveis.Contains(exibicao)) return;
            Remover(exibicao.Mensagem.Id);
            Promover();
        }
        Mudou?.Invoke(this, EventArgs.Empty);
    }
}
