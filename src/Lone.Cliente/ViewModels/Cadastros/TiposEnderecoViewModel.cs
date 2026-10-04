using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.Api;
using Lone.Cliente.Plataforma;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Enderecos;
using Lone.Domain.Comum;
using Lone.Domain.Validacao;
using Lone.Cliente.Grade;

namespace Lone.Cliente.ViewModels.Cadastros;

/// <summary>Linha da tabela de tipos de endereço (Tipo · Em uso · Ativo).</summary>
public sealed class LinhaTipoEndereco
{
    public LinhaTipoEndereco(TipoEnderecoDto tipo) => Tipo = tipo;

    public TipoEnderecoDto Tipo { get; }
    public Guid Id => Tipo.Id;
    public string Nome => Tipo.Nome;
    public string Categoria => "Endereço";
    public string Usos => Tipo.QuantidadeUsos.ToString("N0", TextoTela.Brasil);
    public string Ativo => Tipo.Ativo ? "Sim" : "Não";
}

/// <summary>Ficha de um tipo de endereço. As regras finais (nome único) são da API.</summary>
public sealed partial class TipoEnderecoEdicao : ObservableObject
{
    private TipoEnderecoEdicao(Guid id, bool novo)
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
    [ObservableProperty] private string _ordem = string.Empty;

    public string Titulo => string.IsNullOrWhiteSpace(Nome) ? "Novo tipo" : Nome;
    public string SituacaoTexto => Novo ? "Novo tipo" : Ativo ? "Ativo" : "Desativado (não aparece para novas escolhas; continua nos endereços que já o têm)";
    public string UsoTexto => Novo ? string.Empty : $"{QuantidadeUsos.ToString("N0", TextoTela.Brasil)} endereço(s) ativos com este tipo.";

    public static TipoEnderecoEdicao Criar() => new(IdSequencial.Novo(), novo: true);

    public static TipoEnderecoEdicao De(TipoEnderecoDto t) => new(t.Id, novo: false)
    {
        Versao = t.Versao,
        Ativo = t.Ativo,
        QuantidadeUsos = t.QuantidadeUsos,
        Nome = t.Nome,
        Ordem = t.Ordem.ToString(CultureInfo.InvariantCulture)
    };

    public IReadOnlyList<string> ValidarLocalmente()
    {
        var erros = new List<string>();
        if (string.IsNullOrWhiteSpace(Nome)) erros.Add("Informe o nome do tipo.");
        if (!string.IsNullOrWhiteSpace(Ordem) && !TextoTela.TentarInteiro(Ordem, out _)) erros.Add("Ordem: use um número inteiro.");
        return erros;
    }

    public TipoEnderecoDto ParaDto()
    {
        TextoTela.TentarInteiro(Ordem, out var ordem);
        return new TipoEnderecoDto
        {
            Id = Id,
            Versao = Versao,
            Nome = Nome.Trim(),
            Ordem = ordem ?? 0,
            Ativo = Ativo
        };
    }
}

/// <summary>Tipos de endereço (Sede, Filial, Depósito...). Nada é excluído: desativar esconde das escolhas novas.</summary>
public sealed partial class TiposEnderecoViewModel : CadastroViewModelBase<LinhaTipoEndereco>
{
    /// <summary>Lista em colunas (padrão de tela de cadastro, 03/10/2026).</summary>
    protected override GradeCadastro<LinhaTipoEndereco> CriarGradeDaLista() => new(
        "Tipo de endereço", l => l.Id, l => l.Nome, l => null,
        ColunaCadastro<LinhaTipoEndereco>.Curto("emuso", "Em uso", l => l.Usos, 130),
        ColunaCadastro<LinhaTipoEndereco>.Situacao(l => l.Ativo == "Sim"));

    private readonly TiposEnderecoApi _api;

    public TiposEnderecoViewModel(TiposEnderecoApi api, IDialogos dialogos) : base(dialogos)
    {
        _api = api;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeDesativar), nameof(PodeReativar))]
    private TipoEnderecoEdicao? _formulario;

    public bool PodeDesativar => Formulario is { Novo: false, Ativo: true };
    public bool PodeReativar => Formulario is { Novo: false, Ativo: false };

    protected override string TextoDeBusca(LinhaTipoEndereco item) => item.Nome;

    protected override async Task<IReadOnlyList<LinhaTipoEndereco>> ListarAsync() =>
        (await _api.ListarAsync(incluirInativos: true))
            .OrderBy(t => !t.Ativo).ThenBy(t => t.Ordem)
            .Select(t => new LinhaTipoEndereco(t)).ToList();

    protected override async Task AbrirAsync(LinhaTipoEndereco item) => Formulario = await ObterAsync(item.Id);

    protected override Task NovoItemAsync()
    {
        Formulario = TipoEnderecoEdicao.Criar();
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

    private async Task<TipoEnderecoEdicao> ObterAsync(Guid id) =>
        TipoEnderecoEdicao.De(await _api.ObterAsync(id) ?? throw new ValidacaoException(["Este tipo não existe mais."]));

    [RelayCommand]
    private async Task SalvarAsync()
    {
        if (Formulario is not { } formulario) return;
        if (formulario.ValidarLocalmente() is { Count: > 0 } erros)
        {
            Mostrar(string.Join(Environment.NewLine, erros), TipoMensagem.Erro);
            return;
        }

        TipoEnderecoDto? salvo = null;
        if (!await ExecutarAsync(async () => salvo = await _api.SalvarAsync(formulario.ParaDto())))
            return;

        Formulario = TipoEnderecoEdicao.De(salvo!);
        MarcarFichaSemAlteracoes();
        Mostrar(formulario.Novo ? "Tipo criado. Ele já pode ser escolhido nos endereços (reabra a tela de Pessoas)." : "Alterações salvas.",
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

        TipoEnderecoDto? gravado = null;
        if (!await ExecutarAsync(async () => gravado = desativar
                ? await _api.DesativarAsync(formulario.Id, formulario.Versao)
                : await _api.ReativarAsync(formulario.Id, formulario.Versao)))
            return;

        Formulario = TipoEnderecoEdicao.De(gravado!);
        MarcarFichaSemAlteracoes();
        Mostrar(desativar ? "Tipo desativado." : "Tipo reativado.", TipoMensagem.Sucesso);
        await AtualizarListaAposGravarAsync();
    }
}
