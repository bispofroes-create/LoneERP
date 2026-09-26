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
        MontarMenu();
    }

    private void Sessao_Alterada(object? sender, EventArgs e)
    {
        if (!_sessao.Autenticada) return; // ao sair, o app já está trocando de tela
        OnPropertyChanged(string.Empty);
        MontarMenu();
    }

    // ---- Menu lateral em seções (módulo → telas → ⚙ configurações do módulo) ----

    /// <summary>Seções visíveis para as permissões atuais.</summary>
    public System.Collections.ObjectModel.ObservableCollection<SecaoMenu> Secoes { get; } = new();

    /// <summary>Rota da tela aberta (ex.: "pessoas"), informada pelo Shell a cada navegação.</summary>
    public string RotaAtual { get; private set; } = "inicio";

    /// <summary>Definido pelo Shell: vai para a rota (e pergunta antes se houver alterações não salvas).</summary>
    public Func<string, Task>? Navegar { get; set; }

    /// <summary>Refaz as seções conforme as permissões (sessão nova ou relida). Seção sem item não aparece.</summary>
    public void MontarMenu()
    {
        Secoes.Clear();
        foreach (var secao in CriarSecoes(_sessao.Possui)) Secoes.Add(secao);
        MarcarAtivo();
    }

    /// <summary>Estrutura do menu (estática e testável). Cada item com a mesma permissão de antes.</summary>
    public static IReadOnlyList<SecaoMenu> CriarSecoes(Func<string, bool> possui)
    {
        var secoes = new List<SecaoMenu> { new(null, [new ItemMenu("Início", "inicio")]) };

        var pessoas = new List<ItemMenu>();
        if (possui(Permissoes.Pessoas.Visualizar))
        {
            pessoas.Add(new ItemMenu("Pessoas", "pessoas"));
            pessoas.Add(new ItemMenu("Consulta avançada", "consulta-pessoas"));
        }
        AdicionarConfiguracoes(pessoas, ModulosConfiguracao.Pessoas, possui);
        if (pessoas.Count > 0) secoes.Add(new SecaoMenu("PESSOAS", pessoas));

        var organizacao = new List<ItemMenu>();
        if (possui(Permissoes.Cadastros.GruposEmpresariais)) organizacao.Add(new ItemMenu("Grupos empresariais", "grupos-empresariais"));
        AdicionarConfiguracoes(organizacao, ModulosConfiguracao.Organizacao, possui);
        if (organizacao.Count > 0) secoes.Add(new SecaoMenu("ORGANIZAÇÃO", organizacao));

        var metas = new List<ItemMenu>();
        if (possui(Permissoes.Metas.Visualizar)) metas.Add(new ItemMenu("Metas", "metas"));
        AdicionarConfiguracoes(metas, ModulosConfiguracao.Metas, possui);
        if (metas.Count > 0) secoes.Add(new SecaoMenu("METAS", metas));

        if (ConfiguracoesViewModel.AlgumaPermitida(possui, ModulosConfiguracao.Sistema))
            secoes.Add(new SecaoMenu(null,
            [
                new ItemMenu("Configurações do sistema", ModulosConfiguracao.Rota(ModulosConfiguracao.Sistema), configuracao: true,
                    rotasRelacionadas: ConfiguracoesViewModel.RotasDoModulo(ModulosConfiguracao.Sistema))
            ]));
        return secoes;
    }

    private static void AdicionarConfiguracoes(List<ItemMenu> itens, string modulo, Func<string, bool> possui)
    {
        if (!ConfiguracoesViewModel.AlgumaPermitida(possui, modulo)) return;
        itens.Add(new ItemMenu(ModulosConfiguracao.Titulo(modulo), ModulosConfiguracao.Rota(modulo), configuracao: true,
            rotasRelacionadas: ConfiguracoesViewModel.RotasDoModulo(modulo)));
    }

    /// <summary>O Shell navegou: destaca o item da tela aberta (ex.: "//pessoas" → Pessoas).</summary>
    public void DefinirRotaAtual(string? localizacao)
    {
        var rota = (localizacao ?? string.Empty).Split('?')[0].TrimEnd('/').Split('/').LastOrDefault(r => r.Length > 0);
        RotaAtual = rota ?? "inicio";
        MarcarAtivo();
    }

    private void MarcarAtivo()
    {
        foreach (var item in Secoes.SelectMany(s => s.Itens)) item.Ativo = item.Corresponde(RotaAtual);
    }

    [RelayCommand]
    private Task IrAsync(ItemMenu? item) => item is null || Navegar is null ? Task.CompletedTask : Navegar(item.Rota);

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
    public bool PodeGerenciarGruposEmpresariais => _sessao.Possui(Permissoes.Cadastros.GruposEmpresariais);
    public bool PodeGerenciarProfissoes => _sessao.Possui(Permissoes.Cadastros.Profissoes);
    public bool PodeGerenciarPapeis => _sessao.Possui(Permissoes.Cadastros.Papeis);
    public bool PodeGerenciarTipos => _sessao.Possui(Permissoes.Cadastros.Tipos);
    public bool PodeGerenciarEstrutura => _sessao.Possui(Permissoes.Cadastros.EstruturaOrganizacional);
    public bool PodeGerenciarComercial => _sessao.Possui(Permissoes.Cadastros.Comercial);
    public bool PodeVerMetas => _sessao.Possui(Permissoes.Metas.Visualizar);
    public bool PodeGerenciarMetas => _sessao.Possui(Permissoes.Metas.Gerenciar);
    public bool PodeVerSeguranca => PodeGerenciarUsuarios || PodeGerenciarPerfis;

    /// <summary>Páginas de configurações de cada módulo (rotas do Shell só existem com alguma permissão).</summary>
    public bool PodeVerConfiguracoesPessoas => ConfiguracoesViewModel.AlgumaPermitida(_sessao.Possui, ModulosConfiguracao.Pessoas);
    public bool PodeVerConfiguracoesOrganizacao => ConfiguracoesViewModel.AlgumaPermitida(_sessao.Possui, ModulosConfiguracao.Organizacao);
    public bool PodeVerConfiguracoesMetas => ConfiguracoesViewModel.AlgumaPermitida(_sessao.Possui, ModulosConfiguracao.Metas);
    public bool PodeVerConfiguracoesSistema => ConfiguracoesViewModel.AlgumaPermitida(_sessao.Possui, ModulosConfiguracao.Sistema);

    /// <summary>Iniciais do usuário para o rodapé do menu (ex.: "Maria Souza" → "MS").</summary>
    public string Iniciais
    {
        get
        {
            var partes = NomeUsuario.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return partes.Length switch
            {
                0 => "?",
                1 => char.ToUpperInvariant(partes[0][0]).ToString(),
                _ => $"{char.ToUpperInvariant(partes[0][0])}{char.ToUpperInvariant(partes[^1][0])}"
            };
        }
    }

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
