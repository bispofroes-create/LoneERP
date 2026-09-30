using CommunityToolkit.Mvvm.ComponentModel;

namespace Lone.Cliente.Navegacao;

/// <summary>
/// Uma tela que sabe dizer qual registro está aberto nela e abrir outro (ou nenhum) quando a navegação pede. Nos cadastros
/// "lista + ficha" quem implementa é a base (<c>CadastroViewModelBase</c>): todas as telas ganham ao mesmo tempo.
/// </summary>
public interface ITelaNavegavel
{
    /// <summary>Registro aberto agora (nulo = a lista da tela, sem ficha).</summary>
    ReferenciaRegistro? RegistroAberto { get; }

    /// <summary>
    /// Deixa a tela no registro pedido (nulo = fecha a ficha). Pergunta antes, como sempre, se houver alterações não
    /// salvas. Verdadeiro se chegou.
    /// </summary>
    Task<bool> IrParaRegistroAsync(ReferenciaRegistro? registro);
}

/// <summary>O que o aplicativo (MAUI/Shell) oferece à navegação: trocar de tela e dizer qual está aberta.</summary>
public interface IPlataformaNavegacao
{
    /// <summary>Rota da tela aberta (ex.: "pessoas").</summary>
    string? RotaAtual { get; }

    /// <summary>A tela aberta, se ela participa da navegação por registros.</summary>
    ITelaNavegavel? TelaAtual { get; }

    /// <summary>Vai para a tela (o Shell pergunta antes se houver alterações não salvas e pode não ir).</summary>
    Task IrParaRotaAsync(string rota);
}

/// <summary>
/// Motor global de navegação do Lone: o único caminho para "ir a" e "voltar". Separa as perguntas que um ERP maduro separa:
/// <list type="bullet">
/// <item>"Para onde quero ir?" — o menu (navegação global) pede ao motor com <see cref="OrigemNavegacao.Menu"/>.</item>
/// <item>"De onde eu vim?" — o <see cref="HistoricoNavegacao"/> e o <see cref="VoltarAsync"/>.</item>
/// <item>"Qual registro agora?" — trocar de registro pela lista não vira histórico (substitui).</item>
/// <item>"O que eu estava fazendo?" — registros recentes da sessão (<see cref="RegistrosRecentes"/>).</item>
/// </list>
/// O histórico é montado pelo que <i>de fato</i> aconteceu (a tela informa o que abriu), não pelo que foi pedido: fechar a
/// ficha pelo botão Fechar, pelo voltar do celular ou pelo Voltar dá no mesmo. Tudo em memória (nada de banco), limpo a
/// cada troca de contexto (entrada, saída, outra empresa). Não guarda referência a telas: pergunta à plataforma qual está
/// aberta, então tela fechada ou trocada nunca recebe chamada atrasada.
/// </summary>
public sealed partial class GerenciadorNavegacao : ObservableObject
{
    /// <summary>Registros recentes guardados na sessão (futuro: "Recentes" de registros no menu e na busca).</summary>
    public const int MaximoRecentes = 10;

    /// <summary>O motor do aplicativo (o mesmo registrado na injeção de dependência). As telas usam este.</summary>
    public static GerenciadorNavegacao Padrao { get; } = new();

    private readonly HistoricoNavegacao _historico = new();

    /// <summary>
    /// Estado atual de cada tela ("como estava quando o usuário saiu dela"), com a instância que foi deixada: só se
    /// restaura numa tela recriada (a mesma instância já está como estava). Diferente do retrato de cada entrada do
    /// histórico (<see cref="EntradaNavegacao.Estado"/>), que o ← usa.
    /// </summary>
    private readonly Dictionary<string, (WeakReference<IEstadoNavegavel>? Tela, EstadoTela Estado)> _estadoDasTelas =
        new(StringComparer.Ordinal);
    private readonly List<ReferenciaRegistro> _recentes = [];
    private readonly TimeProvider _relogio;
    private IPlataformaNavegacao? _plataforma;
    private bool _emCurso;
    private int _operacao;
    private Restauracao? _restauracao;
    private DateTimeOffset _ultimoVoltar = DateTimeOffset.MinValue;

    /// <summary>
    /// Pedidos de Voltar mais próximos que isto contam como um só (duplo clique no ←, tecla repetida): voltar duas vezes
    /// tem de ser intencional.
    /// </summary>
    public static readonly TimeSpan IntervaloMinimoVoltar = TimeSpan.FromMilliseconds(400);

    public GerenciadorNavegacao() : this(TimeProvider.System) { }

