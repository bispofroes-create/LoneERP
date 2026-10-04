using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.Api;
using Lone.Cliente.Formularios;
using Lone.Cliente.Grade;
using Lone.Cliente.Plataforma;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Comum;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Validacao;

namespace Lone.Cliente.ViewModels.Cadastros;

/// <summary>Linha da lista de prazos de período.</summary>
public sealed class LinhaPrazoPeriodo
{
    public LinhaPrazoPeriodo(PrazoPeriodoDto item) => Item = item;

    public PrazoPeriodoDto Item { get; }
    public Guid Id => Item.Id;
    public string Nome => Item.Nome;
    public string Unidade => PrazoPeriodoEdicao.TextoUnidade(Item.Unidade);
    public int DiasAproximados => RegrasPrazoPeriodo.DiasAproximados(Item.Quantidade, Item.Unidade);
}

/// <summary>Ficha de um prazo de período: quantidade e unidade (dias, meses ou anos). Regras finais na API.</summary>
public sealed partial class PrazoPeriodoEdicao : ObservableObject
{
    public static readonly Opcao<UnidadePrazo>[] Unidades =
    [
        new(UnidadePrazo.Dias, "Dias"), new(UnidadePrazo.Meses, "Meses"), new(UnidadePrazo.Anos, "Anos")
    ];

    private PrazoPeriodoEdicao(Guid id, bool novo)
    {
        Id = id;
        Novo = novo;
    }

    public Guid Id { get; }
    public bool Novo { get; }
    public byte[]? Versao { get; private set; }
    public bool Ativo { get; private set; } = true;

    public Opcao<UnidadePrazo>[] ListaUnidades => Unidades;

    [ObservableProperty][NotifyPropertyChangedFor(nameof(Titulo), nameof(Exemplo))] private string _quantidade = string.Empty;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(Titulo), nameof(Exemplo))] private Opcao<UnidadePrazo>? _unidade = Unidades[0];

    private int? QuantidadeLida => TextoTela.TentarInteiro(Quantidade, out var n) ? n : null;

    public string Titulo => QuantidadeLida is { } n && Unidade is { } u ? RegrasPrazoPeriodo.Nome(n, u.Valor) : "Novo prazo";

    /// <summary>"Começando hoje (03/10/2026), termina em 01/11/2026." — a conta do campo Prazo, contando o início.</summary>
    public string Exemplo
    {
        get
        {
            if (QuantidadeLida is not { } n || n < 1 || Unidade is not { } u || n > RegrasPrazoPeriodo.Maximo(u.Valor)) return string.Empty;
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var fim = CalculoPrazo.Fim(hoje, PrazosProntos.Converter(new PrazoPeriodoDto { Quantidade = n, Unidade = u.Valor, Nome = Titulo }));
            return $"Exemplo: começando hoje ({TextoTela.Data(hoje)}), termina em {TextoTela.Data(fim)} (o início conta).";
        }
    }

    public string SituacaoTexto => Novo ? "Novo prazo" : Ativo ? "Ativo" : "Desativado (não aparece nos prazos prontos)";

    public static string TextoUnidade(UnidadePrazo unidade) => Unidades.FirstOrDefault(u => u.Valor == unidade)?.Texto ?? "Dias";

    public static PrazoPeriodoEdicao Criar() => new(IdSequencial.Novo(), novo: true);

    public static PrazoPeriodoEdicao De(PrazoPeriodoDto p) => new(p.Id, novo: false)
    {
        Versao = p.Versao,
        Ativo = p.Ativo,
        Quantidade = p.Quantidade.ToString(System.Globalization.CultureInfo.InvariantCulture),
        Unidade = Unidades.FirstOrDefault(u => u.Valor == p.Unidade) ?? Unidades[0]
    };

    public IReadOnlyList<string> ValidarLocalmente()
    {
        var erros = new List<string>();
        if (QuantidadeLida is not { } n || n < 1) erros.Add("Quantidade: use um número inteiro maior que zero.");
        else if (Unidade is { } u && n > RegrasPrazoPeriodo.Maximo(u.Valor))
            erros.Add($"Quantidade: no máximo {RegrasPrazoPeriodo.Maximo(u.Valor)} {u.Texto.ToLowerInvariant()}.");
        if (Unidade is null) erros.Add("Escolha a unidade.");
        return erros;
    }

    public PrazoPeriodoDto ParaDto() => new()
    {
        Id = Id,
        Versao = Versao,
        Quantidade = QuantidadeLida ?? 0,
        Unidade = Unidade?.Valor ?? UnidadePrazo.Dias,
        Ativo = Ativo
    };
}

