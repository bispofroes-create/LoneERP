using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.ViewModels.Cadastros;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enums;

namespace Lone.Cliente.ViewModels.Pessoas;

/// <summary>O que a tela faz com os consentimentos (ações próprias, gravadas na hora, fora do "Salvar" da ficha).</summary>
public sealed class AcoesPrivacidade
{
    public Func<Task>? Conceder { get; set; }
    public Func<PeriodoConsentimentoItem, Task>? Revogar { get; set; }
}

/// <summary>Textos de situação e de decisão (a decisão em si vem da regra do domínio, calculada pela API).</summary>
public static class TextosConsentimento
{
    public static string Situacao(SituacaoConsentimento s) => s switch
    {
        SituacaoConsentimento.Concedido => "Concedido",
        SituacaoConsentimento.Revogado => "Revogado",
        SituacaoConsentimento.NaoAplicavel => "Não aplicável",
        _ => "Não informado"
    };
}

/// <summary>Linha "Finalidade · Situação · Data" (consentimento geral, sem canal específico).</summary>
public sealed class FinalidadeConsentimentoItem
{
    public FinalidadeConsentimentoItem(FinalidadeConsentimentoDto dados) => Dados = dados;

    public FinalidadeConsentimentoDto Dados { get; }
    public string Nome => Dados.Nome;
    public string Situacao => TextosConsentimento.Situacao(Dados.Situacao);
    public string Data => Dados.Desde is { } d ? TextoTela.Data(DateOnly.FromDateTime(d.ToLocalTime())) : string.Empty;
    public string Descricao => Dados.Descricao ?? string.Empty;
}

/// <summary>Um período de consentimento (histórico). "Registro anterior" é só leitura: não se revoga.</summary>
public sealed partial class PeriodoConsentimentoItem : ObservableObject
{
    private readonly AcoesPrivacidade _acoes;

    public PeriodoConsentimentoItem(ConsentimentoDto dados, AcoesPrivacidade acoes)
    {
        Dados = dados;
        _acoes = acoes;
    }

    public ConsentimentoDto Dados { get; }
    public Guid Id => Dados.Id;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Visivel))]
    private bool _mostrarHistorico;

    /// <summary>Em vigor sempre aparece; revogados e registros anteriores só em "Mostrar histórico".</summary>
    public bool Visivel => (Dados.EmVigor && !Dados.SomenteHistorico) || MostrarHistorico;

    public string Titulo => $"{Dados.Finalidade} · {TextosPrivacidade.Canal(Dados.Canal)}";

    public string Situacao =>
        Dados.SomenteHistorico ? "Registro anterior às finalidades (somente histórico; não autoriza comunicação)"
        : Dados.EmVigor ? "Concedido" + Em(Dados.ConcedidoEm)
        : "Revogado" + Em(Dados.RevogadoEm);

    public string Detalhe => string.Join("  ·  ", new[]
    {
        Dados.ConcedidoEm is { } c ? $"concedido em {DataHora(c)}" + (Dados.ConcedidoPor is { } p ? $" por {p}" : string.Empty) : string.Empty,
        Dados.Motivo is { } m ? $"motivo: {m}" : string.Empty,
        Dados.VersaoTermo is { } v ? $"termo: {v}" : string.Empty,
        Dados.Origem is { } o ? $"origem: {o}" : string.Empty,
        Dados.RevogadoEm is { } r ? $"revogado em {DataHora(r)}" + (Dados.RevogadoPor is { } rp ? $" por {rp}" : string.Empty) : string.Empty,
        Dados.MotivoRevogacao is { } mr ? $"motivo da revogação: {mr}" : string.Empty
    }.Where(t => t.Length > 0));

    public bool PodeRevogar => Dados.EmVigor && !Dados.SomenteHistorico;

    [RelayCommand]
    private Task RevogarAsync() => _acoes.Revogar?.Invoke(this) ?? Task.CompletedTask;

    private static string Em(DateTime? utc) => utc is { } d ? " em " + TextoTela.Data(DateOnly.FromDateTime(d.ToLocalTime())) : string.Empty;
    private static string DataHora(DateTime utc) => utc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", TextoTela.Brasil);
}

/// <summary>Telefone/e-mail com as marcas do canal e, por finalidade, o que a regra central decidiu.</summary>
public sealed class CanalPrivacidadeItem
{
    public CanalPrivacidadeItem(CanalPrivacidadeDto dados) => Dados = dados;

    public CanalPrivacidadeDto Dados { get; }

    public string Titulo => (Dados.Tipo == TipoContato.Email ? "E-mail · " : "Telefone · ") + FormatoContato.Exibir(Dados.Tipo, Dados.Valor);

    public string Marcas => "Aceita comunicações: " + (Dados.AceitaComunicacoes ? "Sim" : "Não")
                            + (Dados.UsoParaMarketing is { } m ? "  ·  Uso para marketing: " + (m ? "Sim" : "Não") : string.Empty);

    public IReadOnlyList<string> Decisoes => Dados.Decisoes
        .Select(d => d.Resultado == ResultadoComunicacao.Permitido
            ? $"{d.Finalidade}: pode ser usado"
            : $"{d.Finalidade}: não pode — {d.Motivo}")
        .ToList();
}

