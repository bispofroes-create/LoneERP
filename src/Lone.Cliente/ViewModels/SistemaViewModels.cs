using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.Api;
using Lone.Cliente.Navegacao;
using Lone.Cliente.Sessao;
using Lone.Contracts.Menu;
using Lone.Contracts.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Validacao;

namespace Lone.Cliente.ViewModels;

/// <summary>
/// Menu principal: quem está logado, em qual empresa, e o que pode ver. Criado de novo a cada entrada
/// no sistema (inclusive ao trocar de empresa), então as permissões estão sempre atualizadas.
/// Menu em dois níveis, como nos ERPs maduros: busca, Início, Favoritos, Recentes e os módulos, que abrem e fecham ao
/// tocar no nome. O módulo da tela aberta abre sozinho; os demais ficam como o usuário deixou enquanto o app está aberto.
/// </summary>
public partial class MenuViewModel : ViewModelBase, IDisposable
{
    private readonly SessaoCliente _sessao;
    private readonly ServicoAutenticacao _autenticacao;
    private readonly INavegacao _navegacao;
    private readonly MenuUsuarioApi _preferencias;

    /// <summary>Todas as telas que o perfil pode abrir (itens dos módulos + cadastros de configuração): base da busca e dos atalhos.</summary>
    private List<ItemMenu> _catalogo = new();

    /// <summary>Rotas favoritas (na ordem em que foram marcadas) e recentes (a mais nova primeiro), como a API guarda.</summary>
    private List<string> _favoritos = new();
    private List<string> _recentes = new();

