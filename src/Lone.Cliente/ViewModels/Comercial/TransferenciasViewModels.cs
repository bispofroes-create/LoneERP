using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.Api;
using Lone.Cliente.Navegacao;
using Lone.Cliente.Plataforma;
using Lone.Cliente.ViewModels.Cadastros;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Comercial;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enums;
using Lone.Domain.Validacao;

namespace Lone.Cliente.ViewModels.Comercial;

/// <summary>Textos da transferência e da carteira em uma data (iguais na lista, na prévia e no resultado).</summary>
public static class TextosTransferencia
{
    public static string Clientes(int n) => n == 1 ? "1 cliente" : $"{n.ToString("N0", TextoTela.Brasil)} clientes";

    public static string Resultado(ResultadoItemTransferencia r, bool previa) => r switch
    {
        ResultadoItemTransferencia.Transferido => previa ? "Será transferido" : "Transferido",
        ResultadoItemTransferencia.NaoProcessado => previa ? "Não será processado" : "Não processado",
        _ => "Erro"
    };

    public static string Origem(OrigemVinculoCarteira o) => o switch
    {
        OrigemVinculoCarteira.Substituicao => "substituição na ficha",
        OrigemVinculoCarteira.Transferencia => "transferência",
        OrigemVinculoCarteira.Distribuicao => "distribuição",
        OrigemVinculoCarteira.Importacao => "importação",
        _ => "incluído na ficha"
    };

    /// <summary>"desde 01/01/2025 até 30/09/2026" / "desde 01/01/2025".</summary>
    public static string Periodo(DateOnly? inicio, DateOnly? fim) =>
        (inicio is { } i ? $"desde {TextoTela.Data(i)}" : string.Empty) + (fim is { } f ? $" até {TextoTela.Data(f)}" : string.Empty);
}

// ======================================================================= Lista

/// <summary>Linha da lista de transferências.</summary>
public sealed class LinhaTransferencia
{
    public LinhaTransferencia(TransferenciaDto t) => Item = t;

    public TransferenciaDto Item { get; }
    public Guid Id => Item.Id;
    public string Titulo => $"{Item.Numero} · {Item.Origem ?? "?"}";
    public string Detalhe => $"a partir de {TextoTela.Data(Item.EfeitoEm)} · {Item.Papel ?? "todos os papéis"}" +
                             (Item.Empresa is { } e ? $" · {e}" : string.Empty);
    public string Resumo => ResultadoTransferencia.Contagens(Item);
}

// ======================================================================= Clientes (prévia e resultado)

/// <summary>
/// Um cliente na prévia ou no resultado (os itens vêm por vínculo; aqui ficam juntos por cliente). Na prévia, o cliente
/// que será transferido pode ser desmarcado e, com vários destinos, ter o destino trocado.
/// </summary>
public sealed partial class LinhaClienteTransferencia : ObservableObject
{
    private readonly IReadOnlyList<ItemTransferenciaDto> _itens;

    private LinhaClienteTransferencia(IReadOnlyList<ItemTransferenciaDto> itens, bool previa, IReadOnlyList<Opcao<Guid>> destinos)
    {
        _itens = itens;
        Previa = previa;
        ClienteId = itens[0].ClienteId;
        Cliente = itens[0].Cliente;
        Transferido = itens.Any(i => i.Resultado == ResultadoItemTransferencia.Transferido);
        ComErro = !Transferido && itens.Any(i => i.Resultado == ResultadoItemTransferencia.Erro);
        Destinos = [.. destinos];
        var destino = itens.FirstOrDefault(i => i.Resultado == ResultadoItemTransferencia.Transferido)?.DestinoId ?? itens[0].DestinoId;
        DestinoOriginal = destino;
        _destino = Destinos.FirstOrDefault(d => d.Valor == destino) ?? (Destinos.Length > 0 ? Destinos[0] : new Opcao<Guid>(Guid.Empty, "?"));
    }

    public bool Previa { get; }
    public Guid ClienteId { get; }
    public string Cliente { get; }
    public bool Transferido { get; }
    public bool ComErro { get; }
    public bool NaoProcessado => !Transferido && !ComErro;
    public Guid? DestinoOriginal { get; }
    /// <summary>Array: o Picker precisa de IList.</summary>
    public Opcao<Guid>[] Destinos { get; }

