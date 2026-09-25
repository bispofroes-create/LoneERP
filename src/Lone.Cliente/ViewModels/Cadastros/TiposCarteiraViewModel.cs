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

/// <summary>Linha da tabela de tipos de carteira.</summary>
public sealed class LinhaTipoCarteira
{
    public LinhaTipoCarteira(TipoCarteiraDto item) => Item = item;

    public TipoCarteiraDto Item { get; }
    public Guid Id => Item.Id;
    public string Nome => Item.Nome;
    public string Detalhe => Item.Principal ? "principal (define o vendedor padrão)" : string.Empty;
    public string Usos => Item.QuantidadeUsos.ToString("N0", TextoTela.Brasil);
    public string Ativo => Item.Ativo ? "Sim" : "Não";
}

/// <summary>Ficha de um tipo de carteira. As regras finais (nome único, árvore) são da API.</summary>
public sealed partial class TipoCarteiraEdicao : ObservableObject
{
    private TipoCarteiraEdicao(Guid id, bool novo)
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
    [ObservableProperty] private bool _principal;
    [ObservableProperty] private string _ordem = string.Empty;

    public string Titulo => string.IsNullOrWhiteSpace(Nome) ? "Novo tipo de carteira" : Nome;
    public string SituacaoTexto => Novo ? "Novo tipo de carteira" : Ativo ? "Ativo" : "Desativado (não aparece para novas escolhas; continua onde já está)";
    public string UsoTexto => Novo ? string.Empty : $"{QuantidadeUsos.ToString("N0", TextoTela.Brasil)} vínculo(s) em aberto na carteira com este tipo.";

    public static TipoCarteiraEdicao Criar() => new(IdSequencial.Novo(), novo: true);

    public static TipoCarteiraEdicao De(TipoCarteiraDto t) => new(t.Id, novo: false)
    {
        Versao = t.Versao,
        Ativo = t.Ativo,
        QuantidadeUsos = t.QuantidadeUsos,
        Principal = t.Principal,
        Ordem = t.Ordem.ToString(System.Globalization.CultureInfo.InvariantCulture),
        Nome = t.Nome
    };

    public IReadOnlyList<string> ValidarLocalmente()
    {
        var erros = new List<string>();
        if (string.IsNullOrWhiteSpace(Nome)) erros.Add("Informe o nome.");
        if (!TextoTela.TentarInteiro(Ordem, out _)) erros.Add("Ordem: use um número inteiro.");
        return erros;
    }

    public TipoCarteiraDto ParaDto() => new()
    {
        Id = Id,
        Versao = Versao,
        Nome = Nome.Trim(),
        Principal = Principal,
        Ordem = TextoTela.TentarInteiro(Ordem, out var ordem) ? ordem ?? 0 : 0,
        Ativo = Ativo
    };
}

/// <summary>Tipos de carteira (cadastro comercial). Nada é excluído: desativar esconde das escolhas novas.</summary>
public sealed partial class TiposCarteiraViewModel : CadastroViewModelBase<LinhaTipoCarteira>
{
    private readonly ComercialApi _api;

    public TiposCarteiraViewModel(ComercialApi api, IDialogos dialogos) : base(dialogos)
    {
        _api = api;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeDesativar), nameof(PodeReativar))]
    private TipoCarteiraEdicao? _formulario;

    public bool PodeDesativar => Formulario is { Novo: false, Ativo: true };
    public bool PodeReativar => Formulario is { Novo: false, Ativo: false };

    protected override string TextoDeBusca(LinhaTipoCarteira item) => item.Nome + " " + item.Detalhe;

    protected override async Task<IReadOnlyList<LinhaTipoCarteira>> ListarAsync()
    {
        return (await _api.ListarAsync<TipoCarteiraDto>(Rotas.Comercial.TiposCarteira, incluirInativos: true))
            .OrderBy(t => t.Ordem).ThenBy(t => t.Nome, StringComparer.CurrentCultureIgnoreCase)
            .Select(t => new LinhaTipoCarteira(t)).ToList();
    }

    private TipoCarteiraEdicao Preparar(TipoCarteiraEdicao f)
    {
        
        return f;
    }

    protected override async Task AbrirAsync(LinhaTipoCarteira item) => Formulario = await ObterAsync(item.Id);

    protected override Task NovoItemAsync()
    {
        Formulario = Preparar(TipoCarteiraEdicao.Criar());
        return Task.CompletedTask;
    }

    protected override object? DadosDaFicha() => Formulario?.ParaDto();
    protected override bool FichaNova => Formulario?.Novo ?? true;

    protected override async Task RecarregarFichaAsync()
    {
        if (Formulario is not { Novo: false } formulario) return;
        Formulario = await ObterAsync(formulario.Id);
    }

    private async Task<TipoCarteiraEdicao> ObterAsync(Guid id) =>
        Preparar(TipoCarteiraEdicao.De(await _api.ObterAsync<TipoCarteiraDto>(Rotas.Comercial.TiposCarteira, id) ?? throw new ValidacaoException(["Este cadastro não existe mais."])));

    [RelayCommand]
    private async Task SalvarAsync()
    {
        if (Formulario is not { } formulario) return;
        if (formulario.ValidarLocalmente() is { Count: > 0 } erros)
        {
            Mostrar(string.Join(Environment.NewLine, erros), TipoMensagem.Erro);
            return;
        }

        TipoCarteiraDto? salvo = null;
        if (!await ExecutarAsync(async () => salvo = await _api.SalvarAsync(Rotas.Comercial.TiposCarteira, formulario.Id, formulario.ParaDto())))
            return;

        await AtualizarListaAposGravarAsync();
        Formulario = Preparar(TipoCarteiraEdicao.De(salvo!));
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

        TipoCarteiraDto? gravado = null;
        if (!await ExecutarAsync(async () => gravado = desativar
                ? await _api.DesativarAsync<TipoCarteiraDto>(Rotas.Comercial.TiposCarteira, formulario.Id, formulario.Versao)
                : await _api.ReativarAsync<TipoCarteiraDto>(Rotas.Comercial.TiposCarteira, formulario.Id, formulario.Versao)))
            return;

        await AtualizarListaAposGravarAsync();
        Formulario = Preparar(TipoCarteiraEdicao.De(gravado!));
        MarcarFichaSemAlteracoes();
        Mostrar(desativar ? "Desativado." : "Reativado.", TipoMensagem.Sucesso);
    }
}
