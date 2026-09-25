using CommunityToolkit.Mvvm.ComponentModel;
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

    public PapelOpcao(Guid papelId, TipoPapel? papel, string nome, bool papelAtivo = true, IEnumerable<PapelDto>? periodos = null)
    {
        PapelId = papelId;
        Papel = papel;
        Nome = nome;
        PapelAtivo = papelAtivo;
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
    [NotifyPropertyChangedFor(nameof(Detalhe))]
    private bool _ativo;

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