    public string Situacao => TextosTransferencia.Resultado(
        Transferido ? ResultadoItemTransferencia.Transferido : ComErro ? ResultadoItemTransferencia.Erro : ResultadoItemTransferencia.NaoProcessado, Previa);

    /// <summary>"Vendedor desde 01/01/2025 · Supervisor (Empresa X) desde 03/02/2026".</summary>
    public string Detalhe => string.Join(" · ", _itens.Select(i =>
        $"{i.Papel ?? "?"}{(i.Empresa is { } e ? $" ({e})" : string.Empty)} {TextosTransferencia.Periodo(i.InicioOrigem, i.FimOrigem)}".Trim()));

    /// <summary>Por que algum vínculo não passou (vazio quando todos passaram).</summary>
    public string Motivo => string.Join(Environment.NewLine, _itens.Where(i => i.Resultado != ResultadoItemTransferencia.Transferido && i.Motivo is not null)
        .Select(i => i.Motivo).Distinct());

    public bool TemMotivo => Motivo.Length > 0;

    /// <summary>Para quem vai (ou foi): texto do resultado.</summary>
    public string ParaQuem => Transferido ? "para " + (_itens.First(i => i.Resultado == ResultadoItemTransferencia.Transferido).Destino ?? "?") : string.Empty;

    /// <summary>Na prévia, só quem será transferido entra na conta; desmarcar tira o cliente da transferência.</summary>
    public bool PodeMarcar => Previa && Transferido;

    [ObservableProperty] private bool _marcado = true;

    /// <summary>Com vários destinos, a prévia deixa trocar o destino deste cliente.</summary>
    public bool PodeTrocarDestino => PodeMarcar && Destinos.Length > 1;

    [ObservableProperty] private Opcao<Guid> _destino;

    /// <summary>Junta os itens (um por vínculo) por cliente, na ordem recebida (o servidor já ordena pelo resultado e pelo nome).</summary>
    public static List<LinhaClienteTransferencia> Agrupar(IEnumerable<ItemTransferenciaDto> itens, bool previa, IReadOnlyList<Opcao<Guid>> destinos) =>
        [.. itens.GroupBy(i => i.ClienteId).Select(g => new LinhaClienteTransferencia([.. g], previa, destinos))];
}

/// <summary>Uma transferência gravada, aberta na lista ou logo depois de transferir: cabeçalho e resultado por cliente.</summary>
public sealed class ResultadoTransferencia
{
    public ResultadoTransferencia(TransferenciaDto t)
    {
        Item = t;
        Linhas = LinhaClienteTransferencia.Agrupar(t.Itens, previa: false, []);
    }

    public TransferenciaDto Item { get; }
    public IReadOnlyList<LinhaClienteTransferencia> Linhas { get; }

    public string Titulo => $"Transferência {Item.Numero}";
    public string Descricao => $"Clientes de {Item.Origem ?? "?"} ({Item.Papel ?? "todos os papéis"}" +
                               (Item.Empresa is { } e ? $", {e}" : string.Empty) + $") a partir de {TextoTela.Data(Item.EfeitoEm)}.";
    public string Motivo => $"Motivo: {Item.Motivo}" + (Item.Observacao is { } o ? $" · {o}" : string.Empty);
    public string Quem => $"Feita por {Item.Usuario} em {TextoTela.DataHora(Item.CriadaEm.ToLocalTime())}.";
    public string Resumo => Contagens(Item);

    public static string Contagens(TransferenciaDto t) =>
        $"{TextosTransferencia.Clientes(t.Transferidos)} transferido(s)" +
        (t.NaoProcessados > 0 ? $" · {t.NaoProcessados} não processado(s)" : string.Empty) +
        (t.Erros > 0 ? $" · {t.Erros} com erro" : string.Empty) +
        (t.Concluida ? string.Empty : " · interrompida (o que aparece aqui foi gravado)");
}

// ======================================================================= Assistente

/// <summary>Um destino escolhido no assistente (a tela mostra com "✕" para tirar).</summary>
public sealed record DestinoEscolhido(Guid Id, string Nome);

