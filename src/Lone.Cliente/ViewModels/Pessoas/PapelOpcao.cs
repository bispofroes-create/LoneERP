using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enums;

namespace Lone.Cliente.ViewModels.Pessoas;

/// <summary>
/// Um papel na ficha (marcado/desmarcado), com os períodos já gravados dele. Nada é apagado: desmarcar encerra o
/// período aberto; marcar de novo depois de encerrado começa outro período (o anterior fica no histórico).
/// </summary>
public sealed partial class PapelOpcao : ObservableObject
{
    private readonly List<PapelDto> _encerrados;
    private readonly PapelDto? _aberto;

    public PapelOpcao(Guid papelId, TipoPapel? papel, string nome, bool papelAtivo = true, IEnumerable<PapelDto>? periodos = null,
                      string? descricao = null)
    {
        PapelId = papelId;
        Papel = papel;
        Nome = nome;
        PapelAtivo = papelAtivo;
        Descricao = string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim();
        var lista = (periodos ?? []).ToList();
        _aberto = lista.FirstOrDefault(p => p.Ativo);
        _encerrados = lista.Where(p => !ReferenceEquals(p, _aberto)).OrderBy(p => p.InicioEm).ToList();
        _ativo = _aberto is not null;
    }

    /// <summary>Id no cadastro de papéis.</summary>
    public Guid PapelId { get; }

    /// <summary>Papel de sistema (regras da ficha: abas de cliente e fornecedor, cor/raça de funcionário); nulo nos do usuário.</summary>
    public TipoPapel? Papel { get; }

    public string Nome { get; }

    /// <summary>Falso = papel desativado no cadastro de papéis (só aparece para quem já o tem).</summary>
    public bool PapelAtivo { get; }

    public string Texto => PapelAtivo ? Nome : Nome + " (desativado)";

    /// <summary>A pessoa já teve este papel (algum período gravado).</summary>
    public bool Existia => _aberto is not null || _encerrados.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Detalhe), nameof(EstadoTexto), nameof(Periodo), nameof(DescricaoInterruptor))]
    private bool _ativo;

    // ---- Apresentação no cartão da ficha (só leitura: a regra dos períodos continua no domínio, aplicada ao salvar) ----

    /// <summary>Descrição do cadastro de papéis (vazia = não mostra; nada é inventado).</summary>
    public string? Descricao { get; }
    public bool TemDescricao => Descricao is not null;

    public string EstadoTexto => Ativo ? "● Ativo" : "○ Inativo";

    /// <summary>
    /// Linha do período: "Desde 01/01/2025"; "Último período: 01/01/2024 → 30/06/2024"; e o que acontece ao salvar
    /// ("Começa ao salvar", "encerra ao salvar"), porque o interruptor só muda a ficha — o período é gravado pela API.
    /// </summary>
    public string Periodo => (Ativo, _aberto, _encerrados.LastOrDefault()) switch
    {
        (true, { } aberto, _) => $"Desde {TextoTela.Data(aberto.InicioEm)}",
        (true, null, null) => "Começa ao salvar",
        (true, null, _) => "Novo período começa ao salvar",
        (false, { } aberto, _) => $"Desde {TextoTela.Data(aberto.InicioEm)} · encerra ao salvar",
        (false, null, { } ultimo) => $"Último período: {Intervalo(ultimo)}",
        _ => string.Empty
    };

    /// <summary>Aparece na ficha: em vigor, marcado agora ou com período gravado. Nunca atribuído só aparece em "Adicionar papel".</summary>
    public bool NaFicha => Ativo || Existia;

    /// <summary>Pode ligar/desligar nesta ficha (permissões e papel desativado no cadastro); definido pela ficha.</summary>
    [ObservableProperty] private bool _editavel = true;

    /// <summary>Papel desativado no cadastro não começa período novo (regra da API); o que já está aberto pode ser encerrado.</summary>
    public bool PodeReativar => PapelAtivo || _aberto is not null;

    public string DescricaoInterruptor => $"{Nome}: {(Ativo ? "ativo" : "inativo")}";

    /// <summary>Períodos gravados, do mais novo para o mais antigo (quem alterou e o motivo ficam na auditoria, aba Histórico).</summary>
    public IReadOnlyList<PeriodoPapel> Historico
    {
        get
        {
            var linhas = new List<PeriodoPapel>();
            if (_aberto is { } aberto) linhas.Add(new PeriodoPapel("● Em vigor", $"{TextoTela.Data(aberto.InicioEm)} → atual"));
            linhas.AddRange(_encerrados.AsEnumerable().Reverse().Select(p => new PeriodoPapel("○ Encerrado", Intervalo(p))));
            return linhas;
        }
    }

    public bool TemHistorico => Existia;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoHistorico))]
    private bool _mostrarHistorico;

    public string TextoHistorico => MostrarHistorico ? "Ocultar histórico" : "Histórico ›";

    [RelayCommand]
    private void AlternarHistorico() => MostrarHistorico = !MostrarHistorico;

    private static string Intervalo(PapelDto p) =>
        $"{TextoTela.Data(p.InicioEm)} → {(p.FimEm is { } fim ? TextoTela.Data(fim) : "sem data de fim")}";

    /// <summary>Período em texto curto: "desde 01/01/2025", "a partir de hoje", "até 30/06/2025".</summary>
    public string Detalhe => Ativo
        ? _aberto is { } aberto ? $"desde {TextoTela.Data(aberto.InicioEm)}" : "a partir de hoje"
        : _aberto is not null ? "encerra hoje"
        : _encerrados.LastOrDefault() is { FimEm: { } fim } ? $"até {TextoTela.Data(fim)}"
        : string.Empty;

    /// <summary>
    /// Os períodos para a API: os encerrados intactos e, se marcado, o aberto (ou um novo, que a API começa hoje).
    /// Desmarcar manda o aberto como inativo (a API encerra hoje). Vazio se o papel nunca existiu e segue desmarcado.
    /// </summary>
    public IEnumerable<PapelDto> ParaDtos()
    {
        foreach (var encerrado in _encerrados)
            yield return Copia(encerrado, encerrado.Ativo, encerrado.FimEm);

        if (Ativo)
            yield return _aberto is { } aberto ? Copia(aberto, true, null) : new PapelDto { PapelId = PapelId, Papel = Papel, Ativo = true };
        else if (_aberto is { } aberto)
            yield return Copia(aberto, false, null);
    }

    private PapelDto Copia(PapelDto p, bool ativo, DateOnly? fim) => new()
    {
        Id = p.Id,
        PapelId = PapelId,
        Papel = Papel,
        Ativo = ativo,
        InicioEm = p.InicioEm,
        FimEm = fim,
        Observacoes = p.Observacoes
    };
}

/// <summary>Um período gravado do papel, como aparece no histórico do cartão.</summary>
public sealed record PeriodoPapel(string Estado, string Intervalo);
