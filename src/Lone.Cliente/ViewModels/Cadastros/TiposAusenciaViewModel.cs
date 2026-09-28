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

/// <summary>Linha da tabela de tipos de ausência (Motor Comercial, Fase 1c).</summary>
public sealed class LinhaTipoAusencia
{
    public LinhaTipoAusencia(TipoAusenciaDto item) => Item = item;

    public TipoAusenciaDto Item { get; }
    public Guid Id => Item.Id;
    public string Nome => Item.Nome;
    public string Detalhe => string.Empty;
    public string Usos => Item.QuantidadeUsos.ToString("N0", TextoTela.Brasil);
    public string Ativo => Item.Ativo ? "Sim" : "Não";
}

/// <summary>Ficha de um tipo de ausência. As regras finais (nome único) são da API.</summary>
public sealed partial class TipoAusenciaEdicao : ObservableObject
{
    private TipoAusenciaEdicao(Guid id, bool novo)
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

    public string Titulo => string.IsNullOrWhiteSpace(Nome) ? "Novo tipo de ausência" : Nome;
    public string SituacaoTexto => Novo ? "Novo tipo de ausência" : Ativo ? "Ativo" : "Desativado (não aparece para novas escolhas; continua onde já está)";
    public string UsoTexto => Novo ? string.Empty : $"{QuantidadeUsos.ToString("N0", TextoTela.Brasil)} cobertura(s) com este tipo (fora as canceladas).";

    public static TipoAusenciaEdicao Criar() => new(IdSequencial.Novo(), novo: true);

    public static TipoAusenciaEdicao De(TipoAusenciaDto t) => new(t.Id, novo: false)
    {
        Versao = t.Versao,
        Ativo = t.Ativo,
        QuantidadeUsos = t.QuantidadeUsos,
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

    public TipoAusenciaDto ParaDto() => new()
    {
        Id = Id,
        Versao = Versao,
        Nome = Nome.Trim(),
        Ordem = TextoTela.TentarInteiro(Ordem, out var ordem) ? ordem ?? 0 : 0,
        Ativo = Ativo
    };
}

/// <summary>Tipos de ausência (férias, folga, licença...). Nada é excluído: desativar esconde das escolhas novas.</summary>
public sealed partial class TiposAusenciaViewModel : CadastroViewModelBase<LinhaTipoAusencia>
{
    private readonly ComercialApi _api;

    public TiposAusenciaViewModel(ComercialApi api, IDialogos dialogos) : base(dialogos)
    {
        _api = api;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeDesativar), nameof(PodeReativar))]
    private TipoAusenciaEdicao? _formulario;

    public bool PodeDesativar => Formulario is { Novo: false, Ativo: true };
    public bool PodeReativar => Formulario is { Novo: false, Ativo: false };

    protected override string TextoDeBusca(LinhaTipoAusencia item) => item.Nome + " " + item.Detalhe;

    protected override async Task<IReadOnlyList<LinhaTipoAusencia>> ListarAsync()
    {
        return (await _api.ListarAsync<TipoAusenciaDto>(Rotas.Comercial.TiposAusencia, incluirInativos: true))
            .OrderBy(t => t.Ordem).ThenBy(t => t.Nome, StringComparer.CurrentCultureIgnoreCase)
            .Select(t => new LinhaTipoAusencia(t)).ToList();
    }

    private static TipoAusenciaEdicao Preparar(TipoAusenciaEdicao f) => f;

    protected override async Task AbrirAsync(LinhaTipoAusencia item) => Formulario = await ObterAsync(item.Id);

    protected override Task NovoItemAsync()
    {
        Formulario = Preparar(TipoAusenciaEdicao.Criar());
        return Task.CompletedTask;
    }

    protected override object? DadosDaFicha() => Formulario?.ParaDto();
    protected override bool FichaNova => Formulario?.Novo ?? true;

    protected override async Task RecarregarFichaAsync()
    {
        if (Formulario is not { Novo: false } formulario) return;
        Formulario = await ObterAsync(formulario.Id);
    }

    private async Task<TipoAusenciaEdicao> ObterAsync(Guid id) =>
        Preparar(TipoAusenciaEdicao.De(await _api.ObterAsync<TipoAusenciaDto>(Rotas.Comercial.TiposAusencia, id) ?? throw new ValidacaoException(["Este cadastro não existe mais."])));

    [RelayCommand]
    private async Task SalvarAsync()
    {
        if (Formulario is not { } formulario) return;
        if (formulario.ValidarLocalmente() is { Count: > 0 } erros)
        {
            Mostrar(string.Join(Environment.NewLine, erros), TipoMensagem.Erro);
            return;
        }

        TipoAusenciaDto? salvo = null;
        if (!await ExecutarAsync(async () => salvo = await _api.SalvarAsync(Rotas.Comercial.TiposAusencia, formulario.Id, formulario.ParaDto())))
            return;

        await AtualizarListaAposGravarAsync();
        Formulario = Preparar(TipoAusenciaEdicao.De(salvo!));
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

        TipoAusenciaDto? gravado = null;
        if (!await ExecutarAsync(async () => gravado = desativar
                ? await _api.DesativarAsync<TipoAusenciaDto>(Rotas.Comercial.TiposAusencia, formulario.Id, formulario.Versao)
                : await _api.ReativarAsync<TipoAusenciaDto>(Rotas.Comercial.TiposAusencia, formulario.Id, formulario.Versao)))
            return;

        await AtualizarListaAposGravarAsync();
        Formulario = Preparar(TipoAusenciaEdicao.De(gravado!));
        MarcarFichaSemAlteracoes();
        Mostrar(desativar ? "Desativado." : "Reativado.", TipoMensagem.Sucesso);
    }
}