    public GerenciadorNavegacao(TimeProvider relogio) => _relogio = relogio;

    /// <summary>A tela ainda pode ser aberta pelo usuário (permissões atuais do menu). Nulo = todas.</summary>
    public Func<string, bool>? RotaPermitida { get; set; }

    /// <summary>Nome da tela para "Voltar para …" (ex.: "Cadastro de pessoas"). Nulo = a própria rota.</summary>
    public Func<string, string?>? TituloDaRota { get; set; }

    public IReadOnlyList<EntradaNavegacao> Historico => _historico.Entradas;
    public EntradaNavegacao? Atual => _historico.Atual;
    public EntradaNavegacao? Anterior => _historico.Anterior;

    /// <summary>Há para onde voltar (o botão Voltar aparece habilitado) — contando só telas que o perfil ainda abre.</summary>
    public bool PodeVoltar => AlvoDoVoltar is not null;

    /// <summary>Texto do Voltar (dica e leitor de tela): diz para onde vai, não só "Voltar".</summary>
    public string DescricaoVoltar => AlvoDoVoltar is { } alvo ? $"Voltar para {alvo.Titulo}" : "Nada para voltar";

    /// <summary>A entrada para onde o Voltar leva: a anterior que ainda pode ser aberta.</summary>
    private EntradaNavegacao? AlvoDoVoltar
    {
        get
        {
            // Restaurando: o ← interrompe e registra a chegada antes de voltar — a previsão parte do histórico como ficará.
            var entradas = _restauracao is { } r ? HistoricoPrevisto(r) : _historico.Entradas;
            for (var i = entradas.Count - 2; i >= 0; i--)
                if (RotaPermitida?.Invoke(entradas[i].Local.Rota) != false) return entradas[i];
            return null;
        }
    }

    private IReadOnlyList<EntradaNavegacao> HistoricoPrevisto(Restauracao r)
    {
        var copia = _historico.Copia();
        AplicarChegada(copia, r.Chegada, real: false);
        return copia.Entradas;
    }

    /// <summary>Há um Shell ligado (fora dele — testes de tela, telas de entrada — as telas usam o caminho antigo).</summary>
    public bool Conectado => _plataforma is not null;

    /// <summary>
    /// Uma troca de tela está em andamento (novos pedidos são ignorados: duplo clique não duplica nada). Enquanto a tela
    /// nova só restaura o contexto, não conta: o menu e o ← atendem e interrompem a restauração.
    /// </summary>
    public bool Navegando => _emCurso && _restauracao is null;

    /// <summary>Registros abertos nesta sessão, o mais recente primeiro (só em memória).</summary>
    public IReadOnlyList<ReferenciaRegistro> RegistrosRecentes => _recentes;

    // ---- Ligação com o aplicativo ----

    /// <summary>O Shell novo assume a navegação (histórico recomeça: outro contexto, outras permissões).</summary>
    public void Conectar(IPlataformaNavegacao plataforma)
    {
        _plataforma = plataforma;
        Limpar();
    }

    /// <summary>O Shell saiu (login, outra empresa): solta a plataforma e esquece o histórico.</summary>
    public void Desconectar(IPlataformaNavegacao plataforma)
    {
        if (!ReferenceEquals(_plataforma, plataforma)) return;
        _plataforma = null;
        Limpar();
    }

    public void Limpar()
    {
        if (_restauracao is { } r)
        {
            _restauracao = null;
            r.Cancelamento.Cancel(); // contexto novo: a restauração do antigo para onde está
        }
        _historico.Limpar();
        _recentes.Clear();
        _estadoDasTelas.Clear(); // estado de navegação (inclui pesquisas digitadas) some com o contexto
        Avisar();
    }

    /// <summary>Último estado conhecido da tela (para os testes e diagnósticos; nunca gravado em lugar nenhum).</summary>
    public EstadoTela? EstadoDaTela(string rota) => _estadoDasTelas.TryGetValue(rota, out var e) ? e.Estado : null;

    // ---- O que aconteceu (a plataforma e as telas informam) ----

    /// <summary>O Shell terminou de trocar de tela (fora de uma navegação do motor, ex.: a primeira tela).</summary>
    public void AoChegarNaTela(OrigemNavegacao origem = OrigemNavegacao.Sistema)
    {
        if (!_emCurso) Reconciliar(origem);
    }

