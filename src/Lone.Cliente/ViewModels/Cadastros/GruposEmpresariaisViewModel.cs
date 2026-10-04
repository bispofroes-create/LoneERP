using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.Api;
using Lone.Cliente.Plataforma;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.GruposEmpresariais;
using Lone.Contracts.Pessoas;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Validacao;
using Lone.Cliente.Grade;

namespace Lone.Cliente.ViewModels.Cadastros;

/// <summary>Linha da tabela de grupos empresariais.</summary>
public sealed class LinhaGrupoEmpresarial
{
    public LinhaGrupoEmpresarial(GrupoEmpresarialDto item) => Item = item;

    public GrupoEmpresarialDto Item { get; }
    public Guid Id => Item.Id;
    public string Nome => Item.Nome;
    public string Detalhe => Item.Descricao ?? string.Empty;
    public string Empresas => Item.QuantidadeEmpresas.ToString("N0", TextoTela.Brasil);
    public string Ativo => Item.Ativo ? "Sim" : "Não";
}

/// <summary>Uma empresa do grupo (somente leitura): nome, razão social, CNPJ principal e estabelecimentos.</summary>
public sealed class EmpresaDoGrupoItem
{
    public EmpresaDoGrupoItem(EmpresaDoGrupoEmpresarialDto e) => Dados = e;

    public EmpresaDoGrupoEmpresarialDto Dados { get; }
    public string Nome => Dados.Nome;

    public string Detalhe => string.Join("  ·  ", new[]
    {
        Dados.RazaoSocial != Dados.Nome ? Dados.RazaoSocial : string.Empty,
        Dados.CnpjPrincipal is { Length: > 0 } cnpj ? "CNPJ " + Documento.Formatar(cnpj) : string.Empty,
        Dados.QuantidadeEstabelecimentos == 1
            ? "1 estabelecimento"
            : $"{Dados.QuantidadeEstabelecimentos} estabelecimentos ({Dados.EstabelecimentosAtivos} ativos)",
        NomesPessoa.Situacao(Dados.Situacao)
    }.Where(t => t.Length > 0));
}

/// <summary>Ficha de um grupo empresarial. As regras finais (nome único) são da API.</summary>
public sealed partial class GrupoEmpresarialEdicao : ObservableObject
{
    private GrupoEmpresarialEdicao(Guid id, bool novo)
    {
        Id = id;
        Novo = novo;
    }

    public Guid Id { get; }
    public bool Novo { get; }
    public byte[]? Versao { get; private set; }
    public bool Ativo { get; private set; } = true;
    public int QuantidadeEmpresas { get; private set; }

    [ObservableProperty][NotifyPropertyChangedFor(nameof(Titulo))] private string _nome = string.Empty;
    [ObservableProperty] private string _descricao = string.Empty;

    /// <summary>As pessoas jurídicas do grupo (lidas ao abrir; a participação é decidida na ficha de cada empresa).</summary>
    public ObservableCollection<EmpresaDoGrupoItem> Empresas { get; } = new();
    public bool SemEmpresas => Empresas.Count == 0;

    public string Titulo => string.IsNullOrWhiteSpace(Nome) ? "Novo grupo empresarial" : Nome;
    public string SituacaoTexto => Novo ? "Novo grupo empresarial"
        : Ativo ? "Ativo"
        : "Desativado (não é oferecido para outras empresas; continua nas que já fazem parte)";
    public string UsoTexto => Novo ? string.Empty : $"{QuantidadeEmpresas.ToString("N0", TextoTela.Brasil)} empresa(s) no grupo.";

    public static GrupoEmpresarialEdicao Criar() => new(IdSequencial.Novo(), novo: true);

    public static GrupoEmpresarialEdicao De(GrupoEmpresarialDto g) => new(g.Id, novo: false)
    {
        Versao = g.Versao,
        Ativo = g.Ativo,
        QuantidadeEmpresas = g.QuantidadeEmpresas,
        Nome = g.Nome,
        Descricao = g.Descricao ?? string.Empty
    };

    public void DefinirEmpresas(IEnumerable<EmpresaDoGrupoEmpresarialDto> empresas)
    {
        Empresas.Clear();
        foreach (var e in empresas) Empresas.Add(new EmpresaDoGrupoItem(e));
        OnPropertyChanged(nameof(SemEmpresas));
    }

    public IReadOnlyList<string> ValidarLocalmente()
    {
        var erros = new List<string>();
        if (string.IsNullOrWhiteSpace(Nome)) erros.Add("Informe o nome.");
        else if (Nome.Trim().Length > GrupoEmpresarial.TamanhoMaximoNome)
            erros.Add($"O nome pode ter no máximo {GrupoEmpresarial.TamanhoMaximoNome} caracteres.");
        return erros;
    }

