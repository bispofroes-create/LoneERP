using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enums;

namespace Lone.Cliente.ViewModels.Pessoas;

/// <summary>O que a tela faz com bloqueios e interações (gravados na hora, à parte do "Salvar" da ficha).</summary>
public sealed class AcoesSituacao
{
    public Func<Task>? Bloquear { get; set; }
    public Func<BloqueioItem, Task>? Liberar { get; set; }
    public Func<Task>? RegistrarInteracao { get; set; }
}

/// <summary>Um bloqueio da pessoa (ativo ou já liberado), com quem/quando/por quê.</summary>
public sealed partial class BloqueioItem : ObservableObject
{
    private readonly AcoesSituacao _acoes;

    public BloqueioItem(BloqueioDto dados, AcoesSituacao acoes)
    {
        _acoes = acoes;
        Dados = dados;
    }

    public BloqueioDto Dados { get; private set; }
    public Guid Id => Dados.Id;
    public bool Ativo => Dados.Ativo;

    public string Titulo => $"{SituacoesFormulario.NomeEscopo(Dados.Escopo)}{(Dados.Ativo ? string.Empty : " (liberado)")}";

    public string Detalhe
    {
        get
        {
            var inicio = $"Bloqueado em {Data(Dados.InicioEm)} por {Dados.InicioPor}: {Dados.Motivo}";
            return Dados.FimEm is { } fim ? $"{inicio}{Environment.NewLine}Liberado em {Data(fim)} por {Dados.FimPor}: {Dados.MotivoLiberacao}" : inicio;
        }
    }

    private static string Data(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime().ToString("dd/MM/yyyy HH:mm", TextoTela.Brasil);

    public void Atualizar(BloqueioDto dados)
    {
        Dados = dados;
        OnPropertyChanged(string.Empty);
    }

    [RelayCommand]
    private Task LiberarAsync() => _acoes.Liberar?.Invoke(this) ?? Task.CompletedTask;
}

/// <summary>Interação já em texto para a lista (hora local do aparelho).</summary>
public sealed record InteracaoItem(string Descricao, string Detalhe)
{
    public static InteracaoItem De(InteracaoDto i) => new(i.Descricao,
        $"{SituacoesFormulario.TiposInteracao.FirstOrDefault(t => t.Valor == i.Tipo)?.Texto} · " +
        $"{DateTime.SpecifyKind(i.DataHora, DateTimeKind.Utc).ToLocalTime().ToString("dd/MM/yyyy HH:mm", TextoTela.Brasil)} · {i.Usuario}");
}

/// <summary>Bloqueios e relacionamento da pessoa na ficha (listas e os campos de "novo bloqueio" / "nova interação").</summary>
public sealed partial class SituacoesFormulario : ObservableObject
{
    public static readonly Opcao<EscopoBloqueio>[] Escopos =
    [
        new(EscopoBloqueio.Comercial, "Comercial (vendas)"),
        new(EscopoBloqueio.Financeiro, "Financeiro"),
        new(EscopoBloqueio.Faturamento, "Faturamento (notas)"),
        new(EscopoBloqueio.Cadastral, "Cadastral")
    ];

    public static readonly Opcao<TipoInteracao>[] TiposInteracao =
    [
        new(TipoInteracao.Ligacao, "Ligação"),
        new(TipoInteracao.Visita, "Visita"),
        new(TipoInteracao.Email, "E-mail"),
        new(TipoInteracao.WhatsApp, "WhatsApp"),
        new(TipoInteracao.Reuniao, "Reunião"),
        new(TipoInteracao.Outro, "Outro")
    ];

    public static string NomeEscopo(EscopoBloqueio e) => Escopos.FirstOrDefault(o => o.Valor == e)?.Texto ?? e.ToString();

    public AcoesSituacao Acoes { get; } = new();

    public ObservableCollection<BloqueioItem> Bloqueios { get; } = new();
    public ObservableCollection<InteracaoItem> Interacoes { get; } = new();

    public IReadOnlyList<Opcao<EscopoBloqueio>> ListaEscopos => Escopos;
    public IReadOnlyList<Opcao<TipoInteracao>> ListaTipos => TiposInteracao;

    [ObservableProperty] private Opcao<EscopoBloqueio> _novoEscopo = Escopos[0];
    [ObservableProperty] private string _novoMotivo = string.Empty;
    [ObservableProperty] private Opcao<TipoInteracao> _novoTipo = TiposInteracao[0];
    [ObservableProperty] private string _novaDescricao = string.Empty;

    [ObservableProperty] private string _textoRelacionamento = string.Empty;

    public bool TemBloqueioAtivo => Bloqueios.Any(b => b.Ativo);

    public void Carregar(IEnumerable<BloqueioDto> bloqueios, RelacionamentoDto? relacionamento)
    {
        Bloqueios.Clear();
        foreach (var b in bloqueios.OrderByDescending(b => b.Ativo).ThenByDescending(b => b.InicioEm)) Bloqueios.Add(new BloqueioItem(b, Acoes));
        Interacoes.Clear();
        foreach (var i in relacionamento?.Interacoes ?? []) Interacoes.Add(InteracaoItem.De(i));
        TextoRelacionamento = Texto(relacionamento);
        OnPropertyChanged(nameof(TemBloqueioAtivo));
    }

    public void IncluirBloqueio(BloqueioDto b)
    {
        Bloqueios.Insert(0, new BloqueioItem(b, Acoes));
        NovoMotivo = string.Empty;
        OnPropertyChanged(nameof(TemBloqueioAtivo));
    }

    public void Liberado(BloqueioItem item, BloqueioDto dados)
    {
        item.Atualizar(dados);
        OnPropertyChanged(nameof(TemBloqueioAtivo));
    }

    public void IncluirInteracao(InteracaoDto i)
    {
        Interacoes.Insert(0, InteracaoItem.De(i));
        NovaDescricao = string.Empty;
        TextoRelacionamento = "Relacionamento ativo · última interação: " + Quando(i.DataHora);
    }

    private static string Quando(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime().ToString("dd/MM/yyyy HH:mm", TextoTela.Brasil);

    private static string Texto(RelacionamentoDto? r) => r switch
    {
        null => string.Empty,
        { UltimaInteracaoEm: null } => "Nenhuma interação registrada.",
        { Situacao: SituacaoRelacionamento.Inativo } => $"Relacionamento inativo (mais de {r.DiasInativo} dias sem interação) · última: {Quando(r.UltimaInteracaoEm!.Value)}",
        { Situacao: SituacaoRelacionamento.EmRisco } => $"Relacionamento em risco (mais de {r.DiasEmRisco} dias sem interação) · última: {Quando(r.UltimaInteracaoEm!.Value)}",
        _ => "Relacionamento ativo · última interação: " + Quando(r.UltimaInteracaoEm!.Value)
    };

    [RelayCommand]
    private Task BloquearAsync() => Acoes.Bloquear?.Invoke() ?? Task.CompletedTask;

    [RelayCommand]
    private Task RegistrarInteracaoAsync() => Acoes.RegistrarInteracao?.Invoke() ?? Task.CompletedTask;
}