/// <summary>
/// Assistente "Nova transferência" em 3 passos: (1) de quem sai: pessoa, papel e empresa; (2) para quem, desde quando e por
/// quê; (3) prévia cliente a cliente, com o que será transferido e o que não será (com o motivo). A gravação usa as escolhas
/// da prévia: os clientes marcados e o destino mostrado de cada um (o servidor confere tudo de novo).
/// </summary>
public sealed partial class AssistenteTransferencia : ObservableObject
{
    public static readonly Opcao<Guid?> Nenhum = new(null, "—");
    public static readonly Opcao<Guid?> TodosPapeis = new(null, "Todos os papéis");
    public static readonly Opcao<Guid?> TodasEmpresas = new(null, "Todas as empresas");

    private readonly TransferenciaOpcoesDto _opcoes;

    public AssistenteTransferencia(TransferenciaOpcoesDto opcoes, DateOnly hoje)
    {
        _opcoes = opcoes;
        Origens = [Nenhum, .. opcoes.Pessoas.Select(p => new Opcao<Guid?>(p.Id,
            $"{p.Nome} ({TextosTransferencia.Clientes(opcoes.ClientesHoje.GetValueOrDefault(p.Id))})"))];
        Papeis = [TodosPapeis, .. opcoes.Papeis.Where(p => p.Ativo).Select(p => new Opcao<Guid?>(p.Id, p.Nome))];
        Empresas = [TodasEmpresas, .. opcoes.Empresas.Where(e => e.Ativa).Select(e => new Opcao<Guid?>(e.Id, e.Nome))];
        _origem = Nenhum;
        _papel = TodosPapeis;
        _empresa = TodasEmpresas;
        _efeitoEm = TextoTela.Data(hoje);
        _destinosDisponiveis = [Nenhum];
        _novoDestino = Nenhum;
        Destinos.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(VariosDestinos));
            OnPropertyChanged(nameof(SemDestinos));
            MontarDestinosDisponiveis();
        };
        MontarDestinosDisponiveis();
    }

    // ---- Passos ----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoPasso1), nameof(NoPasso2), nameof(NoPasso3), nameof(TituloPasso), nameof(PodeVoltar))]
    private int _passo = 1;

    public bool NoPasso1 => Passo == 1;
    public bool NoPasso2 => Passo == 2;
    public bool NoPasso3 => Passo == 3;
    public bool PodeVoltar => Passo > 1;

    public string TituloPasso => Passo switch
    {
        1 => "Passo 1 de 3 · De quem sai a carteira",
        2 => "Passo 2 de 3 · Para quem, desde quando e por quê",
        _ => "Passo 3 de 3 · Confira antes de transferir"
    };

    // ---- Passo 1 ----

    // Arrays: o Picker precisa de IList.
    public Opcao<Guid?>[] Origens { get; }
    public Opcao<Guid?>[] Papeis { get; }
    public Opcao<Guid?>[] Empresas { get; }

    [ObservableProperty][NotifyPropertyChangedFor(nameof(ClientesDaOrigem))] private Opcao<Guid?> _origem;
    [ObservableProperty] private Opcao<Guid?> _papel;
    [ObservableProperty] private Opcao<Guid?> _empresa;

    public string ClientesDaOrigem => Origem.Valor is { } id
        ? $"Atende hoje {TextosTransferencia.Clientes(_opcoes.ClientesHoje.GetValueOrDefault(id))} (em todos os papéis e empresas). A prévia mostra quantos o filtro alcança."
        : string.Empty;

    partial void OnOrigemChanged(Opcao<Guid?> value)
    {
        // Quem sai não pode ser destino.
        if (value.Valor is { } id && Destinos.FirstOrDefault(d => d.Id == id) is { } igual) Destinos.Remove(igual);
        MontarDestinosDisponiveis();
    }

    // ---- Passo 2 ----

    /// <summary>Destinos escolhidos (um ou vários), na ordem em que foram incluídos (o primeiro desempata a divisão).</summary>
    public ObservableCollection<DestinoEscolhido> Destinos { get; } = new();

    [ObservableProperty] private Opcao<Guid?>[] _destinosDisponiveis;

    /// <summary>
    /// A pessoa escolhida na lista, ainda não incluída: "+ Incluir" a põe nos destinos. A inclusão não acontece na própria
    /// escolha porque ela reconstrói esta lista, e no Windows trocar a lista de um Picker enquanto ele fecha trava o app.
    /// </summary>
    [ObservableProperty][NotifyPropertyChangedFor(nameof(PodeIncluirDestino))] private Opcao<Guid?> _novoDestino;

    public bool PodeIncluirDestino => NovoDestino.Valor is not null;

    [ObservableProperty] private string _efeitoEm;
    [ObservableProperty] private string _motivo = string.Empty;
    [ObservableProperty] private string _observacao = string.Empty;

    public bool VariosDestinos => Destinos.Count > 1;
    public bool SemDestinos => Destinos.Count == 0;

    public string AjudaEfeito => _opcoes.DiasRetroativosMaximo <= 0
        ? "O primeiro dia de quem recebe; quem sai fica até a véspera. De hoje em diante."
        : $"O primeiro dia de quem recebe; quem sai fica até a véspera. Pode ser até {_opcoes.DiasRetroativosMaximo} dia(s) antes de hoje.";

    public string TextoDivisao =>
        "Com vários destinos, os clientes são divididos pela menor carteira: quem tem menos clientes recebe primeiro. Na prévia, dá para trocar o destino de cada um.";

    /// <summary>"+ Incluir": põe a pessoa escolhida nos destinos e volta a lista para "—" (sem ela).</summary>
    [RelayCommand]
    private void IncluirDestino()
    {
        if (NovoDestino.Valor is not { } id) return;
        if (Destinos.All(d => d.Id != id) && id != Origem.Valor)
            Destinos.Add(new DestinoEscolhido(id, NomeDe(id)));
        NovoDestino = Nenhum;
    }

    [RelayCommand]
    private void TirarDestino(DestinoEscolhido? destino)
    {
        if (destino is not null) Destinos.Remove(destino);
    }

    private string NomeDe(Guid id) => _opcoes.Pessoas.FirstOrDefault(p => p.Id == id)?.Nome ?? "?";

    private void MontarDestinosDisponiveis()
    {
        var escolhidos = Destinos.Select(d => d.Id).ToHashSet();
        DestinosDisponiveis = [Nenhum, .. _opcoes.Pessoas.Where(p => p.Id != Origem.Valor && !escolhidos.Contains(p.Id))
            .Select(p => new Opcao<Guid?>(p.Id, $"{p.Nome} ({TextosTransferencia.Clientes(_opcoes.ClientesHoje.GetValueOrDefault(p.Id))})"))];
    }

    // ---- Passo 3: prévia ----

    public ObservableCollection<LinhaClienteTransferencia> Linhas { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResumoPrevia), nameof(Avisos), nameof(TemAvisos), nameof(PorDestino), nameof(TemPorDestino))]
    private PreviaTransferenciaDto? _previa;

    public string ResumoPrevia => Previa is { } p
        ? $"{TextosTransferencia.Clientes(p.Transferir)} serão transferidos" + (p.NaoProcessar > 0 ? $" · {p.NaoProcessar} não serão processados (veja o motivo em cada um)" : string.Empty)
        : string.Empty;

    public string Avisos => Previa is { } p ? string.Join(Environment.NewLine, p.Avisos.Select(a => "• " + a)) : string.Empty;
    public bool TemAvisos => Previa is { Avisos.Count: > 0 };

    public string PorDestino => Previa is { PorDestino.Count: > 1 } p
        ? "Divisão: " + string.Join(" · ", p.PorDestino.Select(d => $"{d.Destino}: {TextosTransferencia.Clientes(d.Clientes)}"))
        : string.Empty;
    public bool TemPorDestino => PorDestino.Length > 0;

    /// <summary>Clientes marcados para transferir.</summary>
    public int Marcados => Linhas.Count(l => l.PodeMarcar && l.Marcado);
    public bool PodeTransferir => Marcados > 0;
    public string TextoTransferir => $"Transferir {TextosTransferencia.Clientes(Marcados)}";

    /// <summary>Mostra a prévia (passo 3): uma linha por cliente, todos os que serão transferidos já marcados.</summary>
    public void DefinirPrevia(PreviaTransferenciaDto previa)
    {
        foreach (var l in Linhas) l.PropertyChanged -= LinhaMudou;
        Linhas.Clear();
        foreach (var l in LinhaClienteTransferencia.Agrupar(previa.Itens, previa: true, [.. Destinos.Select(d => new Opcao<Guid>(d.Id, d.Nome))]))
        {
            l.PropertyChanged += LinhaMudou;
            Linhas.Add(l);
        }
        Previa = previa;
        Passo = 3;
        AtualizarContagem();
    }

    private void LinhaMudou(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LinhaClienteTransferencia.Marcado)) AtualizarContagem();
    }

    private void AtualizarContagem()
    {
        OnPropertyChanged(nameof(Marcados));
        OnPropertyChanged(nameof(PodeTransferir));
        OnPropertyChanged(nameof(TextoTransferir));
    }

    [RelayCommand]
    private void MarcarTodos()
    {
        foreach (var l in Linhas.Where(l => l.PodeMarcar)) l.Marcado = true;
    }

    [RelayCommand]
    private void DesmarcarTodos()
    {
        foreach (var l in Linhas.Where(l => l.PodeMarcar)) l.Marcado = false;
    }

    // ---- Conferência e pedido ----

    /// <summary>O que falta para sair do passo atual (vazio = pode seguir). O servidor confere de novo.</summary>
    public IReadOnlyList<string> ValidarPasso()
    {
        var erros = new List<string>();
        if (Passo == 1 && Origem.Valor is null) erros.Add("Escolha de quem a carteira sai.");
        if (Passo == 2)
        {
            if (Destinos.Count == 0) erros.Add("Escolha para quem a carteira vai (um ou mais destinos).");
            if (!TextoTela.TentarData(EfeitoEm, out var efeito) || efeito is null) erros.Add("Data de efeito: use dd/mm/aaaa.");
            if (string.IsNullOrWhiteSpace(Motivo)) erros.Add("Informe o motivo (ex.: desligamento, reorganização da equipe).");
        }
        return erros;
    }

    /// <summary>
    /// O pedido para a API. Depois da prévia, leva os clientes marcados e o destino mostrado de cada um (inclusive os não
    /// trocados), para a gravação fazer exatamente o que a prévia mostrou.
    /// </summary>
    public TransferenciaRequisicao ParaRequisicao()
    {
        TextoTela.TentarData(EfeitoEm, out var efeito);
        var requisicao = new TransferenciaRequisicao
        {
            OrigemId = Origem.Valor ?? Guid.Empty,
            TipoCarteiraId = Papel.Valor,
            EmpresaId = Empresa.Valor,
            EfeitoEm = efeito ?? default,
            Destinos = [.. Destinos.Select(d => d.Id)],
            Motivo = TextoTela.Nulo(Motivo)?.Trim(),
            Observacao = TextoTela.Nulo(Observacao)?.Trim()
        };
        if (NoPasso3)
        {
            var marcados = Linhas.Where(l => l.PodeMarcar && l.Marcado).ToList();
            requisicao.Clientes = [.. marcados.Select(l => l.ClienteId)];
            requisicao.DestinoPorCliente = marcados.ToDictionary(l => l.ClienteId, l => l.Destino.Valor);
        }
        return requisicao;
    }

    /// <summary>Texto da confirmação antes de gravar.</summary>
    public string Confirmacao()
    {
        var destinos = string.Join(", ", Destinos.Select(d => d.Nome));
        return $"Transferir {TextosTransferencia.Clientes(Marcados)} de {NomeDe(Origem.Valor ?? Guid.Empty)} para {destinos} a partir de {EfeitoEm}? " +
               "Quem sai fica até a véspera; nada é apagado e cada cliente registra a transferência no histórico.";
    }
}