    /// <summary>Só os dados do grupo: as empresas não fazem parte da gravação (nem da comparação de alterações).</summary>
    public GrupoEmpresarialDto ParaDto() => new()
    {
        Id = Id,
        Versao = Versao,
        Nome = Nome.Trim(),
        Descricao = TextoTela.Nulo(Descricao),
        Ativo = Ativo
    };
}

/// <summary>
/// Grupos empresariais: conjuntos de pessoas jurídicas independentes (cada uma com a sua matriz e filiais). Opcional:
/// uma empresa funciona sem grupo. Nada é excluído: desativar esconde das escolhas novas.
/// </summary>
public sealed partial class GruposEmpresariaisViewModel : CadastroViewModelBase<LinhaGrupoEmpresarial>
{
    /// <summary>Lista em colunas (padrão de tela de cadastro, 03/10/2026).</summary>
    protected override GradeCadastro<LinhaGrupoEmpresarial> CriarGradeDaLista() => new(
        "Grupo empresarial", l => l.Id, l => l.Nome, l => l.Detalhe,
        ColunaCadastro<LinhaGrupoEmpresarial>.Curto("empresas", "Empresas", l => l.Empresas, 130),
        ColunaCadastro<LinhaGrupoEmpresarial>.Situacao(l => l.Ativo == "Sim"));

    private readonly GruposEmpresariaisApi _api;

    public GruposEmpresariaisViewModel(GruposEmpresariaisApi api, IDialogos dialogos) : base(dialogos)
    {
        _api = api;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeDesativar), nameof(PodeReativar))]
    private GrupoEmpresarialEdicao? _formulario;

    public bool PodeDesativar => Formulario is { Novo: false, Ativo: true };
    public bool PodeReativar => Formulario is { Novo: false, Ativo: false };

    protected override string TextoDeBusca(LinhaGrupoEmpresarial item) => item.Nome + " " + item.Detalhe;

    protected override async Task<IReadOnlyList<LinhaGrupoEmpresarial>> ListarAsync() =>
        (await _api.ListarAsync(incluirInativos: true))
            .OrderBy(g => !g.Ativo).ThenBy(g => g.Nome, StringComparer.CurrentCultureIgnoreCase)
            .Select(g => new LinhaGrupoEmpresarial(g)).ToList();

    protected override async Task AbrirAsync(LinhaGrupoEmpresarial item) => Formulario = await ObterAsync(item.Id);

    protected override Task NovoItemAsync()
    {
        Formulario = GrupoEmpresarialEdicao.Criar();
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

    private async Task<GrupoEmpresarialEdicao> ObterAsync(Guid id)
    {
        var ficha = GrupoEmpresarialEdicao.De(await _api.ObterAsync(id) ?? throw new ValidacaoException(["Este cadastro não existe mais."]));
        ficha.DefinirEmpresas(await _api.ListarEmpresasAsync(id));
        return ficha;
    }

    [RelayCommand]
    private async Task SalvarAsync()
    {
        if (Formulario is not { } formulario) return;
        if (formulario.ValidarLocalmente() is { Count: > 0 } erros)
        {
            Mostrar(string.Join(Environment.NewLine, erros), TipoMensagem.Erro);
            return;
        }

        GrupoEmpresarialDto? salvo = null;
        if (!await ExecutarAsync(async () => salvo = await _api.SalvarAsync(formulario.ParaDto())))
            return;

        await AtualizarListaAposGravarAsync();
        var novo = GrupoEmpresarialEdicao.De(salvo!);
        novo.DefinirEmpresas(formulario.Empresas.Select(e => e.Dados));
        Formulario = novo;
        MarcarFichaSemAlteracoes();
        Mostrar(formulario.Novo ? "Grupo empresarial criado. As empresas entram pela ficha de cada uma (aba \"Identificação\")." : "Alterações salvas.",
            TipoMensagem.Sucesso);
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

        GrupoEmpresarialDto? gravado = null;
        if (!await ExecutarAsync(async () => gravado = desativar
                ? await _api.DesativarAsync(formulario.Id, formulario.Versao)
                : await _api.ReativarAsync(formulario.Id, formulario.Versao)))
            return;

        await AtualizarListaAposGravarAsync();
        var novo = GrupoEmpresarialEdicao.De(gravado!);
        novo.DefinirEmpresas(formulario.Empresas.Select(e => e.Dados));
        Formulario = novo;
        MarcarFichaSemAlteracoes();
        Mostrar(desativar ? "Desativado. As empresas continuam no grupo." : "Reativado.", TipoMensagem.Sucesso);
    }
}
