using CommunityToolkit.Mvvm.ComponentModel;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Pessoas;
using Lone.Domain.Comum;

namespace Lone.Cliente.ViewModels.Pessoas;

/// <summary>Conta padrão de cliente (vale para as empresas do grupo que não têm conta própria).</summary>
public sealed partial class ContaClienteFormulario : ObservableObject
{
    private Guid? _vendedorPadraoId;
    private Guid? _perfilGravado;
    private Guid? _condicaoGravada;
    private bool _opcoesCarregadas;

    public ContaClienteFormulario() : this(IdSequencial.Novo(), existia: false) { }

    private ContaClienteFormulario(Guid id, bool existia)
    {
        Id = id;
        Existia = existia;
    }

    public Guid Id { get; }
    public bool Existia { get; }

    [ObservableProperty] private string _limiteCredito = string.Empty;
    [ObservableProperty] private string _diasMaximoAtraso = string.Empty;
    [ObservableProperty] private string _descontoMaximo = string.Empty;
    /// <summary>
    /// Texto anterior ao cadastro de condições (preservado, somente leitura na ficha, como no fornecedor). A migração
    /// ligou ao cadastro o que tinha o mesmo nome; o resto aparece como "não convertida" até alguém escolher a condição.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoCondicaoAnterior), nameof(TemCondicaoAnterior))]
    private string _condicaoPagamento = string.Empty;

    [ObservableProperty] private bool _exigeAprovacaoAcimaLimite = true;
    [ObservableProperty] private string _observacoes = string.Empty;

    /// <summary>Perfil comercial (padrões de venda). Os campos acima valem só onde o perfil não define.</summary>
    [ObservableProperty] private Opcao<Guid?>[] _perfis = [OpcoesComercial.Nenhum];
    [ObservableProperty] private Opcao<Guid?> _perfil = OpcoesComercial.Nenhum;

    /// <summary>Condição de pagamento do cadastro (o texto antigo continua guardado em <see cref="CondicaoPagamento"/>).</summary>
    [ObservableProperty] private Opcao<Guid?>[] _condicoes = [OpcoesComercial.Nenhum];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoCondicaoAnterior), nameof(TemCondicaoAnterior))]
    private Opcao<Guid?> _condicao = OpcoesComercial.Nenhum;

    public Guid? PerfilId => _opcoesCarregadas ? Perfil.Valor : _perfilGravado;
    public Guid? CondicaoId => _opcoesCarregadas ? Condicao.Valor : _condicaoGravada;

    public bool TemCondicaoAnterior => TextoCondicaoAnterior.Length > 0;

    /// <summary>"Condição anterior (texto): 28 dias — não convertida; escolha a condição acima." (mesma regra do fornecedor).</summary>
    public string TextoCondicaoAnterior =>
        string.IsNullOrWhiteSpace(CondicaoPagamento) ? string.Empty
        : CondicaoId is null
            ? $"Condição anterior (texto): {CondicaoPagamento.Trim()} — não convertida; escolha a condição de pagamento acima."
            : $"Condição anterior (texto, guardada): {CondicaoPagamento.Trim()}";

    public void DefinirOpcoes(OpcoesComercial opcoes)
    {
        var perfil = PerfilId;
        var condicao = CondicaoId;
        Perfis = opcoes.Perfis(_perfilGravado);
        Perfil = OpcoesComercial.Escolher(Perfis, perfil);
        Condicoes = opcoes.Condicoes(_condicaoGravada);
        Condicao = OpcoesComercial.Escolher(Condicoes, condicao);
        _opcoesCarregadas = true;
        OnPropertyChanged(nameof(TextoCondicaoAnterior));
        OnPropertyChanged(nameof(TemCondicaoAnterior));
    }

    public static ContaClienteFormulario De(ContaClienteDto? c) => c is null
        ? new ContaClienteFormulario()
        : new ContaClienteFormulario(c.Id, existia: true)
        {
            _vendedorPadraoId = c.VendedorPadraoId,
            _perfilGravado = c.PerfilComercialId,
            _condicaoGravada = c.CondicaoPagamentoId,
            LimiteCredito = TextoTela.Decimal(c.LimiteCredito),
            DiasMaximoAtraso = TextoTela.Inteiro(c.DiasMaximoAtraso),
            DescontoMaximo = TextoTela.Decimal(c.DescontoMaximo),
            CondicaoPagamento = c.CondicaoPagamento ?? string.Empty,
            ExigeAprovacaoAcimaLimite = c.ExigeAprovacaoAcimaLimite,
            Observacoes = c.Observacoes ?? string.Empty
        };

    public IEnumerable<string> Validar()
    {
        if (!TextoTela.TentarDecimal(LimiteCredito, out _)) yield return "Cliente: limite de crédito inválido.";
        if (!TextoTela.TentarInteiro(DiasMaximoAtraso, out _)) yield return "Cliente: dias máximos de atraso inválidos.";
        if (!TextoTela.TentarDecimal(DescontoMaximo, out _)) yield return "Cliente: desconto máximo inválido.";
    }

    public ContaClienteDto ParaDto()
    {
        TextoTela.TentarDecimal(LimiteCredito, out var limite);
        TextoTela.TentarInteiro(DiasMaximoAtraso, out var dias);
        TextoTela.TentarDecimal(DescontoMaximo, out var desconto);
        return new ContaClienteDto
        {
            Id = Id,
            EmpresaId = null,
            LimiteCredito = limite,
            DiasMaximoAtraso = dias,
            DescontoMaximo = desconto,
            CondicaoPagamento = TextoTela.Nulo(CondicaoPagamento),
            ExigeAprovacaoAcimaLimite = ExigeAprovacaoAcimaLimite,
            VendedorPadraoId = _vendedorPadraoId,
            PerfilComercialId = PerfilId,
            CondicaoPagamentoId = CondicaoId,
            Observacoes = TextoTela.Nulo(Observacoes)
        };
    }
}