/// <summary>
/// Aba "Privacidade": o que a pessoa autorizou (consentimentos por finalidade), por quais meios se pode falar com ela
/// (canais, com "Aceita comunicações" e "Uso para marketing") e, por canal, a decisão da regra central. Lida quando a
/// aba abre; conceder/revogar são ações próprias (com motivo), nunca pelo "Salvar" da ficha. Nada aqui altera as marcas
/// dos telefones/e-mails, e elas não alteram o consentimento.
/// </summary>
public sealed partial class PrivacidadeFormulario : ObservableObject
{
    public static readonly Opcao<Guid?> SemFinalidade = new(null, "— escolha —");

    public static readonly Opcao<CanalComunicacao?>[] OpcoesCanal =
    [
        new(null, "Qualquer canal"),
        .. OpcoesPessoa.Canais.Select(c => new Opcao<CanalComunicacao?>(c, NomesPessoa.Canal(c)))
    ];

    public AcoesPrivacidade Acoes { get; } = new();

    /// <summary>Lida para esta ficha (a aba lê na primeira vez que abre).</summary>
    public bool Carregada { get; private set; }

    public ObservableCollection<FinalidadeConsentimentoItem> Finalidades { get; } = new();
    public ObservableCollection<PeriodoConsentimentoItem> Periodos { get; } = new();
    public ObservableCollection<CanalPrivacidadeItem> Canais { get; } = new();

    [ObservableProperty] private bool _mostrarHistorico;

    /// <summary>Há períodos revogados ou registros anteriores (aparecem em "Mostrar histórico").</summary>
    public bool TemHistorico => Periodos.Any(p => !p.Dados.EmVigor || p.Dados.SomenteHistorico);
    public bool SemFinalidades => Carregada && Finalidades.Count == 0;
    public bool SemPeriodosVisiveis => Carregada && !Periodos.Any(p => p.Visivel);
    public bool SemCanais => Carregada && Canais.Count == 0;

    // ---- Conceder ----
    [ObservableProperty] private Opcao<Guid?>[] _opcoesFinalidade = [SemFinalidade];
    [ObservableProperty] private Opcao<Guid?> _novaFinalidade = SemFinalidade;
    [ObservableProperty] private Opcao<CanalComunicacao?> _novoCanal = OpcoesCanal[0];
    [ObservableProperty] private string _novoMotivo = string.Empty;
    [ObservableProperty] private string _novaVersaoTermo = string.Empty;
    [ObservableProperty] private string _novaOrigem = string.Empty;

    /// <summary>Array: o Picker precisa de IList.</summary>
    public Opcao<CanalComunicacao?>[] ListaCanais => OpcoesCanal;

    public void Carregar(PrivacidadeDto dto)
    {
        Finalidades.Clear();
        foreach (var f in dto.Finalidades) Finalidades.Add(new FinalidadeConsentimentoItem(f));

        Periodos.Clear();
        foreach (var c in dto.Consentimentos)
            Periodos.Add(new PeriodoConsentimentoItem(c, Acoes) { MostrarHistorico = MostrarHistorico });

        Canais.Clear();
        foreach (var c in dto.Canais) Canais.Add(new CanalPrivacidadeItem(c));

        var escolhida = NovaFinalidade.Valor;
        OpcoesFinalidade =
        [
            SemFinalidade,
            .. dto.Finalidades.Where(f => f.Situacao != SituacaoConsentimento.NaoAplicavel)
                .Select(f => new Opcao<Guid?>(f.FinalidadeId, f.Nome))
        ];
        // Mantém a escolhida; sem escolha e com uma finalidade só, ela já vem escolhida.
        NovaFinalidade = (escolhida is null ? null : OpcoesFinalidade.FirstOrDefault(o => o.Valor == escolhida))
                         ?? (OpcoesFinalidade.Length == 2 ? OpcoesFinalidade[1] : SemFinalidade);

        Carregada = true;
        OnPropertyChanged(nameof(Carregada));
        AvisarListas();
    }

    partial void OnMostrarHistoricoChanged(bool value)
    {
        foreach (var p in Periodos) p.MostrarHistorico = value;
        AvisarListas();
    }

    /// <summary>Validação local da concessão (a API confere tudo de novo).</summary>
    public IReadOnlyList<string> ValidarConcessao()
    {
        var erros = new List<string>();
        if (NovaFinalidade.Valor is null) erros.Add("Escolha a finalidade.");
        if (string.IsNullOrWhiteSpace(NovoMotivo)) erros.Add("Informe o motivo (ex.: \"Autorizou no balcão\").");
        return erros;
    }

    public ConcederConsentimentoRequisicao ParaConcessao() => new()
    {
        FinalidadeId = NovaFinalidade.Valor ?? Guid.Empty,
        Canal = NovoCanal.Valor,
        Motivo = TextoTela.Nulo(NovoMotivo),
        VersaoTermo = TextoTela.Nulo(NovaVersaoTermo),
        Origem = TextoTela.Nulo(NovaOrigem)
    };

    /// <summary>Depois de conceder: limpa motivo, versão e origem (a finalidade e o canal ficam).</summary>
    public void LimparConcessao()
    {
        NovoMotivo = string.Empty;
        NovaVersaoTermo = string.Empty;
        NovaOrigem = string.Empty;
    }

    [RelayCommand]
    private Task ConcederAsync() => Acoes.Conceder?.Invoke() ?? Task.CompletedTask;

    private void AvisarListas()
    {
        OnPropertyChanged(nameof(TemHistorico));
        OnPropertyChanged(nameof(SemFinalidades));
        OnPropertyChanged(nameof(SemPeriodosVisiveis));
        OnPropertyChanged(nameof(SemCanais));
    }
}