    /// <summary>
    /// A tela abriu, trocou ou fechou o registro. Só vale para a tela aberta (uma tela em segundo plano que termina de
    /// carregar depois que o usuário saiu não mexe no histórico).
    /// </summary>
    public void InformarRegistro(ITelaNavegavel tela, OrigemNavegacao origem = OrigemNavegacao.Lista)
    {
        if (_emCurso || _plataforma is not { } p || !ReferenceEquals(p.TelaAtual, tela)) return;
        Reconciliar(origem);
    }

    // ---- Pedidos ----

    /// <summary>Vai para uma tela como ela está (o menu faz assim: a ficha que estava aberta lá continua aberta).</summary>
    public Task<bool> IrParaTelaAsync(string rota, OrigemNavegacao origem) => IrAsync(new LocalNavegacao(rota), origem, ajustarRegistro: false);

    /// <summary>
    /// Vai a um lugar: a tela e, se informado, o registro (links internos, "Abrir", busca, endereço lone://). Verdadeiro se
    /// chegou. Sem permissão para a tela, não vai.
    /// </summary>
    public Task<bool> AbrirAsync(LocalNavegacao destino, OrigemNavegacao origem) =>
        IrAsync(destino, origem, ajustarRegistro: destino.Registro is not null);

    /// <summary>Abre um endereço interno (<c>lone://pessoas/pessoa/{id}</c>). Falso se o endereço não for do Lone.</summary>
    public Task<bool> AbrirEnderecoAsync(string endereco) =>
        LocalNavegacao.TentarLer(endereco, out var local) ? AbrirAsync(local, OrigemNavegacao.Endereco) : Task.FromResult(false);

    /// <summary>
    /// Volta ao lugar anterior do histórico — o real, não o "pai" lógico. Pula telas que o usuário não pode mais abrir.
    /// Se houver alterações não salvas, a tela pergunta antes (e o usuário pode ficar). Verdadeiro se voltou.
    /// </summary>
    public async Task<bool> VoltarAsync()
    {
        if (_plataforma is not { } p) return false;
        if (_emCurso && _restauracao is null) return false; // troca de tela em andamento (a restauração pode ser interrompida)
        var agora = _relogio.GetUtcNow();
        if (agora - _ultimoVoltar < IntervaloMinimoVoltar) return false; // segundo clique de um duplo clique
        _ultimoVoltar = agora;

        var operacao = ++_operacao;
        var interrompida = InterromperRestauracao();

        // Tela que deixou de ser permitida (permissões mudaram): sai do caminho de volta.
        while (Anterior is { } candidata && RotaPermitida?.Invoke(candidata.Local.Rota) == false)
            _historico.Remover(_historico.Entradas.Count - 2);
        if (Anterior is not { } alvo)
        {
            Avisar();
            return false;
        }

        var indice = _historico.Entradas.Count - 2;
        var chegada = new Chegada(p, OrigemNavegacao.Voltar, SoTela: false, Voltando: true, indice, LocalDaTela(p), alvo.Local.Rota);
        var chegou = false;
        _emCurso = true;
        Avisar();
        try
        {
            if (!interrompida) GuardarEstadoAoSair(p); // interrompida: o estado dela já ficou guardado (o que ia ser restaurado)
            // O ← volta ao retrato daquela entrada; sem retrato (entrada deixada sem trocar de tela), ao último da tela.
            var estado = alvo.Estado as EstadoTela ?? EstadoDaTela(alvo.Local.Rota);
            chegou = await ChegarAsync(p, alvo.Local, ajustarRegistro: true, estado, chegada, operacao);
        }
        finally
        {
            if (operacao == _operacao) // interrompida por um pedido novo: quem pediu depois já registrou tudo
            {
                _emCurso = false;
                AplicarChegada(_historico, chegada, real: true);
                Avisar();
            }
        }
        return chegou;
    }

    // ---- Interno ----

    private async Task<bool> IrAsync(LocalNavegacao destino, OrigemNavegacao origem, bool ajustarRegistro)
    {
        if (_plataforma is not { } p) return false;
        if (_emCurso && !PodeInterromperCom(destino)) return false;
        if (RotaPermitida?.Invoke(destino.Rota) == false) return false;

        var operacao = ++_operacao;
        var interrompida = InterromperRestauracao();
        var chegada = new Chegada(p, origem, SoTela: destino.Registro is null);
        _emCurso = true;
        Avisar();
        try
        {
            if (!interrompida) GuardarEstadoAoSair(p); // interrompida: o estado dela já ficou guardado (o que ia ser restaurado)
            // Menu e links: a tela volta como o usuário a deixou da última vez (o registro pedido, se houver, prevalece).
            return await ChegarAsync(p, destino, ajustarRegistro, EstadoDaTela(destino.Rota), chegada, operacao);
        }
        finally
        {
            if (operacao == _operacao) // interrompida por um pedido novo: quem pediu depois já registrou tudo
            {
                _emCurso = false;
                AplicarChegada(_historico, chegada, real: true);
                Avisar();
            }
        }
    }

