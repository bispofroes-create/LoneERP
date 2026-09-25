using CommunityToolkit.Mvvm.ComponentModel;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Comercial;
using Lone.Domain.Comum;

namespace Lone.Cliente.ViewModels.Pessoas;

/// <summary>
/// Escolhas da aba "Cliente" (perfis, condições de pagamento, tipos de carteira, vendedores e empresas), lidas quando a
/// aba abre. Os desativados vêm junto só para mostrar o que já está gravado; as listas oferecem os ativos.
/// </summary>
public sealed class OpcoesComercial
{
    public static readonly Opcao<Guid?> Nenhum = new(null, "—");
    public static readonly Opcao<Guid?> Todas = new(null, "Todas as empresas");

    public static readonly Opcao<bool?>[] SimNao = [new(null, "— (não muda)"), new(true, "Sim"), new(false, "Não")];

    public OpcoesComercial(ComercialOpcoesDto dados) => Dados = dados;

    public ComercialOpcoesDto Dados { get; }

    public Opcao<Guid?>[] Perfis(Guid? gravado) =>
        OpcoesColaborador.Lista(Dados.Perfis, p => p.Id, p => p.Nome, p => p.Ativo, gravado);

    public Opcao<Guid?>[] Condicoes(Guid? gravado) =>
        OpcoesColaborador.Lista(Dados.Condicoes, c => c.Id, c => $"{c.Nome} ({c.Parcelas})", c => c.Ativo, gravado);

    public Opcao<Guid?>[] Empresas(Guid? gravada) =>
    [
        Todas,
        .. Dados.Empresas.Where(e => e.Ativa || e.Id == gravada)
            .Select(e => new Opcao<Guid?>(e.Id, e.Ativa ? e.Nome : e.Nome + " (inativa)"))
    ];

    public static Opcao<Guid?> Escolher(Opcao<Guid?>[] lista, Guid? id) => lista.FirstOrDefault(o => o.Valor == id) ?? lista[0];
}

/// <summary>
/// Exceção comercial com vigência: sobrescreve só os campos informados do perfil/conta. Exceção gravada nunca é
/// apagada (encerra pelo fim); uma nova, ainda não gravada, pode ser removida.
/// </summary>
public sealed partial class ExcecaoComercialFormulario : ItemDeLista
{
    private readonly ExcecaoComercialDto _gravada;
    private bool _carregadas;

    private ExcecaoComercialFormulario(ExcecaoComercialDto d, bool gravada)
    {
        _gravada = d;
        Gravada = gravada;
        Id = d.Id;
        _inicioEm = TextoTela.Data(d.InicioEm == default ? null : d.InicioEm);
        _fimEm = TextoTela.Data(d.FimEm);
        _limiteCredito = TextoTela.Decimal(d.LimiteCredito);
        _descontoMaximo = TextoTela.Decimal(d.DescontoMaximo);
        _diasMaximoAtraso = TextoTela.Inteiro(d.DiasMaximoAtraso);
        _exigeAprovacao = OpcoesComercial.SimNao.First(o => o.Valor == d.ExigeAprovacaoAcimaLimite);
        _motivo = d.Motivo ?? string.Empty;
    }

    public static ExcecaoComercialFormulario De(ExcecaoComercialDto d) => new(d, gravada: true);

    public static ExcecaoComercialFormulario Nova() =>
        new(new ExcecaoComercialDto { Id = IdSequencial.Novo(), InicioEm = DateOnly.FromDateTime(DateTime.Today) }, gravada: false);

    public Guid Id { get; }
    public bool Gravada { get; }
    public bool PodeRemover => !Gravada;

    [ObservableProperty] private string _inicioEm;
    [ObservableProperty] private string _fimEm;
    [ObservableProperty] private string _limiteCredito;
    [ObservableProperty] private string _descontoMaximo;
    [ObservableProperty] private string _diasMaximoAtraso;
    [ObservableProperty] private Opcao<bool?> _exigeAprovacao;
    [ObservableProperty] private string _motivo;
    [ObservableProperty] private Opcao<Guid?>[] _empresas = [OpcoesComercial.Todas];
    [ObservableProperty] private Opcao<Guid?> _empresa = OpcoesComercial.Todas;
    [ObservableProperty] private Opcao<Guid?>[] _condicoes = [OpcoesComercial.Nenhum];
    [ObservableProperty] private Opcao<Guid?> _condicao = OpcoesComercial.Nenhum;