/// <summary>
/// Prazos de período (03/10/2026; menu do usuário › Administração): os prazos prontos do campo Prazo (▾) em todas as
/// telas com período. Nada é excluído: desativar tira da lista de prazos prontos.
/// </summary>
public sealed partial class PrazosPeriodoViewModel : CadastroViewModelBase<LinhaPrazoPeriodo>
{
    protected override GradeCadastro<LinhaPrazoPeriodo> CriarGradeDaLista() => new(
        "Prazo", l => l.Id, l => l.Nome, l => null,
        // Sem coluna Unidade: "7 dias" já diz a unidade (04/10/2026); a pesquisa por "meses" continua achando.
        ColunaCadastro<LinhaPrazoPeriodo>.Situacao(l => l.Item.Ativo))
    {
        OrdemTitulo = l => l.DiasAproximados // 30 dias antes de 2 meses
    };

    private readonly PrazosPeriodoApi _api;
    private readonly PrazosProntos _prontos;

    public PrazosPeriodoViewModel(PrazosPeriodoApi api, PrazosProntos prontos, IDialogos dialogos) : base(dialogos)
    {
        _api = api;
        _prontos = prontos;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeDesativar), nameof(PodeReativar))]
    private PrazoPeriodoEdicao? _formulario;

    public bool PodeDesativar => Formulario is { Novo: false, Ativo: true };
    public bool PodeReativar => Formulario is { Novo: false, Ativo: false };

    protected override string TextoDeBusca(LinhaPrazoPeriodo item) => item.Nome + " " + item.Unidade;

    protected override async Task<IReadOnlyList<LinhaPrazoPeriodo>> ListarAsync() =>
        (await _api.ListarAsync(incluirInativos: true)).Select(p => new LinhaPrazoPeriodo(p)).ToList();

    protected override async Task AbrirAsync(LinhaPrazoPeriodo item) => Formulario = await ObterAsync(item.Id);

    protected override Task NovoItemAsync()
    {
        Formulario = PrazoPeriodoEdicao.Criar();
        return Task.CompletedTask;
    }

    protected override object? DadosDaFicha() => Formulario?.ParaDto();
    protected override object? FichaObservada => Formulario;
    protected override bool FichaNova => Formulario?.Novo ?? true;

    protected override async Task RecarregarFichaAsync()
    {
        if (Formulario is not { Novo: false } formulario) return;
        Formulario = await ObterAsync(formulario.Id);
    }

    private async Task<PrazoPeriodoEdicao> ObterAsync(Guid id) =>
        PrazoPeriodoEdicao.De(await _api.ObterAsync(id) ?? throw new ValidacaoException(["Este cadastro não existe mais."]));

    [RelayCommand]
    private async Task SalvarAsync()
    {
        if (Formulario is not { } formulario) return;
        if (formulario.ValidarLocalmente() is { Count: > 0 } erros)
        {
            Mostrar(string.Join(Environment.NewLine, erros), TipoMensagem.Erro);
            return;
        }

        PrazoPeriodoDto? salvo = null;
        if (!await ExecutarAsync(async () => salvo = await _api.SalvarAsync(formulario.ParaDto())))
            return;

        _prontos.Invalidar();
        await AtualizarListaAposGravarAsync();
        Formulario = PrazoPeriodoEdicao.De(salvo!);
        MarcarFichaSemAlteracoes();
        Mostrar(formulario.Novo ? "Prazo criado." : "Alterações salvas.", TipoMensagem.Sucesso);
    }

    [RelayCommand]
    private Task DesativarAsync() => AlterarSituacaoAsync(desativar: true);

    [RelayCommand]
    private Task ReativarAsync() => AlterarSituacaoAsync(desativar: false);

    private async Task AlterarSituacaoAsync(bool desativar)
    {
        if (Formulario is not { Novo: false } formulario) return;
        if (TemAlteracoes)
        {
            Mostrar("Salve ou descarte as alterações antes de " + (desativar ? "desativar" : "reativar") + ".", TipoMensagem.Aviso);
            return;
        }

        PrazoPeriodoDto? gravado = null;
        if (!await ExecutarAsync(async () => gravado = desativar
                ? await _api.DesativarAsync(formulario.Id, formulario.Versao)
                : await _api.ReativarAsync(formulario.Id, formulario.Versao)))
            return;

        _prontos.Invalidar();
        await AtualizarListaAposGravarAsync();
        Formulario = PrazoPeriodoEdicao.De(gravado!);
        MarcarFichaSemAlteracoes();
        Mostrar(desativar ? "Desativado." : "Reativado.", TipoMensagem.Sucesso);
    }
}
