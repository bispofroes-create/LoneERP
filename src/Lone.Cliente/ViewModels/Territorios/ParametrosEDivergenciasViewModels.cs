using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.Api;
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

/// <summary>
/// Comercial › Divergências territoriais (DN-14): por mapa, quem o motor colocaria diferente hoje do que está gravado, e
/// por quê. Só leitura; para corrigir, crie uma operação (mesmo sem mudanças) em Operações territoriais e aplique.
/// </summary>
public sealed partial class DivergenciasTerritoriaisViewModel : ViewModelBase
{
    private readonly TerritoriosApi _api;
    private readonly AberturaDeOperacaoTerritorial _abertura;
    private readonly IDialogos _dialogos;
    private Dictionary<Guid, string> _nomes = new();
    private bool _podePlanejar;
    private DateOnly? _data;

    public DivergenciasTerritoriaisViewModel(TerritoriosApi api, AberturaDeOperacaoTerritorial abertura, IDialogos dialogos)
    {
        _api = api;
        _abertura = abertura;
        _dialogos = dialogos;
    }

    /// <summary>Navegação para outra tela (a tela liga ao Shell): usada por "Criar operação com estas".</summary>
    public Func<string, Task>? AbrirTela { get; set; }

    /// <summary>Há divergências conferidas e o usuário pode planejar (PLANEJAR com alcance Tudo).</summary>
    public bool PodeCriarOperacao => _podePlanejar && Total > 0 && _data is not null;

    [ObservableProperty] private Opcao<Guid?>[] _mapas = [];
    [ObservableProperty] private Opcao<Guid?>? _mapa;
    [ObservableProperty] private string _resumo = "Escolha o mapa e clique em Conferir.";
    [ObservableProperty][NotifyPropertyChangedFor(nameof(TemMais), nameof(PodeCriarOperacao), nameof(TemItens))] private int _total;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(TemSelecionado))] private LinhaItemTerritorial? _selecionado;

    /// <summary>A lista e o "Por quê?" só aparecem com divergências / com uma escolhida (03/10/2026).</summary>
    public bool TemItens => Total > 0;
    public bool TemSelecionado => Selecionado is not null;
    public ObservableCollection<LinhaItemTerritorial> Itens { get; } = new();
    public bool TemMais => Itens.Count < Total;

    [RelayCommand]
    private Task CarregarAsync() => ExecutarAsync(async () =>
    {
        var opcoes = await _api.OpcoesOperacoesAsync();
        _podePlanejar = opcoes.PodePlanejar;
        OnPropertyChanged(nameof(PodeCriarOperacao));
        Mapas = [.. opcoes.Mapas.Where(m => m.Ativo).Select(m => new Opcao<Guid?>(m.Id, m.Nome))];
        Mapa ??= Mapas.FirstOrDefault();
    });

    [RelayCommand]
    private Task ConferirAsync() => ListarAsync(0);

    [RelayCommand]
    private Task MaisAsync() => ListarAsync(Itens.Count);

    private async Task ListarAsync(int pular)
    {
        if (Mapa?.Valor is not { } mapaId) return;
        DivergenciasTerritoriaisDto? d = null;
        if (!await ExecutarAsync(async () =>
            {
                if (pular == 0) _nomes = (await _api.ListarDoMapaAsync(mapaId)).Territorios.ToDictionary(t => t.Id, t => t.Nome);
                d = await _api.DivergenciasAsync(mapaId, new FiltroItensOperacaoTerritorialDto { Pular = pular, Quantidade = OperacoesTerritoriaisViewModel.ItensPorPagina });
            }))
            return;
        if (pular == 0) Itens.Clear();
        foreach (var i in d!.Itens.Itens) Itens.Add(new LinhaItemTerritorial(i, _nomes));
        _data = d.Data;
        Total = d.Itens.Total;
        OnPropertyChanged(nameof(TemMais));
        OnPropertyChanged(nameof(PodeCriarOperacao));
        Resumo = Total == 0
            ? $"Nenhuma divergência em {TextoTela.Data(d.Data)}: o que está gravado é o que as regras e exceções dão hoje."
            : $"Em {TextoTela.Data(d.Data)}: {d.Entram} entrariam, {d.Saem} sairiam, {d.Mudam} mudariam de território, {d.OrigemAtualizada} mudariam só a origem, " +
              $"{d.EmConflito} em conflito, {d.Inconsistencias} inconsistência(s)" + (d.Itens.FiltradoPeloAlcance ? " (só os clientes do seu alcance)" : string.Empty) +
              (PodeCriarOperacao
                  ? ". Para corrigir: \"Criar operação com estas\", depois simule e aplique na tela de operações."
                  : ". Para corrigir, quem planeja operações territoriais cria uma operação neste mapa, simula e aplica.");
    }

    partial void OnMapaChanged(Opcao<Guid?>? value)
    {
        // Outro mapa: o que foi conferido não vale para ele.
        Itens.Clear();
        _data = null;
        Total = 0;
        Resumo = "Clique em Conferir.";
    }

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
