using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.Api;
using Lone.Cliente.Plataforma;
using Lone.Cliente.ViewModels.Cadastros;
using Lone.Contracts.Seguranca;
using Lone.Domain.Validacao;

namespace Lone.Cliente.ViewModels.Seguranca;

/// <summary>Tela de usuários: lista com busca e ficha com dados, perfis por empresa, senha e desbloqueio.</summary>
public sealed partial class UsuariosViewModel : CadastroViewModelBase<UsuarioResumo>
{
    private readonly UsuariosApi _api;

    public UsuariosViewModel(UsuariosApi api, IDialogos dialogos) : base(dialogos)
    {
        _api = api;
    }

    /// <summary>Perfis que podem ser dados (lista de escolha).</summary>
    public ObservableCollection<OpcaoPerfil> PerfisDisponiveis { get; } = new();

    /// <summary>"Todas as empresas" e cada empresa do grupo.</summary>
    public ObservableCollection<OpcaoEmpresa> EmpresasDisponiveis { get; } = new();

    [ObservableProperty] private UsuarioFormulario? _formulario;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AdicionarPerfilCommand))]
    private OpcaoPerfil? _perfilParaAdicionar;

    [ObservableProperty] private OpcaoEmpresa? _empresaParaAdicionar;

    protected override string TextoDeBusca(UsuarioResumo item) => $"{item.Nome} {item.Login}";

    protected override async Task AntesDeListarAsync()
    {
        var perfis = await _api.ListarPerfisAsync();
        var empresas = await _api.ListarEmpresasAsync();

        PerfisDisponiveis.Clear();
        foreach (var perfil in perfis.OrderBy(p => p.Nome)) PerfisDisponiveis.Add(OpcaoPerfil.De(perfil));

        EmpresasDisponiveis.Clear();
        EmpresasDisponiveis.Add(OpcaoEmpresa.Todas);
        foreach (var empresa in empresas.OrderBy(e => e.Nome)) EmpresasDisponiveis.Add(OpcaoEmpresa.De(empresa));
        EmpresaParaAdicionar = OpcaoEmpresa.Todas;
    }

    protected override async Task<IReadOnlyList<UsuarioResumo>> ListarAsync() =>
        (await _api.ListarAsync()).OrderBy(u => u.Nome).ToList();

    protected override async Task AbrirAsync(UsuarioResumo item)
    {
        var dto = await _api.ObterAsync(item.Id)
                  ?? throw new ValidacaoException(["Este usuário não existe mais."]);
        Formulario = UsuarioFormulario.De(dto, PerfisDisponiveis, EmpresasDisponiveis);
    }

    protected override Task NovoItemAsync()
    {
        Formulario = UsuarioFormulario.NovoUsuario();
        return Task.CompletedTask;
    }

    protected override object? DadosDaFicha() => Formulario?.ParaRequisicao();
    protected override object? FichaObservada => Formulario;
    protected override bool FichaNova => Formulario?.Novo ?? true;

    protected override async Task RecarregarFichaAsync()
    {
        if (Formulario is not { Novo: false } formulario) return;
        var dto = await _api.ObterAsync(formulario.Id) ?? throw new ValidacaoException(["Este usuário não existe mais."]);
        Formulario = UsuarioFormulario.De(dto, PerfisDisponiveis, EmpresasDisponiveis);
    }

    private bool PodeAdicionarPerfil() => PerfilParaAdicionar is not null;

    [RelayCommand(CanExecute = nameof(PodeAdicionarPerfil))]
    private void AdicionarPerfil()
    {
        if (Formulario is null || PerfilParaAdicionar is null) return;

        var atribuido = new PerfilAtribuido(PerfilParaAdicionar, EmpresaParaAdicionar ?? OpcaoEmpresa.Todas);
        if (Formulario.Perfis.Any(p => p.Perfil.Id == atribuido.Perfil.Id && p.Empresa.Id == atribuido.Empresa.Id))
        {
            Mostrar("Este perfil já foi dado para esta empresa.", TipoMensagem.Aviso);
            return;
        }

        Formulario.Perfis.Add(atribuido);
        PerfilParaAdicionar = null;
    }

    [RelayCommand]
    private void RemoverPerfil(PerfilAtribuido? perfil)
    {
        if (perfil is not null) Formulario?.Perfis.Remove(perfil);
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

        UsuarioDto? salvo = null;
        if (!await ExecutarAsync(async () => salvo = await _api.SalvarAsync(formulario.ParaRequisicao())))
            return;

        // A ficha passa a refletir o gravado (versão nova, sem a senha digitada) antes de reler a lista.
        Formulario = UsuarioFormulario.De(salvo!, PerfisDisponiveis, EmpresasDisponiveis);
        MarcarFichaSemAlteracoes();
        Mostrar(formulario.Novo ? "Usuário cadastrado." : "Alterações salvas.", TipoMensagem.Sucesso);
        await AtualizarListaAposGravarAsync();
    }

    /// <summary>Busca a pessoa para ligar ao usuário (ao menos 2 letras: nome, código, CPF ou CNPJ).</summary>
    [RelayCommand]
    private async Task BuscarPessoaAsync()
    {
        if (Formulario is not { } formulario) return;
        var texto = formulario.BuscaPessoa.Trim();
        if (texto.Length < 2)
        {
            Mostrar("Digite ao menos 2 letras (nome, código, CPF ou CNPJ) para buscar.", TipoMensagem.Aviso);
            return;
        }

        List<Lone.Contracts.Colaboradores.PessoaOpcaoDto>? achadas = null;
        if (!await ExecutarAsync(async () => achadas = await _api.BuscarPessoasAsync(texto))) return;
        formulario.DefinirResultadosPessoa(achadas!);
        if (achadas!.Count == 0) Mostrar("Nenhuma pessoa ativa encontrada.", TipoMensagem.Informacao);
    }

    [RelayCommand]
    private async Task DesbloquearAsync()
    {
        if (Formulario is not { Novo: false } formulario) return;

        if (!await ExecutarAsync(async () =>
            {
                await _api.DesbloquearAsync(formulario.Id);
                var dto = await _api.ObterAsync(formulario.Id) ?? throw new ValidacaoException(["Este usuário não existe mais."]);
                Formulario = UsuarioFormulario.De(dto, PerfisDisponiveis, EmpresasDisponiveis);
            }))
            return;
        MarcarFichaSemAlteracoes();

        Mostrar("Usuário desbloqueado.", TipoMensagem.Sucesso);
        await AtualizarListaAposGravarAsync();
    }
}
