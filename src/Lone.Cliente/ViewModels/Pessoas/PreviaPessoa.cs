using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.Api;
using Lone.Domain.Validacao;

namespace Lone.Cliente.ViewModels.Pessoas;

/// <summary>
/// Prévia ao lado da lista (Etapa 3): o essencial da pessoa sem sair da lista — cabeçalho da linha e o mesmo "Resumo da
/// pessoa" da ficha (situação, bloqueios, documentos, pendências). Só lê: nada é gravado daqui. Fica aberta de uma pessoa
/// para outra (só troca o conteúdo) e ao voltar da ficha, que é relida. Só aparece quando cabe ao lado da lista
/// (<see cref="Cabe"/>); no celular, tocar continua abrindo a ficha.
/// </summary>
public sealed partial class PreviaPessoa : ObservableObject
{
    /// <summary>Largura do painel (a tela soma o espaço entre ele e a lista).</summary>
    public const double Largura = 380;

    private readonly Func<Guid, CancellationToken, Task<PessoaFormulario>> _ler;
    private readonly Func<Guid, SecaoPessoa?, Task> _abrirFicha;
    private readonly Func<IReadOnlyList<LinhaPessoa>> _linhas;
    private CancellationTokenSource? _leitura;

    /// <param name="ler">Lê a pessoa como a ficha lê (mesma permissão e mesmos dados).</param>
    /// <param name="abrirFicha">Abre a ficha da pessoa, opcionalmente já numa aba (item do resumo).</param>
    /// <param name="linhas">Linhas da página atual (anterior/próxima).</param>
    public PreviaPessoa(Func<Guid, CancellationToken, Task<PessoaFormulario>> ler, Func<Guid, SecaoPessoa?, Task> abrirFicha,
                        Func<IReadOnlyList<LinhaPessoa>> linhas)
    {
        _ler = ler;
        _abrirFicha = abrirFicha;
        _linhas = linhas;
    }

    /// <summary>O mesmo resumo da ficha (as fontes são as mesmas: um módulo novo aparece nos dois lugares).</summary>
    public ResumoPessoa Resumo { get; } = new();

    /// <summary>
    /// Espera antes de ler: andando rápido pela lista, só a última pessoa é lida (as anteriores são canceladas).
    /// </summary>
    public TimeSpan EsperaParaLer { get; set; } = TimeSpan.FromMilliseconds(150);

