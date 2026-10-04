using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.Api;
using Lone.Cliente.Grade;
using Lone.Cliente.Navegacao;
using Lone.Cliente.Plataforma;
using Lone.Contracts.Comum;

namespace Lone.Cliente.ViewModels.Cadastros;

/// <summary>
/// Base das telas de cadastro "lista + ficha". No computador as duas ficam lado a lado; em tela estreita
/// (celular) aparece uma de cada vez: a lista, e a ficha ao abrir um item. A tela informa a largura
/// (ModoCompacto) e esta classe decide o que mostrar.
/// Também controla as alterações não salvas, igual em todas as telas: fechar, trocar de item, criar outro ou
/// sair da tela com alterações pergunta antes; "Descartar" desfaz as alterações (nunca apaga nada do banco).
/// </summary>
/// <typeparam name="TItem">Linha da lista (resumo vindo da API).</typeparam>
public abstract partial class CadastroViewModelBase<TItem> : ViewModelBase, IMestreDetalhe, IEstadoNavegavel where TItem : class
{
    private IReadOnlyList<TItem> _todos = [];
    private CancellationTokenSource? _buscaAtrasada;

    /// <summary>A tela trocou a busca por código (ex.: aplicou uma visão) e já vai reler: a leitura com espera não precisa sair.</summary>
    protected void CancelarBuscaAtrasada() => _buscaAtrasada?.Cancel();
    private int _versaoLista;
    private readonly IDialogos _dialogos;

    /// <summary>A ficha como estava ao abrir ou ao salvar (para saber se há alterações).</summary>
    private string? _fotoGravada;

    /// <summary>Item cuja ficha está aberta (para voltar a seleção se o usuário desistir de trocar).</summary>
    private TItem? _itemAberto;
    private bool _ajustandoSelecao;

    /// <summary>Acompanha a ficha aberta para o Salvar e o estado reagirem a cada alteração.</summary>
    private readonly ObservadorFicha _observador;

    /// <summary>Último resultado de <see cref="TemAlteracoes"/> que a tela recebeu.</summary>
    private bool _alterada;
    private bool _avaliando;

    protected CadastroViewModelBase(IDialogos dialogos)
    {
        _dialogos = dialogos;
        _observador = new ObservadorFicha(AvaliarAlteracoes);
        PropertyChanged += (_, e) =>
        {
            // Propriedades da própria tela também podem ser parte da ficha (ex.: operações territoriais).
            if (e.PropertyName is nameof(PodeSalvarAgora) or nameof(PodeDescartar) or nameof(EstadoFicha) or nameof(MostrarEstadoFicha) or nameof(TemAlteracoes)
                or nameof(ConteudoLista)) return;
            if (e.PropertyName is nameof(Ocupado) or nameof(Livre)) OnPropertyChanged(nameof(PodeSalvarAgora));
            AvaliarAlteracoes();
        };
    }

    /// <summary>Linhas visíveis (já filtradas pela busca).</summary>
    public ObservableCollection<TItem> Itens { get; } = new();

