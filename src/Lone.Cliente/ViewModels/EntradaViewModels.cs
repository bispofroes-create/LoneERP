using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.Api;
using Lone.Cliente.Navegacao;
using Lone.Cliente.Sessao;
using Lone.Contracts.Empresas;

namespace Lone.Cliente.ViewModels;

/// <summary>Tela de login, com o endereço do servidor editável (para instalar em outra rede).</summary>
public partial class LoginViewModel : ViewModelBase
{
    private readonly ServicoAutenticacao _autenticacao;
    private readonly FluxoDeEntrada _fluxo;
    private readonly INavegacao _navegacao;
    private readonly ConfiguracaoServidor _servidor;

    public LoginViewModel(ServicoAutenticacao autenticacao, FluxoDeEntrada fluxo, INavegacao navegacao, ConfiguracaoServidor servidor)
    {
        _autenticacao = autenticacao;
        _fluxo = fluxo;
        _navegacao = navegacao;
        _servidor = servidor;
        EnderecoServidor = servidor.Endereco;
    }

    [ObservableProperty] private string _login = string.Empty;
    [ObservableProperty] private string _senha = string.Empty;
    [ObservableProperty] private string _enderecoServidor = string.Empty;
    [ObservableProperty] private bool _mostrarServidor;

    /// <summary>Mensagem trazida de outra tela (ex.: "Sua sessão expirou").</summary>
    public void Receber(string? mensagem)
    {
        if (!string.IsNullOrEmpty(mensagem))
            Mostrar(mensagem, TipoMensagem.Aviso);
    }

    [RelayCommand]
    private void AlternarServidor() => MostrarServidor = !MostrarServidor;

    [RelayCommand]
    private async Task EntrarAsync()
    {
        if (string.IsNullOrWhiteSpace(Login) || string.IsNullOrEmpty(Senha))
        {
            Mostrar("Informe login e senha.", TipoMensagem.Aviso);
            return;
        }

        if (_servidor.Definir(EnderecoServidor) is { } erroEndereco)
        {
            Mostrar(erroEndereco, TipoMensagem.Erro);
            MostrarServidor = true;
            return;
        }

        var entrou = await ExecutarAsync(() => _autenticacao.EntrarAsync(Login, Senha));
        Senha = string.Empty;
        if (entrou)
            await _navegacao.IrParaAsync(_fluxo.Proxima());
    }

    /// <summary>Servidor novo e vazio: vai para a criação do primeiro administrador.</summary>
    [RelayCommand]
    private async Task VerificarPrimeiroAcessoAsync()
    {
        if (_servidor.Definir(EnderecoServidor) is { } erroEndereco)
        {
            Mostrar(erroEndereco, TipoMensagem.Erro);
            return;
        }

        var existe = true;
        if (await ExecutarAsync(async () => existe = await _autenticacao.ExisteUsuarioAsync()))
        {
            if (existe)
                Mostrar("Este servidor já tem usuários. Entre com o seu login.", TipoMensagem.Informacao);
            else
                await _navegacao.IrParaAsync(Tela.PrimeiroAcesso);
        }
    }
}

/// <summary>Primeiro acesso: cria o administrador num servidor sem usuários.</summary>
public partial class PrimeiroAcessoViewModel : ViewModelBase
{
    private readonly ServicoAutenticacao _autenticacao;
    private readonly FluxoDeEntrada _fluxo;
    private readonly INavegacao _navegacao;

    public PrimeiroAcessoViewModel(ServicoAutenticacao autenticacao, FluxoDeEntrada fluxo, INavegacao navegacao)
    {
        _autenticacao = autenticacao;
        _fluxo = fluxo;
        _navegacao = navegacao;
    }

    [ObservableProperty] private string _nome = string.Empty;
    [ObservableProperty] private string _login = string.Empty;
    [ObservableProperty] private string _senha = string.Empty;
    [ObservableProperty] private string _confirmacao = string.Empty;

    [RelayCommand]
    private async Task CriarAsync()
    {
        if (Senha != Confirmacao)
        {
            Mostrar("A confirmação não confere com a senha.", TipoMensagem.Erro);
            return;
        }

        if (await ExecutarAsync(() => _autenticacao.CriarPrimeiroAdministradorAsync(Nome, Login, Senha)))
        {
            Senha = Confirmacao = string.Empty;
            await _navegacao.IrParaAsync(_fluxo.Proxima());
        }
    }

    [RelayCommand]
    private Task VoltarAsync() => _navegacao.IrParaAsync(Tela.Login);
}

/// <summary>Escolha da empresa (estabelecimento) em que o usuário vai trabalhar.</summary>
public partial class EscolherEmpresaViewModel : ViewModelBase
{
    private readonly ServicoAutenticacao _autenticacao;
    private readonly SessaoCliente _sessao;
    private readonly FluxoDeEntrada _fluxo;
    private readonly INavegacao _navegacao;

