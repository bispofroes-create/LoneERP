using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.Api;
using Lone.Cliente.Plataforma;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Papeis;
using Lone.Domain.Comum;
using Lone.Domain.Validacao;
using Lone.Cliente.Grade;

namespace Lone.Cliente.ViewModels.Cadastros;

/// <summary>Linha da tabela de papéis (Papel · Código · Cadastros · Ativo).</summary>
public sealed class LinhaPapel
{
    public LinhaPapel(PapelCadastroDto papel) => Papel = papel;

    public PapelCadastroDto Papel { get; }
    public Guid Id => Papel.Id;
    public string Nome => Papel.PapelSistema is null ? Papel.Nome : Papel.Nome + " ⚙";
    public string Codigo => Papel.Codigo;
    public string Cadastros => Papel.QuantidadePessoas.ToString("N0", TextoTela.Brasil);
    public string Ativo => Papel.Ativo ? "Sim" : "Não";
}

/// <summary>Ficha de um papel. As regras finais (nome e código únicos, papel com regra não desativa) são da API.</summary>
public sealed partial class PapelEdicao : ObservableObject
{
    private PapelEdicao(Guid id, bool novo)
    {
        Id = id;
        Novo = novo;
    }

    public Guid Id { get; }
    public bool Novo { get; }
    public byte[]? Versao { get; private set; }
    public bool Ativo { get; private set; } = true;
    public bool DeSistema { get; private set; }
    public bool Obrigatorio { get; private set; }
    public int QuantidadePessoas { get; private set; }

    /// <summary>O código só é escolhido na criação (depois não muda).</summary>
    public bool PodeMudarCodigo => Novo;

    [ObservableProperty][NotifyPropertyChangedFor(nameof(Titulo))] private string _nome = string.Empty;
    [ObservableProperty] private string _codigo = string.Empty;
    [ObservableProperty] private string _descricao = string.Empty;
    [ObservableProperty] private string _ordem = string.Empty;

    public string Titulo => string.IsNullOrWhiteSpace(Nome) ? "Novo papel" : Nome;
    public string SituacaoTexto => Novo ? "Novo papel"
        : (Ativo ? "Ativo" : "Desativado (não aparece para novas marcações; continua nos cadastros que já o têm)")
          + (Obrigatorio ? " · papel de sistema, usado pelas regras (não pode ser desativado)"
             : DeSistema ? " · papel de sistema" : string.Empty);
    public string UsoTexto => Novo ? string.Empty
        : QuantidadePessoas switch { 0 => "Nenhum cadastro com este papel ativo.", 1 => "1 cadastro com este papel ativo.", var n => $"{n.ToString("N0", TextoTela.Brasil)} cadastros com este papel ativo." };

    public static PapelEdicao Criar() => new(IdSequencial.Novo(), novo: true);

    public static PapelEdicao De(PapelCadastroDto p) => new(p.Id, novo: false)
    {
        Versao = p.Versao,
        Ativo = p.Ativo,
        DeSistema = p.PapelSistema is not null,
        Obrigatorio = p.Obrigatorio,
        QuantidadePessoas = p.QuantidadePessoas,
        Nome = p.Nome,
        Codigo = p.Codigo,
        Descricao = p.Descricao ?? string.Empty,
        Ordem = p.Ordem.ToString(CultureInfo.InvariantCulture)
    };

    public IReadOnlyList<string> ValidarLocalmente()
    {
        var erros = new List<string>();
        if (string.IsNullOrWhiteSpace(Nome)) erros.Add("Informe o nome do papel.");
        if (!string.IsNullOrWhiteSpace(Ordem) && !TextoTela.TentarInteiro(Ordem, out _)) erros.Add("Ordem: use um número inteiro.");
        return erros;
    }

    public PapelCadastroDto ParaDto()
    {
        TextoTela.TentarInteiro(Ordem, out var ordem);
        return new PapelCadastroDto
        {
            Id = Id,
            Versao = Versao,
            Codigo = Codigo.Trim(),
            Nome = Nome.Trim(),
            Descricao = TextoTela.Nulo(Descricao)?.Trim(),
            Ordem = ordem ?? 0,
            Ativo = Ativo
        };
    }
}