// ======================================================================= Tela de transferências

/// <summary>
/// Transferências de carteira (Motor Comercial, Fase 1d): a lista das transferências feitas (resultado por cliente) e o
/// assistente "Nova transferência". Nada é apagado nem desfeito: uma transferência errada se corrige com outra.
/// </summary>
public sealed partial class TransferenciasViewModel : CadastroViewModelBase<LinhaTransferencia>
{
    private readonly ComercialApi _api;
    private readonly AberturaDePessoa _abertura;
    private TransferenciaOpcoesDto? _opcoes;

    public TransferenciasViewModel(ComercialApi api, AberturaDePessoa abertura, IDialogos dialogos) : base(dialogos)
    {
        _api = api;
        _abertura = abertura;
    }

    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.Today);

    /// <summary>Navegação para outra tela (a tela liga ao Shell): usada por "Abrir ficha".</summary>
    public Func<string, Task>? AbrirTela { get; set; }

    public bool PodeAbrirFicha => _abertura.Permitida;

    [ObservableProperty][NotifyPropertyChangedFor(nameof(MostrarAssistente))] private AssistenteTransferencia? _assistente;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(MostrarResultado))] private ResultadoTransferencia? _resultado;

    public bool MostrarAssistente => Assistente is not null;
    public bool MostrarResultado => Resultado is not null;

    protected override string TextoDeBusca(LinhaTransferencia item) => item.Titulo + " " + item.Detalhe;

    protected override async Task<IReadOnlyList<LinhaTransferencia>> ListarAsync() =>
        [.. (await _api.ListarTransferenciasAsync()).Select(t => new LinhaTransferencia(t))];

    protected override async Task AbrirAsync(LinhaTransferencia item)
    {
        Assistente = null;
        Resultado = new ResultadoTransferencia(await _api.ObterTransferenciaAsync(item.Id)
                                               ?? throw new ValidacaoException(["Esta transferência não existe mais."]));
    }

    protected override async Task NovoItemAsync()
    {
        _opcoes ??= await _api.ListarOpcoesTransferenciaAsync();
        Resultado = null;
        Assistente = new AssistenteTransferencia(_opcoes, Hoje);
    }

    /// <summary>Só o assistente tem o que perder (o resultado é só leitura).</summary>
    protected override object? DadosDaFicha() => Assistente?.ParaRequisicao();

    protected override bool FichaNova => Assistente is not null;

    protected override async Task RecarregarFichaAsync()
    {
        if (Resultado is { } r) Resultado = new ResultadoTransferencia(await _api.ObterTransferenciaAsync(r.Item.Id) ?? r.Item);
    }

    [RelayCommand]
    private async Task AvancarAsync()
    {
        if (Assistente is not { } a) return;
        if (a.ValidarPasso() is { Count: > 0 } erros)
        {
            Mostrar(string.Join(Environment.NewLine, erros), TipoMensagem.Erro);
            return;
        }
        if (a.NoPasso1)
        {
            LimparMensagem();
            a.Passo = 2;
            return;
        }
        if (!a.NoPasso2) return;
        PreviaTransferenciaDto? previa = null;
        if (!await ExecutarAsync(async () => previa = await _api.PreviaTransferenciaAsync(a.ParaRequisicao()))) return;
        a.DefinirPrevia(previa!);
        MarcarFichaSemAlteracoes();
    }

    [RelayCommand]
    private void Voltar()
    {
        if (Assistente is not { PodeVoltar: true } a) return;
        LimparMensagem();
        a.Passo--;
    }

    [RelayCommand]
    private async Task TransferirAsync()
    {
        if (Assistente is not { NoPasso3: true, PodeTransferir: true } a) return;
        if (!await ConfirmarAsync("Transferir carteira", a.Confirmacao(), a.TextoTransferir, "Voltar")) return;

        TransferenciaDto? feita = null;
        if (!await ExecutarAsync(async () => feita = await _api.TransferirAsync(a.ParaRequisicao()))) return;
        Assistente = null;
        Resultado = new ResultadoTransferencia(feita!);
        MarcarFichaSemAlteracoes();
        await AtualizarListaAposGravarAsync();
        Mostrar($"Transferência {feita!.Numero} registrada: {ResultadoTransferencia.Contagens(feita)}.",
            feita.Erros > 0 || feita.NaoProcessados > 0 ? TipoMensagem.Aviso : TipoMensagem.Sucesso);
    }

    [RelayCommand]
    private async Task AbrirClienteAsync(LinhaClienteTransferencia? linha)
    {
        if (linha is null || !PodeAbrirFicha || AbrirTela is null) return;
        if (!await PodePerderAlteracoesAsync()) return;
        _abertura.Pedir(linha.ClienteId);
        await AbrirTela(AberturaDePessoa.RotaPessoas);
    }
}

