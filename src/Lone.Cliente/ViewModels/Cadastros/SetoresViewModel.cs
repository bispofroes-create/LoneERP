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

/// <summary>Linha da tabela de setores.</summary>
public sealed class LinhaSetor
{
    public LinhaSetor(SetorDto item) => Item = item;

    public SetorDto Item { get; }
    public Guid Id => Item.Id;
    public string Nome => Item.Nome;
    public string Detalhe => Item.Departamento ?? string.Empty;
    public string Usos => Item.QuantidadeUsos.ToString("N0", TextoTela.Brasil);
    public string Ativo => Item.Ativo ? "Sim" : "Não";
}

/// <summary>Ficha de um setor. As regras finais (nome único, árvore) são da API.</summary>
public sealed partial class SetorEdicao : ObservableObject
{
    private SetorEdicao(Guid id, bool novo)
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
    [ObservableProperty] private Opcao<Guid>[] _departamentos = [SemDepartamento];
    [ObservableProperty] private Opcao<Guid> _departamento = SemDepartamento;
    public static readonly Opcao<Guid> SemDepartamento = new(Guid.Empty, "—");
    private Guid _departamentoGravado;

    public void DefinirDepartamentos(IReadOnlyList<DepartamentoDto> lista)
    {
        var atual = Departamento.Valor == Guid.Empty ? _departamentoGravado : Departamento.Valor;
        Departamentos = [SemDepartamento, .. lista.Where(d => d.Ativo || d.Id == _departamentoGravado)
            .OrderBy(d => d.Nome, StringComparer.CurrentCultureIgnoreCase)
            .Select(d => new Opcao<Guid>(d.Id, d.Ativo ? d.Nome : d.Nome + " (desativado)"))];
        Departamento = Departamentos.FirstOrDefault(d => d.Valor == atual) ?? Departamentos[0];
    }

    public string Titulo => string.IsNullOrWhiteSpace(Nome) ? "Novo setor" : Nome;
    public string SituacaoTexto => Novo ? "Novo setor" : Ativo ? "Ativo" : "Desativado (não aparece para novas escolhas; continua nas lotações que já o têm)";
    public string UsoTexto => Novo ? string.Empty : $"{QuantidadeUsos.ToString("N0", TextoTela.Brasil)} lotação(ões) em aberto com este setor.";

    public static SetorEdicao Criar() => new(IdSequencial.Novo(), novo: true);

    public static SetorEdicao De(SetorDto t) => new(t.Id, novo: false)
    {
        Versao = t.Versao,
        Ativo = t.Ativo,
        QuantidadeUsos = t.QuantidadeUsos,
        _departamentoGravado = t.DepartamentoId,
        Nome = t.Nome
    };

    public IReadOnlyList<string> ValidarLocalmente()
    {
        var erros = new List<string>();
        if (string.IsNullOrWhiteSpace(Nome)) erros.Add("Informe o nome.");
        if (Departamento.Valor == Guid.Empty && _departamentoGravado == Guid.Empty) erros.Add("Escolha o departamento.");
        return erros;
    }

    public SetorDto ParaDto() => new()
    {
        Id = Id,
        Versao = Versao,
        Nome = Nome.Trim(),
            DepartamentoId = Departamento.Valor == Guid.Empty ? _departamentoGravado : Departamento.Valor,
        Ativo = Ativo
    };
}

/// <summary>Setores (estrutura organizacional). Nada é excluído: desativar esconde das escolhas novas.</summary>
public sealed partial class SetoresViewModel : CadastroViewModelBase<LinhaSetor>
{
    private readonly ColaboradoresApi _api;
    private List<DepartamentoDto> _departamentos = [];

    public SetoresViewModel(ColaboradoresApi api, IDialogos dialogos) : base(dialogos)
    {
        _api = api;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeDesativar), nameof(PodeReativar))]
    private SetorEdicao? _formulario;

    public bool PodeDesativar => Formulario is { Novo: false, Ativo: true };
    public bool PodeReativar => Formulario is { Novo: false, Ativo: false };

    protected override string TextoDeBusca(LinhaSetor item) => item.Nome + " " + item.Detalhe;

    protected override async Task<IReadOnlyList<LinhaSetor>> ListarAsync()
    {
        _departamentos = await _api.ListarAsync<DepartamentoDto>(Rotas.Estrutura.Departamentos, incluirInativos: true);
        return (await _api.ListarAsync<SetorDto>(Rotas.Estrutura.Setores, incluirInativos: true))
            .OrderBy(t => !t.Ativo).ThenBy(t => t.Nome, StringComparer.CurrentCultureIgnoreCase)
            .Select(t => new LinhaSetor(t)).ToList();
    }

    private SetorEdicao Preparar(SetorEdicao f)
    {
        f.DefinirDepartamentos(_departamentos);
        return f;
    }

    protected override async Task AbrirAsync(LinhaSetor item) => Formulario = await ObterAsync(item.Id);

    protected override Task NovoItemAsync()
    {
        Formulario = Preparar(SetorEdicao.Criar());
        return Task.CompletedTask;
    }

    protected override object? DadosDaFicha() => Formulario?.ParaDto();
    protected override bool FichaNova => Formulario?.Novo ?? true;

    protected override async Task RecarregarFichaAsync()
    {
        if (Formulario is not { Novo: false } formulario) return;
        Formulario = await ObterAsync(formulario.Id);
    }

    private async Task<SetorEdicao> ObterAsync(Guid id) =>
        Preparar(SetorEdicao.De(await _api.ObterAsync<SetorDto>(Rotas.Estrutura.Setores, id) ?? throw new ValidacaoException(["Este cadastro não existe mais."])));

    [RelayCommand]
    private async Task SalvarAsync()
    {
        if (Formulario is not { } formulario) return;
        if (formulario.ValidarLocalmente() is { Count: > 0 } erros)
        {
            Mostrar(string.Join(Environment.NewLine, erros), TipoMensagem.Erro);
            return;
        }

        SetorDto? salvo = null;
        if (!await ExecutarAsync(async () => salvo = await _api.SalvarAsync(Rotas.Estrutura.Setores, formulario.Id, formulario.ParaDto())))
            return;

        await AtualizarListaAposGravarAsync();
        Formulario = Preparar(SetorEdicao.De(salvo!));
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

        SetorDto? gravado = null;
        if (!await ExecutarAsync(async () => gravado = desativar
                ? await _api.DesativarAsync<SetorDto>(Rotas.Estrutura.Setores, formulario.Id, formulario.Versao)
                : await _api.ReativarAsync<SetorDto>(Rotas.Estrutura.Setores, formulario.Id, formulario.Versao)))
            return;

        await AtualizarListaAposGravarAsync();
        Formulario = Preparar(SetorEdicao.De(gravado!));
        MarcarFichaSemAlteracoes();
        Mostrar(desativar ? "Desativado." : "Reativado.", TipoMensagem.Sucesso);
    }
}
