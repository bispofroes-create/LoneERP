using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.Api;
using Lone.Cliente.Plataforma;
using Lone.Cliente.ViewModels.Cadastros;
using Lone.Cliente.ViewModels.Comum;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Comercial;
using Lone.Domain.Comum;
using Lone.Domain.Enums;
using Lone.Domain.Validacao;

namespace Lone.Cliente.ViewModels.Comercial;

/// <summary>Textos das regras de crédito na ausência (parâmetros e coberturas).</summary>
public static class TextosCobertura
{
    public static readonly Opcao<RegraCreditoAusencia>[] Creditos =
    [
        new(RegraCreditoAusencia.Titular, "Do titular (a carteira é dele)"),
        new(RegraCreditoAusencia.Substituto, "De quem cobre"),
        new(RegraCreditoAusencia.Dividido, "Dividido (% para quem cobre)")
    ];

    public static string Situacao(SituacaoCobertura s) => s switch
    {
        SituacaoCobertura.Agendada => "Agendada",
        SituacaoCobertura.Vigente => "Vigente",
        SituacaoCobertura.Encerrada => "Encerrada",
        _ => "Cancelada"
    };

    public static string Periodo(DateOnly inicio, DateOnly fim) => $"{TextoTela.Data(inicio)} a {TextoTela.Data(fim)}";
}

// ======================================================================= Parâmetros

/// <summary>Parâmetros do módulo Comercial: antecedência do aviso de fim do vínculo e crédito padrão nas ausências.</summary>
public sealed partial class ParametrosComerciaisViewModel : ViewModelBase
{
    private readonly ComercialApi _api;
    private byte[]? _versao;

    public ParametrosComerciaisViewModel(ComercialApi api) => _api = api;

    [ObservableProperty] private string _diasAviso = "30";

    /// <summary>Até quantos dias no passado valem o efeito de uma transferência e o início de uma cobertura (0 = só hoje em diante).</summary>
    [ObservableProperty] private string _diasRetroativos = "30";
    [ObservableProperty][NotifyPropertyChangedFor(nameof(Dividido))] private Opcao<RegraCreditoAusencia> _credito = TextosCobertura.Creditos[0];
    [ObservableProperty] private string _percentualSubstituto = string.Empty;

    public IReadOnlyList<Opcao<RegraCreditoAusencia>> Creditos => TextosCobertura.Creditos;
    public bool Dividido => Credito.Valor == RegraCreditoAusencia.Dividido;

    [RelayCommand]
    private Task CarregarAsync() => ExecutarAsync(async () => Aplicar(await _api.ObterParametrosAsync()));

    [RelayCommand]
    private async Task SalvarAsync()
    {
        var erros = new List<string>();
        if (!TextoTela.TentarInteiro(DiasAviso, out var dias) || dias is null) erros.Add("Aviso de fim do vínculo: informe os dias.");
        if (!TextoTela.TentarInteiro(DiasRetroativos, out var retroativos) || retroativos is null)
            erros.Add("Datas no passado: informe os dias (0 = só hoje ou datas futuras).");
        if (!TextoTela.TentarDecimal(PercentualSubstituto, out var pct)) erros.Add("Percentual de quem cobre: número inválido.");
        if (erros.Count > 0)
        {
            Mostrar(string.Join(Environment.NewLine, erros), TipoMensagem.Erro);
            return;
        }

        ParametrosComerciaisDto? salvo = null;
        if (!await ExecutarAsync(async () => salvo = await _api.SalvarParametrosAsync(new ParametrosComerciaisDto
            {
                Versao = _versao,
                DiasAvisoFimVinculo = dias ?? 30,
                DiasRetroativosMaximo = retroativos ?? 30,
                CreditoNaAusencia = Credito.Valor,
                PercentualSubstitutoPadrao = Dividido ? pct : null
            })))
            return;
        Aplicar(salvo!);
        Mostrar("Parâmetros salvos.", TipoMensagem.Sucesso);
    }