    /// <summary>
    /// Troca de tela (se preciso), restaura o contexto numa tela recriada e ajusta o registro. Para no meio se o Shell foi
    /// trocado, o usuário ficou ou pediu outro lugar durante a restauração.
    /// </summary>
    private async Task<bool> ChegarAsync(IPlataformaNavegacao p, LocalNavegacao destino, bool ajustarRegistro, EstadoTela? estado,
                                         Chegada chegada, int operacao)
    {
        var restaurou = false;
        if (!string.Equals(p.RotaAtual, destino.Rota, StringComparison.Ordinal))
        {
            await p.IrParaRotaAsync(destino.Rota);
            if (!ReferenceEquals(p, _plataforma) || !string.Equals(p.RotaAtual, destino.Rota, StringComparison.Ordinal))
                return false; // outro contexto no meio do caminho, ou o usuário escolheu continuar editando

            // Tela recriada (o Shell recria ao trocar de módulo): volta como estava. Dentro da navegação do motor: nenhum
            // passo novo no histórico. O registro pedido (← ou link) prevalece sobre o do retrato. Enquanto restaura, o
            // menu e o ← continuam atendendo: um pedido novo interrompe a restauração (InterromperRestauracao).
            if (estado is not null && p.TelaAtual is IEstadoNavegavel nova && !MesmaInstanciaDeixada(destino.Rota, nova))
            {
                var efetivo = ajustarRegistro ? estado with { Registro = destino.Registro } : estado;
                var restauracao = new Restauracao(new CancellationTokenSource(), destino.Rota, efetivo, chegada);
                _restauracao = restauracao;
                Avisar();
                try
                {
                    await nova.RestaurarEstadoAsync(efetivo, restauracao.Cancelamento.Token);
                }
                catch (OperationCanceledException) when (restauracao.Cancelamento.IsCancellationRequested)
                {
                    // Interrompida: nada a fazer (quem interrompeu já registrou a chegada e guardou o estado).
                }
                finally
                {
                    if (ReferenceEquals(_restauracao, restauracao)) _restauracao = null;
                }
                if (operacao != _operacao || restauracao.Cancelamento.IsCancellationRequested || !ReferenceEquals(p, _plataforma))
                    return false;
                restaurou = true;
            }
        }

        if (!ajustarRegistro) return true;
        if (p.TelaAtual is not { } tela) return destino.Registro is null;
        if (MesmoRegistro(tela.RegistroAberto, destino.Registro)) return true;
        if (restaurou) return false; // a restauração já tentou (ex.: registro excluído): não insiste nem mostra erro

        await tela.IrParaRegistroAsync(destino.Registro);
        return ReferenceEquals(p, _plataforma) && ReferenceEquals(p.TelaAtual, tela) && MesmoRegistro(tela.RegistroAberto, destino.Registro);
    }

    /// <summary>
    /// Restauração em andamento: a tela já apareceu e está voltando ao estado guardado (primeira carga, releitura, ficha).
    /// Guarda o que é preciso para interrompê-la e registrar a chegada como a operação faria ao terminar.
    /// </summary>
    private sealed record Restauracao(CancellationTokenSource Cancelamento, string Rota, EstadoTela Estado, Chegada Chegada);

    /// <summary>Como uma operação registra no histórico o lugar em que chegou (ao terminar, ou ao ser interrompida).</summary>
    private sealed record Chegada(IPlataformaNavegacao Plataforma, OrigemNavegacao Origem, bool SoTela, bool Voltando = false,
                                  int Indice = -1, LocalNavegacao? Antes = null, string? RotaAlvo = null);

    /// <summary>Menu para a própria tela que está restaurando não interrompe (a restauração é justamente ela voltando).</summary>
    private bool PodeInterromperCom(LocalNavegacao destino) =>
        _restauracao is { } r && !(destino.Registro is null && string.Equals(destino.Rota, r.Rota, StringComparison.Ordinal));

