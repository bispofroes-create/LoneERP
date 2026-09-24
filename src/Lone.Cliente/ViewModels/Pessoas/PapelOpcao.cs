using CommunityToolkit.Mvvm.ComponentModel;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enums;

namespace Lone.Cliente.ViewModels.Pessoas;

/// <summary>Um papel na ficha (ligado/desligado). Guarda o registro existente para não perder o histórico.</summary>
public sealed partial class PapelOpcao : ObservableObject
{
    private PapelDto? _existente;

    public PapelOpcao(TipoPapel papel)
    {
        Papel = papel;
    }

    public TipoPapel Papel { get; }
    public string Texto => NomesPessoa.Papel(Papel);

    /// <summary>A pessoa já teve este papel: desligar só inativa, não apaga.</summary>
    public bool Existia => _existente is not null;

    [ObservableProperty] private bool _ativo;

    public static PapelOpcao De(TipoPapel papel, PapelDto? existente) =>
        new(papel) { _existente = existente, Ativo = existente?.Ativo ?? false };

    /// <summary>Nulo quando o papel nunca existiu e continua desligado.</summary>
    public PapelDto? ParaDto() => !Ativo && _existente is null
        ? null
        : new PapelDto
        {
            Id = _existente?.Id ?? Guid.Empty,
            Papel = Papel,
            Ativo = Ativo,
            InicioEm = _existente?.InicioEm ?? default, // a API usa a data de hoje
            FimEm = Ativo ? null : _existente?.FimEm,
            Observacoes = _existente?.Observacoes
        };
}