/// <summary>Conta padrão de fornecedor.</summary>
public sealed partial class ContaFornecedorFormulario : ObservableObject
{
    private Guid? _transportadoraPadraoId;
    private Guid? _condicaoGravada;
    private bool _opcoesCarregadas;

    public ContaFornecedorFormulario() : this(IdSequencial.Novo(), existia: false) { }

    private ContaFornecedorFormulario(Guid id, bool existia)
    {
        Id = id;
        Existia = existia;
    }

    public Guid Id { get; }
    public bool Existia { get; }

    /// <summary>
    /// Texto anterior ao cadastro de condições (preservado, somente leitura na ficha). A migração ligou ao cadastro o
    /// que tinha o mesmo nome; o resto aparece como "não convertida" até alguém escolher a condição.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoCondicaoAnterior), nameof(TemCondicaoAnterior))]
    private string _condicaoPagamento = string.Empty;

    /// <summary>Condição de pagamento do cadastro (fonte principal, como no cliente).</summary>
    [ObservableProperty] private Opcao<Guid?>[] _condicoes = [OpcoesComercial.Nenhum];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoCondicaoAnterior), nameof(TemCondicaoAnterior))]
    private Opcao<Guid?> _condicao = OpcoesComercial.Nenhum;

    public Guid? CondicaoId => _opcoesCarregadas ? Condicao.Valor : _condicaoGravada;

    public bool TemCondicaoAnterior => TextoCondicaoAnterior.Length > 0;

    /// <summary>"Condição anterior (texto): 28 dias — não convertida; escolha a condição acima."</summary>
    public string TextoCondicaoAnterior =>
        string.IsNullOrWhiteSpace(CondicaoPagamento) ? string.Empty
        : CondicaoId is null
            ? $"Condição anterior (texto): {CondicaoPagamento.Trim()} — não convertida; escolha a condição de pagamento acima."
            : $"Condição anterior (texto, guardada): {CondicaoPagamento.Trim()}";

    public void DefinirOpcoes(OpcoesComercial opcoes)
    {
        var condicao = CondicaoId;
        Condicoes = opcoes.Condicoes(_condicaoGravada);
        Condicao = OpcoesComercial.Escolher(Condicoes, condicao);
        _opcoesCarregadas = true;
        OnPropertyChanged(nameof(TextoCondicaoAnterior));
        OnPropertyChanged(nameof(TemCondicaoAnterior));
    }

    [ObservableProperty] private string _prazoMedioDias = string.Empty;
    [ObservableProperty] private string _leadTimeDias = string.Empty;
    [ObservableProperty] private string _avaliacao = string.Empty;
    [ObservableProperty] private string _observacoes = string.Empty;

    public static ContaFornecedorFormulario De(ContaFornecedorDto? f) => f is null
        ? new ContaFornecedorFormulario()
        : new ContaFornecedorFormulario(f.Id, existia: true)
        {
            _transportadoraPadraoId = f.TransportadoraPadraoId,
            _condicaoGravada = f.CondicaoPagamentoId,
            CondicaoPagamento = f.CondicaoPagamento ?? string.Empty,
            PrazoMedioDias = TextoTela.Inteiro(f.PrazoMedioDias),
            LeadTimeDias = TextoTela.Inteiro(f.LeadTimeDias),
            Avaliacao = TextoTela.Inteiro(f.Avaliacao),
            Observacoes = f.Observacoes ?? string.Empty
        };

    public IEnumerable<string> Validar()
    {
        if (!TextoTela.TentarInteiro(PrazoMedioDias, out _)) yield return "Fornecedor: prazo médio inválido.";
        if (!TextoTela.TentarInteiro(LeadTimeDias, out _)) yield return "Fornecedor: prazo de entrega inválido.";
        if (!TextoTela.TentarInteiro(Avaliacao, out var nota) || nota is < 1 or > 5)
            yield return "Fornecedor: avaliação deve ser de 1 a 5 (vazio = sem avaliação).";
    }

    public ContaFornecedorDto ParaDto()
    {
        TextoTela.TentarInteiro(PrazoMedioDias, out var prazo);
        TextoTela.TentarInteiro(LeadTimeDias, out var lead);
        TextoTela.TentarInteiro(Avaliacao, out var nota);
        return new ContaFornecedorDto
        {
            Id = Id,
            EmpresaId = null,
            CondicaoPagamento = TextoTela.Nulo(CondicaoPagamento),
            CondicaoPagamentoId = CondicaoId,
            PrazoMedioDias = prazo,
            LeadTimeDias = lead,
            TransportadoraPadraoId = _transportadoraPadraoId,
            Avaliacao = nota is { } n ? (byte)n : null,
            Observacoes = TextoTela.Nulo(Observacoes)
        };
    }
}
