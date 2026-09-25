using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.Api;
using Lone.Cliente.Navegacao;
using Lone.Cliente.Sessao;
using Lone.Contracts.Seguranca;

namespace Lone.Cliente.ViewModels;

/// <summary>
/// Menu principal: quem está logado, em qual empresa, e o que pode ver. Criado de novo a cada entrada
/// no sistema (inclusive ao trocar de empresa), então as permissões estão sempre atualizadas.
/// </summary>
public partial class MenuViewModel : ViewModelBase, IDisposable
{
    private readonly SessaoCliente _sessao;
    private readonly ServicoAutenticacao _autenticacao;
    private readonly INavegacao _navegacao;

    public MenuViewModel(SessaoCliente sessao, ServicoAutenticacao autenticacao, INavegacao navegacao)
    {
        _sessao = sessao;
        _autenticacao = autenticacao;
        _navegacao = navegacao;

        // Empresas do grupo ou permissões mudaram (sessão relida): o menu se atualiza sem sair do sistema.
        _sessao.Alterada += Sessao_Alterada;
    }

    private void Sessao_Alterada(object? sender, EventArgs e)
    {
        if (_sessao.Autenticada) OnPropertyChanged(string.Empty); // ao sair, o app já está trocando de tela
    }

    /// <summary>Chamado quando o menu sai da tela (sair, trocar de empresa).</summary>
    public void Dispose() => _sessao.Alterada -= Sessao_Alterada;

    public string NomeUsuario => _sessao.NomeUsuario;
    public string Login => _sessao.Atual?.Login ?? string.Empty;
    public string EmpresaAtiva => _sessao.EmpresaAtiva?.Nome ?? "Nenhuma empresa selecionada";
    /// <summary>Linha de baixo do cabeçalho: só o que o nome não diz (CNPJ e matriz/filial).</summary>
    public string EmpresaDetalhe => _sessao.EmpresaAtiva switch
    {
        null => "Cadastre a empresa em Pessoas com o papel \"Empresa do grupo\".",
        { Cnpj: null } e => e.EhMatriz ? "Matriz" : "Filial",
        var e => $"{e.CnpjFormatado} · {(e.EhMatriz ? "matriz" : "filial")}"
    };
    public bool PodeTrocarEmpresa => _sessao.EmpresasDisponiveis.Count > 1;

    public bool PodeVerPessoas => _sessao.Possui(Permissoes.Pessoas.Visualizar);
    public bool PodeGerenciarUsuarios => _sessao.Possui(Permissoes.Seguranca.GerenciarUsuarios);
    public bool PodeGerenciarPerfis => _sessao.Possui(Permissoes.Seguranca.GerenciarPerfis);
    public bool PodeGerenciarCampos => _sessao.Possui(Permissoes.Cadastros.CamposPersonalizados);
    public bool PodeGerenciarEtiquetas => _sessao.Possui(Permissoes.Cadastros.Etiquetas);
    public bool PodeGerenciarProfissoes => _sessao.Possui(Permissoes.Cadastros.Profissoes);
    public bool PodeGerenciarPapeis => _sessao.Possui(Permissoes.Cadastros.Papeis);
    public bool PodeGerenciarTipos => _sessao.Possui(Permissoes.Cadastros.Tipos);
    public bool PodeGerenciarEstrutura => _sessao.Possui(Permissoes.Cadastros.EstruturaOrganizacional);
    public bool PodeGerenciarComercial => _sessao.Possui(Permissoes.Cadastros.Comercial);
    public bool PodeVerSeguranca => PodeGerenciarUsuarios || PodeGerenciarPerfis;

    [RelayCommand]
    private Task TrocarEmpresaAsync() => _navegacao.IrParaAsync(Tela.EscolherEmpresa);

    [RelayCommand]
    private Task TrocarSenhaAsync() => _navegacao.AbrirTrocaDeSenhaAsync();

    [RelayCommand]
    private async Task SairAsync()
    {
        await _autenticacao.SairAsync();
        await _navegacao.IrParaAsync(Tela.Login);
    }
}

/// <summary>Página inicial: saudação, empresa ativa e um indicador do cadastro.</summary>
public partial class InicioViewModel : ViewModelBase
{
    private readonly SessaoCliente _sessao;
    private readonly PessoasApi _pessoas;

    public InicioViewModel(SessaoCliente sessao, PessoasApi pessoas)
    {
        _sessao = sessao;
        _pessoas = pessoas;
    }

    public string Saudacao => $"{Periodo(DateTime.Now.Hour)}, {PrimeiroNome(_sessao.NomeUsuario)}!";
    public string Data => DateTime.Now.ToString("dddd, d 'de' MMMM 'de' yyyy", new System.Globalization.CultureInfo("pt-BR"));
    public string EmpresaAtiva => _sessao.EmpresaAtiva?.Descricao ?? "Nenhuma empresa selecionada";
    public bool PodeVerPessoas => _sessao.Possui(Permissoes.Pessoas.Visualizar);

    [ObservableProperty] private string _clientesAtivos = "—";

    /// <summary>Clientes pessoa física por faixa de idade (barra proporcional à maior faixa).</summary>
    public System.Collections.ObjectModel.ObservableCollection<FaixaEtariaItem> FaixasEtarias { get; } = new();

    [ObservableProperty] private bool _temFaixasEtarias;

    [RelayCommand]
    private async Task CarregarAsync()
    {
        if (!PodeVerPessoas) return;
        try
        {
            Ocupado = true;
            ClientesAtivos = (await _pessoas.ContarClientesAtivosAsync()).ToString("N0", new System.Globalization.CultureInfo("pt-BR"));

            var faixas = await _pessoas.ListarFaixasEtariasAsync(Lone.Domain.Enums.TipoPapel.Cliente);
            var maior = Math.Max(1, faixas.Select(f => f.Quantidade).DefaultIfEmpty(0).Max());
            FaixasEtarias.Clear();
            foreach (var f in faixas) FaixasEtarias.Add(new FaixaEtariaItem(f.Faixa, f.Quantidade, (double)f.Quantidade / maior));
            TemFaixasEtarias = faixas.Any(f => f.Quantidade > 0);
        }
        catch (SessaoExpiradaException)
        {
            // O aplicativo já volta ao login com a mensagem (evento SessaoCliente.Expirou).
        }
        catch (Exception ex)
        {
            MostrarErro(ex);
        }
        finally
        {
            Ocupado = false;
        }
    }

    private static string Periodo(int hora) => hora switch
    {
        < 12 => "Bom dia",
        < 18 => "Boa tarde",
        _ => "Boa noite"
    };

    private static string PrimeiroNome(string nome) =>
        nome.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? nome;
}

/// <summary>Uma linha do gráfico de faixas etárias da tela inicial.</summary>
public sealed record FaixaEtariaItem(string Faixa, int Quantidade, double Proporcao)
{
    public string QuantidadeTexto => Quantidade.ToString("N0", Comum.TextoTela.Brasil);
}
