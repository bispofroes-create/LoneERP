using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.Api;
using Lone.Cliente.Plataforma;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Contatos;
using Lone.Domain.Comum;
using Lone.Domain.Enums;
using Lone.Domain.Validacao;

namespace Lone.Cliente.ViewModels.Cadastros;

/// <summary>Linha da tabela de tipos de telefone/e-mail (Tipo · Categoria · Em uso · Ativo).</summary>
public sealed class LinhaTipoMeioContato
{
    public LinhaTipoMeioContato(TipoMeioContatoDto tipo) => Tipo = tipo;

    public TipoMeioContatoDto Tipo { get; }
    public Guid Id => Tipo.Id;
    public string Nome => Tipo.Nome;
    public string Categoria => TipoMeioContatoEdicao.NomeCategoria(Tipo.Categoria);
    public string Usos => Tipo.QuantidadeUsos.ToString("N0", TextoTela.Brasil);
    public string Ativo => Tipo.Ativo ? "Sim" : "Não";
}

/// <summary>Ficha de um tipo de telefone/e-mail. As regras finais (nome único por categoria) são da API.</summary>
public sealed partial class TipoMeioContatoEdicao : ObservableObject
{
    public static readonly Opcao<CategoriaMeioContato>[] Categorias =
    [
        new(CategoriaMeioContato.Telefone, "Telefone"),
        new(CategoriaMeioContato.Email, "E-mail")
    ];

    private TipoMeioContatoEdicao(Guid id, bool novo)
    {
        Id = id;
        Novo = novo;
    }

    public Guid Id { get; }
    public bool Novo { get; }
    public byte[]? Versao { get; private set; }
    public bool Ativo { get; private set; } = true;
    public int QuantidadeUsos { get; private set; }

    /// <summary>A categoria só é escolhida na criação.</summary>
    public bool PodeMudarCategoria => Novo;

    public IReadOnlyList<Opcao<CategoriaMeioContato>> ListaCategorias => Categorias;

    [ObservableProperty][NotifyPropertyChangedFor(nameof(Titulo))] private string _nome = string.Empty;
    [ObservableProperty] private Opcao<CategoriaMeioContato> _categoria = Categorias[0];
    [ObservableProperty] private string _ordem = string.Empty;

    public string Titulo => string.IsNullOrWhiteSpace(Nome) ? "Novo tipo" : Nome;
    public string SituacaoTexto => Novo ? "Novo tipo" : Ativo ? "Ativo" : "Desativado (não aparece para novas escolhas; continua nos contatos que já o têm)";
    public string UsoTexto => Novo ? string.Empty : $"{QuantidadeUsos.ToString("N0", TextoTela.Brasil)} telefone(s)/e-mail(s) ativos com este tipo.";

    public static string NomeCategoria(CategoriaMeioContato c) => c == CategoriaMeioContato.Email ? "E-mail" : "Telefone";

    public static TipoMeioContatoEdicao Criar() => new(IdSequencial.Novo(), novo: true);

    public static TipoMeioContatoEdicao De(TipoMeioContatoDto t) => new(t.Id, novo: false)
    {
        Versao = t.Versao,
        Ativo = t.Ativo,
        QuantidadeUsos = t.QuantidadeUsos,
        Nome = t.Nome,
        Categoria = Opcao.De(Categorias, t.Categoria),
        Ordem = t.Ordem.ToString(CultureInfo.InvariantCulture)
    };

    public IReadOnlyList<string> ValidarLocalmente()
    {
        var erros = new List<string>();
        if (string.IsNullOrWhiteSpace(Nome)) erros.Add("Informe o nome do tipo.");
        if (!string.IsNullOrWhiteSpace(Ordem) && !TextoTela.TentarInteiro(Ordem, out _)) erros.Add("Ordem: use um número inteiro.");
        return erros;
    }

    public TipoMeioContatoDto ParaDto()
    {
        TextoTela.TentarInteiro(Ordem, out var ordem);
        return new TipoMeioContatoDto
        {
            Id = Id,
            Versao = Versao,
            Nome = Nome.Trim(),
            Categoria = Categoria.Valor,
            Ordem = ordem ?? 0,
            Ativo = Ativo
        };
    }
}

/// <summary>Tipos de telefone e e-mail (Comercial, Residencial, Pessoal...). Nada é excluído: desativar esconde das escolhas novas.</summary>
public sealed partial class TiposMeioContatoViewModel : CadastroViewModelBase<LinhaTipoMeioContato>
{
    private readonly TiposMeioContatoApi _api;

    public TiposMeioContatoViewModel(TiposMeioContatoApi api, IDialogos dialogos) : base(dialogos)
    {
        _api = api;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeDesativar), nameof(PodeReativar))]
    private TipoMeioContatoEdicao? _formulario;

    public bool PodeDesativar => Formulario is { Novo: false, Ativo: true };
    public bool PodeReativar => Formulario is { Novo: false, Ativo: false };

    protected override string TextoDeBusca(LinhaTipoMeioContato item) => item.Nome + " " + item.Categoria;

    protected override async Task<IReadOnlyList<LinhaTipoMeioContato>> ListarAsync() =>
        (await _api.ListarAsync(incluirInativos: true))
            .OrderBy(t => !t.Ativo).ThenBy(t => t.Categoria).ThenBy(t => t.Ordem)
            .Select(t => new LinhaTipoMeioContato(t)).ToList();

    protected override async Task AbrirAsync(LinhaTipoMeioContato item) => Formulario = await ObterAsync(item.Id);

    protected override Task NovoItemAsync()
    {
        Formulario = TipoMeioContatoEdicao.Criar();
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

    private async Task<TipoMeioContatoEdicao> ObterAsync(Guid id) =>
        TipoMeioContatoEdicao.De(await _api.ObterAsync(id) ?? throw new ValidacaoException(["Este tipo não existe mais."]));

    [RelayCommand]
    private async Task SalvarAsync()
    {
        if (Formulario is not { } formulario) return;
        if (formulario.ValidarLocalmente() is { Count: > 0 } erros)
        {
            Mostrar(string.Join(Environment.NewLine, erros), TipoMensagem.Erro);
            return;
        }

        TipoMeioContatoDto? salvo = null;
        if (!await ExecutarAsync(async () => salvo = await _api.SalvarAsync(formulario.ParaDto())))
            return;

        Formulario = TipoMeioContatoEdicao.De(salvo!);
        MarcarFichaSemAlteracoes();
        Mostrar(formulario.Novo ? "Tipo criado. Ele já pode ser escolhido nos telefones/e-mails (reabra a tela de Pessoas)." : "Alterações salvas.",
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

        TipoMeioContatoDto? gravado = null;
        if (!await ExecutarAsync(async () => gravado = desativar
                ? await _api.DesativarAsync(formulario.Id, formulario.Versao)
                : await _api.ReativarAsync(formulario.Id, formulario.Versao)))
            return;

        Formulario = TipoMeioContatoEdicao.De(gravado!);
        MarcarFichaSemAlteracoes();
        Mostrar(desativar ? "Tipo desativado." : "Tipo reativado.", TipoMensagem.Sucesso);
        await AtualizarListaAposGravarAsync();
    }
}
