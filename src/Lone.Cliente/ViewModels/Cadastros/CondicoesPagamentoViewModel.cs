using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.Api;
using Lone.Cliente.Plataforma;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Comercial;
using Lone.Contracts.Comum;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Validacao;

namespace Lone.Cliente.ViewModels.Cadastros;

/// <summary>Linha da tabela de condições de pagamento.</summary>
public sealed class LinhaCondicaoPagamento
{
    public LinhaCondicaoPagamento(CondicaoPagamentoDto item) => Item = item;

    public CondicaoPagamentoDto Item { get; }
    public Guid Id => Item.Id;
    public string Nome => Item.Nome;
    public string Detalhe => $"{Item.Parcelas} · prazo médio {Item.PrazoMedio:0.#} dia(s)";
    public string Usos => Item.QuantidadeUsos.ToString("N0", TextoTela.Brasil);
    public string Ativo => Item.Ativo ? "Sim" : "Não";
}

/// <summary>Ficha de um condição de pagamento. As regras finais (nome único, árvore) são da API.</summary>
public sealed partial class CondicaoPagamentoEdicao : ObservableObject
{
    private CondicaoPagamentoEdicao(Guid id, bool novo)
    {
        Id = id;
        Novo = novo;
    }

    public Guid Id { get; }
    public bool Novo { get; }
    public byte[]? Versao { get; private set; }
    public bool Ativo { get; private set; } = true;
    public int QuantidadeUsos { get; private set; }

    [ObservableProperty][NotifyPropertyChangedFor(nameof(Titulo))] private string _nome = string.Empty;
    [ObservableProperty] private string _parcelas = "0";
    [ObservableProperty] private string _acrescimo = string.Empty;
    public string PrazoMedio { get; private set; } = string.Empty;

    public string Titulo => string.IsNullOrWhiteSpace(Nome) ? "Novo condição de pagamento" : Nome;
    public string SituacaoTexto => Novo ? "Novo condição de pagamento" : Ativo ? "Ativo" : "Desativado (não aparece para novas escolhas; continua onde já está)";
    public string UsoTexto => Novo ? string.Empty : $"{QuantidadeUsos.ToString("N0", TextoTela.Brasil)} conta(s) de cliente com esta condição.";

    public static CondicaoPagamentoEdicao Criar() => new(IdSequencial.Novo(), novo: true);

    public static CondicaoPagamentoEdicao De(CondicaoPagamentoDto t) => new(t.Id, novo: false)
    {
        Versao = t.Versao,
        Ativo = t.Ativo,
        QuantidadeUsos = t.QuantidadeUsos,
        Parcelas = t.Parcelas,
        Acrescimo = TextoTela.Decimal(t.AcrescimoPercentual),
        PrazoMedio = $"Prazo médio: {t.PrazoMedio:0.#} dia(s).",
        Nome = t.Nome
    };

    public IReadOnlyList<string> ValidarLocalmente()
    {
        var erros = new List<string>();
        if (string.IsNullOrWhiteSpace(Nome)) erros.Add("Informe o nome.");
        if (string.IsNullOrWhiteSpace(Parcelas)) erros.Add("Informe as parcelas em dias (ex.: 0/30/60; 0 = à vista).");
        if (!TextoTela.TentarDecimal(Acrescimo, out _)) erros.Add("Acréscimo/desconto inválido.");
        return erros;
    }

    public CondicaoPagamentoDto ParaDto() => new()
    {
        Id = Id,
        Versao = Versao,
        Nome = Nome.Trim(),
        Parcelas = Parcelas.Trim(),
        AcrescimoPercentual = TextoTela.TentarDecimal(Acrescimo, out var acrescimo) ? acrescimo : null,
        Ativo = Ativo
    };
}

/// <summary>Condições de pagamento (cadastro comercial). Nada é excluído: desativar esconde das escolhas novas.</summary>
public sealed partial class CondicoesPagamentoViewModel : CadastroViewModelBase<LinhaCondicaoPagamento>
{
    private readonly ComercialApi _api;

    public CondicoesPagamentoViewModel(ComercialApi api, IDialogos dialogos) : base(dialogos)
    {
        _api = api;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeDesativar), nameof(PodeReativar))]
    private CondicaoPagamentoEdicao? _formulario;

    public bool PodeDesativar => Formulario is { Novo: false, Ativo: true };
    public bool PodeReativar => Formulario is { Novo: false, Ativo: false };

    protected override string TextoDeBusca(LinhaCondicaoPagamento item) => item.Nome + " " + item.Detalhe;

    protected override async Task<IReadOnlyList<LinhaCondicaoPagamento>> ListarAsync()
    {
        return (await _api.ListarAsync<CondicaoPagamentoDto>(Rotas.Comercial.Condicoes, incluirInativos: true))
            .OrderBy(t => !t.Ativo).ThenBy(t => t.Nome, StringComparer.CurrentCultureIgnoreCase)
            .Select(t => new LinhaCondicaoPagamento(t)).ToList();
    }

    private CondicaoPagamentoEdicao Preparar(CondicaoPagamentoEdicao f)
    {
        
        return f;
    }

    protected override async Task AbrirAsync(LinhaCondicaoPagamento item) => Formulario = await ObterAsync(item.Id);

    protected override Task NovoItemAsync()
    {
        Formulario = Preparar(CondicaoPagamentoEdicao.Criar());
        return Task.CompletedTask;
    }

    protected override object? DadosDaFicha() => Formulario?.ParaDto();
    protected override bool FichaNova => Formulario?.Novo ?? true;

    protected override async Task RecarregarFichaAsync()
    {
        if (Formulario is not { Novo: false } formulario) return;
        Formulario = await ObterAsync(formulario.Id);
    }

    private async Task<CondicaoPagamentoEdicao> ObterAsync(Guid id) =>
        Preparar(CondicaoPagamentoEdicao.De(await _api.ObterAsync<CondicaoPagamentoDto>(Rotas.Comercial.Condicoes, id) ?? throw new ValidacaoException(["Este cadastro não existe mais."])));

    [RelayCommand]
    private async Task SalvarAsync()
    {
        if (Formulario is not { } formulario) return;
        if (formulario.ValidarLocalmente() is { Count: > 0 } erros)
        {
            Mostrar(string.Join(Environment.NewLine, erros), TipoMensagem.Erro);
            return;
        }

        CondicaoPagamentoDto? salvo = null;
        if (!await ExecutarAsync(async () => salvo = await _api.SalvarAsync(Rotas.Comercial.Condicoes, formulario.Id, formulario.ParaDto())))
            return;

        await AtualizarListaAposGravarAsync();
        Formulario = Preparar(CondicaoPagamentoEdicao.De(salvo!));
        MarcarFichaSemAlteracoes();
        Mostrar(formulario.Novo ? "Cadastro criado." : "Alterações salvas.", TipoMensagem.Sucesso);
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

        CondicaoPagamentoDto? gravado = null;
        if (!await ExecutarAsync(async () => gravado = desativar
                ? await _api.DesativarAsync<CondicaoPagamentoDto>(Rotas.Comercial.Condicoes, formulario.Id, formulario.Versao)
                : await _api.ReativarAsync<CondicaoPagamentoDto>(Rotas.Comercial.Condicoes, formulario.Id, formulario.Versao)))
            return;

        await AtualizarListaAposGravarAsync();
        Formulario = Preparar(CondicaoPagamentoEdicao.De(gravado!));
        MarcarFichaSemAlteracoes();
        Mostrar(desativar ? "Desativado." : "Reativado.", TipoMensagem.Sucesso);
    }
}
