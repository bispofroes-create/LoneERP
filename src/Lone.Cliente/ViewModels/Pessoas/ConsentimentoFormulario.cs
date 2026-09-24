using CommunityToolkit.Mvvm.ComponentModel;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Pessoas;
using Lone.Domain.Comum;
using Lone.Domain.Enums;

namespace Lone.Cliente.ViewModels.Pessoas;

/// <summary>
/// Autorização da pessoa para um canal de comunicação (LGPD). As datas são gravadas pela API: ao marcar,
/// "autorizado em"; ao desmarcar uma autorização dada, "revogado em".
/// </summary>
public sealed partial class ConsentimentoFormulario : ObservableObject
{
    private readonly DateTime? _concedidoEm;
    private readonly DateTime? _revogadoEm;
    private readonly bool _concedidoGravado;

    private ConsentimentoFormulario(Guid id, CanalComunicacao canal, bool concedido, DateTime? concedidoEm, DateTime? revogadoEm, string? origem)
    {
        Id = id;
        Canal = canal;
        _concedido = _concedidoGravado = concedido;
        _concedidoEm = concedidoEm;
        _revogadoEm = revogadoEm;
        _origem = origem ?? string.Empty;
    }

    public Guid Id { get; }
    public CanalComunicacao Canal { get; }
    public string Texto => NomesPessoa.Canal(Canal);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Situacao))]
    private bool _concedido;

    [ObservableProperty] private string _origem;

    /// <summary>Ex.: "Autorizado em 24/09/2026" ou "Revogado em 30/09/2026".</summary>
    public string Situacao =>
        Concedido != _concedidoGravado ? "Será registrado ao salvar"
        : Concedido && _concedidoEm is { } em ? $"Autorizado em {Data(em)}"
        : !Concedido && _revogadoEm is { } rev ? $"Revogado em {Data(rev)}"
        : "Sem autorização";

    public static ConsentimentoFormulario Novo(CanalComunicacao canal) => new(IdSequencial.Novo(), canal, false, null, null, null);

    public static ConsentimentoFormulario De(ConsentimentoDto c) => new(c.Id, c.Canal, c.Concedido, c.ConcedidoEm, c.RevogadoEm, c.Origem);

    /// <summary>
    /// Nulo quando nunca houve autorização e continua sem (não cria registro à toa). Reautorizar depois de
    /// revogado manda a data vazia, para a API gravar a nova data.
    /// </summary>
    public ConsentimentoDto? ParaDto()
    {
        if (!Concedido && _concedidoEm is null) return null;
        var reautorizado = Concedido && !_concedidoGravado;
        return new ConsentimentoDto
        {
            Id = Id,
            Canal = Canal,
            Concedido = Concedido,
            ConcedidoEm = reautorizado ? null : _concedidoEm,
            RevogadoEm = Concedido ? null : _revogadoEm,
            Origem = TextoTela.Nulo(Origem)
        };
    }

    private static string Data(DateTime utc) => utc.ToLocalTime().ToString("dd/MM/yyyy", TextoTela.Brasil);
}
