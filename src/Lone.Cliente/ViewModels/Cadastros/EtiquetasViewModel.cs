using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.Api;
using Lone.Cliente.Plataforma;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Etiquetas;
using Lone.Domain.Comum;
using Lone.Domain.Validacao;

namespace Lone.Cliente.ViewModels.Cadastros;

/// <summary>Linha da tabela de etiquetas (Etiqueta · Cadastros · Ativa).</summary>
public sealed class LinhaEtiqueta
{
    public LinhaEtiqueta(EtiquetaDto etiqueta) => Etiqueta = etiqueta;

    public EtiquetaDto Etiqueta { get; }
    public Guid Id => Etiqueta.Id;
    public string Nome => Etiqueta.Nome;
    public string Cadastros => Etiqueta.QuantidadePessoas.ToString("N0", TextoTela.Brasil);
    public string Ativa => Etiqueta.Ativo ? "Sim" : "Não";
    public bool Inativa => !Etiqueta.Ativo;
}

/// <summary>Ficha de uma etiqueta. As regras finais (nome único sem maiúsculas/acentos) são da API.</summary>
public sealed partial class EtiquetaEdicao : ObservableObject
{
    private EtiquetaEdicao(Guid id, bool nova)
    {
        Id = id;
        Nova = nova;
    }

    public Guid Id { get; }
    public bool Nova { get; }
    public byte[]? Versao { get; private set; }
    public bool Ativo { get; private set; } = true;
    public int QuantidadePessoas { get; private set; }

    [ObservableProperty][NotifyPropertyChangedFor(nameof(Titulo))] private string _nome = string.Empty;
    [ObservableProperty] private string _descricao = string.Empty;

    public string Titulo => string.IsNullOrWhiteSpace(Nome) ? "Nova etiqueta" : Nome;
    public string SituacaoTexto => Nova ? "Nova etiqueta"
        : Ativo ? "Ativa"
        : "Desativada (não aparece para novas marcações; continua nos cadastros que já a têm)";
    public string UsoTexto => Nova ? string.Empty
        : QuantidadePessoas switch { 0 => "Nenhum cadastro com esta etiqueta.", 1 => "1 cadastro com esta etiqueta.", var n => $"{n.ToString("N0", TextoTela.Brasil)} cadastros com esta etiqueta." };

    public static EtiquetaEdicao Criar() => new(IdSequencial.Novo(), nova: true);

    public static EtiquetaEdicao De(EtiquetaDto e) => new(e.Id, nova: false)
    {
        Versao = e.Versao,
        Ativo = e.Ativo,
        QuantidadePessoas = e.QuantidadePessoas,
        Nome = e.Nome,
        Descricao = e.Descricao ?? string.Empty
    };

    public IReadOnlyList<string> ValidarLocalmente() =>
        string.IsNullOrWhiteSpace(Nome) ? ["Informe o nome da etiqueta."] : [];

    public EtiquetaDto ParaDto() => new()
    {
        Id = Id,
        Versao = Versao,
        Nome = Nome.Trim(),
        Descricao = TextoTela.Nulo(Descricao)?.Trim(),
        Ativo = Ativo,
        QuantidadePessoas = QuantidadePessoas
    };
}

/// <summary>
/// Cadastro de etiquetas: tabela (etiqueta, quantos cadastros a usam, ativa) e ficha para criar, alterar,
/// desativar, reativar e mesclar. Nada é excluído: mesclar passa os cadastros para outra etiqueta e desativa esta.
/// </summary>
public sealed partial class EtiquetasViewModel : CadastroViewModelBase<LinhaEtiqueta>
{
    private readonly EtiquetasApi _api;