// ======================================================================= Carteira em uma data

/// <summary>Linha de "Carteira em uma data".</summary>
public sealed class LinhaVinculoEmData
{
    public LinhaVinculoEmData(VinculoEmDataDto v, bool porCliente)
    {
        Item = v;
        PorCliente = porCliente;
    }

    public VinculoEmDataDto Item { get; }
    public bool PorCliente { get; }
    public Guid ClienteId => Item.ClienteId;

    /// <summary>Por cliente: "Vendedor: Maria"; por pessoa: o cliente.</summary>
    public string Titulo => PorCliente ? $"{Item.Papel}: {Item.Pessoa}" : Item.Cliente;

    public string Detalhe => string.Join(" · ", new[]
    {
        PorCliente ? null : Item.Papel,
        Item.Empresa,
        TextosTransferencia.Periodo(Item.InicioEm, Item.FimEm),
        Item.Exclusivo ? "exclusivo" : null,
        TextosTransferencia.Origem(Item.Origem) + (Item.Transferencia is { } t ? $" {t}" : string.Empty)
    }.Where(x => !string.IsNullOrEmpty(x)));

    public string Credito => Item.TipoCredito switch
    {
        TipoCreditoComercial.Nenhum => "sem crédito da venda",
        _ when !PorCliente => string.Empty,
        TipoCreditoComercial.Receita => Item.Credito is { } p ? $"crédito de receita: {TextoTela.Decimal(p)}%" : "crédito de receita: divisão não definida",
        _ => $"crédito adicional (sobreposição): {TextoTela.Decimal(Item.Credito ?? 100)}%"
    };