    public EscolherEmpresaViewModel(ServicoAutenticacao autenticacao, SessaoCliente sessao, FluxoDeEntrada fluxo, INavegacao navegacao)
    {
        _autenticacao = autenticacao;
        _sessao = sessao;
        _fluxo = fluxo;
        _navegacao = navegacao;
        Empresas = _sessao.EmpresasDisponiveis;
        Selecionada = _sessao.EmpresaAtiva;
    }

    public IReadOnlyList<EmpresaAtiva> Empresas { get; }
    public string NomeUsuario => _sessao.NomeUsuario;
    public bool SemEmpresas => Empresas.Count == 0;
    public bool TemEmpresas => Empresas.Count > 0;

    /// <summary>Já havia empresa ativa: "Voltar" retorna ao sistema em vez de sair.</summary>
    public bool PodeVoltar => _sessao.EmpresaAtiva is not null;

    public string TextoVoltar => PodeVoltar ? "Voltar" : "Sair";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmarCommand))]
    private EmpresaAtiva? _selecionada;

    private bool PodeConfirmar() => Selecionada is not null;

    [RelayCommand(CanExecute = nameof(PodeConfirmar))]
    private async Task ConfirmarAsync()
    {
        if (Selecionada is not { } empresa) return;
        if (await ExecutarAsync(() => _autenticacao.SelecionarEmpresaAsync(empresa.EstabelecimentoId)))
            await _navegacao.IrParaAsync(_fluxo.Proxima());
    }

    /// <summary>Sem empresa cadastrada ainda (primeira instalação): entra para o administrador cadastrá-la.</summary>
    [RelayCommand]
    private Task ContinuarSemEmpresaAsync() => _navegacao.IrParaAsync(Tela.Sistema);

    [RelayCommand]
    private async Task VoltarOuSairAsync()
    {
        if (PodeVoltar)
        {
            await _navegacao.IrParaAsync(Tela.Sistema);
            return;
        }

        await _autenticacao.SairAsync();
        await _navegacao.IrParaAsync(Tela.Login);
    }
}

/// <summary>Troca de senha: obrigatória (senha definida pelo administrador) ou pedida pelo usuário no menu.</summary>
public partial class TrocarSenhaViewModel : ViewModelBase
{
    private readonly ServicoAutenticacao _autenticacao;
    private readonly SessaoCliente _sessao;
    private readonly FluxoDeEntrada _fluxo;
    private readonly INavegacao _navegacao;

    public TrocarSenhaViewModel(ServicoAutenticacao autenticacao, SessaoCliente sessao, FluxoDeEntrada fluxo, INavegacao navegacao)
    {
        _autenticacao = autenticacao;
        _sessao = sessao;
        _fluxo = fluxo;
        _navegacao = navegacao;
        Obrigatoria = sessao.DeveTrocarSenha;
    }

    public bool Obrigatoria { get; }

    public string Explicacao => Obrigatoria
        ? "Sua senha foi definida pelo administrador. Crie uma senha pessoal para continuar."
        : "Informe a senha atual e a nova senha.";

    public string TextoCancelar => Obrigatoria ? "Sair" : Concluida ? "Fechar" : "Cancelar";

    /// <summary>Troca pelo menu já feita: a tela fica aberta mostrando "Senha alterada." até o usuário fechar.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoCancelar), nameof(PodeSalvar))]
    [NotifyCanExecuteChangedFor(nameof(SalvarCommand))]
    private bool _concluida;

    public bool PodeSalvar => !Concluida;

    [ObservableProperty] private string _senhaAtual = string.Empty;
    [ObservableProperty] private string _novaSenha = string.Empty;
    [ObservableProperty] private string _confirmacao = string.Empty;

    [RelayCommand(CanExecute = nameof(PodeSalvar))]
    private async Task SalvarAsync()
    {
        if (NovaSenha != Confirmacao)
        {
            Mostrar("A confirmação não confere com a nova senha.", TipoMensagem.Erro);
            return;
        }

        if (!await ExecutarAsync(() => _autenticacao.TrocarSenhaAsync(SenhaAtual, NovaSenha)))
            return;

        SenhaAtual = NovaSenha = Confirmacao = string.Empty;
        if (Obrigatoria)
            await _navegacao.IrParaAsync(_fluxo.Proxima());
        else
        {
            Concluida = true;
            Mostrar("Senha alterada. Use a nova senha no próximo acesso.", TipoMensagem.Sucesso);
        }
    }

    [RelayCommand]
    private async Task CancelarAsync()
    {
        if (!Obrigatoria)
        {
            await _navegacao.FecharAsync();
            return;
        }

        await _autenticacao.SairAsync();
        await _navegacao.IrParaAsync(Tela.Login);
    }
}
