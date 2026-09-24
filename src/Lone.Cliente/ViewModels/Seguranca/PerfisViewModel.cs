using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.Api;
using Lone.Cliente.Plataforma;
using Lone.Cliente.ViewModels.Cadastros;
using Lone.Contracts.Seguranca;
using Lone.Domain.Validacao;

namespace Lone.Cliente.ViewModels.Seguranca;

/// <summary>Tela de perfis de acesso: lista e ficha com as permissões agrupadas por módulo.</summary>
public sealed partial class PerfisViewModel : CadastroViewModelBase<PerfilResumo>
{
    private readonly PerfisApi _api;
    private IReadOnlyList<DefinicaoPermissao> _catalogo = [];

    public PerfisViewModel(PerfisApi api, IDialogos dialogos) : base(dialogos)
    {
        _api = api;
    }

    [ObservableProperty] private PerfilFormulario? _formulario;

    protected override string TextoDeBusca(PerfilResumo item) => item.Nome;

    protected override async Task AntesDeListarAsync() => _catalogo = await _api.ListarPermissoesAsync();

    protected override async Task<IReadOnlyList<PerfilResumo>> ListarAsync() =>
        (await _api.ListarAsync()).OrderBy(p => p.Nome).ToList();

    protected override async Task AbrirAsync(PerfilResumo item)
    {
        var dto = await _api.ObterAsync(item.Id) ?? throw new ValidacaoException(["Este perfil não existe mais."]);
        Formulario = PerfilFormulario.De(dto, _catalogo);
    }

    protected override Task NovoItemAsync()
    {
        Formulario = PerfilFormulario.NovoPerfil(_catalogo);
        return Task.CompletedTask;
    }

    protected override object? DadosDaFicha() => Formulario?.ParaDto();
    protected override bool FichaNova => Formulario?.Novo ?? true;

    protected override async Task RecarregarFichaAsync()
    {
        if (Formulario is not { Novo: false } formulario) return;
        var dto = await _api.ObterAsync(formulario.Id) ?? throw new ValidacaoException(["Este perfil não existe mais."]);
        Formulario = PerfilFormulario.De(dto, _catalogo);
    }

    [RelayCommand]
    private async Task SalvarAsync()
    {
        if (Formulario is not { } formulario) return;

        PerfilDto? salvo = null;
        if (!await ExecutarAsync(async () => salvo = await _api.SalvarAsync(formulario.ParaDto())))
            return;

        Formulario = PerfilFormulario.De(salvo!, _catalogo);
        MarcarFichaSemAlteracoes();
        Mostrar(formulario.Novo ? "Perfil cadastrado." : "Alterações salvas.", TipoMensagem.Sucesso);
        await AtualizarListaAposGravarAsync();
    }
}
