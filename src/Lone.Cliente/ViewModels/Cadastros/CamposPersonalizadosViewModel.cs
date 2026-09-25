using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.Api;
using Lone.Cliente.Plataforma;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Documentos;
using Lone.Domain.Enums;
using Lone.Domain.Validacao;

namespace Lone.Cliente.ViewModels.Cadastros;

/// <summary>
/// Administração dos campos personalizados das pessoas e dos documentos (escolhidos no topo): tabela (campo, tipo, obrigatório, ativo) e
/// ficha para criar, alterar, ordenar, desativar e reativar. Campos nunca são excluídos: desativar preserva os
/// valores gravados e o histórico.
/// </summary>
public sealed partial class CamposPersonalizadosViewModel : CadastroViewModelBase<LinhaCampoPersonalizado>
{
    private readonly CamposPersonalizadosApi _api;
    private readonly TiposDocumentoApi _tiposDocumentoApi;
    private List<TipoDocumentoDto> _tiposDocumento = [];

    public CamposPersonalizadosViewModel(CamposPersonalizadosApi api, TiposDocumentoApi tiposDocumentoApi, IDialogos dialogos) : base(dialogos)
    {
        _api = api;
        _tiposDocumentoApi = tiposDocumentoApi;
    }

    public static readonly Opcao<EntidadePersonalizavel>[] Escopos =
    [
        new(EntidadePersonalizavel.Pessoa, "Pessoas (aba Informações adicionais)"),
        new(EntidadePersonalizavel.Documento, "Documentos (por tipo de documento)")
    ];

    public IReadOnlyList<Opcao<EntidadePersonalizavel>> ListaEscopos => Escopos;

    /// <summary>De qual cadastro são os campos mostrados (pessoas ou documentos).</summary>
    [ObservableProperty] private Opcao<EntidadePersonalizavel> _escopo = Escopos[0];

    private EntidadePersonalizavel Entidade => Escopo.Valor;

