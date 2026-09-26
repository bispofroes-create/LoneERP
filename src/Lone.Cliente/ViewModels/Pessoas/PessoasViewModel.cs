using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.Api;
using Lone.Cliente.Plataforma;
using Lone.Cliente.Sessao;
using Lone.Cliente.ViewModels.Cadastros;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Etiquetas;
using Lone.Contracts.Profissoes;
using Lone.Contracts.Papeis;
using Lone.Contracts.Contatos;
using Lone.Contracts.Enderecos;
using Lone.Contracts.Documentos;
using Lone.Contracts.Integracoes;
using Lone.Contracts.Pessoas;
using Lone.Contracts.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Enums;
using Lone.Domain.ObjetosDeValor;
using Lone.Domain.Validacao;

namespace Lone.Cliente.ViewModels.Pessoas;

/// <summary>
/// Central de Pessoas: lista com busca no servidor e filtros; ficha com abas (geral, dados pessoais ou empresa,
/// contatos, endereços, documentos, cliente, fornecedor, informações adicionais, LGPD, situação e histórico),
/// consultas de CNPJ e CEP, municípios do IBGE e as ações de desativar e reativar.
/// </summary>
public sealed partial class PessoasViewModel : CadastroViewModelBase<PessoaResumo>
{
    private readonly PessoasApi _pessoas;
    private readonly ConsultasApi _consultas;
    private readonly SessaoCliente _sessao;
    private readonly ServicoAutenticacao _autenticacao;
    private readonly MunicipiosApi _municipios;
    private readonly CamposPersonalizadosApi _camposApi;
    private readonly EtiquetasApi _etiquetasApi;
    private readonly ProfissoesApi _profissoesApi;
    private readonly PapeisApi _papeisApi;
    private readonly TiposMeioContatoApi _tiposMeioApi;
    private readonly TiposEnderecoApi _tiposEnderecoApi;
    private readonly TiposDocumentoApi _tiposDocumentoApi;
    private readonly AnexosApi _anexosApi;
    private readonly ColaboradoresApi _colaboradoresApi;
    private readonly ComercialApi _comercialApi;
    private readonly IArquivos _arquivos;

    /// <summary>Campos personalizados ativos (lidos ao abrir a tela).</summary>
    private IReadOnlyList<CampoPersonalizadoDto> _campos = [];

    /// <summary>Campos personalizados dos documentos (ativos; cada um de um tipo de documento).</summary>
    private IReadOnlyList<CampoPersonalizadoDto> _camposDocumento = [];

    /// <summary>Cadastro de etiquetas, com as desativadas (lido ao abrir a tela; a ficha oferece só as ativas).</summary>
    private List<EtiquetaDto> _etiquetas = [];

    /// <summary>Cadastro de profissões, com as desativadas (a ficha oferece só as ativas e mostra a gravada).</summary>
    private List<ProfissaoDto> _profissoes = [];

    /// <summary>Cadastro de papéis, com os desativados (a ficha oferece os ativos e mostra os que a pessoa tem).</summary>
    private List<PapelCadastroDto> _papeis = [];

    /// <summary>Tipos de telefone/e-mail (Comercial, Residencial...), com os desativados.</summary>
    private List<TipoMeioContatoDto> _tiposMeio = [];

    /// <summary>Tipos de endereço (Sede, Depósito...), com os desativados.</summary>
    private List<TipoEnderecoDto> _tiposEndereco = [];
    private List<FinalidadeEnderecoDto> _finalidadesEndereco = [];

    /// <summary>Tipos de documento (RG, CNH, Alvará...), com os desativados.</summary>
    private List<TipoDocumentoDto> _tiposDocumento = [];