    public bool TemCredito => Credito.Length > 0;
    public string Cobertura => Item.Cobertura is { } c ? "Ausente: " + c : string.Empty;
    public bool TemCobertura => Cobertura.Length > 0;
}

/// <summary>
/// "Como estava a carteira em DD/MM" (princípio 99 do prompt mestre): por cliente (quem ocupava cada papel, o crédito e as
/// ausências) ou por quem atende (os clientes dela). Responde pelas vigências gravadas, não pelo estado de hoje.
/// </summary>
public sealed partial class CarteiraEmDataViewModel : ViewModelBase
{
    public static readonly Opcao<bool>[] Modos = [new(true, "Por cliente"), new(false, "Por quem atende")];
    private static readonly Opcao<Guid?> Nenhuma = new(null, "—");

    private readonly ComercialApi _comercial;
    private readonly PessoasApi _pessoasApi;
    private readonly AberturaDePessoa _abertura;
    private bool _atendentesLidos;

    public CarteiraEmDataViewModel(ComercialApi comercial, PessoasApi pessoasApi, AberturaDePessoa abertura)
    {
        _comercial = comercial;
        _pessoasApi = pessoasApi;
        _abertura = abertura;
        _modo = Modos[0];
        _data = TextoTela.Data(DateOnly.FromDateTime(DateTime.Today));
        _pessoa = Nenhuma;
    }