    [ObservableProperty] private string _busca = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrarLista), nameof(MostrarFicha), nameof(MostrarVazio))]
    private bool _modoCompacto;

    /// <summary>Há uma ficha aberta (item existente ou novo).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrarLista), nameof(MostrarFicha), nameof(MostrarVazio), nameof(SemFicha))]
    private bool _editando;

    /// <summary>Linha marcada na lista (abre a ficha).</summary>
    [ObservableProperty] private TItem? _selecionado;

    public bool MostrarLista => !ModoCompacto || !Editando;
    public bool MostrarFicha => Editando;

    /// <summary>Sem ficha aberta: as mensagens aparecem em cima da lista.</summary>
    public bool SemFicha => !Editando;

    /// <summary>Computador sem ficha aberta: mensagem "escolha um item" no lugar da ficha.</summary>
    public bool MostrarVazio => !ModoCompacto && !Editando;

    public bool ListaVazia => Itens.Count == 0;

    /// <summary>
    /// Pode criar registro nesta tela (padrão do botão Novo, 03/10/2026): sem permissão, o "+ Novo" e o Ctrl+N somem
    /// (a API continua conferindo). Padrão: sim — nas telas cujo acesso já é a permissão de manter o cadastro.
    /// </summary>
    public virtual bool PodeCriar => true;

    /// <summary>
    /// Falso (padrão): a lista vem inteira e a busca filtra no aparelho. Verdadeiro: cada busca vai ao servidor
    /// (cadastros grandes, como pessoas), com uma pequena espera enquanto o usuário digita.
    /// </summary>
    protected virtual bool BuscaNoServidor => false;

    /// <summary>Texto usado pela busca no aparelho (nome, login etc.).</summary>
    protected abstract string TextoDeBusca(TItem item);

    /// <summary>Busca as linhas na API.</summary>
    protected abstract Task<IReadOnlyList<TItem>> ListarAsync();

    /// <summary>Abre a ficha do item escolhido na lista.</summary>
    protected abstract Task AbrirAsync(TItem item);

    /// <summary>Prepara a ficha vazia de um item novo.</summary>
    protected abstract Task NovoItemAsync();

    /// <summary>Conteúdo atual da ficha como será enviado (o DTO); nulo sem ficha. Base da detecção de alterações.</summary>
    protected abstract object? DadosDaFicha();

    /// <summary>A ficha aberta é de um item ainda não gravado.</summary>
    protected abstract bool FichaNova { get; }

    /// <summary>Relê do servidor o item aberto (usado ao descartar as alterações de um item existente).</summary>
    protected abstract Task RecarregarFichaAsync();

    // ---- Alterações não salvas ----

    /// <summary>Há algo na ficha diferente do que foi aberto ou salvo.</summary>
    public bool TemAlteracoes => Editando && Foto() != _fotoGravada;

    /// <summary>Chamado depois de abrir, criar, salvar ou descartar: o que está na ficha passa a ser o "gravado".</summary>
    protected void MarcarFichaSemAlteracoes()
    {
        _fotoGravada = Foto();
        _observador.Observar(Editando ? FichaObservada : null);
        AvaliarAlteracoes();
        Navegacao.InformarRegistro(this, _origemRelato); // abriu, criou, gravou (o nome pode ter mudado) ou descartou
    }

    /// <summary>
    /// O objeto da ficha (normalmente o formulário) que o observador acompanha, em qualquer nível. Nulo: só as propriedades
    /// da própria tela (quando a ficha é feita delas).
    /// </summary>
    protected virtual object? FichaObservada => null;

    /// <summary>
    /// Salvar habilitado: item novo (salvar mostra o que falta preencher) ou item existente com alteração. Sem alteração,
    /// não há o que gravar nem o que confirmar (regra de feedback: nada de "Salvo" redundante).
    /// </summary>
    public bool PodeSalvarAgora => Livre && Editando && (FichaNova || _alterada);

    /// <summary>Descartar só com o que desfazer (sem alterações, "Fechar" já sai da ficha).</summary>
    public bool PodeDescartar => Editando && _alterada;

    /// <summary>Estado da ficha na barra de ações (texto, não só a cor do botão: acessível e sem ambiguidade).</summary>
    public string EstadoFicha => !Editando ? string.Empty
        : _alterada ? "Alterações não salvas"
        : FichaNova ? "Novo cadastro, ainda não salvo"
        : "Sem alterações";

    /// <summary>
    /// O estado merece aparecer: só com algo a avisar (alterações não salvas ou cadastro novo). Sem alterações a barra não
    /// diz nada (padrão da barra da ficha, 03/10/2026, docs/UX-ARQUITETURA.md). Telas que ainda mostram sempre o
    /// <see cref="EstadoFicha"/> continuam iguais.
    /// </summary>
    public bool MostrarEstadoFicha => Editando && (_alterada || FichaNova);

    /// <summary>Compara a ficha com a versão gravada e avisa a tela se o resultado mudou.</summary>
    private void AvaliarAlteracoes()
    {
        if (_avaliando) return;
        _avaliando = true;
        try
        {
            // Montar a ficha para comparar não pode derrubar a digitação: se falhar num estado intermediário, conta
            // como alterada (o Salvar fica disponível e a validação da gravação explica o que falta).
            try { _alterada = TemAlteracoes; }
            catch (Exception) { _alterada = Editando; }
            OnPropertyChanged(nameof(PodeSalvarAgora));
            OnPropertyChanged(nameof(PodeDescartar));
            OnPropertyChanged(nameof(EstadoFicha));
            OnPropertyChanged(nameof(MostrarEstadoFicha));
            DescartarCommand.NotifyCanExecuteChanged();
        }
        finally
        {
            _avaliando = false;
        }
    }

    private string? Foto() => DadosDaFicha() is { } dados ? JsonSerializer.Serialize(dados, dados.GetType(), OpcoesJson.Padrao) : null;

    /// <summary>Sem alterações, ou o usuário aceitou perdê-las. Pergunta só quando há o que perder.</summary>
    protected async Task<bool> PodePerderAlteracoesAsync()
    {
        if (!TemAlteracoes) return true;
        return await _dialogos.ConfirmarAsync(
            "Alterações não salvas",
            "Existem alterações não salvas. Deseja realmente descartá-las?",
            "Descartar alterações",
            "Continuar editando");
    }

    /// <summary>
    /// Para a tela (ex.: menu): pode sair desta tela agora? Quem aceita perder as alterações as perde de fato: a ficha
    /// relê o que está gravado (ou fecha, se era nova). Assim o descarte nunca "volta" depois, mesmo que a tela não seja
    /// recriada.
    /// </summary>
    public async Task<bool> PodeSairAsync()
    {
        if (!TemAlteracoes) return true;
        if (!await PodePerderAlteracoesAsync()) return false;
        await DescartarAoSairAsync();
        return true;
    }

    private async Task DescartarAoSairAsync()
    {
        if (FichaNova)
        {
            FecharSemPerguntar();
            return;
        }
        if (await ExecutarAsync(RecarregarFichaAsync)) MarcarFichaSemAlteracoes();
        else FecharSemPerguntar(); // sem conseguir reler: fecha (nunca fica o conteúdo descartado)
    }

    protected Task<bool> ConfirmarAsync(string titulo, string mensagem, string aceitar, string cancelar) =>
        _dialogos.ConfirmarAsync(titulo, mensagem, aceitar, cancelar);

    protected Task<string?> PerguntarAsync(string titulo, string mensagem, string aceitar, string cancelar, string? dica = null, int tamanhoMaximo = 200) =>
        _dialogos.PerguntarAsync(titulo, mensagem, aceitar, cancelar, dica, tamanhoMaximo);

    /// <summary>Menu de ações ("⋯"): a opção escolhida ou nulo.</summary>
    protected Task<string?> EscolherAsync(string titulo, IReadOnlyList<string> opcoes) =>
        _dialogos.EscolherAsync(titulo, "Cancelar", opcoes);

    /// <summary>
    /// Descartar = desfazer o que não foi salvo. Item novo: cancela a inclusão e volta à lista. Item existente:
    /// volta aos dados gravados. Sem alterações: só volta à lista. Nunca exclui nada.
    /// </summary>
    [RelayCommand(CanExecute = nameof(PodeDescartar))]
    private async Task DescartarAsync()
    {
        if (!Editando) return;

        if (!TemAlteracoes)
        {
            FecharSemPerguntar();
            return;
        }

        if (!await PodePerderAlteracoesAsync()) return;

        if (FichaNova)
        {
            FecharSemPerguntar();
            return;
        }

        if (await ExecutarAsync(RecarregarFichaAsync))
        {
            MarcarFichaSemAlteracoes();
            Mostrar("Alterações descartadas.", TipoMensagem.Informacao);
        }
    }

    [RelayCommand]
    private async Task CarregarAsync()
    {
        try
        {
            await ExecutarAsync(async () =>
            {
                await AntesDeListarAsync();
                await BuscarListaAsync();
            });
        }
        finally
        {
            _primeiraCarga.TrySetResult(); // com ou sem sucesso: quem esperava pode seguir
        }
    }

    /// <summary>
    /// Terminou a primeira carga da tela. O Shell recria a tela ao voltar de outro módulo: um registro pedido pela navegação
    /// (Voltar, link) espera a tela nova carregar antes de abrir — senão a abertura seria recusada por "ocupado".
    /// </summary>
    private readonly TaskCompletionSource _primeiraCarga = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Limite da espera pela primeira carga (tela que não carrega sozinha não trava a navegação).</summary>
    internal static TimeSpan EsperaMaximaCarga { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>Carrega dados de apoio (perfis, empresas...) antes da lista. Por padrão, nada.</summary>
    protected virtual Task AntesDeListarAsync() => Task.CompletedTask;

    [RelayCommand]
    private async Task NovoAsync()
    {
        if (!await PodePerderAlteracoesAsync()) return;
        DefinirSelecao(null);
        _itemAberto = null;
        if (await ExecutarAsync(NovoItemAsync))
        {
            Editando = true;
            MarcarFichaSemAlteracoes();
        }
    }

    /// <summary>Fecha a ficha (no celular, volta para a lista), perguntando antes se houver alterações.</summary>
    [RelayCommand]
    private async Task FecharFichaAsync()
    {
        if (await PodePerderAlteracoesAsync())
            FecharSemPerguntar();
    }

    protected void FecharSemPerguntar()
    {
        Editando = false;
        _itemAberto = null;
        _fotoGravada = null;
        _observador.Observar(null); // solta a ficha fechada
        DefinirSelecao(null);
        LimparMensagem();
        AvaliarAlteracoes();
        Navegacao.InformarRegistro(this); // de volta à lista
    }

    /// <summary>Muda a linha marcada sem abrir ficha (ex.: voltar a marcação quando o usuário desiste de trocar).</summary>
    private void DefinirSelecao(TItem? item)
    {
        _ajustandoSelecao = true;
        try { Selecionado = item; }
        finally { _ajustandoSelecao = false; }
    }

    partial void OnBuscaChanged(string value)
    {
        if (BuscaNoServidor) _ = RecarregarComAtrasoAsync();
        else Filtrar();
    }

    /// <summary>Relê a lista no servidor (ex.: filtro mudou). Erros aparecem na tela.</summary>
    protected async Task RecarregarAsync()
    {
        try
        {
            await BuscarListaAsync();
        }
        catch (SessaoExpiradaException)
        {
            // O aplicativo já volta ao login com a mensagem.
        }
        catch (Exception ex)
        {
            MostrarErro(ex);
        }
    }

    private async Task RecarregarComAtrasoAsync()
    {
        _buscaAtrasada?.Cancel();
        var cts = _buscaAtrasada = new CancellationTokenSource();
        try
        {
            await Task.Delay(350, cts.Token);
            await RecarregarAsync();
        }
        catch (OperationCanceledException)
        {
            // O usuário continuou digitando: vale a busca mais nova.
        }
        finally
        {
            if (ReferenceEquals(_buscaAtrasada, cts)) _buscaAtrasada = null;
            cts.Dispose();
        }
    }

    /// <summary>Só a resposta da busca mais recente é mostrada (uma mais antiga pode chegar depois).</summary>
    private async Task BuscarListaAsync()
    {
        var versao = ++_versaoLista;
        var itens = await ListarAsync();
        if (versao != _versaoLista) return;
        _todos = itens;
        Filtrar();
    }

    partial void OnSelecionadoChanged(TItem? value)
    {
        MarcarLinhaAberta(value);
        if (value is null || _ajustandoSelecao || (Editando && ReferenceEquals(value, _itemAberto))) return;
        _ = AbrirItemAsync(value);
    }

    /// <summary>Abre a ficha do item (perguntando antes se houver alterações). Verdadeiro se abriu.</summary>
    private async Task<bool> AbrirItemAsync(TItem item)
    {
        if (!await PodePerderAlteracoesAsync())
        {
            DefinirSelecao(_itemAberto); // continua editando o que estava aberto
            return false;
        }

        if (await ExecutarAsync(() => AbrirAsync(item)))
        {
            _itemAberto = item;
            Editando = true;
            MarcarFichaSemAlteracoes();
            return true;
        }
        if (ReferenceEquals(Selecionado, item))
            DefinirSelecao(null); // não abriu: desmarca para poder tentar de novo
        return false;
    }

    // ---- Navegação global (motor de navegação: histórico, Voltar, links) ----

    /// <summary>Motor de navegação do aplicativo; os testes trocam por um próprio.</summary>
    public GerenciadorNavegacao Navegacao { get; set; } = GerenciadorNavegacao.Padrao;

    /// <summary>
    /// Registro aberto na ficha, como a navegação o conhece (tipo, identificação e nome de exibição — nunca documento).
    /// Nulo = só a lista. Ficha de item novo: identificação nula.
    /// </summary>
    public ReferenciaRegistro? RegistroAberto => !Editando ? null
        : FichaNova ? new ReferenciaRegistro(TipoRegistro, null, "Novo cadastro", Novo: true)
        : new ReferenciaRegistro(TipoRegistro, IdDaFicha, TituloDaFicha ?? "Registro");

    /// <summary>Tipo do registro nos endereços internos (lone://rota/tipo/id). Por padrão, o nome da tela.</summary>
    protected virtual string TipoRegistro =>
        (GetType().Name.EndsWith("ViewModel", StringComparison.Ordinal) ? GetType().Name[..^"ViewModel".Length] : GetType().Name).ToLowerInvariant();

    /// <summary>Identificação do registro aberto: o Id da ficha ou, na falta, o da linha da lista.</summary>
    protected virtual Guid? IdDaFicha => LeituraDePropriedade.Id(FichaObservada) ?? (_itemAberto is { } item ? IdDoItem(item) : null);

    /// <summary>Identificação de uma linha da lista (por padrão, a propriedade Id). Telas com outra forma sobrescrevem.</summary>
    protected virtual Guid? IdDoItem(TItem item) => LeituraDePropriedade.Id(item);

    /// <summary>Nome do registro aberto: título ou nome da ficha; na falta, o texto da linha da lista.</summary>
    protected virtual string? TituloDaFicha =>
        LeituraDePropriedade.Texto(FichaObservada, "Titulo") ?? LeituraDePropriedade.Texto(FichaObservada, "Nome")
        ?? (_itemAberto is { } item ? TextoDeBusca(item) : null);

    /// <summary>
    /// Linha para abrir um registro que não está na lista carregada (ex.: link de outra tela). Nulo = esta tela só abre o
    /// que está na lista.
    /// </summary>
    protected virtual TItem? CriarItemParaAbrir(Guid id) => null;

    /// <summary>A navegação pede um registro (ou a lista, com nulo). Pergunta antes se houver alterações não salvas.</summary>
    public async Task<bool> IrParaRegistroAsync(ReferenciaRegistro? registro)
    {
        if (registro is not null && !_primeiraCarga.Task.IsCompleted)
            await Task.WhenAny(_primeiraCarga.Task, Task.Delay(EsperaMaximaCarga));
        if (registro is null)
        {
            if (!Editando) return true;
            await FecharFichaCommand.ExecuteAsync(null);
            return !Editando;
        }
        if (registro.Novo)
        {
            await NovoCommand.ExecuteAsync(null);
            return Editando && FichaNova;
        }
        if (RegistroAberto?.MesmoQue(registro) == true) return true;
        if (registro.Id is not { } id) return false; // sem identificação não há como reabrir

        var item = Itens.FirstOrDefault(i => IdDoItem(i) == id)
                   ?? _todos.FirstOrDefault(i => IdDoItem(i) == id)
                   ?? CriarItemParaAbrir(id);
        if (item is null) return false;
        DefinirSelecao(item);
        return await AbrirItemAsync(item);
    }

    // ---- Estado de navegação (preservação de contexto: pesquisa, ficha, rolagem; cada tela pode acrescentar) ----

    /// <summary>Posições das rolagens marcadas (<c>Rolagem.Preservar</c>) desta tela.</summary>
    public MemoriaRolagem Rolagem { get; } = new();

    /// <summary>
    /// Como a tela está (o motor guarda ao sair — antes de perguntar sobre alterações não salvas). Nunca o conteúdo
    /// editado: da ficha, só o tipo e o Id (sem o título, que pode trazer um nome digitado e depois descartado).
    /// </summary>
    public virtual EstadoTela CapturarEstado() => new()
    {
        Busca = Busca,
        Registro = RegistroAberto is { Novo: false, Id: not null } registro ? registro with { Titulo = string.Empty } : null,
        Rolagens = Rolagem.Capturar()
    };

    /// <summary>
    /// Reaplica o estado numa tela recriada, na ordem aprovada: primeira carga → contexto da lista (uma releitura, se
    /// preciso) → ficha → detalhes (aba, prévia) → rolagem. Tolerante (registro que não abre mais fica de fora, sem
    /// mensagem de erro) e idempotente (só muda o que difere). Cancelada (o usuário foi para outro lugar): nenhum passo
    /// seguinte começa e a lista que ainda estiver sendo lida é descartada ao chegar.
    /// </summary>
    public async Task RestaurarEstadoAsync(EstadoTela estado, CancellationToken cancelamento = default)
    {
        if (cancelamento.IsCancellationRequested) return;
        using var descartarLista = cancelamento.Register(() => _versaoLista++); // leitura em andamento: resultado ignorado

        if (!_primeiraCarga.Task.IsCompleted)
            await Task.WhenAny(_primeiraCarga.Task, Task.Delay(EsperaMaximaCarga, cancelamento));
        if (cancelamento.IsCancellationRequested) return;

        if (await AplicarContextoDaListaAsync(estado) && !cancelamento.IsCancellationRequested) await RecarregarAsync();
        if (cancelamento.IsCancellationRequested) return;

        // Registro que já não abriu nesta tela (excluído, sem permissão): não tenta de novo (idempotente, sem nova leitura).
        if (estado.Registro is { Novo: false, Id: { } id } registro && RegistroAberto?.MesmoQue(registro) != true &&
            _registroRecusadoNaRestauracao != id)
        {
            var mensagemAntes = Mensagem;
            var abriu = await IrParaRegistroAsync(registro);
            if (cancelamento.IsCancellationRequested) return;
            if (abriu) _registroRecusadoNaRestauracao = null;
            else
            {
                _registroRecusadoNaRestauracao = id;
                if (TipoMensagem == TipoMensagem.Erro && Mensagem != mensagemAntes)
                    LimparMensagem(); // fica na lista, sem erro de navegação ("o mundo mudou" não é erro)
            }
        }

        await RestaurarDetalhesAsync(estado);
        if (cancelamento.IsCancellationRequested) return;
        Rolagem.Restaurar(estado.Rolagens);
    }

    /// <summary>Registro do estado que esta tela já tentou reabrir e não conseguiu.</summary>
    private Guid? _registroRecusadoNaRestauracao;

    /// <summary>
    /// Reaplica o contexto da lista (a base: a pesquisa). Verdadeiro se a lista precisa ser relida do servidor — a base
    /// relê uma vez só, depois de tudo aplicado.
    /// </summary>
    protected virtual Task<bool> AplicarContextoDaListaAsync(EstadoTela estado)
    {
        if (Busca == estado.Busca) return Task.FromResult(false);
        Busca = estado.Busca;
        if (!BuscaNoServidor) return Task.FromResult(false); // pesquisa local já filtrou
        CancelarBuscaAtrasada(); // a releitura única (quem chama) já leva a pesquisa
        return Task.FromResult(true);
    }

    /// <summary>Reaplica o que depende da lista e da ficha já prontas (aba da ficha, prévia). Por padrão, nada.</summary>
    protected virtual Task RestaurarDetalhesAsync(EstadoTela estado) => Task.CompletedTask;

    /// <summary>Como a próxima abertura de ficha é informada ao histórico (<see cref="AbrirPorLinkAsync"/> muda).</summary>
    private OrigemNavegacao _origemRelato = OrigemNavegacao.Lista;

    /// <summary>
    /// Abre o registro pedido por outra tela ("Abrir ficha", pedido pendente retirado ao aparecer): para o histórico, a
    /// chegada à tela e a ficha são um passo só, e o Voltar leva de volta à tela de origem.
    /// </summary>
    public async Task<bool> AbrirPorLinkAsync(ReferenciaRegistro registro)
    {
        _origemRelato = OrigemNavegacao.Link;
        try { return await IrParaRegistroAsync(registro); }
        finally { _origemRelato = OrigemNavegacao.Lista; }
    }

    /// <summary>
    /// Depois de gravar: relê a lista mantendo a ficha aberta. A gravação já valeu; se a releitura falhar,
    /// só avisa (sem desfazer nada nem fechar a ficha).
    /// </summary>
    protected async Task AtualizarListaAposGravarAsync()
    {
        try
        {
            await BuscarListaAsync();
        }
        catch (SessaoExpiradaException)
        {
            // O aplicativo já volta ao login com a mensagem.
        }
        catch (Exception ex)
        {
            Mostrar($"A alteração foi gravada, mas a lista não pôde ser atualizada: {ex.Message}", TipoMensagem.Aviso);
        }
    }

    private void Filtrar()
    {
        var termo = BuscaNoServidor ? string.Empty : Busca.Trim();
        Itens.Clear();
        foreach (var item in _todos.Where(i => termo.Length == 0
                                              || TextoDeBusca(i).Contains(termo, StringComparison.CurrentCultureIgnoreCase)))
            Itens.Add(item);
        OnPropertyChanged(nameof(ListaVazia));
        if (GradeDaLista is { } grade) ConteudoLista = grade.Montar(Itens, Selecionado); // uma troca só, depois de filtrar
        DepoisDeListar();
    }

    // ---- Lista em colunas (padrão de tela de cadastro, 03/10/2026) ----

    private GradeCadastro<TItem>? _gradeDaLista;
    private bool _gradeCriada;

    /// <summary>
    /// Colunas da lista desta tela (padrão de tela de cadastro: título, lista larga em colunas, ficha em página própria).
    /// Nulo = a tela ainda usa o layout antigo (lista estreita ao lado da ficha).
    /// </summary>
    protected virtual GradeCadastro<TItem>? CriarGradeDaLista() => null;

    public GradeCadastro<TItem>? GradeDaLista
    {
        get
        {
            if (!_gradeCriada)
            {
                _gradeCriada = true;
                _gradeDaLista = CriarGradeDaLista();
                // Clicou no título de uma coluna: a lista se remonta na nova ordem (a ficha aberta continua marcada).
                if (_gradeDaLista is { } grade) grade.OrdemMudou += () => ConteudoLista = grade.Montar(Itens, Selecionado);
            }
            return _gradeDaLista;
        }
    }

    /// <summary>O que a lista em colunas mostra (colunas e linhas juntas).</summary>
    [ObservableProperty] private ConteudoGrade _conteudoLista = ConteudoGrade.Vazio;

    /// <summary>Tocar numa linha abre a ficha do registro.</summary>
    [RelayCommand]
    private void AbrirRegistroDaLinha(ILinhaGrade? linha)
    {
        if (linha is LinhaCadastro { Item: TItem item })
        {
            if (ReferenceEquals(Selecionado, item) && !Editando) DefinirSelecao(null); // reabrir o mesmo depois de fechar
            Selecionado = item;
        }
    }

    /// <summary>Marca na lista o registro aberto.</summary>
    private void MarcarLinhaAberta(TItem? aberto)
    {
        foreach (var linha in ConteudoLista.Linhas.OfType<LinhaCadastro>())
            linha.Selecionada = ReferenceEquals(linha.Item, aberto);
    }

    /// <summary>Depois que as linhas visíveis mudaram (ex.: resumo da paginação). Por padrão, nada.</summary>
    protected virtual void DepoisDeListar() { }
}