/// <summary>
/// Cadastro de papéis: os oito de sistema (com regras no código; o nome pode mudar) e os criados pelo usuário.
/// Nada é excluído: desativar esconde o papel das marcações novas e mantém os cadastros que já o têm.
/// </summary>
public sealed partial class PapeisViewModel : CadastroViewModelBase<LinhaPapel>
{
    /// <summary>Lista em colunas (padrão de tela de cadastro, 03/10/2026).</summary>
    protected override GradeCadastro<LinhaPapel> CriarGradeDaLista() => new(
        "Papel", l => l.Id, l => l.Nome, l => l.Codigo,
        ColunaCadastro<LinhaPapel>.Curto("cadastros", "Cadastros", l => l.Cadastros, 130),
        ColunaCadastro<LinhaPapel>.Situacao(l => l.Ativo == "Sim"));

    private readonly PapeisApi _api;

    public PapeisViewModel(PapeisApi api, IDialogos dialogos) : base(dialogos)
    {
        _api = api;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeDesativar), nameof(PodeReativar))]
    private PapelEdicao? _formulario;

    public bool PodeDesativar => Formulario is { Novo: false, Ativo: true, Obrigatorio: false };
    public bool PodeReativar => Formulario is { Novo: false, Ativo: false };

    protected override string TextoDeBusca(LinhaPapel item) => item.Nome + " " + item.Codigo;

    protected override async Task<IReadOnlyList<LinhaPapel>> ListarAsync() =>
        (await _api.ListarAsync(incluirInativos: true))
            .OrderBy(p => !p.Ativo).ThenBy(p => p.Ordem).ThenBy(p => p.Nome, StringComparer.CurrentCultureIgnoreCase)
            .Select(p => new LinhaPapel(p)).ToList();

    protected override async Task AbrirAsync(LinhaPapel item) => Formulario = await ObterAsync(item.Id);

    protected override Task NovoItemAsync()
    {
        Formulario = PapelEdicao.Criar();
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

    private async Task<PapelEdicao> ObterAsync(Guid id) =>
        PapelEdicao.De(await _api.ObterAsync(id) ?? throw new ValidacaoException(["Este papel não existe mais."]));

    [RelayCommand]
    private async Task SalvarAsync()
    {
        if (Formulario is not { } formulario) return;
        if (formulario.ValidarLocalmente() is { Count: > 0 } erros)
        {
            Mostrar(string.Join(Environment.NewLine, erros), TipoMensagem.Erro);
            return;
        }

        PapelCadastroDto? salvo = null;
        if (!await ExecutarAsync(async () => salvo = await _api.SalvarAsync(formulario.ParaDto())))
            return;

        Formulario = PapelEdicao.De(salvo!);
        MarcarFichaSemAlteracoes();
        Mostrar(formulario.Novo ? "Papel criado. Ele já aparece na ficha das pessoas (reabra a tela de Pessoas)." : "Alterações salvas.",
            TipoMensagem.Sucesso);
        await AtualizarListaAposGravarAsync();
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
            Mostrar("Salve ou descarte as alterações antes de " + (desativar ? "desativar" : "reativar") + " o papel.", TipoMensagem.Aviso);
            return;
        }

        if (desativar && !await ConfirmarAsync(
                "Desativar papel",
                $"\"{formulario.Nome}\" deixará de aparecer para novas marcações. Os cadastros que já o têm continuam com ele, e ele pode ser reativado.",
                "Desativar", "Cancelar"))
            return;

        PapelCadastroDto? gravado = null;
        if (!await ExecutarAsync(async () => gravado = desativar
                ? await _api.DesativarAsync(formulario.Id, formulario.Versao)
                : await _api.ReativarAsync(formulario.Id, formulario.Versao)))
            return;

        Formulario = PapelEdicao.De(gravado!);
        MarcarFichaSemAlteracoes();
        Mostrar(desativar ? "Papel desativado." : "Papel reativado.", TipoMensagem.Sucesso);
        await AtualizarListaAposGravarAsync();
    }
}
