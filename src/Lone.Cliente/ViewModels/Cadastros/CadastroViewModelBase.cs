using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.Api;
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
public abstract partial class CadastroViewModelBase<TItem> : ViewModelBase, IMestreDetalhe where TItem : class
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
            if (e.PropertyName is nameof(PodeSalvarAgora) or nameof(PodeDescartar) or nameof(EstadoFicha) or nameof(TemAlteracoes)) return;
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

    /// <summary>Para a tela (ex.: menu): pode sair desta tela agora?</summary>
    public Task<bool> PodeSairAsync() => PodePerderAlteracoesAsync();

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
        await ExecutarAsync(async () =>
        {
            await AntesDeListarAsync();
            await BuscarListaAsync();
        });
    }

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
        if (value is null || _ajustandoSelecao || (Editando && ReferenceEquals(value, _itemAberto))) return;
        _ = AbrirSelecionadoAsync(value);
    }

    private async Task AbrirSelecionadoAsync(TItem item)
    {
        if (!await PodePerderAlteracoesAsync())
        {
            DefinirSelecao(_itemAberto); // continua editando o que estava aberto
            return;
        }

        if (await ExecutarAsync(() => AbrirAsync(item)))
        {
            _itemAberto = item;
            Editando = true;
            MarcarFichaSemAlteracoes();
        }
        else if (ReferenceEquals(Selecionado, item))
        {
            DefinirSelecao(null); // não abriu: desmarca para poder tentar de novo
        }
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
        DepoisDeListar();
    }

    /// <summary>Depois que as linhas visíveis mudaram (ex.: resumo da paginação). Por padrão, nada.</summary>
    protected virtual void DepoisDeListar() { }
}
