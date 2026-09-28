using CommunityToolkit.Mvvm.ComponentModel;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Comercial;
using Lone.Domain.Comercial;
using Lone.Domain.Comum;
using Lone.Domain.Enums;

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
/// Vínculo da carteira de clientes: quem atende o cliente num papel comercial (Vendedor, Supervisor...), com vigência.
/// Gravado não é apagado: encerra pelo fim, ou é desativado se foi lançado por engano. Depois que começou, papel, pessoa,
/// empresa, início, exclusivo e crédito ficam travados (o histórico não é reescrito): mudar é "Trocar" (encerra este na
/// véspera e abre outro). A API confere as mesmas regras (RegrasComercial.ValidarHistorico).
/// </summary>
public sealed partial class CarteiraFormulario : ItemDeLista
{
    private readonly CarteiraDto _gravada;
    private readonly DateOnly _hoje;
    private bool _carregadas;
    private IReadOnlyDictionary<Guid, TipoCarteiraDto> _papeis = new Dictionary<Guid, TipoCarteiraDto>();
    private IReadOnlyList<AtendenteOpcaoDto> _atendentes = [];
    private IReadOnlyDictionary<Guid, string> _classificacoes = new Dictionary<Guid, string>();

    private CarteiraFormulario(CarteiraDto d, bool gravada, DateOnly? hoje = null)
    {
        _gravada = d;
        _hoje = hoje ?? DateOnly.FromDateTime(DateTime.Today);
        Gravada = gravada;
        Id = d.Id;
        _inicioEm = TextoTela.Data(d.InicioEm == default ? null : d.InicioEm);
        _fimEm = TextoTela.Data(d.FimEm);
        _exclusivo = d.Exclusivo;
        _percentualCredito = TextoTela.Decimal(d.PercentualCredito);
        _observacao = d.Observacao ?? string.Empty;
        _ativo = d.Ativo;
        _tipos = [new Opcao<Guid?>(d.TipoCarteiraId == Guid.Empty ? null : d.TipoCarteiraId, "(papel gravado)")];
        _tipo = _tipos[0];
        _vendedores = [new Opcao<Guid?>(d.VendedorId == Guid.Empty ? null : d.VendedorId, d.Vendedor ?? "—")];
        _vendedor = _vendedores[0];
    }

    public static CarteiraFormulario De(CarteiraDto d, DateOnly? hoje = null) => new(d, gravada: true, hoje);

    /// <summary>Vínculo novo do papel informado (ou, sem ele, do responsável da conta), começando hoje.</summary>
    public static CarteiraFormulario Nova(OpcoesComercial? opcoes, Guid? papelId = null, DateOnly? hoje = null)
    {
        var dia = hoje ?? DateOnly.FromDateTime(DateTime.Today);
        var papel = papelId ?? opcoes?.Dados.TiposCarteira.FirstOrDefault(t => t.ResponsavelDaConta && t.Ativo)?.Id;
        var nova = new CarteiraFormulario(new CarteiraDto
        {
            Id = IdSequencial.Novo(),
            InicioEm = dia,
            TipoCarteiraId = papel ?? Guid.Empty
        }, gravada: false, dia);
        if (opcoes is not null) nova.DefinirOpcoes(opcoes);
        return nova;
    }

    public Guid Id { get; }
    public bool Gravada { get; }
    public bool PodeRemover => true;

    /// <summary>Gravado e já começou: papel, pessoa, empresa, início, exclusivo e crédito não mudam mais.</summary>
    public bool Travado => Gravada && _gravada.InicioEm != default && _gravada.InicioEm <= _hoje;
    public bool Editavel => !Travado;

    /// <summary>Estava encerrado (ou desativado) quando a ficha abriu: fica na parte "Histórico" da carteira.</summary>
    public bool NoHistorico => Gravada && (!_gravada.Ativo || _gravada.FimEm < _hoje);

    /// <summary>Gravado, ativo e ainda valendo (ou a começar): pode ser trocado a partir de uma data.</summary>
    public bool PodeTrocar => Travado && Ativo && _gravada.Ativo && !(_gravada.FimEm < _hoje) && Substituto is null;