    private void Aplicar(ParametrosComerciaisDto p)
    {
        _versao = p.Versao;
        DiasAviso = TextoTela.Inteiro(p.DiasAvisoFimVinculo);
        DiasRetroativos = TextoTela.Inteiro(p.DiasRetroativosMaximo);
        Credito = Opcao.De(TextosCobertura.Creditos, p.CreditoNaAusencia);
        PercentualSubstituto = TextoTela.Decimal(p.PercentualSubstitutoPadrao);
    }
}

// ======================================================================= Carteira vencendo

/// <summary>Linha de "Carteira vencendo".</summary>
public sealed class LinhaVencendo
{
    public LinhaVencendo(VinculoVencendoDto v) => Item = v;

    public VinculoVencendoDto Item { get; }
    public string Cliente => Item.Cliente;
    public string Quem => $"{Item.Papel}: {Item.Pessoa}" + (Item.Empresa is { } e ? $" · {e}" : string.Empty);
    public string Fim => TextoTela.Data(Item.FimEm);
    public string Faltam => Item.DiasRestantes switch
    {
        0 => "termina hoje",
        1 => "falta 1 dia",
        var n => $"faltam {n} dias"
    };
}

/// <summary>
/// Vínculos da carteira que terminam dentro da antecedência de aviso (parâmetros do Comercial) ou do prazo escolhido:
/// para renovar, trocar ou deixar encerrar a tempo.
/// </summary>
public sealed partial class CarteiraVencendoViewModel : ViewModelBase
{
    private readonly ComercialApi _api;

    public CarteiraVencendoViewModel(ComercialApi api) => _api = api;

    public ObservableCollection<LinhaVencendo> Itens { get; } = new();
    public bool Vazia => Itens.Count == 0;

    /// <summary>Vazio = a antecedência dos parâmetros.</summary>
    [ObservableProperty] private string _dias = string.Empty;
    [ObservableProperty] private string _resumo = string.Empty;

    [RelayCommand]
    private async Task CarregarAsync()
    {
        if (!TextoTela.TentarInteiro(Dias, out var dias))
        {
            Mostrar("Dias: use um número inteiro (vazio = o aviso configurado).", TipoMensagem.Erro);
            return;
        }
        List<VinculoVencendoDto>? lista = null;
        if (!await ExecutarAsync(async () => lista = await _api.CarteiraVencendoAsync(dias))) return;
        Itens.Clear();
        foreach (var v in lista!) Itens.Add(new LinhaVencendo(v));
        Resumo = Itens.Count == 0 ? "Nenhum vínculo termina no prazo." : $"{Itens.Count} vínculo(s) terminam no prazo, do mais próximo ao mais distante.";
        OnPropertyChanged(nameof(Vazia));
    }
}

// ======================================================================= Coberturas

/// <summary>Linha da lista de coberturas.</summary>
public sealed class LinhaCobertura
{
    public LinhaCobertura(CoberturaDto c) => Item = c;

    public CoberturaDto Item { get; }
    public Guid Id => Item.Id;
    public string Titular => Item.Titular ?? "?";
    public string Detalhe => $"{Item.TipoAusencia} · {TextosCobertura.Periodo(Item.InicioEm, Item.FimEm)} · por {Item.QuemCobre}";
    public string Situacao => TextosCobertura.Situacao(Item.Situacao);
    public string Clientes => Item.ClientesAfetados == 1 ? "1 cliente" : $"{Item.ClientesAfetados} clientes";
}

/// <summary>
/// Ficha de uma cobertura. Antes de começar, tudo muda (e pode ser cancelada); depois, só fim, observação e acesso
/// (a API confere as mesmas regras: RegrasCobertura).
/// </summary>
public sealed partial class CoberturaEdicao : ObservableObject
{
    public static readonly Opcao<Guid?> Todos = new(null, "Todos os papéis");
    public static readonly Opcao<Guid?> Todas = new(null, "Todas as empresas");
    public static readonly Opcao<Guid?> Nenhum = new(null, "—");
    public static readonly Opcao<bool>[] Modos = [new(false, "Uma pessoa"), new(true, "Uma equipe")];

