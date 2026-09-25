using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Metas;
using Lone.Domain.Comum;
using Lone.Domain.Enums;

namespace Lone.Cliente.ViewModels.Metas;

/// <summary>Indicador da meta com o peso na nota (a soma dos pesos é 100).</summary>
public sealed partial class MetaItemFormulario : ItemDeLista
{
    public MetaItemFormulario(Guid id, IReadOnlyList<IndicadorDto> indicadores, Guid? indicadorId, decimal? peso, bool editavel)
    {
        Id = id;
        Editavel = editavel;
        Catalogo = indicadores;
        var lista = indicadores.Where(i => i.Ativo || i.Id == indicadorId).OrderBy(i => i.Nome)
            .Select(i => new Opcao<Guid?>(i.Id, i.Ativo ? i.Nome : i.Nome + " (desativado)")).ToList();
        if (indicadorId is { } g && lista.All(o => o.Valor != g)) lista.Insert(0, new Opcao<Guid?>(g, "(indicador gravado)"));
        _indicadores = [OpcoesMetas.Nenhum, .. lista];
        _indicador = OpcoesMetas.Escolher(_indicadores, indicadorId);
        _peso = TextoTela.Decimal(peso);
    }

    public Guid Id { get; }
    public bool Editavel { get; }
    public IReadOnlyList<IndicadorDto> Catalogo { get; }

    /// <summary>Chamado quando o indicador muda (os alvos dos participantes acompanham).</summary>
    public Action? AoMudar { get; set; }

    [ObservableProperty] private Opcao<Guid?>[] _indicadores;
    [ObservableProperty] private Opcao<Guid?> _indicador;
    [ObservableProperty] private string _peso;

    partial void OnIndicadorChanged(Opcao<Guid?> value) => AoMudar?.Invoke();

    public IndicadorDto? Dados => Catalogo.FirstOrDefault(i => i.Id == Indicador.Valor);
    public bool Informado => Dados?.Fonte is null or FonteIndicador.Informado;
    public string Nome => Indicador.Valor is null ? "(escolha o indicador)" : Indicador.Texto;

    /// <summary>Texto na lista de escolha da importação.</summary>
    public override string ToString() => Nome;
}

/// <summary>Faixa de desempenho: a partir de quanto % da nota, o nome e o % de prêmio.</summary>
public sealed partial class MetaFaixaFormulario : ItemDeLista
{
    public MetaFaixaFormulario(MetaFaixaDto d, bool editavel)
    {
        Id = d.Id == Guid.Empty ? IdSequencial.Novo() : d.Id;
        Editavel = editavel;
        _inicio = TextoTela.Decimal(d.InicioPercentual);
        _nome = d.Nome;
        _premio = TextoTela.Decimal(d.PercentualPremio);
    }

    public Guid Id { get; }
    public bool Editavel { get; }
    [ObservableProperty] private string _inicio;
    [ObservableProperty] private string _nome;
    [ObservableProperty] private string _premio;
}

/// <summary>Alvo de um participante num item, com o realizado (lançável só nos itens informados).</summary>
public sealed partial class MetaAlvoFormulario : ObservableObject
{
    public MetaAlvoFormulario(Guid id, MetaItemFormulario item) { Id = id; Item = item; }

    public Guid Id { get; }
    public MetaItemFormulario Item { get; }
    public Guid ItemId => Item.Id;
    public string Indicador => Item.Nome;

    [ObservableProperty] private string _alvo = string.Empty;
    [ObservableProperty] private string _realizado = string.Empty;

    /// <summary>"12 (importado em 03/10/2026 por ana)" — só leitura.</summary>
    [ObservableProperty] private string _realizadoInfo = string.Empty;

    public bool PodeEditarAlvo { get; set; }
    public bool PodeLancar { get; set; }
    public bool MostrarRealizadoSomenteLeitura => !PodeLancar;
}

/// <summary>Participante (nível + cadastro) com um alvo por item.</summary>
public sealed partial class MetaParticipanteFormulario : ItemDeLista
{
    private readonly IReadOnlyList<ParticipanteOpcaoDto> _opcoes;
    private readonly string? _nomeGravado;
    private readonly Guid _gravado;

    public MetaParticipanteFormulario(MetaParticipanteDto d, IReadOnlyList<ParticipanteOpcaoDto> opcoes, bool editavel)
    {
        Id = d.Id == Guid.Empty ? IdSequencial.Novo() : d.Id;
        _opcoes = opcoes;
        _nomeGravado = d.Nome;
        _gravado = d.ReferenciaId;
        Editavel = editavel;
        _nivel = OpcoesMetas.Niveis.First(n => n.Valor == d.Nivel);
        _referencias = OpcoesMetas.Participantes(opcoes, d.Nivel, d.ReferenciaId == Guid.Empty ? null : d.ReferenciaId, d.Nome);
        _referencia = OpcoesMetas.Escolher(_referencias, d.ReferenciaId);
        Resultado = d.NotaFinal is { } nota
            ? $"Resultado congelado: {TextoTela.Decimal(nota)}% · {d.Faixa ?? "abaixo das faixas"} · prêmio {TextoTela.Decimal(d.PercentualPremio ?? 0)}%"
            : string.Empty;
    }

    public Guid Id { get; }
    public bool Editavel { get; }
    public string Resultado { get; }
    public bool TemResultado => Resultado.Length > 0;
    public IReadOnlyList<Opcao<NivelParticipante>> ListaNiveis => OpcoesMetas.Niveis;

    [ObservableProperty] private Opcao<NivelParticipante> _nivel;
    [ObservableProperty] private Opcao<Guid?>[] _referencias;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(Titulo))] private Opcao<Guid?> _referencia;

    public string Titulo => Referencia.Valor is null ? $"{Nivel.Texto}: (escolha)" : $"{Nivel.Texto}: {Referencia.Texto}";

    partial void OnNivelChanged(Opcao<NivelParticipante> value)
    {
        Referencias = OpcoesMetas.Participantes(_opcoes, value.Valor, null, null);
        Referencia = Referencias[0];
        OnPropertyChanged(nameof(Titulo));
    }

    public ObservableCollection<MetaAlvoFormulario> Alvos { get; } = new();

    public Guid ReferenciaId => Referencia.Valor ?? _gravado;
    public string NomeParaImportar => Referencia.Valor is null ? _nomeGravado ?? string.Empty : Referencia.Texto;
}
