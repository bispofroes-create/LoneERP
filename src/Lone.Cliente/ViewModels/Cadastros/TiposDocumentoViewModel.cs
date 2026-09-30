using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.Api;
using Lone.Cliente.Plataforma;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Documentos;
using Lone.Domain.Comum;
using Lone.Domain.Validacao;

namespace Lone.Cliente.ViewModels.Cadastros;

/// <summary>Linha da tabela de tipos de documento (Tipo · Em uso · Ativo).</summary>
public sealed class LinhaTipoDocumento
{
    public LinhaTipoDocumento(TipoDocumentoDto tipo) => Tipo = tipo;

    public TipoDocumentoDto Tipo { get; }
    public Guid Id => Tipo.Id;
    public string Nome => Tipo.Nome;
    public string Validade => Tipo.ExigeValidade ? $"Exige · aviso {Tipo.DiasAvisoVencimento} dia(s)" : $"Opcional · aviso {Tipo.DiasAvisoVencimento} dia(s)";
    public string Sistema => Tipo.TipoSistema is null ? string.Empty : "do sistema";
    public string Usos => Tipo.QuantidadeUsos.ToString("N0", TextoTela.Brasil);
    public string Ativo => Tipo.Ativo ? "Sim" : "Não";
}

/// <summary>Ficha de um tipo de documento. As regras finais (nome único) são da API.</summary>
public sealed partial class TipoDocumentoEdicao : ObservableObject
{
    private TipoDocumentoEdicao(Guid id, bool novo)
    {
        Id = id;
        Novo = novo;
    }

    public Guid Id { get; }
    public bool Novo { get; }
    public byte[]? Versao { get; private set; }
    public bool Ativo { get; private set; } = true;
    public int QuantidadeUsos { get; private set; }

    /// <summary>Ligado ao enum TipoDocumento (RG, CNH...): nasce com a base; só nome, ordem e validade mudam.</summary>
    public bool DoSistema { get; private set; }

    [ObservableProperty][NotifyPropertyChangedFor(nameof(Titulo))] private string _nome = string.Empty;
    [ObservableProperty] private string _ordem = string.Empty;
    [ObservableProperty] private bool _exigeValidade;
    [ObservableProperty] private string _diasAvisoVencimento = "30";

    public string Titulo => string.IsNullOrWhiteSpace(Nome) ? "Novo tipo" : Nome;
    public string SituacaoTexto => Novo ? "Novo tipo" : Ativo ? "Ativo" : "Desativado (não aparece para novas escolhas; continua nos documentos que já o têm)";
    public string UsoTexto => Novo ? string.Empty : $"{QuantidadeUsos.ToString("N0", TextoTela.Brasil)} documento(s) ativos com este tipo.";

    public static TipoDocumentoEdicao Criar() => new(IdSequencial.Novo(), novo: true);

    public static TipoDocumentoEdicao De(TipoDocumentoDto t) => new(t.Id, novo: false)
    {
        Versao = t.Versao,
        Ativo = t.Ativo,
        QuantidadeUsos = t.QuantidadeUsos,
        DoSistema = t.TipoSistema is not null,
        ExigeValidade = t.ExigeValidade,
        DiasAvisoVencimento = t.DiasAvisoVencimento.ToString(CultureInfo.InvariantCulture),
        Nome = t.Nome,
        Ordem = t.Ordem.ToString(CultureInfo.InvariantCulture)
    };

    public IReadOnlyList<string> ValidarLocalmente()
    {
        var erros = new List<string>();
        if (string.IsNullOrWhiteSpace(Nome)) erros.Add("Informe o nome do tipo.");
        if (!string.IsNullOrWhiteSpace(Ordem) && !TextoTela.TentarInteiro(Ordem, out _)) erros.Add("Ordem: use um número inteiro.");
        if (!TextoTela.TentarInteiro(DiasAvisoVencimento, out var dias) || dias is null or < 0)
            erros.Add("Dias de aviso: use um número inteiro (0 = avisar só quando vencer).");
        return erros;
    }

    public TipoDocumentoDto ParaDto()
    {
        TextoTela.TentarInteiro(Ordem, out var ordem);
        TextoTela.TentarInteiro(DiasAvisoVencimento, out var dias);
        return new TipoDocumentoDto
        {
            Id = Id,
            Versao = Versao,
            Nome = Nome.Trim(),
            Ordem = ordem ?? 0,
            Ativo = Ativo,
            ExigeValidade = ExigeValidade,
            DiasAvisoVencimento = dias ?? 30
        };
    }
}

/// <summary>Tipos de documento (RG, CNH, Alvará...). Nada é excluído: desativar esconde das escolhas novas.</summary>
public sealed partial class TiposDocumentoViewModel : CadastroViewModelBase<LinhaTipoDocumento>
{
    private readonly TiposDocumentoApi _api;

    public TiposDocumentoViewModel(TiposDocumentoApi api, IDialogos dialogos) : base(dialogos)
    {
        _api = api;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeDesativar), nameof(PodeReativar))]
    private TipoDocumentoEdicao? _formulario;

    public bool PodeDesativar => Formulario is { Novo: false, Ativo: true };
    public bool PodeReativar => Formulario is { Novo: false, Ativo: false };

    protected override string TextoDeBusca(LinhaTipoDocumento item) => item.Nome;

    protected override async Task<IReadOnlyList<LinhaTipoDocumento>> ListarAsync() =>
        (await _api.ListarAsync(incluirInativos: true))
            .OrderBy(t => !t.Ativo).ThenBy(t => t.Ordem)
            .Select(t => new LinhaTipoDocumento(t)).ToList();

    protected override async Task AbrirAsync(LinhaTipoDocumento item) => Formulario = await ObterAsync(item.Id);

    protected override Task NovoItemAsync()
    {
        Formulario = TipoDocumentoEdicao.Criar();
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

    private async Task<TipoDocumentoEdicao> ObterAsync(Guid id) =>
        TipoDocumentoEdicao.De(await _api.ObterAsync(id) ?? throw new ValidacaoException(["Este tipo não existe mais."]));

    [RelayCommand]
    private async Task SalvarAsync()
    {
        if (Formulario is not { } formulario) return;
        if (formulario.ValidarLocalmente() is { Count: > 0 } erros)
        {
            Mostrar(string.Join(Environment.NewLine, erros), TipoMensagem.Erro);
            return;
        }

        TipoDocumentoDto? salvo = null;
        if (!await ExecutarAsync(async () => salvo = await _api.SalvarAsync(formulario.ParaDto())))
            return;

        Formulario = TipoDocumentoEdicao.De(salvo!);
        MarcarFichaSemAlteracoes();
        Mostrar(formulario.Novo ? "Tipo criado. Ele já pode ser escolhido nos documentos (reabra a tela de Pessoas)." : "Alterações salvas.",
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
            Mostrar("Salve ou descarte as alterações antes de " + (desativar ? "desativar" : "reativar") + " o tipo.", TipoMensagem.Aviso);
            return;
        }

        TipoDocumentoDto? gravado = null;
        if (!await ExecutarAsync(async () => gravado = desativar
                ? await _api.DesativarAsync(formulario.Id, formulario.Versao)
                : await _api.ReativarAsync(formulario.Id, formulario.Versao)))
            return;

        Formulario = TipoDocumentoEdicao.De(gravado!);
        MarcarFichaSemAlteracoes();
        Mostrar(desativar ? "Tipo desativado." : "Tipo reativado.", TipoMensagem.Sucesso);
        await AtualizarListaAposGravarAsync();
    }
}
