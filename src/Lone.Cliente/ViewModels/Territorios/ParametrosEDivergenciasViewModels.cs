using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.Api;
using Lone.Cliente.Grade;
using Lone.Cliente.Navegacao;
using Lone.Cliente.Plataforma;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Territorios;
using Lone.Domain.Enums;

namespace Lone.Cliente.ViewModels.Territorios;

/// <summary>
/// Comercial › Configurações › Parâmetros territoriais (DN-08): até quantos dias antes de hoje uma operação territorial
/// pode ter efeito. Próprio dos territórios (não o das coberturas); padrão 30.
/// </summary>
public sealed partial class ParametrosTerritoriaisViewModel : ViewModelBase
{
    private readonly TerritoriosApi _api;
    private byte[]? _versao;

    public ParametrosTerritoriaisViewModel(TerritoriosApi api) => _api = api;

    [ObservableProperty] private string _diasRetroativos = "30";
    [ObservableProperty] private string _textoLimites = string.Empty;

    [RelayCommand]
    private Task CarregarAsync() => ExecutarAsync(async () => Aplicar(await _api.ObterParametrosAsync()));

    [RelayCommand]
    private async Task SalvarAsync()
    {
        if (!TextoTela.TentarInteiro(DiasRetroativos, out var dias) || dias is null)
        {
            Mostrar("Datas no passado: informe os dias (0 = só hoje ou datas futuras).", TipoMensagem.Erro);
            return;
        }
        ParametrosTerritoriaisDto? salvo = null;
        if (!await ExecutarAsync(async () => salvo = await _api.SalvarParametrosAsync(new ParametrosTerritoriaisDto { Versao = _versao, DiasRetroativosMaximo = dias.Value })))
            return;
        Aplicar(salvo!);
        Mostrar("Parâmetros salvos.", TipoMensagem.Sucesso);
    }

    private void Aplicar(ParametrosTerritoriaisDto p)
    {
        _versao = p.Versao;
        DiasRetroativos = TextoTela.Inteiro(p.DiasRetroativosMaximo);
        TextoLimites = $"De 0 a {p.Maximo} dias; o sistema nasceu com {p.Padrao}.";
    }
}

/// <summary>Atalho da barra da lista de divergências: um tipo de efeito (nulo = todas), com a contagem da API.</summary>
public sealed partial class AtalhoEfeito : ObservableObject
{
    public AtalhoEfeito(EfeitoNoCliente? efeito, string texto)
    {
        Efeito = efeito;
        Texto = texto;
    }

    public EfeitoNoCliente? Efeito { get; }
    public string Texto { get; }
    [ObservableProperty] private int _quantidade;
    [ObservableProperty] private bool _selecionado;
    [ObservableProperty] private bool _visivel = true;
}

/// <summary>
/// Comercial › Divergências territoriais (DN-14; refeita em 04/10/2026 no padrão de consulta do Lone): por mapa, quem o
/// motor colocaria diferente hoje do que está gravado, e por quê. Filtro (mapa + Conferir); barra da lista com o contador,
/// atalhos por efeito (filtrados na API, que também conta), "Criar operação com estas" e Exportar ▾; lista em colunas
/// ordenável; "Por quê?" do cliente tocado ao lado; lista vazia em uma frase. Só leitura: para corrigir, crie uma operação
/// (mesmo sem mudanças) e aplique em Operações territoriais.
/// </summary>
public sealed partial class DivergenciasTerritoriaisViewModel : ViewModelBase
{
    private readonly TerritoriosApi _api;
    private readonly AberturaDeOperacaoTerritorial _abertura;
    private readonly IDialogos _dialogos;
    private readonly IArquivos _arquivos;
    private Dictionary<Guid, string> _nomes = new();
    private bool _podePlanejar;
    private DateOnly? _data;
    private DivergenciasTerritoriaisDto? _consulta;
    private string? _mapaConferido;
    private int _totalTodas;
    private bool _semMapas;

    public DivergenciasTerritoriaisViewModel(TerritoriosApi api, AberturaDeOperacaoTerritorial abertura, IDialogos dialogos, IArquivos arquivos)
    {
        _api = api;
        _abertura = abertura;
        _dialogos = dialogos;
        _arquivos = arquivos;
        GradeDaLista.OrdemMudou += Montar;
    }

    /// <summary>Navegação para outra tela (a tela liga ao Shell): usada por "Criar operação com estas".</summary>
    public Func<string, Task>? AbrirTela { get; set; }