    public MenuViewModel(SessaoCliente sessao, ServicoAutenticacao autenticacao, INavegacao navegacao, MenuUsuarioApi preferencias)
    {
        _sessao = sessao;
        _autenticacao = autenticacao;
        _navegacao = navegacao;
        _preferencias = preferencias;

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

    // ---- Estrutura: módulos em seções que abrem e fecham ----

    /// <summary>Primeiro item do menu, sempre visível e fora dos módulos (os atalhos vêm logo abaixo dele).</summary>
    public ItemMenu Inicio { get; } = new("Início", ItemMenu.RotaInicio);

    /// <summary>Início como lista, para a tela desenhar a linha com o mesmo modelo dos itens dos módulos.</summary>
    public IReadOnlyList<ItemMenu> ItensDoTopo => [Inicio];

    /// <summary>Módulos e entradas soltas (Configurações do sistema) visíveis para as permissões atuais.</summary>
    public System.Collections.ObjectModel.ObservableCollection<SecaoMenu> Secoes { get; } = new();

    public GrupoAtalhosMenu Favoritos { get; } = new("Favoritos");
    public GrupoAtalhosMenu Recentes { get; } = new("Recentes");

    /// <summary>Rota da tela aberta (ex.: "pessoas"), informada pelo Shell a cada navegação.</summary>
    public string RotaAtual { get; private set; } = ItemMenu.RotaInicio;

    /// <summary>Definido pelo Shell: vai para a rota (e pergunta antes se houver alterações não salvas).</summary>
    public Func<string, Task>? Navegar { get; set; }

    /// <summary>Último registro de acesso enviado à API (os testes aguardam; a tela não depende dele).</summary>
    public Task UltimoRegistroDeAcesso { get; private set; } = Task.CompletedTask;

    /// <summary>
    /// Refaz as seções conforme as permissões (sessão nova ou relida). Seção sem item não aparece. Os módulos que estavam
    /// abertos continuam abertos; favoritos e recentes sem permissão deixam de aparecer (continuam guardados na API).
    /// </summary>
    public void MontarMenu()
    {
        var abertas = Secoes.Where(s => s.Expandida).Select(s => s.Titulo).ToHashSet();
        var secoes = CriarSecoes(_sessao.Possui);
        foreach (var secao in secoes) secao.Expandida = abertas.Contains(secao.Titulo);
        _catalogo = CriarCatalogo(Inicio, secoes, _sessao.Possui).ToList();

        Secoes.Clear();
        foreach (var secao in secoes) Secoes.Add(secao);
        AplicarFavoritos();
        AplicarRecentes();
        MarcarAtivo();
        Filtrar();
    }

    /// <summary>
    /// Estrutura do menu abaixo de Início e dos atalhos (estática e testável). Cada item com a mesma permissão de antes.
    /// Os itens dizem o que abrem, sem repetir o nome do módulo que já está no cabeçalho da seção (decisão do usuário);
    /// o nome completo vai na descrição (atalhos, busca e leitor de tela).
    /// </summary>
    public static IReadOnlyList<SecaoMenu> CriarSecoes(Func<string, bool> possui)
    {
        var secoes = new List<SecaoMenu>();

        var nomePessoas = ModulosConfiguracao.Nome(ModulosConfiguracao.Pessoas);
        var pessoas = new List<ItemMenu>();
        if (possui(Permissoes.Pessoas.Visualizar))
        {
            pessoas.Add(new ItemMenu("Cadastro", "pessoas", descricao: "Cadastro de pessoas", caminho: nomePessoas));
            pessoas.Add(new ItemMenu("Consulta avançada", "consulta-pessoas", descricao: "Consulta avançada de pessoas", caminho: nomePessoas));
        }
        AdicionarConfiguracoes(pessoas, ModulosConfiguracao.Pessoas, possui);
        if (pessoas.Count > 0) secoes.Add(new SecaoMenu(nomePessoas, pessoas));

        var nomeOrganizacao = ModulosConfiguracao.Nome(ModulosConfiguracao.Organizacao);
        var organizacao = new List<ItemMenu>();
        if (possui(Permissoes.Cadastros.GruposEmpresariais))
            organizacao.Add(new ItemMenu("Grupos empresariais", "grupos-empresariais", caminho: nomeOrganizacao));
        AdicionarConfiguracoes(organizacao, ModulosConfiguracao.Organizacao, possui);
        if (organizacao.Count > 0) secoes.Add(new SecaoMenu(nomeOrganizacao, organizacao));

        var nomeMetas = ModulosConfiguracao.Nome(ModulosConfiguracao.Metas);
        var metas = new List<ItemMenu>();
        if (possui(Permissoes.Metas.Visualizar)) metas.Add(new ItemMenu("Painel", "metas", descricao: "Painel de metas", caminho: nomeMetas));
        AdicionarConfiguracoes(metas, ModulosConfiguracao.Metas, possui);
        if (metas.Count > 0) secoes.Add(new SecaoMenu(nomeMetas, metas));

        if (ConfiguracoesViewModel.AlgumaPermitida(possui, ModulosConfiguracao.Sistema))
            secoes.Add(new SecaoMenu(null,
            [
                new ItemMenu(ModulosConfiguracao.Titulo(ModulosConfiguracao.Sistema), ModulosConfiguracao.Rota(ModulosConfiguracao.Sistema),
                    configuracao: true, rotasRelacionadas: ConfiguracoesViewModel.RotasDoModulo(ModulosConfiguracao.Sistema))
            ]));
        return secoes;
    }

    private static void AdicionarConfiguracoes(List<ItemMenu> itens, string modulo, Func<string, bool> possui)
    {
        if (!ConfiguracoesViewModel.AlgumaPermitida(possui, modulo)) return;
        // "Configurações" dentro da seção do módulo; o nome completo fica para os atalhos, a busca e o leitor de tela.
        itens.Add(new ItemMenu("Configurações", ModulosConfiguracao.Rota(modulo), configuracao: true,
            rotasRelacionadas: ConfiguracoesViewModel.RotasDoModulo(modulo), descricao: ModulosConfiguracao.Titulo(modulo),
            caminho: ModulosConfiguracao.Nome(modulo)));
    }

    /// <summary>
    /// Todas as telas que o perfil pode abrir, uma vez cada: os itens das seções e os cadastros de configuração de cada
    /// módulo (ex.: "Papéis", em "Pessoas › Configurações"). A busca, os favoritos e os recentes usam esta lista.
    /// </summary>
    public static IReadOnlyList<ItemMenu> CriarCatalogo(ItemMenu inicio, IReadOnlyList<SecaoMenu> secoes, Func<string, bool> possui)
    {
        var itens = new List<ItemMenu> { inicio };
        itens.AddRange(secoes.SelectMany(s => s.Itens));
        foreach (var modulo in ModulosConfiguracao.Todos)
            foreach (var cadastro in ConfiguracoesViewModel.Montar(possui, modulo).SelectMany(g => g.Itens))
                itens.Add(new ItemMenu(cadastro.Titulo, cadastro.Rota, caminho: ModulosConfiguracao.Caminho(modulo)));
        return itens.DistinctBy(i => i.Rota).ToList();
    }

    // ---- Tela aberta ----

    /// <summary>
    /// O Shell navegou: destaca o item da tela aberta (ex.: "//pessoas" → Cadastro), abre o módulo dela e a coloca no
    /// topo dos recentes.
    /// </summary>
    public void DefinirRotaAtual(string? localizacao)
    {
        var rota = (localizacao ?? string.Empty).Split('?')[0].TrimEnd('/').Split('/').LastOrDefault(r => r.Length > 0);
        RotaAtual = rota ?? ItemMenu.RotaInicio;
        MarcarAtivo();
        foreach (var secao in Secoes.Where(s => s.ContemAtivo)) secao.Expandida = true;
        RegistrarRecente(RotaAtual);
    }

    private void MarcarAtivo()
    {
        foreach (var item in _catalogo) item.Ativo = item.Corresponde(RotaAtual);
        foreach (var secao in Secoes) secao.ContemAtivo = secao.TemTitulo && secao.Itens.Any(i => i.Ativo);
    }

    [RelayCommand]
    private Task IrAsync(ItemMenu? item)
    {
        if (item is null || Navegar is null) return Task.CompletedTask;
        Busca = string.Empty; // escolheu pela busca: o menu volta ao normal
        return Navegar(item.Rota);
    }

    // ---- Favoritos e recentes (guardados na API, por usuário) ----

    /// <summary>
    /// Lê favoritos e recentes do usuário (chamado quando o menu aparece). Sem resposta da API, o menu funciona sem eles.
    /// Telas abertas antes da resposta ficam na frente dos recentes que vieram do servidor.
    /// </summary>
    [RelayCommand]
    private async Task CarregarPreferenciasAsync()
    {
        try
        {
            var preferencias = await _preferencias.ObterAsync();
            _favoritos = preferencias.Favoritos.Distinct().ToList();
            _recentes = _recentes.Concat(preferencias.Recentes).Distinct().ToList();
            AplicarFavoritos();
            AplicarRecentes();
        }
        catch (SessaoExpiradaException)
        {
            // O aplicativo já volta ao login com a mensagem (evento SessaoCliente.Expirou).
        }
        catch (Exception)
        {
            Aviso = "Não foi possível carregar favoritos e recentes agora.";
        }
    }

    /// <summary>Marca ou desmarca a tela como favorita. A tela muda na hora; se a API recusar, volta como estava e avisa.</summary>
    [RelayCommand]
    private async Task AlternarFavoritoAsync(ItemMenu? item)
    {
        if (item is null || !item.PodeFavoritar) return;
        Aviso = string.Empty;
        var marcar = !item.Favorito;
        if (marcar && _favoritos.Count >= LimitesMenu.MaximoFavoritos)
        {
            Aviso = $"Você já tem {LimitesMenu.MaximoFavoritos} favoritos. Remova um para marcar outro.";
            return;
        }

        var anteriores = _favoritos.ToList();
        if (marcar) _favoritos.Add(item.Rota);
        else _favoritos.Remove(item.Rota);
        AplicarFavoritos();

        try
        {
            await _preferencias.DefinirFavoritoAsync(item.Rota, marcar);
        }
        catch (SessaoExpiradaException)
        {
            // O aplicativo já volta ao login.
        }
        catch (Exception ex)
        {
            _favoritos = anteriores;
            AplicarFavoritos();
            Aviso = ex is ValidacaoException validacao
                ? string.Join(Environment.NewLine, validacao.Erros)
                : "Não foi possível salvar o favorito agora. Tente de novo.";
        }
    }

    /// <summary>Coloca a tela no topo dos recentes e avisa a API em segundo plano (uma falha aqui não atrapalha o trabalho).</summary>
    private void RegistrarRecente(string rota)
    {
        if (rota == ItemMenu.RotaInicio || _catalogo.All(i => i.Rota != rota)) return;
        _recentes.Remove(rota);
        _recentes.Insert(0, rota);
        AplicarRecentes();
        UltimoRegistroDeAcesso = EnviarAcessoAsync(rota);
    }

    private async Task EnviarAcessoAsync(string rota)
    {
        try
        {
            await _preferencias.RegistrarAcessoAsync(rota);
        }
        catch (Exception)
        {
            // Recente é conveniência: sem rede, a lista local continua certa e o próximo acesso atualiza a API.
        }
    }

    private ItemMenu? PorRota(string rota) => _catalogo.FirstOrDefault(i => i.Rota == rota);

    private void AplicarFavoritos()
    {
        foreach (var item in _catalogo) item.Favorito = _favoritos.Contains(item.Rota);
        Favoritos.Definir(_favoritos.Select(PorRota).OfType<ItemMenu>());
    }

    private void AplicarRecentes() =>
        Recentes.Definir(_recentes.Select(PorRota).OfType<ItemMenu>().Take(LimitesMenu.RecentesExibidos));

    /// <summary>Recusa de favorito ou falha ao ler as preferências, mostrada no próprio menu até a próxima ação.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemAviso))]
    private string _aviso = string.Empty;

    public bool TemAviso => Aviso.Length > 0;

    // ---- Busca no menu ----

    /// <summary>Texto da busca: com ele preenchido, o menu mostra só as telas encontradas.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Buscando), nameof(MostrarMenu))]
    private string _busca = string.Empty;

    public bool Buscando => Busca.Trim().Length > 0;
    public bool MostrarMenu => !Buscando;
    public System.Collections.ObjectModel.ObservableCollection<ItemMenu> Resultados { get; } = new();
    public bool SemResultados => Buscando && Resultados.Count == 0;

    partial void OnBuscaChanged(string value) => Filtrar();

    /// <summary>
    /// Todas as palavras digitadas precisam aparecer (sem acento e sem diferença de maiúsculas) no nome, no caminho ou no
    /// texto curto. Ordem: primeiro as telas com mais palavras no próprio nome ("config pessoas" → Configurações de Pessoas
    /// antes dos cadastros que só ficam em Pessoas › Configurações), depois as que começam pela primeira palavra, depois
    /// em ordem alfabética.
    /// </summary>
    public static IReadOnlyList<ItemMenu> Pesquisar(IEnumerable<ItemMenu> catalogo, string? texto)
    {
        var termo = TextoBusca.Normalizar(texto);
        if (termo.Length == 0) return [];
        var palavras = termo.Split(' ');
        return catalogo
            .Where(i => palavras.All(p => i.ChaveBusca.Contains(p, StringComparison.Ordinal)))
            .OrderByDescending(i => palavras.Count(p => i.DescricaoBusca.Contains(p, StringComparison.Ordinal)))
            .ThenBy(i => i.DescricaoBusca.StartsWith(palavras[0], StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(i => i.Descricao, StringComparer.Create(Comum.TextoTela.Brasil, ignoreCase: true))
            .ToList();
    }

    private void Filtrar()
    {
        Resultados.Clear();
        foreach (var item in Pesquisar(_catalogo, Busca)) Resultados.Add(item);
        OnPropertyChanged(nameof(SemResultados));
    }

    /// <summary>Enter na busca abre a primeira tela encontrada.</summary>
    [RelayCommand]
    private Task AbrirPrimeiroResultadoAsync() => Resultados.Count > 0 ? IrAsync(Resultados[0]) : Task.CompletedTask;

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