    /// <summary>A leitura em andamento (os testes esperam por ela).</summary>
    public Task Lendo { get; private set; } = Task.CompletedTask;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Visivel))]
    private bool _aberta;

    /// <summary>Há espaço ao lado da lista (definido pela largura da tela).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Visivel))]
    private bool _cabe;

    public bool Visivel => Aberta && Cabe;

    /// <summary>
    /// A janela estreitou (a prévia some, a linha deixa de ficar marcada) ou alargou (volta, relendo se ficou
    /// desatualizada enquanto estava escondida).
    /// </summary>
    partial void OnCabeChanged(bool value)
    {
        if (!Aberta || Linha is not { } linha) return;
        linha.NaPrevia = value;
        if (value && _desatualizada) Reler();
    }

    /// <summary>Precisava reler (voltou da ficha) quando estava escondida: relê ao aparecer.</summary>
    private bool _desatualizada;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemLinha), nameof(Nome), nameof(Detalhe), nameof(Iniciais), nameof(EhEmpresa),
        nameof(Papeis), nameof(TemTelefone), nameof(TemEmail), nameof(Posicao), nameof(TemAnterior), nameof(TemProxima))]
    private LinhaPessoa? _linha;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrarResumo))]
    private bool _carregando;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemErro), nameof(MostrarResumo))]
    private string _erro = string.Empty;

    public bool TemLinha => Linha is not null;
    public bool TemErro => Erro.Length > 0;
    public bool MostrarResumo => !Carregando && !TemErro;

    public string Nome => Linha?.Nome ?? string.Empty;
    public string Iniciais => Linha?.Iniciais ?? string.Empty;
    public bool EhEmpresa => Linha?.EhEmpresa == true;
    public IReadOnlyList<SeloPapel> Papeis => Linha?.Papeis ?? [];
    public bool TemTelefone => Linha?.TemTelefone == true;
    public bool TemEmail => Linha?.TemEmail == true;

    /// <summary>"Cód. 12 · 123.456.789-09 · Curvelo/MG" (o que a pessoa tiver).</summary>
    public string Detalhe => Linha?.Pessoa is not { } p
        ? string.Empty
        : string.Join(" · ", new[] { $"Cód. {p.CodigoFormatado}", p.DocumentoFormatado, p.Local }.Where(t => t.Length > 0));

    /// <summary>"3 de 50" (posição na página atual).</summary>
    public string Posicao => Indice() is { } i && i >= 0 ? $"{i + 1} de {_linhas().Count}" : string.Empty;
    public bool TemAnterior => Indice() > 0;
    public bool TemProxima => Indice() is { } i && i >= 0 && i < _linhas().Count - 1;

    private int? Indice()
    {
        if (Linha is not { } atual) return null;
        var linhas = _linhas();
        for (var i = 0; i < linhas.Count; i++)
            if (linhas[i].Pessoa.Id == atual.Pessoa.Id) return i;
        return -1;
    }

    /// <summary>Mostra a pessoa (abre a prévia, se estava fechada). A mesma pessoa já mostrada não é lida de novo.</summary>
    public void Mostrar(LinhaPessoa linha)
    {
        var mesma = Linha?.Pessoa.Id == linha.Pessoa.Id && Aberta && !TemErro;
        Aberta = true;
        if (mesma)
        {
            Marcar(linha);
            return;
        }
        TrocarPara(linha);
    }

    /// <summary>Relê a pessoa mostrada (ex.: ao voltar da ficha, que pode ter sido alterada).</summary>
    public void Reler()
    {
        if (!Aberta || Linha is not { } linha) return;
        _desatualizada = !Cabe; // escondida: relê só quando aparecer
        if (Cabe) Lendo = LerAsync(linha.Pessoa.Id);
    }

    /// <summary>Cancela a leitura em andamento (ex.: o duplo clique já vai abrir a ficha).</summary>
    public void CancelarLeitura() => _leitura?.Cancel();

    /// <summary>
    /// A lista foi relida (busca, página, colunas): a linha mostrada passa a ser a nova, com a mesma pessoa, sem ler de
    /// novo. Se a pessoa saiu da página, a prévia continua mostrando-a.
    /// </summary>
    public void Sincronizar(IEnumerable<LinhaPessoa> linhas)
    {
        if (!Aberta || Linha is not { } atual) return; // fechada: nenhuma linha fica marcada
        if (linhas.FirstOrDefault(l => l.Pessoa.Id == atual.Pessoa.Id) is { } nova) Marcar(nova);
        else OnPropertyChanged(string.Empty); // posição, anterior e próxima mudaram
    }

    [RelayCommand]
    private void Fechar()
    {
        Aberta = false;
        _leitura?.Cancel();
        if (Linha is { } linha) linha.NaPrevia = false;
    }

    [RelayCommand]
    private Task AbrirFichaAsync() => Linha is { } linha ? _abrirFicha(linha.Pessoa.Id, null) : Task.CompletedTask;

    [RelayCommand]
    private void Anterior()
    {
        if (Indice() is { } i && i > 0) TrocarPara(_linhas()[i - 1]);
    }

    [RelayCommand]
    private void Proxima()
    {
        var linhas = _linhas();
        if (Indice() is { } i && i >= 0 && i < linhas.Count - 1) TrocarPara(linhas[i + 1]);
    }

    private void TrocarPara(LinhaPessoa linha)
    {
        Marcar(linha);
        Lendo = LerAsync(linha.Pessoa.Id);
    }

    /// <summary>Destaca a linha mostrada na lista (e só ela).</summary>
    private void Marcar(LinhaPessoa linha)
    {
        if (Linha is { } anterior && !ReferenceEquals(anterior, linha)) anterior.NaPrevia = false;
        linha.NaPrevia = Cabe;
        if (ReferenceEquals(Linha, linha)) OnPropertyChanged(string.Empty);
        else Linha = linha;
    }

    private async Task LerAsync(Guid id)
    {
        _leitura?.Cancel();
        var cts = _leitura = new CancellationTokenSource();
        Carregando = true;
        Erro = string.Empty;
        _desatualizada = false;
        try
        {
            if (EsperaParaLer > TimeSpan.Zero) await Task.Delay(EsperaParaLer, cts.Token);
            var ficha = await _ler(id, cts.Token);
            if (cts.IsCancellationRequested) return;
            Resumo.Atualizar(ficha, aba => _ = _abrirFicha(id, aba));
        }
        catch (OperationCanceledException)
        {
            // Outra pessoa foi escolhida antes: vale a mais nova.
        }
        catch (SessaoExpiradaException)
        {
            // O aplicativo já volta ao login.
        }
        catch (Exception ex)
        {
            if (cts.IsCancellationRequested) return;
            Resumo.Atualizar(null, _ => { });
            Erro = ex is ValidacaoException v ? string.Join(" ", v.Erros) : "Não foi possível mostrar o resumo agora.";
        }
        finally
        {
            if (ReferenceEquals(_leitura, cts))
            {
                _leitura = null;
                Carregando = false;
            }
            cts.Dispose();
        }
    }
}
