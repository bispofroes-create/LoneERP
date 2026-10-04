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
using Lone.Cliente.Grade;

namespace Lone.Cliente.ViewModels.Cadastros;

/// <summary>Linha da tabela de departamentos.</summary>
public sealed class LinhaDepartamento
{
    public LinhaDepartamento(DepartamentoDto item) => Item = item;

    public DepartamentoDto Item { get; }
    public Guid Id => Item.Id;
    public string Nome => Item.Nome;
    public string Detalhe => string.Empty;
    public string Usos => Item.QuantidadeUsos.ToString("N0", TextoTela.Brasil);
    public string Ativo => Item.Ativo ? "Sim" : "Não";
}

/// <summary>Ficha de um departamento. As regras finais (nome único, árvore) são da API.</summary>
public sealed partial class DepartamentoEdicao : ObservableObject
{
    private DepartamentoEdicao(Guid id, bool novo)
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

    public string Titulo => string.IsNullOrWhiteSpace(Nome) ? "Novo departamento" : Nome;
    public string SituacaoTexto => Novo ? "Novo departamento" : Ativo ? "Ativo" : "Desativado (não aparece para novas escolhas; continua nas lotações que já o têm)";
    public string UsoTexto => Novo ? string.Empty : $"{QuantidadeUsos.ToString("N0", TextoTela.Brasil)} lotação(ões) em aberto com este departamento.";

    public static DepartamentoEdicao Criar() => new(IdSequencial.Novo(), novo: true);

    public static DepartamentoEdicao De(DepartamentoDto t) => new(t.Id, novo: false)
    {
        Versao = t.Versao,
        Ativo = t.Ativo,
        QuantidadeUsos = t.QuantidadeUsos,

        Nome = t.Nome
    };

    public IReadOnlyList<string> ValidarLocalmente()
    {
        var erros = new List<string>();
        if (string.IsNullOrWhiteSpace(Nome)) erros.Add("Informe o nome.");

        return erros;
    }

    public DepartamentoDto ParaDto() => new()
    {
        Id = Id,
        Versao = Versao,
        Nome = Nome.Trim(),

        Ativo = Ativo
    };
}

/// <summary>Departamentos (estrutura organizacional). Nada é excluído: desativar esconde das escolhas novas.</summary>
public sealed partial class DepartamentosViewModel : CadastroViewModelBase<LinhaDepartamento>
{
    /// <summary>Lista em colunas (padrão de tela de cadastro, 03/10/2026).</summary>
    protected override GradeCadastro<LinhaDepartamento> CriarGradeDaLista() => new(
        "Departamento", l => l.Id, l => l.Nome, l => l.Detalhe,
        ColunaCadastro<LinhaDepartamento>.Curto("emuso", "Em uso", l => l.Usos, 130),
        ColunaCadastro<LinhaDepartamento>.Situacao(l => l.Ativo == "Sim"));

    private readonly ColaboradoresApi _api;

    public DepartamentosViewModel(ColaboradoresApi api, IDialogos dialogos) : base(dialogos)
    {
        _api = api;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeDesativar), nameof(PodeReativar))]
    private DepartamentoEdicao? _formulario;

    public bool PodeDesativar => Formulario is { Novo: false, Ativo: true };
    public bool PodeReativar => Formulario is { Novo: false, Ativo: false };

    protected override string TextoDeBusca(LinhaDepartamento item) => item.Nome + " " + item.Detalhe;

    protected override async Task<IReadOnlyList<LinhaDepartamento>> ListarAsync()
    {
        return (await _api.ListarAsync<DepartamentoDto>(Rotas.Estrutura.Departamentos, incluirInativos: true))
            .OrderBy(t => !t.Ativo).ThenBy(t => t.Nome, StringComparer.CurrentCultureIgnoreCase)
            .Select(t => new LinhaDepartamento(t)).ToList();
    }

    private DepartamentoEdicao Preparar(DepartamentoEdicao f)
    {
        
        return f;
    }

    protected override async Task AbrirAsync(LinhaDepartamento item) => Formulario = await ObterAsync(item.Id);

    protected override Task NovoItemAsync()
    {
        Formulario = Preparar(DepartamentoEdicao.Criar());
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

    private async Task<DepartamentoEdicao> ObterAsync(Guid id) =>
        Preparar(DepartamentoEdicao.De(await _api.ObterAsync<DepartamentoDto>(Rotas.Estrutura.Departamentos, id) ?? throw new ValidacaoException(["Este cadastro não existe mais."])));

    [RelayCommand]
    private async Task SalvarAsync()
    {
        if (Formulario is not { } formulario) return;
        if (formulario.ValidarLocalmente() is { Count: > 0 } erros)
        {
            Mostrar(string.Join(Environment.NewLine, erros), TipoMensagem.Erro);
            return;
        }

        DepartamentoDto? salvo = null;
        if (!await ExecutarAsync(async () => salvo = await _api.SalvarAsync(Rotas.Estrutura.Departamentos, formulario.Id, formulario.ParaDto())))
            return;

        await AtualizarListaAposGravarAsync();
        Formulario = Preparar(DepartamentoEdicao.De(salvo!));
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

        DepartamentoDto? gravado = null;
        if (!await ExecutarAsync(async () => gravado = desativar
                ? await _api.DesativarAsync<DepartamentoDto>(Rotas.Estrutura.Departamentos, formulario.Id, formulario.Versao)
                : await _api.ReativarAsync<DepartamentoDto>(Rotas.Estrutura.Departamentos, formulario.Id, formulario.Versao)))
            return;

        await AtualizarListaAposGravarAsync();
        Formulario = Preparar(DepartamentoEdicao.De(gravado!));
        MarcarFichaSemAlteracoes();
        Mostrar(desativar ? "Desativado." : "Reativado.", TipoMensagem.Sucesso);
    }
}