    /// <summary>Lista em colunas: Cliente · Efeito · Território atual · Território certo · Motivo.</summary>
    public GradeCadastro<LinhaItemTerritorial> GradeDaLista { get; } = new(
        "Cliente", l => l.Item.PessoaId, l => l.Titulo, l => null,
        ColunaCadastro<LinhaItemTerritorial>.Selo("efeito", "Efeito", l => l.Item.EfeitoNome, l => DivergenciasTerritoriais.Tom(l.Item.Efeito), 140),
        ColunaCadastro<LinhaItemTerritorial>.Texto("atual", "Território atual", l => DivergenciasTerritoriais.Territorio(l.Item.TerritorioAtual), 170),
        ColunaCadastro<LinhaItemTerritorial>.Texto("certo", "Território certo", l => DivergenciasTerritoriais.Territorio(l.Item.TerritorioProposto), 170),
        ColunaCadastro<LinhaItemTerritorial>.Texto("motivo", "Motivo", l => l.Item.Motivo, 220))
    {
        OrdemTitulo = l => l.Item.Pessoa
    };

    [ObservableProperty] private ConteudoGrade _conteudoLista = ConteudoGrade.Vazio;

    // ---- Filtro ----
    [ObservableProperty][NotifyPropertyChangedFor(nameof(PodeConferir))] private Opcao<Guid?>[] _mapas = [];
    [ObservableProperty][NotifyPropertyChangedFor(nameof(PodeConferir))] private Opcao<Guid?>? _mapa;

    public bool PodeConferir => Mapa?.Valor is not null;

    // ---- Barra da lista ----
    public IReadOnlyList<AtalhoEfeito> Atalhos { get; } =
    [
        new(null, "Todas") { Selecionado = true },
        new(EfeitoNoCliente.Entra, "Entrariam"),
        new(EfeitoNoCliente.Sai, "Sairiam"),
        new(EfeitoNoCliente.Muda, "Mudariam"),
        new(EfeitoNoCliente.OrigemAtualizada, "Só a origem"),
        new(EfeitoNoCliente.Bloqueado, "Inconsistências")
    ];

    private EfeitoNoCliente? _efeito;

    [ObservableProperty] private string _tituloLista = DivergenciasTerritoriais.TituloLista(null, 0);
    [ObservableProperty] private bool _mostrarAtalhos;

    /// <summary>Com alcance restrito a API conta o mapa todo: os atalhos aparecem sem o número.</summary>
    [ObservableProperty] private bool _mostrarQuantidades = true;
    [ObservableProperty] private bool _mostrarExportar;

    /// <summary>Há divergências conferidas e o usuário pode planejar (PLANEJAR com alcance Tudo).</summary>
    public bool PodeCriarOperacao => _podePlanejar && (_consulta?.Itens.Total ?? 0) > 0 && _data is not null;

    // ---- Lista e "Por quê?" ----
    public ObservableCollection<LinhaItemTerritorial> Itens { get; } = new();
    [ObservableProperty][NotifyPropertyChangedFor(nameof(TemSelecionado))] private LinhaItemTerritorial? _selecionado;
    public bool TemSelecionado => Selecionado is not null;

    /// <summary>Total do filtro em uso (a API pagina; normalmente vem tudo numa leitura).</summary>
    public int Total => _consulta?.Itens.Total ?? 0;
    public bool TemMais => Itens.Count < Total;
    public string TextoMais => $"{Itens.Count.ToString("N0", TextoTela.Brasil)} de {Total.ToString("N0", TextoTela.Brasil)}";

