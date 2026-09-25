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

/// <summary>Linha da tabela de centros de custo.</summary>
public sealed class LinhaCentroCusto
{
    public LinhaCentroCusto(CentroCustoDto item) => Item = item;

    public CentroCustoDto Item { get; }
    public Guid Id => Item.Id;
    public string Nome => new string(' ', Item.Nivel * 4) + Item.Codigo + " " + Item.Nome;
    public string Detalhe => Item.Analitico ? "analítico" : "sintético (agrupa)";
    public string Usos => Item.QuantidadeUsos.ToString("N0", TextoTela.Brasil);
    public string Ativo => Item.Ativo ? "Sim" : "Não";
}

/// <summary>Ficha de um centro de custo. As regras finais (nome único, árvore) são da API.</summary>
public sealed partial class CentroCustoEdicao : ObservableObject
{
    private CentroCustoEdicao(Guid id, bool novo)
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
    [ObservableProperty] private string _codigo = string.Empty;
    [ObservableProperty] private bool _analitico = true;
    [ObservableProperty] private Opcao<Guid?>[] _pais = [SemPai];
    [ObservableProperty] private Opcao<Guid?> _pai = SemPai;
    public static readonly Opcao<Guid?> SemPai = new(null, "— (raiz)");
    private Guid? _paiGravado;

    /// <summary>Só sintéticos podem ser pai; o próprio centro (e os abaixo dele) não entram — a API confere de novo.</summary>
    public void DefinirPais(IReadOnlyList<CentroCustoDto> todos)
    {
        var abaixo = new HashSet<Guid> { Id };
        bool mudou;
        do
        {
            mudou = false;
            foreach (var c in todos.Where(c => c.PaiId is { } p && abaixo.Contains(p) && !abaixo.Contains(c.Id)))
                mudou |= abaixo.Add(c.Id);
        } while (mudou);

        var atual = Pai.Valor ?? _paiGravado;
        Pais = [SemPai, .. todos.Where(c => !abaixo.Contains(c.Id) && ((!c.Analitico && c.Ativo) || c.Id == _paiGravado))
            .OrderBy(c => c.Codigo, StringComparer.Ordinal)
            .Select(c => new Opcao<Guid?>(c.Id, $"{c.Codigo} {c.Nome}" + (c.Ativo ? string.Empty : " (desativado)")))];
        Pai = Pais.FirstOrDefault(p => p.Valor == atual) ?? Pais[0];
    }

    public string Titulo => string.IsNullOrWhiteSpace(Nome) ? "Novo centro de custo" : Nome;
    public string SituacaoTexto => Novo ? "Novo centro de custo" : Ativo ? "Ativo" : "Desativado (não aparece para novas escolhas; continua nas lotações que já o têm)";
    public string UsoTexto => Novo ? string.Empty : $"{QuantidadeUsos.ToString("N0", TextoTela.Brasil)} lotação(ões) em aberto com este centro de custo.";

    public static CentroCustoEdicao Criar() => new(IdSequencial.Novo(), novo: true);

    public static CentroCustoEdicao De(CentroCustoDto t) => new(t.Id, novo: false)
    {
        Versao = t.Versao,
        Ativo = t.Ativo,
        QuantidadeUsos = t.QuantidadeUsos,
        Codigo = t.Codigo,
        Analitico = t.Analitico,
        _paiGravado = t.PaiId,
        Nome = t.Nome
    };

    public IReadOnlyList<string> ValidarLocalmente()
    {
        var erros = new List<string>();
        if (string.IsNullOrWhiteSpace(Nome)) erros.Add("Informe o nome.");
        if (string.IsNullOrWhiteSpace(Codigo)) erros.Add("Informe o código (ex.: 1.01).");
        return erros;
    }

    public CentroCustoDto ParaDto() => new()
    {
        Id = Id,
        Versao = Versao,
        Nome = Nome.Trim(),
            Codigo = Codigo.Trim(),
            PaiId = Pais.Length > 1 ? Pai.Valor : _paiGravado,
            Analitico = Analitico,
        Ativo = Ativo
    };
}

/// <summary>Centros de custo (estrutura organizacional). Nada é excluído: desativar esconde das escolhas novas.</summary>
public sealed partial class CentrosCustoViewModel : CadastroViewModelBase<LinhaCentroCusto>
{
    private readonly ColaboradoresApi _api;
    private List<CentroCustoDto> _todos = [];

    public CentrosCustoViewModel(ColaboradoresApi api, IDialogos dialogos) : base(dialogos)
    {
        _api = api;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeDesativar), nameof(PodeReativar))]
    private CentroCustoEdicao? _formulario;

    public bool PodeDesativar => Formulario is { Novo: false, Ativo: true };
    public bool PodeReativar => Formulario is { Novo: false, Ativo: false };

    protected override string TextoDeBusca(LinhaCentroCusto item) => item.Nome + " " + item.Detalhe;

    protected override async Task<IReadOnlyList<LinhaCentroCusto>> ListarAsync()
    {
        _todos = await _api.ListarAsync<CentroCustoDto>(Rotas.Estrutura.CentrosCusto, incluirInativos: true);
        return _todos
            .OrderBy(t => t.Codigo, StringComparer.Ordinal)
            .Select(t => new LinhaCentroCusto(t)).ToList();
    }

    private CentroCustoEdicao Preparar(CentroCustoEdicao f)
    {
        f.DefinirPais(_todos);
        return f;
    }

    protected override async Task AbrirAsync(LinhaCentroCusto item) => Formulario = await ObterAsync(item.Id);

    protected override Task NovoItemAsync()
    {
        Formulario = Preparar(CentroCustoEdicao.Criar());
        return Task.CompletedTask;
    }

    protected override object? DadosDaFicha() => Formulario?.ParaDto();
    protected override bool FichaNova => Formulario?.Novo ?? true;

    protected override async Task RecarregarFichaAsync()
    {
        if (Formulario is not { Novo: false } formulario) return;
        Formulario = await ObterAsync(formulario.Id);
    }

    private async Task<CentroCustoEdicao> ObterAsync(Guid id) =>
        Preparar(CentroCustoEdicao.De(await _api.ObterAsync<CentroCustoDto>(Rotas.Estrutura.CentrosCusto, id) ?? throw new ValidacaoException(["Este cadastro não existe mais."])));

    [RelayCommand]
    private async Task SalvarAsync()
    {
        if (Formulario is not { } formulario) return;
        if (formulario.ValidarLocalmente() is { Count: > 0 } erros)
        {
            Mostrar(string.Join(Environment.NewLine, erros), TipoMensagem.Erro);
            return;
        }

        CentroCustoDto? salvo = null;
        if (!await ExecutarAsync(async () => salvo = await _api.SalvarAsync(Rotas.Estrutura.CentrosCusto, formulario.Id, formulario.ParaDto())))
            return;

        await AtualizarListaAposGravarAsync();
        Formulario = Preparar(CentroCustoEdicao.De(salvo!));
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

        CentroCustoDto? gravado = null;
        if (!await ExecutarAsync(async () => gravado = desativar
                ? await _api.DesativarAsync<CentroCustoDto>(Rotas.Estrutura.CentrosCusto, formulario.Id, formulario.Versao)
                : await _api.ReativarAsync<CentroCustoDto>(Rotas.Estrutura.CentrosCusto, formulario.Id, formulario.Versao)))
            return;

        await AtualizarListaAposGravarAsync();
        Formulario = Preparar(CentroCustoEdicao.De(gravado!));
        MarcarFichaSemAlteracoes();
        Mostrar(desativar ? "Desativado." : "Reativado.", TipoMensagem.Sucesso);
    }
}