    private readonly CoberturaDto _gravada;
    private readonly DateOnly _hoje;
    private CoberturaOpcoesDto _opcoes = new();

    private CoberturaEdicao(CoberturaDto d, bool novo, DateOnly hoje)
    {
        _gravada = d;
        _hoje = hoje;
        Novo = novo;
        Id = d.Id;
        _inicioEm = TextoTela.Data(d.InicioEm == default ? null : d.InicioEm);
        _fimEm = TextoTela.Data(d.FimEm == default ? null : d.FimEm);
        _porEquipe = Modos[d.EquipeSubstitutaId is null ? 0 : 1];
        _credito = Opcao.De(TextosCobertura.Creditos, d.RegraCredito);
        _percentualSubstituto = TextoTela.Decimal(d.PercentualSubstituto);
        _permiteAcesso = d.PermiteAcesso;
        _observacao = d.Observacao ?? string.Empty;
    }

    public Guid Id { get; }
    public bool Novo { get; }
    public byte[]? Versao => _gravada.Versao;
    public bool Cancelada => _gravada.Cancelada;

    /// <summary>Gravada e já começou: titular, tipo, início, quem cobre, escopo e crédito ficam travados.</summary>
    public bool Travado => !Novo && (_gravada.Cancelada || _gravada.InicioEm <= _hoje);
    public bool Editavel => !Travado;
    public bool PodeCancelar => !Novo && !_gravada.Cancelada && _gravada.InicioEm > _hoje;
    public bool PodeEncerrarHoje => !Novo && !_gravada.Cancelada && _gravada.InicioEm <= _hoje && _gravada.FimEm >= _hoje;

    public string Titulo => Novo ? "Nova cobertura" : $"Cobertura de {_gravada.Titular}";
    public string SituacaoTexto => Novo ? "Nova cobertura"
        : _gravada.Cancelada ? $"Cancelada: {_gravada.MotivoCancelamento}"
        : $"{TextosCobertura.Situacao(_gravada.Situacao)} · {(_gravada.ClientesAfetados == 1 ? "1 cliente" : $"{_gravada.ClientesAfetados} clientes")} na carteira do titular, no escopo";