    partial void OnEscopoChanged(Opcao<EntidadePersonalizavel> value)
    {
        if (TemAlteracoes)
        {
            Mostrar("Salve ou descarte as alterações do campo aberto antes de trocar o cadastro.", TipoMensagem.Aviso);
            return;
        }
        Formulario = null;
        _ = CarregarCommand.ExecuteAsync(null);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeDesativar), nameof(PodeReativar))]
    private CampoPersonalizadoEdicao? _formulario;

    public bool PodeDesativar => Formulario is { Nova: false, Ativo: true };
    public bool PodeReativar => Formulario is { Nova: false, Ativo: false };

    protected override string TextoDeBusca(LinhaCampoPersonalizado item) => item.Nome;

    protected override async Task<IReadOnlyList<LinhaCampoPersonalizado>> ListarAsync()
    {
        if (Entidade == EntidadePersonalizavel.Documento)
            _tiposDocumento = await _tiposDocumentoApi.ListarAsync(incluirInativos: true);
        var nomes = _tiposDocumento.ToDictionary(t => t.Id, t => t.Nome);
        return (await _api.ListarAsync(Entidade, incluirInativos: true))
            .OrderBy(c => c.Ordem).ThenBy(c => c.Nome)
            .Select(c => new LinhaCampoPersonalizado(c, c.TipoDocumentoId is { } t ? nomes.GetValueOrDefault(t) : null)).ToList();
    }

    private CampoPersonalizadoEdicao Preparar(CampoPersonalizadoEdicao f)
    {
        if (f.DeDocumento) f.DefinirTiposDocumento(_tiposDocumento);
        return f;
    }

    protected override async Task AbrirAsync(LinhaCampoPersonalizado item)
    {
        var dto = await _api.ObterAsync(item.Id) ?? throw new ValidacaoException(["Este campo não existe mais."]);
        Formulario = Preparar(CampoPersonalizadoEdicao.De(dto));
    }

    protected override Task NovoItemAsync()
    {
        Formulario = Preparar(CampoPersonalizadoEdicao.Novo(Entidade));
        return Task.CompletedTask;
    }

    protected override object? DadosDaFicha() => Formulario?.ParaDto();
    protected override bool FichaNova => Formulario?.Nova ?? true;

    protected override async Task RecarregarFichaAsync()
    {
        if (Formulario is not { Nova: false } formulario) return;
        var dto = await _api.ObterAsync(formulario.Id) ?? throw new ValidacaoException(["Este campo não existe mais."]);
        Formulario = Preparar(CampoPersonalizadoEdicao.De(dto));
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

        CampoPersonalizadoDto? salvo = null;
        if (!await ExecutarAsync(async () => salvo = await _api.SalvarAsync(formulario.ParaDto())))
            return;

        Formulario = Preparar(CampoPersonalizadoEdicao.De(salvo!));
        MarcarFichaSemAlteracoes();
        Mostrar(formulario.Nova
                ? (formulario.DeDocumento
                    ? "Campo criado. Ele já aparece nos documentos deste tipo (reabra a tela de Pessoas)."
                    : "Campo criado. Ele já aparece na aba \"Informações adicionais\" do cadastro de pessoas (reabra a tela de Pessoas).")
                : "Alterações salvas.",
            TipoMensagem.Sucesso);
        await AtualizarListaAposGravarAsync();
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
            Mostrar("Salve ou descarte as alterações antes de " + (desativar ? "desativar" : "reativar") + " o campo.", TipoMensagem.Aviso);
            return;
        }

        if (desativar && !await ConfirmarAsync(
                "Desativar campo",
                $"\"{formulario.Nome}\" deixará de aparecer nos cadastros. Os valores já gravados continuam guardados e o campo pode ser reativado.",
                "Desativar", "Cancelar"))
            return;

        CampoPersonalizadoDto? gravado = null;
        if (!await ExecutarAsync(async () => gravado = desativar
                ? await _api.DesativarAsync(formulario.Id, formulario.Versao)
                : await _api.ReativarAsync(formulario.Id, formulario.Versao)))
            return;

        Formulario = Preparar(CampoPersonalizadoEdicao.De(gravado!));
        MarcarFichaSemAlteracoes();
        Mostrar(desativar ? "Campo desativado." : "Campo reativado.", TipoMensagem.Sucesso);
        await AtualizarListaAposGravarAsync();
    }

    /// <summary>Sobe ou desce um campo na lista e grava a nova ordem.</summary>
    [RelayCommand]
    private Task SubirAsync(LinhaCampoPersonalizado? linha) => MoverAsync(linha, -1);

    [RelayCommand]
    private Task DescerAsync(LinhaCampoPersonalizado? linha) => MoverAsync(linha, +1);

    private async Task MoverAsync(LinhaCampoPersonalizado? linha, int passo)
    {
        if (linha is null || !string.IsNullOrWhiteSpace(Busca)) return; // com busca, a lista não mostra a ordem inteira
        if (TemAlteracoes)
        {
            Mostrar("Salve ou descarte as alterações do campo aberto antes de mudar a ordem.", TipoMensagem.Aviso);
            return;
        }

        var ordem = Itens.ToList();
        var indice = ordem.FindIndex(l => l.Id == linha.Id);
        var destino = indice + passo;
        if (indice < 0 || destino < 0 || destino >= ordem.Count) return;

        (ordem[indice], ordem[destino]) = (ordem[destino], ordem[indice]);
        // A ordem muda a versão dos campos: a ficha aberta é relida para não dar conflito ao salvar depois.
        if (!await ExecutarAsync(async () =>
            {
                await _api.ReordenarAsync(ordem.Select(l => l.Id).ToList(), Entidade);
                await RecarregarFichaAsync();
            }))
            return;
        if (Formulario is not null) MarcarFichaSemAlteracoes();
        await AtualizarListaAposGravarAsync();
    }
}