    public PessoasViewModel(PessoasApi pessoas, ConsultasApi consultas, SessaoCliente sessao, ServicoAutenticacao autenticacao,
                            MunicipiosApi municipios, CamposPersonalizadosApi camposApi, EtiquetasApi etiquetasApi, ProfissoesApi profissoesApi,
                            PapeisApi papeisApi, TiposMeioContatoApi tiposMeioApi, TiposEnderecoApi tiposEnderecoApi,
                            TiposDocumentoApi tiposDocumentoApi, AnexosApi anexosApi, ColaboradoresApi colaboradoresApi, ComercialApi comercialApi,
                            IArquivos arquivos, IDialogos dialogos)
        : base(dialogos)
    {
        _pessoas = pessoas;
        _consultas = consultas;
        _sessao = sessao;
        _autenticacao = autenticacao;
        _municipios = municipios;
        _camposApi = camposApi;
        _etiquetasApi = etiquetasApi;
        _profissoesApi = profissoesApi;
        _papeisApi = papeisApi;
        _tiposMeioApi = tiposMeioApi;
        _tiposEnderecoApi = tiposEnderecoApi;
        _tiposDocumentoApi = tiposDocumentoApi;
        _anexosApi = anexosApi;
        _colaboradoresApi = colaboradoresApi;
        _comercialApi = comercialApi;
        _arquivos = arquivos;

        // Buscar outro texto volta para a página 1 (a busca no servidor sai logo depois, com uma pequena espera).
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Busca)) _pagina = 1;
        };
    }

    protected override bool BuscaNoServidor => true;

    // ---- Lista ----

    /// <summary>"Todos" e cada papel do cadastro (antes de ler o cadastro, os de sistema).</summary>
    public ObservableCollection<Opcao<Guid?>> FiltrosPapel { get; } =
        new(global::Lone.Domain.Papeis.PapeisSistema.Todos.OrderBy(p => p.Ordem)
            .Select(p => new Opcao<Guid?>(p.Id, p.Nome)).Prepend(TodosPapeis).ToList());

    private static readonly Opcao<Guid?> TodosPapeis = new(null, "Todos os papéis");

    [ObservableProperty] private Opcao<Guid?> _filtroPapel = TodosPapeis;
    [ObservableProperty] private bool _mostrarInativos;

    /// <summary>Só cadastros com município antigo (texto) a escolher na tabela do IBGE.</summary>
    [ObservableProperty] private bool _somenteMunicipioACorrigir;

    /// <summary>"Todas" e cada etiqueta do cadastro (as desativadas também: podem estar em cadastros antigos).</summary>
    public ObservableCollection<Opcao<Guid?>> FiltrosEtiqueta { get; } = new() { TodasEtiquetas };
    [ObservableProperty] private Opcao<Guid?> _filtroEtiqueta = TodasEtiquetas;
    private static readonly Opcao<Guid?> TodasEtiquetas = new(null, "Todas as etiquetas");

    /// <summary>Atalho "Nova etiqueta" na ficha: só para quem gerencia etiquetas (a API confere de novo).</summary>
    public bool PodeCriarEtiqueta => _sessao.Possui(Permissoes.Cadastros.Etiquetas);

    /// <summary>Atalho "Nova profissão" na ficha: só para quem gerencia profissões (a API confere de novo).</summary>
    public bool PodeCriarProfissao => _sessao.Possui(Permissoes.Cadastros.Profissoes);

    public bool PodeCriar => _sessao.Possui(Permissoes.Pessoas.Criar);

    partial void OnFiltroPapelChanged(Opcao<Guid?> value)
    {
        // A lista de escolha manda nulo quando o item escolhido sai dela: volta para "todos".
        if (value is null)
        {
            FiltroPapel = TodosPapeis;
            return;
        }
        FiltroAvancadoMudou();
    }
    partial void OnMostrarInativosChanged(bool value) => FiltroAvancadoMudou();
    partial void OnSomenteMunicipioACorrigirChanged(bool value) => FiltroAvancadoMudou();

    /// <summary>Filtro do painel mudou: volta para a página 1 (e não relê várias vezes ao limpar todos de uma vez).</summary>
    private void FiltroAvancadoMudou()
    {
        OnPropertyChanged(nameof(TextoBotaoFiltros));
        if (_limpandoFiltros) return;
        _ = RecarregarDaPrimeiraPaginaAsync();
    }
    partial void OnFiltroEtiquetaChanged(Opcao<Guid?> value)
    {
        // A lista de escolha manda nulo quando o item escolhido sai dela: volta para "todas".
        if (value is null)
        {
            FiltroEtiqueta = TodasEtiquetas;
            return;
        }
        FiltroAvancadoMudou();
    }

    /// <summary>
    /// Falha ao ler as etiquetas não impede a lista de aparecer (o filtro e a ficha ficam sem opções, e as já
    /// marcadas numa pessoa voltam intactas ao salvar). Falha ao ler os
    /// campos personalizados também não: a ficha abre sem a aba "Informações adicionais" e avisa.
    /// </summary>
    protected override async Task AntesDeListarAsync()
    {
        try { await AtualizarEtiquetasAsync(); }
        catch (Exception ex) when (ex is not SessaoExpiradaException) { /* filtro sem etiquetas por enquanto */ }

        try { _campos = await _camposApi.ListarAsync(EntidadePersonalizavel.Pessoa, incluirInativos: false); }
        catch (Exception ex) when (ex is not SessaoExpiradaException) { _campos = []; }

        // Sem a lista, a ficha mostra a profissão vazia, mas a gravada volta intacta ao salvar.
        try { _profissoes = await _profissoesApi.ListarAsync(incluirInativas: true); }
        catch (Exception ex) when (ex is not SessaoExpiradaException) { _profissoes = []; }

        // Sem o cadastro de papéis, a ficha usa os papéis de sistema e os outros períodos voltam intactos.
        try
        {
            _papeis = await _papeisApi.ListarAsync(incluirInativos: true);
            AtualizarFiltrosPapel();
        }
        catch (Exception ex) when (ex is not SessaoExpiradaException) { _papeis = []; }

        // Sem os tipos, os telefones/e-mails ficam sem classificação na tela, mas a gravada volta intacta.
        try { _tiposMeio = await _tiposMeioApi.ListarAsync(incluirInativos: true); }
        catch (Exception ex) when (ex is not SessaoExpiradaException) { _tiposMeio = []; }

        // Idem para os endereços: sem a lista, o tipo gravado volta intacto.
        try { _tiposEndereco = await _tiposEnderecoApi.ListarAsync(incluirInativos: true); }
        catch (Exception ex) when (ex is not SessaoExpiradaException) { _tiposEndereco = []; }

        // Finalidades de endereço (cadastro): sem a lista, as gravadas voltam intactas, mas não dá para acrescentar.
        try { _finalidadesEndereco = await _tiposEnderecoApi.ListarFinalidadesAsync(); }
        catch (Exception ex) when (ex is not SessaoExpiradaException) { _finalidadesEndereco = []; }

        // Sem os tipos de documento, a ficha oferece os de sistema e o tipo gravado volta intacto.
        try { _tiposDocumento = await _tiposDocumentoApi.ListarAsync(incluirInativos: true); }
        catch (Exception ex) when (ex is not SessaoExpiradaException) { _tiposDocumento = []; }

        // Sem os campos dos documentos, a ficha não os mostra e os valores gravados voltam intactos.
        try { _camposDocumento = await _camposApi.ListarAsync(EntidadePersonalizavel.Documento, incluirInativos: false); }
        catch (Exception ex) when (ex is not SessaoExpiradaException) { _camposDocumento = []; }
    }

    private void AtualizarFiltrosPapel()
    {
        var escolhido = FiltroPapel?.Valor;
        while (FiltrosPapel.Count > 1) FiltrosPapel.RemoveAt(1);
        foreach (var p in _papeis.OrderBy(p => !p.Ativo).ThenBy(p => p.Ordem))
            FiltrosPapel.Add(new Opcao<Guid?>(p.Id, p.Ativo ? p.Nome : p.Nome + " (desativado)"));
        var mesmo = FiltrosPapel.FirstOrDefault(o => o.Valor == escolhido) ?? TodosPapeis;
        if (!ReferenceEquals(mesmo, FiltroPapel)) FiltroPapel = mesmo;
    }

    private async Task AtualizarEtiquetasAsync()
    {
        _etiquetas = await _etiquetasApi.ListarAsync(incluirInativas: true);
        AtualizarFiltrosEtiqueta();
    }

    private void AtualizarFiltrosEtiqueta()
    {
        var escolhida = FiltroEtiqueta?.Valor;
        var opcoes = _etiquetas
            .OrderBy(e => !e.Ativo).ThenBy(e => e.Nome, StringComparer.CurrentCultureIgnoreCase)
            .Select(e => new Opcao<Guid?>(e.Id, e.Ativo ? e.Nome : e.Nome + " (desativada)"))
            .ToList();
        while (FiltrosEtiqueta.Count > 1) FiltrosEtiqueta.RemoveAt(1);
        foreach (var opcao in opcoes) FiltrosEtiqueta.Add(opcao);
        // Mesma escolha se ela ainda existir (outra instância, mesmo valor); senão, "todas".
        var mesma = FiltrosEtiqueta.FirstOrDefault(o => o.Valor == escolhida) ?? TodasEtiquetas;
        if (!ReferenceEquals(mesma, FiltroEtiqueta)) FiltroEtiqueta = mesma;
    }

    protected override string TextoDeBusca(PessoaResumo item) => item.Nome;

    // ---- Lista: atalhos, filtros avançados, paginação e colunas (tela de Pessoas) ----

    /// <summary>Atalhos acima da tabela (um marcado por vez). Os filtros avançados ficam no painel "Filtros".</summary>
    public IReadOnlyList<FiltroRapido> FiltrosRapidos { get; } = FiltroRapido.Criar();

    private string _filtroRapido = FiltroRapido.Todos;

    [RelayCommand]
    private Task EscolherFiltroRapidoAsync(FiltroRapido? filtro)
    {
        if (filtro is null || filtro.Chave == _filtroRapido) return Task.CompletedTask;
        _filtroRapido = filtro.Chave;
        foreach (var f in FiltrosRapidos) f.Selecionado = ReferenceEquals(f, filtro);
        return RecarregarDaPrimeiraPaginaAsync();
    }

    /// <summary>Painel de filtros avançados (papel, etiqueta, incluir inativos, município a corrigir).</summary>
    [ObservableProperty] private bool _mostrarFiltros;

    [RelayCommand]
    private void AlternarFiltros() => MostrarFiltros = !MostrarFiltros;

    /// <summary>"Filtros" ou "Filtros (2)": quantos filtros avançados estão ligados.</summary>
    public string TextoBotaoFiltros
    {
        get
        {
            var ligados = (FiltroPapel?.Valor is null ? 0 : 1) + (FiltroEtiqueta?.Valor is null ? 0 : 1)
                          + (MostrarInativos ? 1 : 0) + (SomenteMunicipioACorrigir ? 1 : 0);
            return ligados == 0 ? "Filtros" : $"Filtros ({ligados})";
        }
    }

    [RelayCommand]
    private Task LimparFiltrosAsync()
    {
        _limpandoFiltros = true;
        try
        {
            FiltroPapel = TodosPapeis;
            FiltroEtiqueta = TodasEtiquetas;
            MostrarInativos = false;
            SomenteMunicipioACorrigir = false;
        }
        finally { _limpandoFiltros = false; }
        OnPropertyChanged(nameof(TextoBotaoFiltros));
        return RecarregarDaPrimeiraPaginaAsync();
    }

    private bool _limpandoFiltros;

    /// <summary>Filtro que vai para a API: atalho + filtros avançados (o papel escolhido no painel vale sobre o do atalho).</summary>
    public FiltroPessoas FiltroAtual()
    {
        var filtro = new FiltroPessoas
        {
            Texto = Busca,
            PapelId = FiltroPapel?.Valor,
            IncluirInativos = MostrarInativos,
            MunicipioACorrigir = SomenteMunicipioACorrigir,
            EtiquetaId = FiltroEtiqueta?.Valor
        };
        switch (_filtroRapido)
        {
            case FiltroRapido.Fisicas: filtro.Natureza = NaturezaPessoa.Fisica; break;
            case FiltroRapido.Juridicas: filtro.Natureza = NaturezaPessoa.Juridica; break;
            case FiltroRapido.Clientes: filtro.PapelId ??= global::Lone.Domain.Papeis.PapeisSistema.Id(TipoPapel.Cliente); break;
            case FiltroRapido.Fornecedores: filtro.PapelId ??= global::Lone.Domain.Papeis.PapeisSistema.Id(TipoPapel.Fornecedor); break;
            case FiltroRapido.Ativos: filtro.SomenteAtivos = true; break;
            case FiltroRapido.Inativos: filtro.SomenteInativos = true; break;
        }
        return filtro;
    }

    private int _pagina = 1;
    public const int TamanhoPagina = PaginaListaPessoas.TamanhoPadrao;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResumoPaginacao), nameof(TemVariasPaginas), nameof(PodeVoltarPagina), nameof(PodeAvancarPagina))]
    private int _totalRegistros;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResumoPaginacao), nameof(PodeVoltarPagina), nameof(PodeAvancarPagina))]
    private int _paginaAtual = 1;

    public int TotalPaginas => Paginacao.Paginas(TotalRegistros, TamanhoPagina);
    public string ResumoPaginacao => Paginacao.Resumo(PaginaAtual, TamanhoPagina, TotalRegistros, Itens.Count);
    public bool TemVariasPaginas => TotalPaginas > 1;
    public bool PodeVoltarPagina => PaginaAtual > 1;
    public bool PodeAvancarPagina => PaginaAtual < TotalPaginas;
    public ObservableCollection<PaginaItem> Paginas { get; } = new();

    /// <summary>Lista vazia depois de ler (não durante a primeira leitura): mostra o estado vazio com "Nova pessoa".</summary>
    public bool MostrarEstadoVazio => ListaCarregada && ListaVazia;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrarEstadoVazio))]
    private bool _listaCarregada;

    protected override async Task<IReadOnlyList<PessoaResumo>> ListarAsync()
    {
        var pagina = await _pessoas.ListarPaginaAsync(FiltroAtual(), _pagina, TamanhoPagina);
        // Página que deixou de existir (ex.: filtro reduziu o total): volta para a última que existe.
        if (pagina.Itens.Count == 0 && pagina.Total > 0 && _pagina > 1)
        {
            _pagina = Paginacao.Paginas(pagina.Total, TamanhoPagina);
            pagina = await _pessoas.ListarPaginaAsync(FiltroAtual(), _pagina, TamanhoPagina);
        }
        TotalRegistros = pagina.Total;
        PaginaAtual = pagina.Pagina;
        Paginas.Clear();
        foreach (var p in Paginacao.Janela(PaginaAtual, TotalPaginas)) Paginas.Add(p);
        OnPropertyChanged(nameof(TotalPaginas));
        OnPropertyChanged(nameof(TemVariasPaginas));
        ListaCarregada = true;
        return pagina.Itens;
    }

    /// <summary>Chamado depois que a base troca as linhas (o resumo conta as linhas da página).</summary>
    protected override void DepoisDeListar()
    {
        OnPropertyChanged(nameof(ResumoPaginacao));
        OnPropertyChanged(nameof(MostrarEstadoVazio));
    }

    private Task RecarregarDaPrimeiraPaginaAsync()
    {
        _pagina = 1;
        return RecarregarAsync();
    }

    [RelayCommand]
    private Task IrParaPaginaAsync(PaginaItem? item)
    {
        if (item is not { Numero: { } numero } || numero == PaginaAtual) return Task.CompletedTask;
        _pagina = numero;
        return RecarregarAsync();
    }

    [RelayCommand]
    private Task PaginaAnteriorAsync()
    {
        if (!PodeVoltarPagina) return Task.CompletedTask;
        _pagina = PaginaAtual - 1;
        return RecarregarAsync();
    }

    [RelayCommand]
    private Task ProximaPaginaAsync()
    {
        if (!PodeAvancarPagina) return Task.CompletedTask;
        _pagina = PaginaAtual + 1;
        return RecarregarAsync();
    }

    // Colunas da tabela conforme a largura (só apresentação; a tela informa a largura disponível).
    [ObservableProperty] private bool _mostrarColunaDocumento = true;
    [ObservableProperty] private bool _mostrarColunaTipo = true;
    [ObservableProperty] private bool _mostrarColunaPapeis = true;
    [ObservableProperty] private bool _mostrarColunaCidade = true;

    /// <summary>Documento embaixo do nome quando a coluna de documento não cabe (tela estreita).</summary>
    public bool MostrarDocumentoNoNome => !MostrarColunaDocumento;

    partial void OnMostrarColunaDocumentoChanged(bool value) => OnPropertyChanged(nameof(MostrarDocumentoNoNome));

    /// <summary>Esconde primeiro as colunas menos importantes: cidade, papéis, tipo e, por último, o documento.</summary>
    public void DefinirLarguraDaLista(double largura)
    {
        if (largura <= 0) return;
        MostrarColunaCidade = largura >= 1100;
        MostrarColunaPapeis = largura >= 940;
        MostrarColunaTipo = largura >= 720;
        MostrarColunaDocumento = largura >= 600;
    }

    // ---- Ações da linha ("⋯"), respeitando as permissões ----

    private const string AcaoAbrir = "Abrir ficha";
    private const string AcaoHistorico = "Ver histórico";
    private const string AcaoDesativar = "Desativar cadastro";
    private const string AcaoReativar = "Reativar cadastro";

    [RelayCommand]
    private async Task AcoesDaLinhaAsync(PessoaResumo? linha)
    {
        if (linha is null) return;
        var podeInativar = _sessao.Possui(Permissoes.Pessoas.Inativar);
        var opcoes = new List<string> { AcaoAbrir, AcaoHistorico };
        if (podeInativar && linha.EmUso) opcoes.Add(AcaoDesativar);
        if (podeInativar && linha.Situacao == SituacaoPessoa.Inativo) opcoes.Add(AcaoReativar);

        var escolha = await EscolherAsync(linha.Nome, opcoes);
        if (escolha is null) return;

        Selecionado = linha; // abre a ficha (pergunta antes se houver alterações não salvas em outra)
        await EsperarFichaAsync(linha.Id);
        if (Formulario?.Id != linha.Id) return; // não abriu (erro ou o usuário desistiu)

        switch (escolha)
        {
            case AcaoHistorico:
                SecaoSelecionada = Secoes.FirstOrDefault(s => s.Secao == SecaoPessoa.Historico) ?? SecaoSelecionada;
                break;
            case AcaoDesativar:
                await DesativarCommand.ExecuteAsync(null);
                break;
            case AcaoReativar:
                await ReativarCommand.ExecuteAsync(null);
                break;
        }
    }

    /// <summary>A ficha abre de forma assíncrona (seleção da lista): espera até ela ser desta pessoa (no máximo alguns segundos).</summary>
    private async Task EsperarFichaAsync(Guid id)
    {
        for (var i = 0; i < 100 && (Formulario?.Id != id || Ocupado); i++)
            await Task.Delay(50);
    }

    /// <summary>Atalho da ficha e da lista: abre "Configurações de Pessoas" (a tela navega).</summary>
    public Func<Task>? AbrirConfiguracoesDoModulo { get; set; }

    [RelayCommand]
    private Task ConfiguracoesDoModuloAsync() => AbrirConfiguracoesDoModulo?.Invoke() ?? Task.CompletedTask;

    /// <summary>Ações da ficha aberta ("⋯" do cabeçalho), respeitando as permissões.</summary>
    [RelayCommand]
    private async Task AcoesDaFichaAsync()
    {
        if (Formulario is not { } ficha) return;
        var opcoes = new List<string>();
        if (ficha.Existente) opcoes.Add(AcaoHistorico);
        if (PodeDesativar) opcoes.Add(AcaoDesativar);
        if (PodeReativar) opcoes.Add(AcaoReativar);
        opcoes.Add("Fechar ficha");
        var escolha = await EscolherAsync(ficha.Titulo, opcoes);
        switch (escolha)
        {
            case AcaoHistorico:
                SecaoSelecionada = Secoes.FirstOrDefault(s => s.Secao == SecaoPessoa.Historico) ?? SecaoSelecionada;
                break;
            case AcaoDesativar:
                await DesativarCommand.ExecuteAsync(null);
                break;
            case AcaoReativar:
                await ReativarCommand.ExecuteAsync(null);
                break;
            case "Fechar ficha":
                await FecharFichaCommand.ExecuteAsync(null);
                break;
        }
    }

    // ---- Ficha ----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeSalvar), nameof(PodeDesativar), nameof(PodeReativar))]
    private PessoaFormulario? _formulario;

    public ObservableCollection<SecaoOpcao> Secoes { get; } = new();
    public ObservableCollection<HistoricoItem> Historico { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NaGeral), nameof(NosPessoais), nameof(NosEstabelecimentos), nameof(NosEnderecos),
                              nameof(NosContatos), nameof(NosDocumentos), nameof(NoComercial), nameof(NoCliente), nameof(NoFornecedor),
                              nameof(NoRelacionamento), nameof(NoHistorico), nameof(NasAdicionais), nameof(NaSituacao),
                              nameof(NoColaborador), nameof(NosRelacionamentosPessoas), nameof(NaPrivacidade))]
    private SecaoOpcao? _secaoSelecionada;

    public bool NaGeral => Aba == SecaoPessoa.Geral;
    public bool NosPessoais => Aba == SecaoPessoa.Pessoais;
    public bool NoRelacionamento => Aba == SecaoPessoa.Relacionamento;
    public bool NosEstabelecimentos => Aba == SecaoPessoa.Estabelecimentos;
    public bool NosEnderecos => Aba == SecaoPessoa.Enderecos;
    public bool NosContatos => Aba == SecaoPessoa.Contatos;
    public bool NosDocumentos => Aba == SecaoPessoa.Documentos;
    /// <summary>Aba "Comercial": um bloco por papel comercial ativo (Cliente e/ou Fornecedor), na mesma pessoa.</summary>
    public bool NoComercial => Aba == SecaoPessoa.Comercial;
    public bool NoCliente => NoComercial && Formulario?.PapelCliente.Ativo == true;
    public bool NoFornecedor => NoComercial && Formulario?.PapelFornecedor.Ativo == true;
    public bool NosRelacionamentosPessoas => Aba == SecaoPessoa.RelacionamentosPessoas;
    public bool NaPrivacidade => Aba == SecaoPessoa.Privacidade;

    /// <summary>Grupo empresarial e vínculos societários (sócio, administrador) exigem a permissão de estrutura empresarial.</summary>
    public bool PodeAlterarEstruturaEmpresarial => _sessao.Possui(Permissoes.Pessoas.EstruturaEmpresarial);
    public bool NoHistorico => Aba == SecaoPessoa.Historico;
    public bool NasAdicionais => Aba == SecaoPessoa.Adicionais;
    public bool NaSituacao => Aba == SecaoPessoa.Situacao;
    public bool NoColaborador => Aba == SecaoPessoa.Colaborador;

    private SecaoPessoa Aba => SecaoSelecionada?.Secao ?? SecaoPessoa.Geral;

    /// <summary>Incluir exige "cadastrar"; alterar exige "alterar". A API confere de novo ao gravar.</summary>
    public bool PodeSalvar => Formulario is { EstaArquivado: false } f && _sessao.Possui(f.Nova ? Permissoes.Pessoas.Criar : Permissoes.Pessoas.Editar);

    /// <summary>Desativar/reativar: cadastro já gravado e permissão de inativar (a API confere de novo).</summary>
    public bool PodeDesativar => Formulario is { Existente: true, PodeEscolherSituacao: true } && _sessao.Possui(Permissoes.Pessoas.Inativar);
    public bool PodeReativar => Formulario is { Existente: true, EstaInativo: true } && _sessao.Possui(Permissoes.Pessoas.Inativar);

    protected override async Task AbrirAsync(PessoaResumo item)
    {
        var dto = await _pessoas.ObterAsync(item.Id) ?? throw new ValidacaoException(["Este cadastro não existe mais."]);
        Formulario = PessoaFormulario.De(dto, _campos, _etiquetas, _profissoes, _papeis, _tiposMeio, _tiposEndereco, _tiposDocumento, _camposDocumento, _finalidadesEndereco);
    }

    protected override Task NovoItemAsync()
    {
        Formulario = PessoaFormulario.NovaPessoa(_campos, _etiquetas, _profissoes, _papeis, _tiposMeio, _tiposEndereco, _tiposDocumento, _camposDocumento, _finalidadesEndereco);
        return Task.CompletedTask;
    }

    protected override object? DadosDaFicha() => Formulario?.ParaDto();
    protected override bool FichaNova => Formulario?.Nova ?? true;

    /// <summary>Volta a ficha ao que está gravado, na mesma aba.</summary>
    protected override async Task RecarregarFichaAsync()
    {
        if (Formulario is not { Existente: true } formulario) return;
        var dto = await _pessoas.ObterAsync(formulario.Id) ?? throw new ValidacaoException(["Este cadastro não existe mais."]);
        MostrarGravada(dto);
    }

    /// <summary>Troca a ficha pela versão gravada, ficando na aba atual se ela ainda existir.</summary>
    private void MostrarGravada(PessoaDto dto)
    {
        var aba = Aba;
        Formulario = PessoaFormulario.De(dto, _campos, _etiquetas, _profissoes, _papeis, _tiposMeio, _tiposEndereco, _tiposDocumento, _camposDocumento, _finalidadesEndereco);
        SecaoSelecionada = Secoes.FirstOrDefault(s => s.Secao == aba) ?? Secoes[0];
    }

    partial void OnFormularioChanged(PessoaFormulario? oldValue, PessoaFormulario? newValue)
    {
        if (oldValue is not null) Desligar(oldValue);
        Historico.Clear();
        _historicoDe = null;
        _ultimoDoHistorico = null;
        TemMaisHistorico = false;
        if (newValue is null) return;

        newValue.PodeVerDadosSensiveis = _sessao.Possui(Permissoes.Pessoas.VisualizarDadosSensiveis);
        newValue.PodeVerPrivacidade = _sessao.Possui(Permissoes.Pessoas.Privacidade);
        newValue.Privacidade.Acoes.Conceder = ConcederConsentimentoAsync;
        newValue.Privacidade.Acoes.Revogar = RevogarConsentimentoAsync;
        newValue.ConsultaCep = ConsultarCepAsync;
        newValue.Confirmar = ConfirmarAsync; // diálogo da base (CadastroViewModelBase)
        newValue.ConsolidarNoServidor = ConsolidarEnderecosAsync;
        newValue.TemAlteracoesNaoSalvas = () => TemAlteracoes;
        newValue.ConsultaCnpj = ConsultarCnpjAsync;
        newValue.FonteMunicipios = uf => _municipios.ListarDaUfAsync(uf);
        newValue.AcoesAnexos.Anexar = AnexarAsync;
        newValue.Situacoes.Acoes.Bloquear = BloquearAsync;
        newValue.Situacoes.Acoes.Liberar = LiberarBloqueioAsync;
        newValue.Situacoes.Acoes.RegistrarInteracao = RegistrarInteracaoAsync;
        newValue.Relacionamentos.Acoes.BuscarPessoa = BuscarPessoaParaRelacionamentoAsync;
        newValue.Relacionamentos.Acoes.Incluir = IncluirRelacionamentoAsync;
        newValue.Relacionamentos.Acoes.Encerrar = EncerrarRelacionamentoAsync;
        newValue.Relacionamentos.Acoes.Desativar = DesativarRelacionamentoAsync;
        newValue.AcoesAnexos.Abrir = AbrirAnexoAsync;
        newValue.AcoesAnexos.AlterarAtivo = AlterarAnexoAsync;
        newValue.PropertyChanged += Formulario_PropertyChanged;
        foreach (var papel in newValue.Papeis) papel.PropertyChanged += Papel_PropertyChanged;
        AtualizarSecoes(manterAba: false);
    }

    private void Desligar(PessoaFormulario formulario)
    {
        formulario.ConsultaCep = null;
        formulario.Confirmar = null;
        formulario.ConsultaCnpj = null;
        formulario.FonteMunicipios = null;
        formulario.AcoesAnexos.Anexar = null;
        formulario.Situacoes.Acoes.Bloquear = null;
        formulario.Situacoes.Acoes.Liberar = null;
        formulario.Situacoes.Acoes.RegistrarInteracao = null;
        formulario.Relacionamentos.Acoes.BuscarPessoa = null;
        formulario.Relacionamentos.Acoes.Incluir = null;
        formulario.Relacionamentos.Acoes.Encerrar = null;
        formulario.Relacionamentos.Acoes.Desativar = null;
        formulario.Privacidade.Acoes.Conceder = null;
        formulario.Privacidade.Acoes.Revogar = null;
        formulario.AcoesAnexos.Abrir = null;
        formulario.AcoesAnexos.AlterarAtivo = null;
        formulario.PropertyChanged -= Formulario_PropertyChanged;
        foreach (var papel in formulario.Papeis) papel.PropertyChanged -= Papel_PropertyChanged;
    }

    private void Formulario_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PessoaFormulario.Natureza)) AtualizarSecoes(manterAba: true);
    }

    private void Papel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PapelOpcao.Ativo)) return;
        AtualizarSecoes(manterAba: true);
        // Na aba "Comercial", o bloco do papel aparece ou some na hora (a aba continua a mesma).
        OnPropertyChanged(nameof(NoCliente));
        OnPropertyChanged(nameof(NoFornecedor));
    }

    /// <summary>Refaz as abas (natureza e papéis mudam quais aparecem), ficando na atual se ela ainda existir.</summary>
    private void AtualizarSecoes(bool manterAba)
    {
        if (Formulario is null) return;
        var atual = Aba;
        Secoes.Clear();
        foreach (var secao in SecaoOpcao.Para(Formulario)) Secoes.Add(secao);
        var nova = (manterAba ? Secoes.FirstOrDefault(s => s.Secao == atual) : null) ?? Secoes[0];
        // SecaoOpcao é record: a mesma aba (ex.: Identificação ao abrir outra ficha) não dispara a troca, mas a ficha
        // nova (ou a natureza nova) pode precisar das cargas da aba.
        if (Equals(SecaoSelecionada, nova)) CarregarDaAba(nova);
        else SecaoSelecionada = nova;
    }

    /// <summary>Histórico já lido para a ficha aberta (lido uma vez, ao abrir a aba).</summary>
    private Guid? _historicoDe;

    [ObservableProperty] private bool _carregandoHistorico;

    partial void OnSecaoSelecionadaChanged(SecaoOpcao? value) => CarregarDaAba(value);

    /// <summary>Cargas sob demanda: cada uma só na primeira vez que a aba precisa, nesta ficha.</summary>
    private void CarregarDaAba(SecaoOpcao? value)
    {
        if (value?.Secao == SecaoPessoa.Historico && Formulario is { Existente: true } f && _historicoDe != f.Id)
            _ = CarregarHistoricoAsync(f.Id);
        if (value?.Secao == SecaoPessoa.Colaborador && Formulario is { OpcoesColaboradorCarregadas: false } ficha)
            _ = CarregarOpcoesColaboradorAsync(ficha);
        if (value?.Secao == SecaoPessoa.Comercial && Formulario is { OpcoesComercialCarregadas: false } fichaComercial)
            _ = CarregarOpcoesComercialAsync(fichaComercial);
        // O grupo empresarial fica na Identificação (só pessoa jurídica).
        if (value?.Secao == SecaoPessoa.Geral && Formulario is { EhJuridica: true, OpcoesEstruturaCarregadas: false } fichaEmpresa)
            _ = CarregarOpcoesEstruturaAsync(fichaEmpresa);
        if (value?.Secao == SecaoPessoa.RelacionamentosPessoas && Formulario is { Existente: true } fichaRelacoes)
            _ = CarregarRelacionamentosAsync(fichaRelacoes);
        if (value?.Secao == SecaoPessoa.Privacidade && Formulario is { Existente: true, Privacidade.Carregada: false } fichaPrivacidade)
            _ = CarregarPrivacidadeAsync(fichaPrivacidade);
    }

    private Task? _cargaEstrutura;
    private PessoaFormulario? _cargaEstruturaDe;

    /// <summary>
    /// Grupos empresariais e tipos de relacionamento: lidos na primeira vez que uma aba precisa, nesta ficha.
    /// Identificação e Relacionamentos podem pedir ao mesmo tempo: a segunda espera a leitura em andamento.
    /// </summary>
    private Task CarregarOpcoesEstruturaAsync(PessoaFormulario ficha)
    {
        if (ficha.OpcoesEstruturaCarregadas) return Task.CompletedTask;
        if (ReferenceEquals(_cargaEstruturaDe, ficha) && _cargaEstrutura is { IsCompleted: false } emAndamento) return emAndamento;
        _cargaEstruturaDe = ficha;
        return _cargaEstrutura = LerOpcoesEstruturaAsync(ficha);
    }

    private async Task LerOpcoesEstruturaAsync(PessoaFormulario ficha)
    {
        try
        {
            var opcoes = await _pessoas.ListarOpcoesEstruturaAsync();
            if (!ReferenceEquals(Formulario, ficha)) return;
            ficha.DefinirGruposEmpresariais(opcoes.GruposEmpresariais); // o grupo gravado continua o mesmo
            ficha.Relacionamentos.DefinirTipos(opcoes.TiposRelacionamento);
        }
        catch (SessaoExpiradaException)
        {
        }
        catch (Exception ex)
        {
            MostrarErro(ex); // sem as opções, o grupo gravado volta intacto ao salvar
        }
    }

    /// <summary>Relacionamentos da pessoa (os dois sentidos), lidos quando a aba abre.</summary>
    private async Task CarregarRelacionamentosAsync(PessoaFormulario ficha)
    {
        try
        {
            if (!ficha.OpcoesEstruturaCarregadas) await CarregarOpcoesEstruturaAsync(ficha);
            if (ficha.Relacionamentos.Carregados) return;
            var vinculos = await _pessoas.ListarRelacionamentosAsync(ficha.Id);
            if (!ReferenceEquals(Formulario, ficha)) return;
            ficha.Relacionamentos.Carregar(vinculos);
        }
        catch (SessaoExpiradaException)
        {
        }
        catch (Exception ex)
        {
            MostrarErro(ex);
        }
    }

    /// <summary>Id do último registro mostrado: a próxima página começa antes dele.</summary>
    private long? _ultimoDoHistorico;

    /// <summary>A última página veio cheia: pode haver registros mais antigos.</summary>
    [ObservableProperty] private bool _temMaisHistorico;

    [RelayCommand]
    private Task CarregarMaisHistoricoAsync() =>
        Formulario is { Existente: true } f && TemMaisHistorico && !CarregandoHistorico
            ? CarregarHistoricoAsync(f.Id, continuar: true)
            : Task.CompletedTask;

    /// <summary>Independente do "ocupado" da tela: não apaga mensagens nem espera outra operação.</summary>
    private async Task CarregarHistoricoAsync(Guid pessoaId, bool continuar = false)
    {
        _historicoDe = pessoaId;
        CarregandoHistorico = true;
        try
        {
            var registros = await _pessoas.ListarHistoricoAsync(pessoaId, continuar ? _ultimoDoHistorico : null);
            if (Formulario?.Id != pessoaId) return; // outra ficha foi aberta enquanto lia
            if (!continuar) Historico.Clear();
            foreach (var r in registros) Historico.Add(HistoricoItem.De(r));
            if (registros.Count > 0) _ultimoDoHistorico = registros[^1].Id;
            TemMaisHistorico = registros.Count >= PessoasApi.PaginaHistorico;
        }
        catch (SessaoExpiradaException)
        {
            // O aplicativo já volta ao login.
        }
        catch (Exception ex)
        {
            _historicoDe = null; // tenta de novo na próxima vez que a aba abrir
            MostrarErro(ex);
        }
        finally
        {
            CarregandoHistorico = false;
        }
    }

    // ---- Gravação ----

    [RelayCommand]
    private async Task SalvarAsync()
    {
        if (Formulario is not { } formulario) return;

        if (formulario.ValidarLocalmente() is { Count: > 0 } erros)
        {
            Mostrar(string.Join(Environment.NewLine, erros), TipoMensagem.Erro);
            return;
        }

        var eraEmpresaDoGrupo = formulario.PapelEmpresaDoGrupo.Existia && !formulario.Nova;
        ResultadoSalvarPessoa? resultado = null;
        if (!await ExecutarAsync(async () => resultado = await _pessoas.SalvarAsync(formulario.ParaDto())))
            return;

        MostrarGravada(resultado!.Pessoa);
        MarcarFichaSemAlteracoes();

        Mostrar(resultado.Avisos.Count > 0
                ? "Salvo, com avisos:" + Environment.NewLine + string.Join(Environment.NewLine, resultado.Avisos)
                : "Cadastro salvo.",
            resultado.Avisos.Count > 0 ? TipoMensagem.Aviso : TipoMensagem.Sucesso);

        // Empresas do grupo mudaram: a sessão relê as empresas disponíveis (menu "Trocar empresa").
        if (eraEmpresaDoGrupo || resultado.Pessoa.TemPapel(TipoPapel.EmpresaDoGrupo))
            await AtualizarEmpresasDaSessaoAsync();

        await AtualizarListaAposGravarAsync();
    }

    private async Task AtualizarEmpresasDaSessaoAsync()
    {
        try
        {
            await _autenticacao.AtualizarSessaoAsync();
        }
        catch (SessaoExpiradaException)
        {
            // O aplicativo já volta ao login com a mensagem.
        }
        catch (Exception)
        {
            Mostrar(Mensagem + Environment.NewLine + "A lista de empresas será atualizada no próximo acesso.", TipoMensagem.Aviso);
        }
    }

    // ---- Situação (desativar e reativar) ----

    [RelayCommand]
    private Task DesativarAsync() => AlterarSituacaoAsync(desativar: true);

    [RelayCommand]
    private Task ReativarAsync() => AlterarSituacaoAsync(desativar: false);

    /// <summary>
    /// Pede o motivo (opcional) e chama a ação própria da API. Não mistura com alterações não salvas: o usuário
    /// salva ou descarta antes, para ficar claro o que foi gravado.
    /// </summary>
    private async Task AlterarSituacaoAsync(bool desativar)
    {
        if (Formulario is not { Existente: true } formulario) return;
        if (TemAlteracoes)
        {
            Mostrar("Salve ou descarte as alterações antes de " + (desativar ? "desativar" : "reativar") + " o cadastro.", TipoMensagem.Aviso);
            return;
        }

        var motivo = await PerguntarAsync(
            desativar ? "Desativar cadastro" : "Reativar cadastro",
            desativar
                ? $"{formulario.Nome} deixará de aparecer nas buscas e operações (continua no filtro \"Inativos\", com todo o histórico, e pode ser reativado). Motivo (opcional):"
                : $"{formulario.Nome} volta a aparecer nas buscas e operações. Motivo (opcional):",
            desativar ? "Desativar" : "Reativar",
            "Cancelar",
            desativar ? "Ex.: não compra há 2 anos" : null,
            200);
        if (motivo is null) return; // cancelou

        PessoaDto? gravada = null;
        if (!await ExecutarAsync(async () => gravada = desativar
                ? await _pessoas.DesativarAsync(formulario.Id, formulario.Versao, TextoTela.Nulo(motivo))
                : await _pessoas.ReativarAsync(formulario.Id, formulario.Versao, TextoTela.Nulo(motivo))))
            return;

        MostrarGravada(gravada!);
        MarcarFichaSemAlteracoes();
        Mostrar(desativar ? "Cadastro desativado. Ele continua no filtro \"Inativos\"." : "Cadastro reativado.", TipoMensagem.Sucesso);
        await AtualizarListaAposGravarAsync();
    }

    /// <summary>
    /// Consolidação de endereços: operação própria da API (como desativar), gravada na hora. Não mistura com alterações
    /// não salvas (a ficha confere antes de perguntar); depois, a ficha mostra o que o servidor gravou.
    /// </summary>
    private async Task ConsolidarEnderecosAsync(Guid origemId, Guid destinoId)
    {
        if (Formulario is not { Existente: true } formulario) return;
        PessoaDto? gravada = null;
        if (!await ExecutarAsync(async () => gravada = await _pessoas.ConsolidarEnderecosAsync(formulario.Id, formulario.Versao, origemId, destinoId)))
            return;

        MostrarGravada(gravada!);
        MarcarFichaSemAlteracoes();
        Mostrar("Endereços consolidados. O endereço consolidado ficou inativo, no histórico.", TipoMensagem.Sucesso);
    }

    // ---- Itens das listas da ficha ----

    [RelayCommand]
    private void AdicionarEstabelecimento() => Formulario?.AdicionarEstabelecimento();

    /// <summary>O primeiro item de cada lista já nasce como principal.</summary>
    [RelayCommand]
    private void AdicionarEndereco()
    {
        if (Formulario is { } f) f.AdicionarEndereco(new EnderecoFormulario()); // finalidades e principal: escolhidos pelo usuário
    }

    /// <summary>Aba Contatos: cada lista tem o seu "Adicionar" (o tipo já vem escolhido).</summary>
    [RelayCommand]
    private void AdicionarTelefone() => Formulario?.NovoMeio(TipoContato.Celular);

    [RelayCommand]
    private void AdicionarEmail() => Formulario?.NovoMeio(TipoContato.Email);

    [RelayCommand]
    private void AdicionarContato()
    {
        if (Formulario is { } f) f.AdicionarContato(new ContatoFormulario { Principal = f.Contatos.Count == 0 });
    }

    [RelayCommand]
    private void AdicionarDocumento() => Formulario?.AdicionarDocumento(new DocumentoFormulario());

    [RelayCommand]
    private void AdicionarVinculo() => Formulario?.NovoVinculo();

    [RelayCommand]
    private void AdicionarExcecao() => Formulario?.NovaExcecao();

    [RelayCommand]
    private void AdicionarCarteira() => Formulario?.NovaCarteira();

    /// <summary>Perfis, condições, tipos de carteira e vendedores: lidos na primeira vez que a aba "Comercial" abre nesta ficha.</summary>
    private async Task CarregarOpcoesComercialAsync(PessoaFormulario ficha)
    {
        try
        {
            var opcoes = await _comercialApi.ListarOpcoesAsync();
            if (!ReferenceEquals(Formulario, ficha)) return;
            ficha.DefinirOpcoesComercial(opcoes);
        }
        catch (SessaoExpiradaException)
        {
        }
        catch (Exception ex)
        {
            MostrarErro(ex); // sem as opções, perfil, condição e carteira gravados voltam intactos ao salvar
        }
    }

    /// <summary>Empresas, gestores e estrutura: lidos na primeira vez que a aba "Colaborador" abre nesta ficha.</summary>
    private async Task CarregarOpcoesColaboradorAsync(PessoaFormulario ficha)
    {
        try
        {
            var opcoes = await _colaboradoresApi.ListarOpcoesAsync();
            if (!ReferenceEquals(Formulario, ficha)) return; // outra ficha foi aberta enquanto lia
            ficha.DefinirOpcoesColaborador(opcoes); // as escolhas gravadas continuam as mesmas: a ficha não fica "alterada"
        }
        catch (SessaoExpiradaException)
        {
            // O aplicativo já volta ao login.
        }
        catch (Exception ex)
        {
            MostrarErro(ex); // sem as opções, as escolhas gravadas continuam e voltam intactas ao salvar
        }
    }

    /// <summary>
    /// Atalho da ficha: cria a etiqueta no cadastro (fica disponível para todos) e já a marca nesta pessoa.
    /// A pessoa só é gravada ao salvar a ficha.
    /// </summary>
    [RelayCommand]
    private async Task NovaEtiquetaAsync()
    {
        if (Formulario is not { } formulario) return;
        var nome = await PerguntarAsync(
            "Nova etiqueta",
            "Nome da etiqueta. Ela fica disponível para todos os cadastros:",
            "Criar", "Cancelar", "Ex.: Cliente VIP", global::Lone.Domain.Entidades.Etiqueta.TamanhoMaximoNome);
        if (string.IsNullOrWhiteSpace(nome)) return;

        EtiquetaDto? criada = null;
        if (!await ExecutarAsync(async () =>
                criada = await _etiquetasApi.SalvarAsync(new EtiquetaDto { Id = IdSequencial.Novo(), Nome = nome.Trim() })))
            return;

        var etiqueta = criada!;
        _etiquetas.Add(etiqueta);
        FiltrosEtiqueta.Add(new Opcao<Guid?>(etiqueta.Id, etiqueta.Nome));
        formulario.Etiquetas.Incluir(etiqueta, marcar: true);
        Mostrar($"Etiqueta \"{etiqueta.Nome}\" criada e marcada. Salve o cadastro para gravar a marcação.", TipoMensagem.Sucesso);
    }

    /// <summary>Atalho da ficha: cria a profissão no cadastro (fica disponível para todos) e já a escolhe.</summary>
    [RelayCommand]
    private async Task NovaProfissaoAsync()
    {
        if (Formulario is not { } formulario) return;
        var nome = await PerguntarAsync(
            "Nova profissão",
            "Nome da profissão. Ela fica disponível para todos os cadastros (a ocupação CBO pode ser informada depois, em Configurações, \"Profissões\"):",
            "Criar", "Cancelar", "Ex.: Advogado", global::Lone.Domain.Entidades.Profissao.TamanhoMaximoNome);
        if (string.IsNullOrWhiteSpace(nome)) return;

        ProfissaoDto? criada = null;
        if (!await ExecutarAsync(async () =>
                criada = await _profissoesApi.SalvarAsync(new ProfissaoDto { Id = IdSequencial.Novo(), Nome = nome.Trim() })))
            return;

        var profissao = criada!;
        _profissoes.Add(profissao);
        formulario.IncluirProfissao(profissao);
        Mostrar($"Profissão \"{profissao.Nome}\" criada e escolhida. Salve o cadastro para gravar.", TipoMensagem.Sucesso);
    }

    // ---- Consultas externas ----

    private async Task ConsultarCnpjAsync(EstabelecimentoFormulario estabelecimento)
    {
        if (!Documento.CnpjValido(estabelecimento.Cnpj))
        {
            Mostrar("Digite um CNPJ válido para consultar.", TipoMensagem.Aviso);
            return;
        }

        DadosCnpj? dados = null;
        if (!await ExecutarAsync(async () => dados = await _consultas.ConsultarCnpjAsync(Documento.Normalizar(estabelecimento.Cnpj))))
            return;

        if (dados is null)
        {
            Mostrar("CNPJ não encontrado na Receita Federal.", TipoMensagem.Aviso);
            return;
        }

        Formulario?.AplicarCnpj(estabelecimento, dados);
        Mostrar($"Dados preenchidos pela consulta ({dados.Fonte}). " +
                (estabelecimento.InscricaoVeioDaConsulta
                    ? "A inscrição estadual também foi encontrada. Confira e salve."
                    : "A inscrição estadual não foi encontrada nas fontes públicas: informe-a (o sistema confere o dígito da UF ao salvar)."),
            TipoMensagem.Informacao);
    }

    // ---- Bloqueios e interações (gravados na hora, à parte do "Salvar" da ficha) ----

    private async Task BloquearAsync()
    {
        if (Formulario is not { Existente: true } ficha)
        {
            Mostrar("Salve o cadastro antes de bloquear.", TipoMensagem.Aviso);
            return;
        }
        var s = ficha.Situacoes;
        if (string.IsNullOrWhiteSpace(s.NovoMotivo))
        {
            Mostrar("Informe o motivo do bloqueio.", TipoMensagem.Aviso);
            return;
        }

        BloqueioDto? bloqueio = null;
        if (!await ExecutarAsync(async () => bloqueio = await _pessoas.BloquearAsync(ficha.Id,
                new BloquearRequisicao { Escopo = s.NovoEscopo.Valor, Motivo = s.NovoMotivo.Trim() })))
            return;
        s.IncluirBloqueio(bloqueio!);
        Mostrar($"Bloqueio {SituacoesFormulario.NomeEscopo(bloqueio!.Escopo).ToLowerInvariant()} registrado.", TipoMensagem.Sucesso);
    }

    private async Task LiberarBloqueioAsync(BloqueioItem item)
    {
        if (Formulario is not { } ficha) return;
        var motivo = await PerguntarAsync("Liberar bloqueio", $"{item.Titulo}: motivo da liberação (fica no histórico):", "Liberar", "Cancelar");
        if (string.IsNullOrWhiteSpace(motivo)) return;

        BloqueioDto? liberado = null;
        if (!await ExecutarAsync(async () => liberado = await _pessoas.LiberarBloqueioAsync(ficha.Id, item.Id, motivo.Trim())))
            return;
        ficha.Situacoes.Liberado(item, liberado!);
        Mostrar("Bloqueio liberado.", TipoMensagem.Sucesso);
    }

    private async Task RegistrarInteracaoAsync()
    {
        if (Formulario is not { Existente: true } ficha)
        {
            Mostrar("Salve o cadastro antes de registrar interações.", TipoMensagem.Aviso);
            return;
        }
        var s = ficha.Situacoes;
        InteracaoDto? interacao = null;
        if (!await ExecutarAsync(async () => interacao = await _pessoas.RegistrarInteracaoAsync(ficha.Id,
                new RegistrarInteracaoRequisicao { Tipo = s.NovoTipo.Valor, Descricao = s.NovaDescricao })))
            return;
        s.IncluirInteracao(interacao!);
        Mostrar("Interação registrada.", TipoMensagem.Sucesso);
    }

    // ---- Privacidade (LGPD): consentimentos gravados na hora, à parte do "Salvar" da ficha ----

    /// <summary>Consentimentos, canais e decisões (calculadas pela regra do domínio na API), lidos quando a aba abre.</summary>
    private async Task CarregarPrivacidadeAsync(PessoaFormulario ficha)
    {
        try
        {
            var privacidade = await _pessoas.ObterPrivacidadeAsync(ficha.Id);
            if (!ReferenceEquals(Formulario, ficha)) return;
            ficha.Privacidade.Carregar(privacidade);
        }
        catch (SessaoExpiradaException)
        {
        }
        catch (Exception ex)
        {
            MostrarErro(ex);
        }
    }

    private async Task ConcederConsentimentoAsync()
    {
        if (Formulario is not { Existente: true } ficha) return;
        var p = ficha.Privacidade;
        if (p.ValidarConcessao() is { Count: > 0 } erros)
        {
            Mostrar(string.Join(Environment.NewLine, erros), TipoMensagem.Aviso);
            return;
        }

        PrivacidadeDto? atualizada = null;
        if (!await ExecutarAsync(async () => atualizada = await _pessoas.ConcederConsentimentoAsync(ficha.Id, p.ParaConcessao())))
            return;
        p.Carregar(atualizada!);
        p.LimparConcessao();
        Mostrar("Consentimento concedido e registrado no histórico.", TipoMensagem.Sucesso);
    }

    private async Task RevogarConsentimentoAsync(PeriodoConsentimentoItem item)
    {
        if (Formulario is not { } ficha) return;
        var motivo = await PerguntarAsync("Revogar consentimento",
            $"{item.Titulo}: o consentimento deixa de valer a partir de agora e o período fica no histórico. Motivo:",
            "Revogar", "Cancelar", "Ex.: pediu por e-mail", 250);
        if (string.IsNullOrWhiteSpace(motivo)) return;

        PrivacidadeDto? atualizada = null;
        if (!await ExecutarAsync(async () => atualizada = await _pessoas.RevogarConsentimentoAsync(ficha.Id, item.Id, motivo.Trim())))
            return;
        ficha.Privacidade.Carregar(atualizada!);
        Mostrar("Consentimento revogado. O período continua no histórico.", TipoMensagem.Sucesso);
    }

    // ---- Relacionamentos com outros cadastros (gravados na hora, à parte do "Salvar" da ficha) ----

    private async Task BuscarPessoaParaRelacionamentoAsync()
    {
        if (Formulario is not { } ficha) return;
        var r = ficha.Relacionamentos;
        if (string.IsNullOrWhiteSpace(r.BuscaPessoa) || r.BuscaPessoa.Trim().Length < 2)
        {
            Mostrar("Digite ao menos 2 letras (nome, código, CPF ou CNPJ) para buscar.", TipoMensagem.Aviso);
            return;
        }

        List<PessoaResumo>? achadas = null;
        if (!await ExecutarAsync(async () => achadas = await _pessoas.ListarAsync(new FiltroPessoas { Texto = r.BuscaPessoa.Trim(), Limite = 20 })))
            return;
        r.DefinirResultados(achadas!.Where(p => p.Id != ficha.Id)); // a própria pessoa não se relaciona com ela mesma
        if (r.ResultadosBusca.Count == 0) Mostrar("Nenhum cadastro encontrado.", TipoMensagem.Informacao);
    }

    private async Task IncluirRelacionamentoAsync()
    {
        if (Formulario is not { Existente: true } ficha)
        {
            Mostrar("Salve o cadastro antes de registrar relacionamentos.", TipoMensagem.Aviso);
            return;
        }
        var r = ficha.Relacionamentos;
        if (r.ValidarNovo() is { Count: > 0 } erros)
        {
            Mostrar(string.Join(Environment.NewLine, erros), TipoMensagem.Aviso);
            return;
        }
        if (r.NovoTipo.Valor is { Societario: true } && !PodeAlterarEstruturaEmpresarial)
        {
            Mostrar("Vínculos societários (sócio, administrador) exigem a permissão \"Alterar estrutura empresarial\".", TipoMensagem.Aviso);
            return;
        }

        PessoaRelacionamentoDto? incluido = null;
        if (!await ExecutarAsync(async () => incluido = await _pessoas.IncluirRelacionamentoAsync(ficha.Id, r.ParaRequisicao())))
            return;
        r.Incluido(incluido!);
        Mostrar($"Relacionamento registrado: {incluido!.Tipo} {incluido.OutraPessoaNome}.", TipoMensagem.Sucesso);
    }

    private async Task EncerrarRelacionamentoAsync(RelacionamentoItem item)
    {
        if (Formulario is not { } ficha) return;
        var texto = await PerguntarAsync("Encerrar relacionamento",
            $"{item.Titulo}: data de fim (dd/mm/aaaa; vazio = hoje). O relacionamento continua gravado, com o período.",
            "Encerrar", "Cancelar", "dd/mm/aaaa", 10);
        if (texto is null) return; // cancelou
        if (!TextoTela.TentarData(texto, out var fim))
        {
            Mostrar("Data inválida (use dd/mm/aaaa).", TipoMensagem.Aviso);
            return;
        }

        PessoaRelacionamentoDto? encerrado = null;
        if (!await ExecutarAsync(async () => encerrado = await _pessoas.EncerrarRelacionamentoAsync(ficha.Id, item.Id,
                new EncerrarRelacionamentoRequisicao { FimEm = fim })))
            return;
        ficha.Relacionamentos.Atualizado(item, encerrado!);
        Mostrar("Relacionamento encerrado (continua no histórico; aparece em \"Mostrar encerrados\").", TipoMensagem.Sucesso);
    }

    private async Task DesativarRelacionamentoAsync(RelacionamentoItem item)
    {
        if (Formulario is not { } ficha) return;
        var motivo = await PerguntarAsync("Desativar relacionamento",
            $"{item.Titulo}: use só para o que foi lançado por engano (um vínculo que acabou deve ser encerrado). Motivo (opcional):",
            "Desativar", "Cancelar", "Ex.: pessoa errada", 250);
        if (motivo is null) return;

        PessoaRelacionamentoDto? desativado = null;
        if (!await ExecutarAsync(async () => desativado = await _pessoas.DesativarRelacionamentoAsync(ficha.Id, item.Id, TextoTela.Nulo(motivo))))
            return;
        ficha.Relacionamentos.Atualizado(item, desativado!);
        Mostrar("Relacionamento desativado (continua no histórico).", TipoMensagem.Sucesso);
    }

    // ---- Anexos dos documentos (gravados na hora, à parte do "Salvar" da ficha) ----

    private async Task AnexarAsync(DocumentoFormulario documento)
    {
        if (Formulario is not { } ficha) return;
        if (!documento.PodeAnexar)
        {
            Mostrar(documento.Gravado ? "Reative o documento para anexar arquivos." : "Salve o cadastro antes de anexar arquivos a este documento.",
                TipoMensagem.Aviso);
            return;
        }

        var arquivo = await _arquivos.EscolherAsync("Arquivo do documento (PDF, JPG ou PNG)");
        if (arquivo is null) return;

        AnexoDto? enviado = null;
        if (!await ExecutarAsync(async () => enviado = await _anexosApi.EnviarAsync(ficha.Id, documento.Id, arquivo.Nome, arquivo.Conteudo)))
            return;
        documento.IncluirAnexo(enviado!);
        Mostrar($"Arquivo \"{enviado!.NomeArquivo}\" anexado.", TipoMensagem.Sucesso);
    }

    private async Task AbrirAnexoAsync(AnexoFormulario anexo)
    {
        AnexoConteudoDto? arquivo = null;
        if (!await ExecutarAsync(async () => arquivo = await _anexosApi.BaixarAsync(anexo.Id)))
            return;
        try
        {
            await _arquivos.AbrirAsync(arquivo!.NomeArquivo, arquivo.Conteudo);
        }
        catch (Exception ex)
        {
            Mostrar($"Não foi possível abrir o arquivo neste aparelho: {ex.Message}", TipoMensagem.Erro);
        }
    }

    private async Task AlterarAnexoAsync(AnexoFormulario anexo, bool ativo)
    {
        AnexoDto? gravado = null;
        if (!await ExecutarAsync(async () => gravado = ativo ? await _anexosApi.ReativarAsync(anexo.Id) : await _anexosApi.DesativarAsync(anexo.Id)))
            return;
        anexo.Atualizar(gravado!);
        Mostrar(ativo ? "Anexo reativado." : "Anexo removido (continua guardado; aparece em \"Mostrar inativos\").", TipoMensagem.Sucesso);
    }

    private async Task ConsultarCepAsync(EnderecoFormulario endereco)
    {
        if (!Cep.TentarCriar(endereco.Cep, out var cep))
        {
            Mostrar("Digite um CEP válido (8 dígitos) para buscar.", TipoMensagem.Aviso);
            return;
        }

        DadosCep? dados = null;
        if (!await ExecutarAsync(async () => dados = await _consultas.ConsultarCepAsync(cep!.Valor)))
            return;

        if (dados is null)
        {
            Mostrar($"O CEP {cep!.Formatado} não existe na base dos Correios. Confira o número.", TipoMensagem.Aviso);
            return;
        }

        endereco.AplicarCep(dados);
        if (string.IsNullOrWhiteSpace(dados.Logradouro))
            Mostrar($"CEP geral de {dados.Cidade}/{dados.Uf}: esta cidade não tem CEP por rua. Informe o logradouro e o bairro.",
                TipoMensagem.Informacao);
    }
}
