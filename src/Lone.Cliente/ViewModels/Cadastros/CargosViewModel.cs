using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.Api;
using Lone.Cliente.Plataforma;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Colaboradores;
using Lone.Contracts.Comum;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Validacao;

namespace Lone.Cliente.ViewModels.Cadastros;

/// <summary>Linha da tabela de cargos.</summary>
public sealed class LinhaCargo
{
    public LinhaCargo(CargoDto item) => Item = item;

    public CargoDto Item { get; }
    public Guid Id => Item.Id;
    public string Nome => Item.Nome;
    public string Detalhe => Item.Ocupacao ?? string.Empty;
    public string Usos => Item.QuantidadeUsos.ToString("N0", TextoTela.Brasil);
    public string Ativo => Item.Ativo ? "Sim" : "Não";
}

/// <summary>Ficha de um cargo. As regras finais (nome único, árvore) são da API.</summary>
public sealed partial class CargoEdicao : ObservableObject
{
    private CargoEdicao(Guid id, bool novo)
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
    [ObservableProperty] private string _codigoCbo = string.Empty;

    /// <summary>"0000-00 · título" da ocupação gravada (somente leitura).</summary>
    public string Ocupacao { get; private set; } = string.Empty;

    public string Titulo => string.IsNullOrWhiteSpace(Nome) ? "Novo cargo" : Nome;
    public string SituacaoTexto => Novo ? "Novo cargo" : Ativo ? "Ativo" : "Desativado (não aparece para novas escolhas; continua nas lotações que já o têm)";
    public string UsoTexto => Novo ? string.Empty : $"{QuantidadeUsos.ToString("N0", TextoTela.Brasil)} lotação(ões) em aberto com este cargo.";

    public static CargoEdicao Criar() => new(IdSequencial.Novo(), novo: true);

    public static CargoEdicao De(CargoDto t) => new(t.Id, novo: false)
    {
        Versao = t.Versao,
        Ativo = t.Ativo,
        QuantidadeUsos = t.QuantidadeUsos,
        CodigoCbo = t.OcupacaoCboId is { } cbo ? OcupacaoCbo.Formatar(cbo) : string.Empty,
        Ocupacao = t.Ocupacao ?? string.Empty,
        Nome = t.Nome
    };

    public IReadOnlyList<string> ValidarLocalmente()
    {
        var erros = new List<string>();
        if (string.IsNullOrWhiteSpace(Nome)) erros.Add("Informe o nome.");
        if (!string.IsNullOrWhiteSpace(CodigoCbo) && OcupacaoCbo.Codigo(CodigoCbo) is null)
            erros.Add("Código CBO: use 6 dígitos (ex.: 4211-25).");
        return erros;
    }

    public CargoDto ParaDto() => new()
    {
        Id = Id,
        Versao = Versao,
        Nome = Nome.Trim(),
            OcupacaoCboId = string.IsNullOrWhiteSpace(CodigoCbo) ? null : OcupacaoCbo.Codigo(CodigoCbo),
        Ativo = Ativo
    };
}

/// <summary>Cargos (estrutura organizacional). Nada é excluído: desativar esconde das escolhas novas.</summary>
public sealed partial class CargosViewModel : CadastroViewModelBase<LinhaCargo>
{
    private readonly ColaboradoresApi _api;

    public CargosViewModel(ColaboradoresApi api, IDialogos dialogos) : base(dialogos)
    {
        _api = api;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeDesativar), nameof(PodeReativar))]
    private CargoEdicao? _formulario;

    public bool PodeDesativar => Formulario is { Novo: false, Ativo: true };
    public bool PodeReativar => Formulario is { Novo: false, Ativo: false };

    protected override string TextoDeBusca(LinhaCargo item) => item.Nome + " " + item.Detalhe;

    protected override async Task<IReadOnlyList<LinhaCargo>> ListarAsync()
    {
        return (await _api.ListarAsync<CargoDto>(Rotas.Estrutura.Cargos, incluirInativos: true))
            .OrderBy(t => !t.Ativo).ThenBy(t => t.Nome, StringComparer.CurrentCultureIgnoreCase)
            .Select(t => new LinhaCargo(t)).ToList();
    }

    private CargoEdicao Preparar(CargoEdicao f)
    {
        
        return f;
    }

    protected override async Task AbrirAsync(LinhaCargo item) => Formulario = await ObterAsync(item.Id);

    protected override Task NovoItemAsync()
    {
        Formulario = Preparar(CargoEdicao.Criar());
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

    private async Task<CargoEdicao> ObterAsync(Guid id) =>
        Preparar(CargoEdicao.De(await _api.ObterAsync<CargoDto>(Rotas.Estrutura.Cargos, id) ?? throw new ValidacaoException(["Este cadastro não existe mais."])));

    [RelayCommand]
    private async Task SalvarAsync()
    {
        if (Formulario is not { } formulario) return;
        if (formulario.ValidarLocalmente() is { Count: > 0 } erros)
        {
            Mostrar(string.Join(Environment.NewLine, erros), TipoMensagem.Erro);
            return;
        }

        CargoDto? salvo = null;
        if (!await ExecutarAsync(async () => salvo = await _api.SalvarAsync(Rotas.Estrutura.Cargos, formulario.Id, formulario.ParaDto())))
            return;

        await AtualizarListaAposGravarAsync();
        Formulario = Preparar(CargoEdicao.De(salvo!));
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

        CargoDto? gravado = null;
        if (!await ExecutarAsync(async () => gravado = desativar
                ? await _api.DesativarAsync<CargoDto>(Rotas.Estrutura.Cargos, formulario.Id, formulario.Versao)
                : await _api.ReativarAsync<CargoDto>(Rotas.Estrutura.Cargos, formulario.Id, formulario.Versao)))
            return;

        await AtualizarListaAposGravarAsync();
        Formulario = Preparar(CargoEdicao.De(gravado!));
        MarcarFichaSemAlteracoes();
        Mostrar(desativar ? "Desativado." : "Reativado.", TipoMensagem.Sucesso);
    }
}