    public EtiquetasViewModel(EtiquetasApi api, IDialogos dialogos) : base(dialogos)
    {
        _api = api;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeDesativar), nameof(PodeReativar), nameof(PodeMesclar))]
    private EtiquetaEdicao? _formulario;

    /// <summary>Etiquetas ativas que podem receber os cadastros desta (array: o Picker precisa de IList).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeMesclar))]
    private Opcao<Guid>[] _destinosMesclagem = [];

    [ObservableProperty] private Opcao<Guid>? _destinoMesclagem;

    public bool PodeDesativar => Formulario is { Nova: false, Ativo: true };
    public bool PodeReativar => Formulario is { Nova: false, Ativo: false };
    public bool PodeMesclar => Formulario is { Nova: false } && DestinosMesclagem.Length > 0;

    protected override string TextoDeBusca(LinhaEtiqueta item) => item.Nome;

    /// <summary>Lista inteira mais recente (a busca só filtra o que aparece; os destinos da mesclagem saem daqui).</summary>
    private IReadOnlyList<EtiquetaDto> _cadastro = [];

    protected override async Task<IReadOnlyList<LinhaEtiqueta>> ListarAsync()
    {
        _cadastro = await _api.ListarAsync(incluirInativas: true);
        return _cadastro
            .OrderBy(e => !e.Ativo).ThenBy(e => e.Nome, StringComparer.CurrentCultureIgnoreCase)
            .Select(e => new LinhaEtiqueta(e)).ToList();
    }

    protected override async Task AbrirAsync(LinhaEtiqueta item) => ExibirFicha(await ObterAsync(item.Id));

    protected override Task NovoItemAsync()
    {
        ExibirFicha(EtiquetaEdicao.Criar());
        return Task.CompletedTask;
    }

    protected override object? DadosDaFicha() => Formulario?.ParaDto();
    protected override object? FichaObservada => Formulario;
    protected override bool FichaNova => Formulario?.Nova ?? true;

    protected override async Task RecarregarFichaAsync()
    {
        if (Formulario is not { Nova: false } formulario) return;
        ExibirFicha(await ObterAsync(formulario.Id));
    }

    private async Task<EtiquetaEdicao> ObterAsync(Guid id) =>
        EtiquetaEdicao.De(await _api.ObterAsync(id) ?? throw new ValidacaoException(["Esta etiqueta não existe mais."]));

    /// <summary>Mostra a ficha e monta a lista de destinos da mesclagem (as outras etiquetas ativas da lista).</summary>
    private void ExibirFicha(EtiquetaEdicao ficha)
    {
        Formulario = ficha;
        DestinoMesclagem = null;
        DestinosMesclagem = ficha.Nova
            ? []
            : _cadastro
                .Where(e => e.Ativo && e.Id != ficha.Id)
                .OrderBy(e => e.Nome, StringComparer.CurrentCultureIgnoreCase)
                .Select(e => new Opcao<Guid>(e.Id, e.Nome))
                .ToArray();
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

        EtiquetaDto? salva = null;
        if (!await ExecutarAsync(async () => salva = await _api.SalvarAsync(formulario.ParaDto())))
            return;

        await AtualizarListaAposGravarAsync();
        ExibirFicha(EtiquetaEdicao.De(salva!));
        MarcarFichaSemAlteracoes();
        Mostrar(formulario.Nova ? "Etiqueta criada. Ela já pode ser marcada nos cadastros de pessoas." : "Alterações salvas.",
            TipoMensagem.Sucesso);
    }

    [RelayCommand]
    private Task DesativarAsync() => AlterarSituacaoAsync(desativar: true);

    [RelayCommand]
    private Task ReativarAsync() => AlterarSituacaoAsync(desativar: false);

    private async Task AlterarSituacaoAsync(bool desativar)
    {
        if (Formulario is not { Nova: false } formulario) return;
        if (TemAlteracoes)
        {
            Mostrar("Salve ou descarte as alterações antes de " + (desativar ? "desativar" : "reativar") + " a etiqueta.", TipoMensagem.Aviso);
            return;
        }

        if (desativar && !await ConfirmarAsync(
                "Desativar etiqueta",
                $"\"{formulario.Nome}\" deixará de aparecer para novas marcações. Os cadastros que já a têm continuam com ela, e ela pode ser reativada.",
                "Desativar", "Cancelar"))
            return;

        EtiquetaDto? gravada = null;
        if (!await ExecutarAsync(async () => gravada = desativar
                ? await _api.DesativarAsync(formulario.Id, formulario.Versao)
                : await _api.ReativarAsync(formulario.Id, formulario.Versao)))
            return;

        await AtualizarListaAposGravarAsync();
        ExibirFicha(EtiquetaEdicao.De(gravada!));
        MarcarFichaSemAlteracoes();
        Mostrar(desativar ? "Etiqueta desativada." : "Etiqueta reativada.", TipoMensagem.Sucesso);
    }

    /// <summary>
    /// Passa todos os cadastros desta etiqueta para a escolhida e desativa esta (para juntar duplicadas como
    /// "VIP" e "Cliente VIP"). Cada cadastro alterado fica com a mudança no próprio histórico.
    /// </summary>
    [RelayCommand]
    private async Task MesclarAsync()
    {
        if (Formulario is not { Nova: false } formulario) return;
        if (TemAlteracoes)
        {
            Mostrar("Salve ou descarte as alterações antes de mesclar a etiqueta.", TipoMensagem.Aviso);
            return;
        }
        if (DestinoMesclagem is not { } destino)
        {
            Mostrar("Escolha a etiqueta que vai receber os cadastros.", TipoMensagem.Aviso);
            return;
        }

        if (!await ConfirmarAsync(
                "Mesclar etiquetas",
                $"Os cadastros com \"{formulario.Nome}\" ({formulario.QuantidadePessoas.ToString("N0", TextoTela.Brasil)}) passarão para \"{destino.Texto}\", " +
                $"e \"{formulario.Nome}\" será desativada. A mudança fica no histórico de cada cadastro. Continuar?",
                "Mesclar", "Cancelar"))
            return;

        ResultadoMesclarEtiqueta? resultado = null;
        if (!await ExecutarAsync(async () => resultado = await _api.MesclarAsync(formulario.Id, destino.Valor, formulario.Versao)))
            return;

        await AtualizarListaAposGravarAsync();
        ExibirFicha(EtiquetaEdicao.De(resultado!.Origem));
        MarcarFichaSemAlteracoes();
        Mostrar($"{resultado.CadastrosAlterados.ToString("N0", TextoTela.Brasil)} cadastro(s) passaram para \"{resultado.Destino.Nome}\". " +
                $"\"{resultado.Origem.Nome}\" foi desativada.", TipoMensagem.Sucesso);
    }
}