    [ObservableProperty] private Opcao<Guid?>[] _pessoas = [Nenhum];
    [ObservableProperty] private Opcao<Guid?> _titular = Nenhum;
    [ObservableProperty] private Opcao<Guid?>[] _tipos = [Nenhum];
    [ObservableProperty] private Opcao<Guid?> _tipo = Nenhum;
    [ObservableProperty] private string _inicioEm;
    [ObservableProperty] private string _fimEm;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(CobrePessoa))] private Opcao<bool> _porEquipe;
    [ObservableProperty] private Opcao<Guid?>[] _substitutos = [Nenhum];
    [ObservableProperty] private Opcao<Guid?> _substituto = Nenhum;
    [ObservableProperty] private Opcao<Guid?>[] _equipes = [Nenhum];
    [ObservableProperty] private Opcao<Guid?> _equipe = Nenhum;
    [ObservableProperty] private Opcao<Guid?>[] _papeis = [Todos];
    [ObservableProperty] private Opcao<Guid?> _papel = Todos;
    [ObservableProperty] private Opcao<Guid?>[] _empresas = [Todas];
    [ObservableProperty] private Opcao<Guid?> _empresa = Todas;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(Dividido))] private Opcao<RegraCreditoAusencia> _credito;
    [ObservableProperty] private string _percentualSubstituto;
    [ObservableProperty] private bool _permiteAcesso;
    [ObservableProperty] private string _observacao;

    public IReadOnlyList<Opcao<bool>> ListaModos => Modos;
    public IReadOnlyList<Opcao<RegraCreditoAusencia>> Creditos => TextosCobertura.Creditos;
    public bool CobrePessoa => !PorEquipe.Valor;
    public bool Dividido => Credito.Valor == RegraCreditoAusencia.Dividido;

    public static CoberturaEdicao Criar(DateOnly hoje, ParametrosComerciaisDto parametros) => new(new CoberturaDto
    {
        Id = IdSequencial.Novo(),
        InicioEm = hoje,
        RegraCredito = parametros.CreditoNaAusencia,
        PercentualSubstituto = parametros.PercentualSubstitutoPadrao,
        PermiteAcesso = true
    }, novo: true, hoje);

    public static CoberturaEdicao De(CoberturaDto d, DateOnly hoje) => new(d, novo: false, hoje);

    /// <summary>Monta as listas (ativos, mais o gravado se estiver desativado) e escolhe o que está gravado.</summary>
    public void DefinirOpcoes(CoberturaOpcoesDto opcoes)
    {
        _opcoes = opcoes;
        Pessoas = Lista(opcoes.Pessoas.Select(p => (p.Id, p.Nome)), _gravada.TitularId, _gravada.Titular);
        Titular = OpcoesComercial.Escolher(Pessoas, _gravada.TitularId == Guid.Empty ? null : _gravada.TitularId);
        Tipos = Lista(opcoes.TiposAusencia.Where(t => t.Ativo || t.Id == _gravada.TipoAusenciaId).Select(t => (t.Id, t.Ativo ? t.Nome : t.Nome + " (desativado)")),
            _gravada.TipoAusenciaId, _gravada.TipoAusencia);
        Tipo = OpcoesComercial.Escolher(Tipos, _gravada.TipoAusenciaId == Guid.Empty ? (opcoes.TiposAusencia.FirstOrDefault(t => t.Ativo)?.Id) : _gravada.TipoAusenciaId);
        Papeis = [Todos, .. opcoes.Papeis.Where(p => p.Ativo || p.Id == _gravada.TipoCarteiraId).Select(p => new Opcao<Guid?>(p.Id, p.Ativo ? p.Nome : p.Nome + " (desativado)"))];
        Papel = OpcoesComercial.Escolher(Papeis, _gravada.TipoCarteiraId);
        Empresas = [Todas, .. opcoes.Empresas.Where(e => e.Ativa || e.Id == _gravada.EmpresaId).Select(e => new Opcao<Guid?>(e.Id, e.Nome))];
        Empresa = OpcoesComercial.Escolher(Empresas, _gravada.EmpresaId);
        Equipes = Lista(opcoes.Equipes.Select(e => (e.Id, e.Nome)), _gravada.EquipeSubstitutaId ?? Guid.Empty,
            _gravada.EquipeSubstitutaId is null ? null : _gravada.QuemCobre);
        Equipe = OpcoesComercial.Escolher(Equipes, _gravada.EquipeSubstitutaId);
        MontarSubstitutos(_gravada.SubstitutoId);
    }

    partial void OnPapelChanged(Opcao<Guid?> value) => MontarSubstitutos(Substituto.Valor);

    partial void OnTitularChanged(Opcao<Guid?> value) => MontarSubstitutos(Substituto.Valor);

    /// <summary>Quem pode cobrir: quem pode ocupar o papel da cobertura (ou algum papel, quando vale para todos).</summary>
    private void MontarSubstitutos(Guid? escolhido)
    {
        var aceitas = (Papel.Valor is { } id && _opcoes.Papeis.FirstOrDefault(p => p.Id == id) is { } papel
            ? papel.Classificacoes
            : _opcoes.Papeis.Where(p => p.Ativo).SelectMany(p => p.Classificacoes)).ToHashSet();
        var lista = _opcoes.Pessoas.Where(p => p.Id != Titular.Valor && p.Classificacoes.Any(aceitas.Contains)).Select(p => (p.Id, p.Nome));
        Substitutos = Lista(lista, _gravada.SubstitutoId ?? Guid.Empty, _gravada.SubstitutoId is null ? null : _gravada.QuemCobre);
        Substituto = OpcoesComercial.Escolher(Substitutos, escolhido);
    }

    private static Opcao<Guid?>[] Lista(IEnumerable<(Guid Id, string Nome)> itens, Guid gravado, string? nomeGravado)
    {
        var lista = itens.Select(i => new Opcao<Guid?>(i.Id, i.Nome)).ToList();
        if (gravado != Guid.Empty && lista.All(o => o.Valor != gravado))
            lista.Add(new Opcao<Guid?>(gravado, (nomeGravado ?? "(gravado)") + " (fora da lista)"));
        return [Nenhum, .. lista];
    }

    public IReadOnlyList<string> ValidarLocalmente()
    {
        var erros = new List<string>();
        if (Titular.Valor is null) erros.Add("Escolha quem vai se ausentar.");
        if (Tipo.Valor is null) erros.Add("Escolha o tipo de ausência.");
        if (!TextoTela.TentarData(InicioEm, out var inicio) || inicio is null) erros.Add("Início: use dd/mm/aaaa.");
        if (!TextoTela.TentarData(FimEm, out var fim) || fim is null) erros.Add("Fim: use dd/mm/aaaa (obrigatório).");
        if (CobrePessoa && Substituto.Valor is null) erros.Add("Escolha quem cobre.");
        if (!CobrePessoa && Equipe.Valor is null) erros.Add("Escolha a equipe que cobre.");
        if (!TextoTela.TentarDecimal(PercentualSubstituto, out _)) erros.Add("Percentual de quem cobre: número inválido.");
        return erros;
    }

    public CoberturaDto ParaDto()
    {
        TextoTela.TentarData(InicioEm, out var inicio);
        TextoTela.TentarData(FimEm, out var fim);
        TextoTela.TentarDecimal(PercentualSubstituto, out var pct);
        return new CoberturaDto
        {
            Id = Id,
            Versao = _gravada.Versao,
            TitularId = Titular.Valor ?? Guid.Empty,
            TipoAusenciaId = Tipo.Valor ?? Guid.Empty,
            InicioEm = inicio ?? default,
            FimEm = fim ?? default,
            SubstitutoId = CobrePessoa ? Substituto.Valor : null,
            EquipeSubstitutaId = CobrePessoa ? null : Equipe.Valor,
            TipoCarteiraId = Papel.Valor,
            EmpresaId = Empresa.Valor,
            RegraCredito = Credito.Valor,
            PercentualSubstituto = Dividido ? pct : null,
            PermiteAcesso = PermiteAcesso,
            Observacao = TextoTela.Nulo(Observacao)?.Trim(),
            Cancelada = _gravada.Cancelada
        };
    }
}