    /// <summary>Só o nome de quem atende (a lista mostra também a classificação), para mensagens e confirmações.</summary>
    public string NomePessoa => Vendedor.Valor is { } id
        ? _atendentes.FirstOrDefault(a => a.Id == id)?.Nome ?? (id == _gravada.VendedorId ? _gravada.Vendedor : null) ?? Vendedor.Texto
        : Vendedor.Texto;

    public string TextoRemover => Gravada ? "Desativar (lançado por engano)" : "Remover";

    /// <summary>Definido pela ficha: "Trocar" abre o vínculo que substitui este.</summary>
    public Action? AoTrocar { get; set; }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void Trocar() => AoTrocar?.Invoke();

    /// <summary>Desfaz o "lançado por engano" antes de gravar (ou reativa um desativado).</summary>
    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void Reativar() => Ativo = true;

    [ObservableProperty][NotifyPropertyChangedFor(nameof(Situacao), nameof(Prazo), nameof(PertoDoFim), nameof(AvisoFim), nameof(AvisoCobertura), nameof(TemCobertura))] private string _inicioEm;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(Situacao), nameof(PodeTrocar), nameof(Prazo), nameof(PertoDoFim), nameof(AvisoFim), nameof(AvisoCobertura), nameof(TemCobertura))] private string _fimEm;
    [ObservableProperty] private bool _exclusivo;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(RecebeCredito))] private string _percentualCredito;
    [ObservableProperty] private string _observacao;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(Inativo), nameof(Situacao), nameof(PodeTrocar), nameof(PertoDoFim), nameof(AvisoFim), nameof(AvisoCobertura), nameof(TemCobertura))] private bool _ativo;
    [ObservableProperty] private Opcao<Guid?>[] _tipos;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(RecebeCredito), nameof(DicaCredito), nameof(RotuloPessoa), nameof(AvisoCobertura), nameof(TemCobertura))] private Opcao<Guid?> _tipo;
    [ObservableProperty] private Opcao<Guid?>[] _vendedores;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(AvisoCobertura), nameof(TemCobertura))] private Opcao<Guid?> _vendedor;
    [ObservableProperty] private Opcao<Guid?>[] _empresas = [OpcoesComercial.Todas];
    [ObservableProperty][NotifyPropertyChangedFor(nameof(AvisoCobertura), nameof(TemCobertura))] private Opcao<Guid?> _empresa = OpcoesComercial.Todas;

    public bool Inativo => !Ativo;

    private TipoCarteiraDto? Papel => Tipo.Valor is { } id ? _papeis.GetValueOrDefault(id) : null;

    /// <summary>O campo da pessoa leva o nome do papel ("Vendedor", "Supervisor"...).</summary>
    public string RotuloPessoa => Papel?.Nome ?? "Quem atende";

    /// <summary>"Vigente desde 01/03/2026", "Encerrado em 14/03/2026", "Começa em 01/10/2026", "Lançado por engano".</summary>
    public string Situacao
    {
        get
        {
            if (!Ativo) return "Lançado por engano (fica só no histórico)";
            TextoTela.TentarData(InicioEm, out var inicio);
            TextoTela.TentarData(FimEm, out var fim);
            if (fim is { } f && f < _hoje) return $"Encerrado em {TextoTela.Data(f)}";
            if (inicio is { } i && i > _hoje) return $"Começa em {TextoTela.Data(i)}" + (fim is { } f2 ? $" · até {TextoTela.Data(f2)}" : string.Empty);
            var desde = inicio is { } i2 ? $"Vigente desde {TextoTela.Data(i2)}" : "Vigente";
            return fim is { } f3 ? $"{desde} · até {TextoTela.Data(f3)}" : desde;
        }
    }

    // ---- Prazo: duração, quanto falta e destaque perto do fim (pedido do usuário, 28/09/2026) ----

    /// <summary>Com quantos dias de antecedência o fim é destacado (vem das opções; padrão 30).</summary>
    private int _diasAviso = RegrasComercial.DiasAvisoFimPadrao;

    private static string Dias(int n) => n == 1 ? "1 dia" : $"{n.ToString("N0", TextoTela.Brasil)} dias";

    private (DateOnly? Inicio, DateOnly? Fim) Periodo()
    {
        TextoTela.TentarData(InicioEm, out var inicio);
        TextoTela.TentarData(FimEm, out var fim);
        return (inicio, fim);
    }

    /// <summary>Dias até o fim (0 = termina hoje), para vínculo ativo que ainda não terminou; nulo sem fim ou já encerrado.</summary>
    private int? Faltam => Ativo && Periodo() is { Fim: { } fim } && fim >= _hoje ? fim.DayNumber - _hoje.DayNumber : null;

    /// <summary>"93 dias · faltam 12 dias", "sem fim", "30 dias · começa em 5 dias". Atualiza a cada dia (é calculado).</summary>
    public string Prazo
    {
        get
        {
            var (inicio, fim) = Periodo();
            if (fim is not { } f) return "sem data de fim";
            var duracao = inicio is { } i && f >= i ? Dias(f.DayNumber - i.DayNumber + 1) : string.Empty;
            string? resto = !Ativo || f < _hoje ? null
                : inicio is { } ini && ini > _hoje ? $"começa em {Dias(ini.DayNumber - _hoje.DayNumber)}"
                : f == _hoje ? "termina hoje" : $"faltam {Dias(f.DayNumber - _hoje.DayNumber)}";
            return string.Join(" · ", new[] { duracao, resto }.Where(x => !string.IsNullOrEmpty(x)));
        }
    }

    // ---- Ausência de quem atende (coberturas vigentes ou agendadas, Fase 1c) ----

    private IReadOnlyList<CoberturaAvisoDto> _coberturas = [];

    /// <summary>
    /// "rafael: Férias de 01/10/2026 a 15/10/2026 · atendimento por Maria (crédito do titular)": as coberturas do titular
    /// deste vínculo no escopo dele (papel e empresa), que ainda não terminaram. A carteira não muda por causa delas.
    /// </summary>
    public string AvisoCobertura
    {
        get
        {
            if (!Ativo || Vendedor.Valor is not { } pessoa || _coberturas.Count == 0) return string.Empty;
            var (inicio, fim) = Periodo();
            var empresa = _carregadas ? Empresa.Valor : _gravada.EmpresaId;
            var textos = _coberturas
                .Where(c => c.TitularId == pessoa && (c.TipoCarteiraId is null || c.TipoCarteiraId == Tipo.Valor) &&
                            (c.EmpresaId is null || empresa is null || c.EmpresaId == empresa) &&
                            (inicio is null || c.FimEm >= inicio) && (fim is null || c.InicioEm <= fim))
                .OrderBy(c => c.InicioEm).Select(c => c.Texto).ToList();
            return string.Join(Environment.NewLine, textos);
        }
    }

    public bool TemCobertura => AvisoCobertura.Length > 0;

    /// <summary>Vínculo que termina dentro da antecedência de aviso: o cartão fica destacado para o usuário decidir.</summary>
    public bool PertoDoFim => Faltam is { } n && n <= _diasAviso;

    public string AvisoFim => Faltam is { } n && n <= _diasAviso
        ? (n == 0 ? "Termina hoje" : $"Termina em {Dias(n)}") + ": renove (mude ou limpe o fim), troque ou deixe encerrar."
        : string.Empty;

    public bool RecebeCredito => Papel is { TipoCredito: not TipoCreditoComercial.Nenhum } || !string.IsNullOrWhiteSpace(PercentualCredito);

    public string DicaCredito => Papel switch
    {
        { TipoCredito: TipoCreditoComercial.Receita, PercentualPadrao: { } p } => $"vazio = {TextoTela.Decimal(p)}% (padrão)",
        { TipoCredito: TipoCreditoComercial.Receita } => "vazio = 100% se for o único",
        { TipoCredito: TipoCreditoComercial.Sobreposicao, PercentualPadrao: { } p } => $"extra; vazio = {TextoTela.Decimal(p)}%",
        { TipoCredito: TipoCreditoComercial.Sobreposicao } => "extra; vazio = 100%",
        _ => "este papel não recebe crédito"
    };

    // ---- Trocar: este vínculo novo entra no lugar de um gravado, que fica até a véspera do início deste ----

    /// <summary>O vínculo gravado que este (novo) substitui, e o fim que ele tinha antes da troca.</summary>
    public CarteiraFormulario? Substitui { get; private set; }
    private string _fimAntesDaTroca = string.Empty;

    /// <summary>O vínculo novo que entra no lugar deste (enquanto a troca não é gravada).</summary>
    public CarteiraFormulario? Substituto { get; private set; }

    public bool EhTroca => Substitui is not null;

    public string TextoTroca => Substitui is { } anterior
        ? $"Entra no lugar de {anterior.NomePessoa}, que fica até a véspera do início deste."
        : string.Empty;

    /// <summary>Começa o vínculo que substitui este: mesmo papel, empresa, exclusivo e crédito; a pessoa é escolhida.</summary>
    public CarteiraFormulario IniciarTroca(OpcoesComercial? opcoes)
    {
        TextoTela.TentarData(InicioEm, out var inicioAtual);
        var inicio = inicioAtual is { } i && i >= _hoje ? i.AddDays(1) : _hoje;
        var novo = new CarteiraFormulario(new CarteiraDto
        {
            Id = IdSequencial.Novo(),
            TipoCarteiraId = Tipo.Valor ?? Guid.Empty,
            EmpresaId = ParaDto().EmpresaId,
            Exclusivo = Exclusivo,
            InicioEm = inicio
        }, gravada: false, _hoje)
        {
            Substitui = this,
            PercentualCredito = PercentualCredito
        };
        _fimAntesDaTroca = FimEm;
        Substituto = novo;
        if (opcoes is not null) novo.DefinirOpcoes(opcoes);
        novo.AjustarFimDoSubstituido();
        OnPropertyChanged(nameof(PodeTrocar));
        return novo;
    }

    /// <summary>A troca foi desfeita (o vínculo novo saiu da ficha): este volta como estava.</summary>
    public void DesfazerTroca()
    {
        if (Substituto is null) return;
        FimEm = _fimAntesDaTroca;
        Substituto = null;
        OnPropertyChanged(nameof(PodeTrocar));
    }

    partial void OnInicioEmChanged(string value) => AjustarFimDoSubstituido();

    /// <summary>O substituído fica até a véspera do início deste (acompanha a data digitada).</summary>
    private void AjustarFimDoSubstituido()
    {
        if (Substitui is not { } anterior || !TextoTela.TentarData(InicioEm, out var inicio) || inicio is not { } dia) return;
        anterior.FimEm = TextoTela.Data(dia.AddDays(-1));
    }

    // ---- Opções ----

    public void DefinirOpcoes(OpcoesComercial opcoes)
    {
        var atual = ParaDto();
        var d = opcoes.Dados;
        _papeis = d.TiposCarteira.ToDictionary(t => t.Id);
        _atendentes = d.Atendentes;
        _classificacoes = d.Classificacoes.ToDictionary(c => c.Id, c => c.Nome);
        Tipos = OpcoesColaborador.Lista(d.TiposCarteira.OrderBy(t => t.Ordem), t => t.Id,
            t => t.ResponsavelDaConta ? t.Nome + " (responsável da conta)" : t.Nome, t => t.Ativo, _gravada.TipoCarteiraId);
        Tipo = OpcoesComercial.Escolher(Tipos, atual.TipoCarteiraId == Guid.Empty ? null : atual.TipoCarteiraId);
        MontarPessoas(atual.VendedorId);
        Empresas = opcoes.Empresas(_gravada.EmpresaId);
        Empresa = OpcoesComercial.Escolher(Empresas, atual.EmpresaId);
        _diasAviso = d.DiasAvisoFimVinculo > 0 ? d.DiasAvisoFimVinculo : RegrasComercial.DiasAvisoFimPadrao;
        _coberturas = d.Coberturas;
        _carregadas = true;
        OnPropertyChanged(nameof(PertoDoFim));
        OnPropertyChanged(nameof(AvisoFim));
        OnPropertyChanged(nameof(AvisoCobertura));
        OnPropertyChanged(nameof(TemCobertura));
        OnPropertyChanged(nameof(RecebeCredito));
        OnPropertyChanged(nameof(DicaCredito));
        OnPropertyChanged(nameof(RotuloPessoa));
    }

    /// <summary>Trocar o papel muda quem pode ser escolhido (a pessoa fica se ainda puder ocupar o papel novo).</summary>
    partial void OnTipoChanged(Opcao<Guid?> value)
    {
        if (_carregadas) MontarPessoas(Vendedor.Valor ?? Guid.Empty);
    }

    /// <summary>
    /// Quem pode ocupar o papel escolhido (as classificações aceitas por ele), com a classificação ao lado do nome. A pessoa
    /// gravada que não pode mais ocupá-lo continua na lista, marcada (o período gravado fica como está).
    /// </summary>
    private void MontarPessoas(Guid escolhida)
    {
        var aceitas = Papel?.Classificacoes.ToHashSet() ?? [];
        var lista = _atendentes
            .Where(a => a.Classificacoes.Any(aceitas.Contains))
            .Select(a => new Opcao<Guid?>(a.Id, a.Nome + " · " + string.Join(", ",
                a.Classificacoes.Where(aceitas.Contains).Select(c => _classificacoes.GetValueOrDefault(c, "?")))))
            .ToList();
        if (_gravada.VendedorId != Guid.Empty && lista.All(v => v.Valor != _gravada.VendedorId) && Tipo.Valor == _gravada.TipoCarteiraId)
            lista.Add(new Opcao<Guid?>(_gravada.VendedorId, (_gravada.Vendedor ?? "(pessoa gravada)") + $" (não pode mais ser {RotuloPessoa})"));
        Vendedores = [OpcoesComercial.Nenhum, .. lista];
        Vendedor = OpcoesComercial.Escolher(Vendedores, escolhida == Guid.Empty ? null : escolhida);
    }

    public IEnumerable<string> Validar(string rotulo)
    {
        if (!Ativo) yield break;
        if (!TextoTela.TentarDecimal(PercentualCredito, out var pct) || pct is < 0 or > 100)
            yield return $"{rotulo}: crédito inválido (de 0 a 100%).";
        if (!TextoTela.TentarData(InicioEm, out var inicio) || inicio is null) yield return $"{rotulo}: informe o início (dd/mm/aaaa).";
        if (!TextoTela.TentarData(FimEm, out _)) yield return $"{rotulo}: fim inválido (use dd/mm/aaaa).";
        if (Vendedor.Valor is null) yield return $"{rotulo}: escolha quem será {RotuloPessoa}.";
        if (Tipo.Valor is null) yield return $"{rotulo}: escolha o papel.";
        if (Substitui is { } anterior && inicio is { } dia && TextoTela.TentarData(anterior.InicioEm, out var inicioAnterior) &&
            inicioAnterior is { } desde && dia <= desde)
            yield return $"{rotulo}: a troca precisa começar depois de {TextoTela.Data(desde)} (início de {anterior.NomePessoa}).";
    }

    public CarteiraDto ParaDto()
    {
        TextoTela.TentarData(InicioEm, out var inicio);
        TextoTela.TentarData(FimEm, out var fim);
        TextoTela.TentarDecimal(PercentualCredito, out var percentual);
        return new CarteiraDto
        {
            Id = Id,
            EmpresaId = _carregadas ? Empresa.Valor : _gravada.EmpresaId,
            TipoCarteiraId = Tipo.Valor ?? Guid.Empty,
            VendedorId = Vendedor.Valor ?? Guid.Empty,
            InicioEm = inicio ?? default,
            FimEm = fim,
            Exclusivo = Exclusivo,
            PercentualCredito = percentual,
            Origem = _gravada.Origem,
            Observacao = TextoTela.Nulo(Observacao)?.Trim(),
            Ativo = Ativo
        };
    }
}
