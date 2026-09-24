using CommunityToolkit.Mvvm.ComponentModel;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Pessoas;
using Lone.Domain.Comum;
using Lone.Domain.Enums;
using Lone.Domain.ObjetosDeValor;

namespace Lone.Cliente.ViewModels.Pessoas;

/// <summary>Telefone, celular, WhatsApp ou e-mail da própria pessoa.</summary>
public sealed partial class MeioContatoFormulario : ItemDeLista
{
    public MeioContatoFormulario() : this(IdSequencial.Novo()) { }
    private MeioContatoFormulario(Guid id) => Id = id;

    public Guid Id { get; }
    public IReadOnlyList<Opcao<TipoContato>> Tipos => OpcoesPessoa.TiposContato;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Mascara))]
    private Opcao<TipoContato> _tipo = OpcoesPessoa.TiposContato[0];

    /// <summary>Telefone, celular e WhatsApp com máscara; e-mail e "outro" livres.</summary>
    public TipoMascara Mascara => Tipo.Valor is TipoContato.Telefone or TipoContato.Celular or TipoContato.WhatsApp
        ? TipoMascara.Telefone
        : TipoMascara.Nenhuma;
    [ObservableProperty] private string _valor = string.Empty;
    [ObservableProperty] private string _descricao = string.Empty;
    [ObservableProperty] private bool _principal;
    [ObservableProperty] private bool _permiteComunicacao = true;

    public static MeioContatoFormulario De(MeioContatoDto m) => new(m.Id)
    {
        Tipo = Opcao.De(OpcoesPessoa.TiposContato, m.Tipo),
        Valor = FormatoContato.Exibir(m.Tipo, m.Valor),
        Descricao = m.Descricao ?? string.Empty,
        Principal = m.Principal,
        PermiteComunicacao = m.PermiteComunicacao
    };

    public MeioContatoDto ParaDto() => new()
    {
        Id = Id,
        Tipo = Tipo.Valor,
        Valor = Valor,
        Descricao = TextoTela.Nulo(Descricao),
        Principal = Principal,
        PermiteComunicacao = PermiteComunicacao
    };
}

/// <summary>Pessoa de contato (ex.: "Maria — Financeiro").</summary>
public sealed partial class ContatoFormulario : ItemDeLista
{
    private Guid? _pessoaVinculadaId;

    public ContatoFormulario() : this(IdSequencial.Novo()) { }
    private ContatoFormulario(Guid id) => Id = id;

    public Guid Id { get; }

    [ObservableProperty] private string _nome = string.Empty;
    [ObservableProperty] private string _cargo = string.Empty;
    [ObservableProperty] private string _departamento = string.Empty;
    [ObservableProperty] private string _telefone = string.Empty;
    [ObservableProperty] private string _celular = string.Empty;
    [ObservableProperty] private bool _celularWhatsApp;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _observacoes = string.Empty;
    [ObservableProperty] private bool _principal;

    public static ContatoFormulario De(ContatoDto c) => new(c.Id)
    {
        _pessoaVinculadaId = c.PessoaVinculadaId,
        Nome = c.Nome,
        Cargo = c.Cargo ?? string.Empty,
        Departamento = c.Departamento ?? string.Empty,
        Telefone = FormatoContato.Exibir(TipoContato.Telefone, c.Telefone),
        Celular = FormatoContato.Exibir(TipoContato.Celular, c.Celular),
        CelularWhatsApp = c.CelularWhatsApp,
        Email = c.Email ?? string.Empty,
        Observacoes = c.Observacoes ?? string.Empty,
        Principal = c.Principal
    };

    public ContatoDto ParaDto() => new()
    {
        Id = Id,
        Nome = Nome,
        Cargo = TextoTela.Nulo(Cargo),
        Departamento = TextoTela.Nulo(Departamento),
        Telefone = TextoTela.Nulo(Telefone),
        Celular = TextoTela.Nulo(Celular),
        CelularWhatsApp = CelularWhatsApp,
        Email = TextoTela.Nulo(Email),
        Observacoes = TextoTela.Nulo(Observacoes),
        Principal = Principal,
        PessoaVinculadaId = _pessoaVinculadaId
    };
}

/// <summary>Telefones são gravados só com dígitos; na tela aparecem formatados.</summary>
internal static class FormatoContato
{
    public static string Exibir(TipoContato tipo, string? valor) =>
        valor is null ? string.Empty
        : tipo != TipoContato.Email && Telefone.TentarCriar(valor, out var telefone) ? telefone!.Formatado
        : valor;
}