/// <summary>
/// Ausências e coberturas (Motor Comercial, Fase 1c): quem cobre a carteira de quem sai de férias, licença... A carteira
/// não muda; a cobertura vale pelas datas e acaba sozinha. Nada é apagado: cancela (antes de começar) ou encerra.
/// </summary>
public sealed partial class CoberturasViewModel : CadastroViewModelBase<LinhaCobertura>
{
    private readonly ComercialApi _api;
    private CoberturaOpcoesDto _opcoes = new();

    public CoberturasViewModel(ComercialApi api, IDialogos dialogos) : base(dialogos) => _api = api;

    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.Today);

    [ObservableProperty] private CoberturaEdicao? _formulario;

    /// <summary>Mostra também as encerradas e as canceladas (histórico).</summary>
    [ObservableProperty] private bool _incluirEncerradas;

    partial void OnIncluirEncerradasChanged(bool value) => _ = RecarregarAsync();

    protected override string TextoDeBusca(LinhaCobertura item) => item.Titular + " " + item.Detalhe;

    protected override async Task AntesDeListarAsync() => _opcoes = await _api.ListarOpcoesCoberturaAsync();

    protected override async Task<IReadOnlyList<LinhaCobertura>> ListarAsync() =>
        [.. (await _api.ListarCoberturasAsync(IncluirEncerradas)).Select(c => new LinhaCobertura(c))];

    private CoberturaEdicao Preparar(CoberturaEdicao f)
    {
        f.DefinirOpcoes(_opcoes);
        return f;
    }

    protected override async Task AbrirAsync(LinhaCobertura item) => Formulario = await ObterAsync(item.Id);

    protected override Task NovoItemAsync()
    {
        Formulario = Preparar(CoberturaEdicao.Criar(Hoje, _opcoes.Parametros));
        return Task.CompletedTask;
    }

    protected override object? DadosDaFicha() => Formulario?.ParaDto();
    protected override bool FichaNova => Formulario?.Novo ?? true;

    protected override async Task RecarregarFichaAsync()
    {
        if (Formulario is not { Novo: false } formulario) return;
        Formulario = await ObterAsync(formulario.Id);
    }

    private async Task<CoberturaEdicao> ObterAsync(Guid id) =>
        Preparar(CoberturaEdicao.De(await _api.ObterCoberturaAsync(id) ?? throw new ValidacaoException(["Esta cobertura não existe mais."]), Hoje));

    [RelayCommand]
    private async Task SalvarAsync()
    {
        if (Formulario is not { } formulario) return;
        if (formulario.ValidarLocalmente() is { Count: > 0 } erros)
        {
            Mostrar(string.Join(Environment.NewLine, erros), TipoMensagem.Erro);
            return;
        }
        await GravarAsync(formulario.ParaDto(), formulario.Novo ? "Cobertura cadastrada." : "Alterações salvas.");
    }

    /// <summary>Encerra hoje a cobertura que já começou (o fim passa a ser hoje).</summary>
    [RelayCommand]
    private async Task EncerrarHojeAsync()
    {
        if (Formulario is not { PodeEncerrarHoje: true } formulario) return;
        if (TemAlteracoes)
        {
            Mostrar("Salve ou descarte as alterações antes de encerrar.", TipoMensagem.Aviso);
            return;
        }
        if (!await ConfirmarAsync("Encerrar cobertura", $"A cobertura passa a terminar hoje ({TextoTela.Data(Hoje)}). O período em que valeu fica no histórico.",
                "Encerrar hoje", "Voltar"))
            return;
        var dto = formulario.ParaDto();
        dto.FimEm = Hoje;
        await GravarAsync(dto, "Cobertura encerrada hoje.");
    }

    /// <summary>Cancela a cobertura que ainda não começou (pede o motivo; fica no histórico).</summary>
    [RelayCommand]
    private async Task CancelarCoberturaAsync()
    {
        if (Formulario is not { PodeCancelar: true } formulario) return;
        if (TemAlteracoes)
        {
            Mostrar("Salve ou descarte as alterações antes de cancelar.", TipoMensagem.Aviso);
            return;
        }
        var motivo = await PerguntarAsync("Cancelar cobertura", "Por que a cobertura foi cancelada? (fica no histórico)", "Cancelar cobertura", "Voltar",
            dica: "Ex.: férias adiadas", tamanhoMaximo: 250);
        if (string.IsNullOrWhiteSpace(motivo)) return;

        CoberturaDto? gravada = null;
        if (!await ExecutarAsync(async () => gravada = await _api.CancelarCoberturaAsync(formulario.Id, formulario.Versao, motivo))) return;
        await AtualizarListaAposGravarAsync();
        Formulario = Preparar(CoberturaEdicao.De(gravada!, Hoje));
        MarcarFichaSemAlteracoes();
        Mostrar("Cobertura cancelada.", TipoMensagem.Sucesso);
    }

    private async Task GravarAsync(CoberturaDto dto, string sucesso)
    {
        CoberturaDto? salvo = null;
        if (!await ExecutarAsync(async () => salvo = await _api.SalvarCoberturaAsync(dto))) return;
        await AtualizarListaAposGravarAsync();
        Formulario = Preparar(CoberturaEdicao.De(salvo!, Hoje));
        MarcarFichaSemAlteracoes();
        Mostrar(sucesso, TipoMensagem.Sucesso);
    }
}