    /// <summary>Navegação para outra tela (a tela liga ao Shell): usada por "Abrir ficha".</summary>
    public Func<string, Task>? AbrirTela { get; set; }

    public bool PodeAbrirFicha => _abertura.Permitida;
    public IReadOnlyList<Opcao<bool>> ListaModos => Modos;

    [ObservableProperty][NotifyPropertyChangedFor(nameof(PorCliente), nameof(PorPessoa))] private Opcao<bool> _modo;
    [ObservableProperty] private string _data;

    public bool PorCliente => Modo.Valor;
    public bool PorPessoa => !Modo.Valor;

    // ---- Cliente ----

    [ObservableProperty] private string _buscaCliente = string.Empty;
    public ObservableCollection<PessoaResumo> ClientesEncontrados { get; } = new();

    /// <summary>Escolher um cliente da busca consulta na hora.</summary>
    [ObservableProperty] private PessoaResumo? _cliente;

    // ---- Pessoa ----

    [ObservableProperty] private Opcao<Guid?>[] _pessoas = [Nenhuma];
    [ObservableProperty] private Opcao<Guid?> _pessoa;

    // ---- Resultado ----

    public ObservableCollection<LinhaVinculoEmData> Linhas { get; } = new();
    [ObservableProperty] private string _resumo = string.Empty;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(TemAusencias))] private string _ausencias = string.Empty;
    public bool TemAusencias => Ausencias.Length > 0;

    partial void OnModoChanged(Opcao<bool> value)
    {
        Linhas.Clear();
        Resumo = string.Empty;
        Ausencias = string.Empty;
        if (!value.Valor) _ = LerAtendentesAsync();
    }

    partial void OnClienteChanged(PessoaResumo? value)
    {
        if (value is not null) _ = ConsultarAsync();
    }

    partial void OnPessoaChanged(Opcao<Guid?> value)
    {
        if (value.Valor is not null) _ = ConsultarAsync();
    }

    /// <summary>Quem pode atender (lido uma vez, quando o modo "Por quem atende" é escolhido).</summary>
    private async Task LerAtendentesAsync()
    {
        if (_atendentesLidos) return;
        ComercialOpcoesDto? opcoes = null;
        if (!await ExecutarAsync(async () => opcoes = await _comercial.ListarOpcoesAsync())) return;
        _atendentesLidos = true;
        Pessoas = [Nenhuma, .. opcoes!.Atendentes.Select(a => new Opcao<Guid?>(a.Id, a.Nome))];
        Pessoa = Nenhuma;
    }

    [RelayCommand]
    private async Task BuscarClienteAsync()
    {
        if (string.IsNullOrWhiteSpace(BuscaCliente))
        {
            Mostrar("Digite parte do nome, o código ou o CPF/CNPJ do cliente.", TipoMensagem.Aviso);
            return;
        }
        List<PessoaResumo>? achados = null;
        if (!await ExecutarAsync(async () => achados = await _pessoasApi.ListarAsync(new FiltroPessoas { Texto = BuscaCliente.Trim(), Limite = 20 })))
            return;
        ClientesEncontrados.Clear();
        foreach (var p in achados!) ClientesEncontrados.Add(p);
        if (ClientesEncontrados.Count == 0) Mostrar("Ninguém encontrado com esse texto.", TipoMensagem.Aviso);
    }

    [RelayCommand]
    private async Task ConsultarAsync()
    {
        if (!TextoTela.TentarData(Data, out var data) || data is null)
        {
            Mostrar("Data: use dd/mm/aaaa.", TipoMensagem.Erro);
            return;
        }
        Guid? clienteId = PorCliente ? Cliente?.Id : null;
        Guid? pessoaId = PorPessoa ? Pessoa.Valor : null;
        if (clienteId is null && pessoaId is null)
        {
            Mostrar(PorCliente ? "Busque e escolha o cliente." : "Escolha quem atende.", TipoMensagem.Aviso);
            return;
        }

        CarteiraEmDataDto? carteira = null;
        if (!await ExecutarAsync(async () => carteira = await _comercial.CarteiraEmDataAsync(clienteId, pessoaId, data.Value))) return;
        Linhas.Clear();
        foreach (var v in carteira!.Vinculos) Linhas.Add(new LinhaVinculoEmData(v, PorCliente));
        var nome = carteira.Nome ?? "?";
        Resumo = carteira.Vinculos.Count == 0
            ? (PorCliente ? $"Em {TextoTela.Data(data)}, {nome} não tinha ninguém na carteira." : $"Em {TextoTela.Data(data)}, {nome} não atendia nenhum cliente.")
            : PorCliente
                ? $"Em {TextoTela.Data(data)}, a carteira de {nome} era esta:"
                : $"Em {TextoTela.Data(data)}, {nome} atendia {TextosTransferencia.Clientes(carteira.Vinculos.Select(v => v.ClienteId).Distinct().Count())}" +
                  (carteira.Cortada ? " (a lista mostra os primeiros)." : ":");
        Ausencias = string.Join(Environment.NewLine, carteira.Ausencias.Select(a => "Ausente: " + a));
    }

    [RelayCommand]
    private async Task AbrirClienteAsync(LinhaVinculoEmData? linha)
    {
        if (linha is null || !PodeAbrirFicha || AbrirTela is null) return;
        _abertura.Pedir(linha.ClienteId);
        await AbrirTela(AberturaDePessoa.RotaPessoas);
    }
}