    public IReadOnlyList<Opcao<bool?>> ListaSimNao => OpcoesComercial.SimNao;

    public void DefinirOpcoes(OpcoesComercial opcoes)
    {
        var atual = ParaDto();
        Empresas = opcoes.Empresas(_gravada.EmpresaId);
        Empresa = OpcoesComercial.Escolher(Empresas, atual.EmpresaId);
        Condicoes = opcoes.Condicoes(_gravada.CondicaoPagamentoId);
        Condicao = OpcoesComercial.Escolher(Condicoes, atual.CondicaoPagamentoId);
        _carregadas = true;
    }

    public IEnumerable<string> Validar(string rotulo)
    {
        if (!TextoTela.TentarData(InicioEm, out var inicio) || inicio is null) yield return $"{rotulo}: informe o início (dd/mm/aaaa).";
        if (!TextoTela.TentarData(FimEm, out _)) yield return $"{rotulo}: fim inválido (use dd/mm/aaaa).";
        if (!TextoTela.TentarDecimal(LimiteCredito, out _)) yield return $"{rotulo}: limite de crédito inválido.";
        if (!TextoTela.TentarDecimal(DescontoMaximo, out _)) yield return $"{rotulo}: desconto máximo inválido.";
        if (!TextoTela.TentarInteiro(DiasMaximoAtraso, out _)) yield return $"{rotulo}: dias de atraso inválidos.";
    }

    public ExcecaoComercialDto ParaDto()
    {
        TextoTela.TentarData(InicioEm, out var inicio);
        TextoTela.TentarData(FimEm, out var fim);
        TextoTela.TentarDecimal(LimiteCredito, out var limite);
        TextoTela.TentarDecimal(DescontoMaximo, out var desconto);
        TextoTela.TentarInteiro(DiasMaximoAtraso, out var dias);
        return new ExcecaoComercialDto
        {
            Id = Id,
            EmpresaId = _carregadas ? Empresa.Valor : _gravada.EmpresaId,
            InicioEm = inicio ?? default,
            FimEm = fim,
            LimiteCredito = limite,
            DescontoMaximo = desconto,
            DiasMaximoAtraso = dias,
            CondicaoPagamentoId = _carregadas ? Condicao.Valor : _gravada.CondicaoPagamentoId,
            ExigeAprovacaoAcimaLimite = ExigeAprovacao.Valor,
            Motivo = TextoTela.Nulo(Motivo)?.Trim()
        };
    }
}

/// <summary>
/// Vínculo da carteira de clientes: vendedor/representante que atende o cliente, com vigência. Gravado não é apagado:
/// encerra pelo fim, ou é desativado se foi lançado por engano.
/// </summary>
public sealed partial class CarteiraFormulario : ItemDeLista
{
    private readonly CarteiraDto _gravada;
    private bool _carregadas;

    private CarteiraFormulario(CarteiraDto d, bool gravada)
    {
        _gravada = d;
        Gravada = gravada;
        Id = d.Id;
        _inicioEm = TextoTela.Data(d.InicioEm == default ? null : d.InicioEm);
        _fimEm = TextoTela.Data(d.FimEm);
        _exclusivo = d.Exclusivo;
        _observacao = d.Observacao ?? string.Empty;
        _ativo = d.Ativo;
        _tipos = [new Opcao<Guid?>(d.TipoCarteiraId == Guid.Empty ? null : d.TipoCarteiraId, "(tipo gravado)")];
        _tipo = _tipos[0];
        _vendedores = [new Opcao<Guid?>(d.VendedorId == Guid.Empty ? null : d.VendedorId, d.Vendedor ?? "—")];
        _vendedor = _vendedores[0];
    }

    public static CarteiraFormulario De(CarteiraDto d) => new(d, gravada: true);