    // ---- Lista vazia ----
    public bool ListaVazia => Itens.Count == 0;
    [ObservableProperty] private string _tituloVazio = DivergenciasTerritoriais.TituloAntesDeConferir;
    [ObservableProperty] private string _textoVazio = DivergenciasTerritoriais.TextoAntesDeConferir;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(MostrarAcaoVazio))] private string _textoAcaoVazio = string.Empty;
    public bool MostrarAcaoVazio => TextoAcaoVazio.Length > 0;

    [RelayCommand]
    private Task CarregarAsync() => ExecutarAsync(async () =>
    {
        var opcoes = await _api.OpcoesOperacoesAsync();
        _podePlanejar = opcoes.PodePlanejar;
        OnPropertyChanged(nameof(PodeCriarOperacao));
        Mapas = [.. opcoes.Mapas.Where(m => m.Ativo).Select(m => new Opcao<Guid?>(m.Id, m.Nome))];
        _semMapas = Mapas.Length == 0;
        Mapa ??= Mapas.FirstOrDefault();
        AtualizarVazio();
    });

    /// <summary>Conferir: lê de novo, do começo, com todos os efeitos.</summary>
    [RelayCommand]
    private async Task ConferirAsync()
    {
        _efeito = null;
        foreach (var a in Atalhos) a.Selecionado = a.Efeito is null;
        await ListarAsync(0);
    }

    [RelayCommand]
    private Task MaisAsync() => ListarAsync(Itens.Count);

    /// <summary>Atalho da barra: só um tipo de efeito (tocar de novo volta para Todas). A API filtra e conta.</summary>
    [RelayCommand]
    private async Task SelecionarEfeitoAsync(AtalhoEfeito? atalho)
    {
        if (atalho is null || _consulta is null) return;
        _efeito = atalho.Selecionado && atalho.Efeito is not null ? null : atalho.Efeito;
        foreach (var a in Atalhos) a.Selecionado = a.Efeito == _efeito;
        await ListarAsync(0);
    }

    /// <summary>Tocar numa linha mostra o "Por quê?" ao lado; tocar de novo na mesma fecha.</summary>
    [RelayCommand]
    private void AbrirLinha(ILinhaGrade? linha)
    {
        if (linha is not LinhaCadastro { Item: LinhaItemTerritorial l }) return;
        Selecionado = ReferenceEquals(Selecionado, l) ? null : l;
        Montar();
    }

    [RelayCommand]
    private void FecharPorQue()
    {
        Selecionado = null;
        Montar();
    }

    /// <summary>Ação da lista vazia: com um atalho marcado, volta para Todas.</summary>
    [RelayCommand]
    private Task AcaoVazioAsync() => _efeito is null ? Task.CompletedTask : ConferirAsync();

    private async Task ListarAsync(int pular)
    {
        if (Mapa?.Valor is not { } mapaId) return;
        DivergenciasTerritoriaisDto? d = null;
        if (!await ExecutarAsync(async () =>
            {
                if (pular == 0) _nomes = (await _api.ListarDoMapaAsync(mapaId)).Territorios.ToDictionary(t => t.Id, t => t.Nome);
                d = await _api.DivergenciasAsync(mapaId, new FiltroItensOperacaoTerritorialDto
                {
                    Efeito = _efeito, Pular = pular, Quantidade = FiltroItensOperacaoTerritorialDto.QuantidadeMaxima
                });
            }))
            return;
        if (pular == 0)
        {
            Itens.Clear();
            Selecionado = null;
        }
        foreach (var i in d!.Itens.Itens) Itens.Add(new LinhaItemTerritorial(i, _nomes));
        _consulta = d;
        _data = d.Data;
        _mapaConferido = Mapa.Texto;

        // Atalhos: por efeito, a contagem é do mapa (a API conta antes do alcance); "Todas" é o total sem atalho.
        // Sem divergência nenhuma, somem.
        if (_efeito is null) _totalTodas = d.Itens.Total;
        foreach (var a in Atalhos)
        {
            a.Quantidade = a.Efeito is { } e ? DivergenciasTerritoriais.Quantidade(d, e) : _totalTodas;
            a.Visivel = a.Efeito is null || a.Quantidade > 0 || a.Selecionado;
        }
        MostrarQuantidades = !d.Itens.FiltradoPeloAlcance;
        MostrarAtalhos = _totalTodas > 0;
        if (d.Inconsistencias > 0)
            Mostrar(DivergenciasTerritoriais.AvisoInconsistencias(d.Inconsistencias), TipoMensagem.Aviso);
        Montar();
    }

    private void Montar()
    {
        ConteudoLista = GradeDaLista.Montar(Itens, Selecionado);
        TituloLista = DivergenciasTerritoriais.TituloLista(_consulta?.Data, Total);
        MostrarExportar = Itens.Count > 0;
        AtualizarVazio();
        OnPropertyChanged(nameof(ListaVazia));
        OnPropertyChanged(nameof(TemMais));
        OnPropertyChanged(nameof(TextoMais));
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(PodeCriarOperacao));
    }

    /// <summary>Lista vazia em uma frase: antes de conferir, o que fazer; depois, a boa notícia (sem botão).</summary>
    private void AtualizarVazio()
    {
        if (_consulta is not { } c)
        {
            TituloVazio = _semMapas ? DivergenciasTerritoriais.TituloSemMapas : DivergenciasTerritoriais.TituloAntesDeConferir;
            TextoVazio = _semMapas ? DivergenciasTerritoriais.TextoSemMapas : DivergenciasTerritoriais.TextoAntesDeConferir;
            TextoAcaoVazio = string.Empty;
            return;
        }
        if (_efeito is not null)
        {
            TituloVazio = "Nenhuma divergência deste tipo";
            TextoVazio = "As contagens mudaram desde a última conferência.";
            TextoAcaoVazio = "Ver todas";
            return;
        }
        TituloVazio = $"Nenhuma divergência no mapa {_mapaConferido}";
        TextoVazio = $"O cadastro está de acordo com as regras e exceções de hoje ({TextoTela.Data(c.Data)}).";
        TextoAcaoVazio = string.Empty;
    }

    partial void OnMapaChanged(Opcao<Guid?>? value)
    {
        // Outro mapa: o que foi conferido não vale para ele.
        Itens.Clear();
        Selecionado = null;
        _consulta = null;
        _data = null;
        _efeito = null;
        foreach (var a in Atalhos) a.Selecionado = a.Efeito is null;
        MostrarAtalhos = false;
        Montar();
    }

    /// <summary>Exportar › Imprimir / PDF: a lista (com o atalho em uso) no navegador.</summary>
    [RelayCommand]
    private async Task ImprimirAsync()
    {
        if (_consulta is null) return;
        var html = DivergenciasTerritoriais.Html(_mapaConferido ?? string.Empty, DescreverFiltro(), GradeDaLista.Ordenar(Itens), _consulta, DateTime.Now);
        await ExecutarAsync(() => _arquivos.AbrirAsync($"divergencias-territoriais-{DateTime.Now:yyyyMMdd-HHmm}.html", System.Text.Encoding.UTF8.GetBytes(html)));
    }

    /// <summary>Exportar › Excel (CSV): abre no Excel (ou no programa padrão de planilhas).</summary>
    [RelayCommand]
    private async Task ExportarCsvAsync()
    {
        var csv = DivergenciasTerritoriais.Csv(GradeDaLista.Ordenar(Itens));
        var bytes = System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(csv)).ToArray(); // BOM: acentos certos no Excel
        await ExecutarAsync(() => _arquivos.AbrirAsync($"divergencias-territoriais-{DateTime.Now:yyyyMMdd-HHmm}.csv", bytes));
    }

    private string DescreverFiltro() =>
        Atalhos.FirstOrDefault(a => a.Selecionado && a.Efeito is not null) is { } a ? $"Só: {a.Texto.ToLowerInvariant()}" : string.Empty;

    /// <summary>
    /// "Criar operação com estas" (plano 2b-1b, seção M; DN-14): um rascunho TE- neste mapa, com efeito na data conferida e
    /// sem mudanças planejadas. A simulação dele mostra só divergências e a aplicação as corrige; se, até lá, não houver mais
    /// nada a gravar, a aplicação recusa com "Nada a aplicar". Abre a operação criada na tela de operações.
    /// </summary>
    [RelayCommand]
    private async Task CriarOperacaoAsync()
    {
        if (!PodeCriarOperacao || Mapa?.Valor is not { } mapaId || _data is not { } data) return;
        var motivo = await _dialogos.PerguntarAsync("Criar operação com estas",
            $"Uma operação TE- em rascunho no mapa {Mapa.Texto}, com efeito em {TextoTela.Data(data)} e sem mudanças planejadas: " +
            "a simulação mostra as divergências e a aplicação as corrige. Motivo (obrigatório):",
            "Criar", "Voltar", "Ex.: endereços atualizados no cadastro", 250);
        if (string.IsNullOrWhiteSpace(motivo)) return;
        OperacaoTerritorialDto? criada = null;
        if (!await ExecutarAsync(async () => criada = await _api.CriarOperacaoAsync(new CriarOperacaoTerritorialRequisicao
            {
                MapaId = mapaId, EfeitoEm = data, Motivo = motivo.Trim()
            })))
            return;
        _abertura.Pedir(criada!.Id);
        if (AbrirTela is not null) await AbrirTela(AberturaDeOperacaoTerritorial.RotaOperacoes);
    }
}
