using CommunityToolkit.Mvvm.ComponentModel;
using Lone.Cliente.ViewModels.Comum;
using CommunityToolkit.Mvvm.Input;
using Lone.Contracts.Contatos;
using Lone.Contracts.Pessoas;
using Lone.Domain.Contatos;
using Lone.Domain.Comum;
using Lone.Domain.Enums;
using Lone.Domain.ObjetosDeValor;

namespace Lone.Cliente.ViewModels.Pessoas;

/// <summary>
/// Telefone ou e-mail da própria pessoa. "Remover" num já gravado só desativa (fica gravado, escondido da lista;
/// dá para ver e reativar em "Mostrar inativos"); num que ainda não foi gravado, tira da lista.
/// </summary>
public sealed partial class MeioContatoFormulario : ItemDeLista
{
    private IReadOnlyList<TipoMeioContatoDto> _catalogo = [];
    private Guid? _classificacaoGravada;
    private bool _catalogoDefinido;

    public MeioContatoFormulario() : this(IdSequencial.Novo(), gravado: false) { }
    private MeioContatoFormulario(Guid id, bool gravado)
    {
        Id = id;
        Gravado = gravado;
    }

    public Guid Id { get; }

    /// <summary>Já existe no banco: remover desativa em vez de tirar da lista.</summary>
    public bool Gravado { get; }

    public IReadOnlyList<Opcao<TipoContato>> Tipos => OpcoesPessoa.TiposContato;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Mascara), nameof(EhTelefone), nameof(EhFixo), nameof(EhEmail), nameof(TemClassificacao))]
    private Opcao<TipoContato> _tipo = OpcoesPessoa.TiposContato[0];

    /// <summary>Telefones com máscara; e-mail e "outro" livres.</summary>
    public TipoMascara Mascara => EhTelefone ? TipoMascara.Telefone : TipoMascara.Nenhuma;

    public bool EhTelefone => RegrasMeioContato.EhTelefone(Tipo.Valor);
    public bool EhFixo => Tipo.Valor == TipoContato.Telefone;
    public bool EhEmail => Tipo.Valor == TipoContato.Email;

    [ObservableProperty] private string _valor = string.Empty;
    [ObservableProperty] private string _descricao = string.Empty;
    [ObservableProperty] private string _ramal = string.Empty;
    [ObservableProperty] private bool _whatsApp;
    [ObservableProperty] private bool _sms;
    [ObservableProperty] private bool _financeiro;
    [ObservableProperty] private bool _cobranca;
    [ObservableProperty] private bool _nfe;
    [ObservableProperty] private bool _marketing;
    [ObservableProperty] private bool _principal;
    [ObservableProperty] private bool _permiteComunicacao = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Visivel), nameof(Inativo))]
    private bool _ativo = true;

    /// <summary>Ligado pela ficha em "Mostrar inativos".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Visivel))]
    private bool _mostrarSeInativo;

    public bool Visivel => Ativo || MostrarSeInativo;
    public bool Inativo => !Ativo;

    // ---- Classificação (cadastro de tipos: Comercial, Residencial...) ----

    /// <summary>"—" e os tipos ativos da categoria (mais o gravado, se desativado). Array: o Picker precisa de IList.</summary>
    [ObservableProperty] private Opcao<Guid?>[] _classificacoes = [SemClassificacao];

    [ObservableProperty] private Opcao<Guid?> _classificacao = SemClassificacao;

    public static readonly Opcao<Guid?> SemClassificacao = new(null, "—");

    public bool TemClassificacao => RegrasMeioContato.Categoria(Tipo.Valor) is not null;

    /// <summary>Chamado pela ficha ao incluir o item: a lista de tipos do cadastro.</summary>
    public void DefinirCatalogo(IReadOnlyList<TipoMeioContatoDto> catalogo)
    {
        _catalogo = catalogo;
        MontarClassificacoes();
        _catalogoDefinido = true;
    }

    partial void OnTipoChanged(Opcao<TipoContato> value) => MontarClassificacoes();

    private void MontarClassificacoes()
    {
        // Até a lista chegar, vale a gravada; depois, o que estiver escolhido na tela.
        var atual = _catalogoDefinido ? Classificacao?.Valor : _classificacaoGravada;
        var categoria = RegrasMeioContato.Categoria(Tipo.Valor);
        Classificacoes =
        [
            SemClassificacao,
            .. _catalogo
                .Where(t => t.Categoria == categoria && (t.Ativo || t.Id == _classificacaoGravada))
                .OrderBy(t => t.Ordem).ThenBy(t => t.Nome, StringComparer.CurrentCultureIgnoreCase)
                .Select(t => new Opcao<Guid?>(t.Id, t.Ativo ? t.Nome : t.Nome + " (desativado)"))
        ];
        Classificacao = Classificacoes.FirstOrDefault(o => o.Valor == atual) ?? SemClassificacao;
    }

    [RelayCommand]
    private void Reativar() => Ativo = true;

    public static MeioContatoFormulario De(MeioContatoDto m) => new(m.Id, gravado: true)
    {
        _classificacaoGravada = m.TipoMeioContatoId,
        Tipo = Opcao.De(OpcoesPessoa.TiposContato, m.Tipo == TipoContato.WhatsApp ? TipoContato.Celular : m.Tipo),
        Valor = FormatoContato.Exibir(m.Tipo, m.Valor),
        Descricao = m.Descricao ?? string.Empty,
        Ramal = m.Ramal ?? string.Empty,
        WhatsApp = m.WhatsApp || m.Tipo == TipoContato.WhatsApp,
        Sms = m.Sms,
        Financeiro = m.Finalidades.HasFlag(FinalidadeEmail.Financeiro),
        Cobranca = m.Finalidades.HasFlag(FinalidadeEmail.Cobranca),
        Nfe = m.Finalidades.HasFlag(FinalidadeEmail.NFe),
        Marketing = m.Finalidades.HasFlag(FinalidadeEmail.Marketing),
        Principal = m.Principal,
        PermiteComunicacao = m.PermiteComunicacao,
        Ativo = m.Ativo
    };

    public MeioContatoDto ParaDto() => new()
    {
        Id = Id,
        Tipo = Tipo.Valor,
        Valor = Valor,
        // Sem a lista de tipos (falha ao ler), a classificação gravada volta intacta.
        TipoMeioContatoId = !TemClassificacao ? null : _catalogo.Count > 0 ? Classificacao?.Valor : _classificacaoGravada,
        Ramal = EhFixo ? TextoTela.Nulo(Ramal) : null,
        WhatsApp = EhTelefone && WhatsApp,
        Sms = EhTelefone && Sms,
        Finalidades = !EhEmail ? FinalidadeEmail.Nenhuma
            : (Financeiro ? FinalidadeEmail.Financeiro : 0) | (Cobranca ? FinalidadeEmail.Cobranca : 0)
              | (Nfe ? FinalidadeEmail.NFe : 0) | (Marketing ? FinalidadeEmail.Marketing : 0),
        Descricao = TextoTela.Nulo(Descricao),
        Principal = Ativo && Principal,
        PermiteComunicacao = PermiteComunicacao,
        Ativo = Ativo
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