    public static CarteiraFormulario Nova(OpcoesComercial? opcoes)
    {
        var principal = opcoes?.Dados.TiposCarteira.FirstOrDefault(t => t.Principal && t.Ativo);
        var nova = new CarteiraFormulario(new CarteiraDto
        {
            Id = IdSequencial.Novo(),
            InicioEm = DateOnly.FromDateTime(DateTime.Today),
            TipoCarteiraId = principal?.Id ?? Guid.Empty
        }, gravada: false);
        if (opcoes is not null) nova.DefinirOpcoes(opcoes);
        return nova;
    }

    public Guid Id { get; }
    public bool Gravada { get; }
    public bool PodeRemover => !Gravada;

    [ObservableProperty] private string _inicioEm;
    [ObservableProperty] private string _fimEm;
    [ObservableProperty] private bool _exclusivo;
    [ObservableProperty] private string _observacao;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(Inativo))] private bool _ativo;
    [ObservableProperty] private Opcao<Guid?>[] _tipos;
    [ObservableProperty] private Opcao<Guid?> _tipo;
    [ObservableProperty] private Opcao<Guid?>[] _vendedores;
    [ObservableProperty] private Opcao<Guid?> _vendedor;
    [ObservableProperty] private Opcao<Guid?>[] _empresas = [OpcoesComercial.Todas];
    [ObservableProperty] private Opcao<Guid?> _empresa = OpcoesComercial.Todas;

    public bool Inativo => !Ativo;

    public void DefinirOpcoes(OpcoesComercial opcoes)
    {
        var atual = ParaDto();
        var d = opcoes.Dados;
        Tipos = OpcoesColaborador.Lista(d.TiposCarteira.OrderBy(t => t.Ordem), t => t.Id, t => t.Principal ? t.Nome + " (principal)" : t.Nome,
            t => t.Ativo, _gravada.TipoCarteiraId);
        Tipo = OpcoesComercial.Escolher(Tipos, atual.TipoCarteiraId == Guid.Empty ? null : atual.TipoCarteiraId);
        var vendedores = d.Vendedores.Select(v => new Opcao<Guid?>(v.Id, v.Nome)).ToList();
        if (_gravada.VendedorId != Guid.Empty && vendedores.All(v => v.Valor != _gravada.VendedorId))
            vendedores.Add(new Opcao<Guid?>(_gravada.VendedorId, (_gravada.Vendedor ?? "(vendedor gravado)") + " (sem o papel de vendedor)"));
        Vendedores = [OpcoesComercial.Nenhum, .. vendedores];
        Vendedor = OpcoesComercial.Escolher(Vendedores, atual.VendedorId == Guid.Empty ? null : atual.VendedorId);
        Empresas = opcoes.Empresas(_gravada.EmpresaId);
        Empresa = OpcoesComercial.Escolher(Empresas, atual.EmpresaId);
        _carregadas = true;
    }

    public IEnumerable<string> Validar(string rotulo)
    {
        if (!Ativo) yield break;
        if (!TextoTela.TentarData(InicioEm, out var inicio) || inicio is null) yield return $"{rotulo}: informe o início (dd/mm/aaaa).";
        if (!TextoTela.TentarData(FimEm, out _)) yield return $"{rotulo}: fim inválido (use dd/mm/aaaa).";
        if (Vendedor.Valor is null) yield return $"{rotulo}: escolha o vendedor.";
        if (Tipo.Valor is null) yield return $"{rotulo}: escolha o tipo.";
    }

    public CarteiraDto ParaDto()
    {
        TextoTela.TentarData(InicioEm, out var inicio);
        TextoTela.TentarData(FimEm, out var fim);
        return new CarteiraDto
        {
            Id = Id,
            EmpresaId = _carregadas ? Empresa.Valor : _gravada.EmpresaId,
            TipoCarteiraId = Tipo.Valor ?? Guid.Empty,
            VendedorId = Vendedor.Valor ?? Guid.Empty,
            InicioEm = inicio ?? default,
            FimEm = fim,
            Exclusivo = Exclusivo,
            Observacao = TextoTela.Nulo(Observacao)?.Trim(),
            Ativo = Ativo
        };
    }
}