    /// <summary>
    /// Um pedido novo chegou durante a restauração: cancela-a (a tela para onde está; leituras em andamento são
    /// descartadas por ela), registra a chegada àquela tela — navegação real do usuário, um passo como qualquer outro, sem
    /// a ficha que a restauração ainda ia abrir — e mantém como estado da tela (e retrato da entrada) o que ia ser
    /// restaurado, nunca a tela pela metade. Libera o motor para o pedido novo.
    /// </summary>
    private bool InterromperRestauracao()
    {
        if (_restauracao is not { } r) return false;
        _restauracao = null;
        r.Cancelamento.Cancel();
        AplicarChegada(_historico, r.Chegada, real: true);
        _estadoDasTelas[r.Rota] = (null, r.Estado); // sem instância: voltar a ela restaura de novo, por inteiro
        if (Atual is { } atual && string.Equals(atual.Local.Rota, r.Rota, StringComparison.Ordinal))
            _historico.DefinirEstadoDaAtual(r.Estado);
        _emCurso = false;
        return true;
    }

    /// <summary>Registra no histórico (o real, ou uma cópia para prever o ←) onde a operação de fato chegou.</summary>
    private void AplicarChegada(HistoricoNavegacao historico, Chegada chegada, bool real)
    {
        if (!ReferenceEquals(chegada.Plataforma, _plataforma) || LocalDaTela(chegada.Plataforma) is not { } local) return;
        if (chegada.Voltando)
        {
            // Chegou à tela de volta, mesmo que o registro não abra mais (ex.: excluído): a entrada de volta vira o que de
            // fato abriu, em vez de empilhar um passo novo.
            if (!local.MesmoQue(chegada.Antes) && string.Equals(local.Rota, chegada.RotaAlvo, StringComparison.Ordinal))
            {
                historico.VoltarPara(chegada.Indice, local);
                if (real) LembrarRecente(local);
            }
            else Registrar(local, OrigemNavegacao.Voltar, historico: historico, real: real);
            return;
        }
        Registrar(local, chegada.Origem, chegada.SoTela, historico, real);
    }

    private static bool MesmoRegistro(ReferenciaRegistro? a, ReferenciaRegistro? b) => a is null ? b is null : a.MesmoQue(b);

    /// <summary>
    /// Saindo de onde está: guarda o estado atual da tela e o retrato da entrada atual do histórico. Uma tela que falha ao
    /// se descrever não impede a navegação.
    /// </summary>
    private void GuardarEstadoAoSair(IPlataformaNavegacao p)
    {
        if (p.RotaAtual is not { Length: > 0 } rota || p.TelaAtual is not IEstadoNavegavel tela) return;
        EstadoTela estado;
        try { estado = tela.CapturarEstado(); }
        catch (Exception) { return; }
        _estadoDasTelas[rota] = (new WeakReference<IEstadoNavegavel>(tela), estado);
        if (Atual is { } atual && string.Equals(atual.Local.Rota, rota, StringComparison.Ordinal))
            _historico.DefinirEstadoDaAtual(estado);
    }

    private bool MesmaInstanciaDeixada(string rota, IEstadoNavegavel tela) =>
        _estadoDasTelas.TryGetValue(rota, out var guardado) && guardado.Tela is { } referencia && referencia.TryGetTarget(out var deixada) &&
        ReferenceEquals(deixada, tela);

    private static LocalNavegacao? LocalDaTela(IPlataformaNavegacao p) =>
        p.RotaAtual is { Length: > 0 } rota ? new LocalNavegacao(rota, p.TelaAtual?.RegistroAberto) : null;

    private void Reconciliar(OrigemNavegacao origem, bool soTela = false)
    {
        if (_plataforma is { } p && LocalDaTela(p) is { } local) Registrar(local, origem, soTela);
    }

    private void Registrar(LocalNavegacao local, OrigemNavegacao origem, bool soTela = false, HistoricoNavegacao? historico = null,
                           bool real = true)
    {
        var titulo = TituloDaRota?.Invoke(local.Rota) ?? local.Rota;
        var quando = real ? _relogio.GetUtcNow() : default;
        if ((historico ?? _historico).Registrar(new EntradaNavegacao(local, origem, titulo, quando, SoTela: soTela)) && real) Avisar();
        if (real) LembrarRecente(local);
    }

    private void LembrarRecente(LocalNavegacao local)
    {
        if (local.Registro is not { Novo: false } registro) return;
        _recentes.RemoveAll(r => r.MesmoQue(registro));
        _recentes.Insert(0, registro);
        if (_recentes.Count > MaximoRecentes) _recentes.RemoveAt(_recentes.Count - 1);
    }

    private void Avisar()
    {
        OnPropertyChanged(nameof(PodeVoltar));
        OnPropertyChanged(nameof(DescricaoVoltar));
        OnPropertyChanged(nameof(Navegando));
        OnPropertyChanged(nameof(Atual));
    }
}
