using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.Api;
using Lone.Cliente.Mensagens;
using Lone.Cliente.Navegacao;
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
    private readonly TerritoriosApi _territoriosApi;
    private readonly IArquivos _arquivos;

    /// <summary>Campos personalizados ativos (lidos ao abrir a tela).</summary>
    private IReadOnlyList<CampoPersonalizadoDto> _campos = [];

    /// <summary>Campos personalizados dos documentos (ativos; cada um de um tipo de documento).</summary>
    private IReadOnlyList<CampoPersonalizadoDto> _camposDocumento = [];

    /// <summary>Cadastro de etiquetas, com as desativadas (lido ao abrir a tela; a ficha oferece só as ativas).</summary>
    private List<EtiquetaDto> _etiquetas = [];

    /// <summary>Cadastro de profissões, com as desativadas (a ficha oferece só as ativas e mostra a gravada).</summary>
    private List<ProfissaoDto> _profissoes = [];

    /// <summary>Tabela oficial da CBO: a ficha oferece as ocupações que ainda não têm profissão (vazia se não importada).</summary>
    private List<OcupacaoCboDto> _ocupacoesCbo = [];

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
                            TerritoriosApi territoriosApi, IArquivos arquivos, IDialogos dialogos)
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
        _territoriosApi = territoriosApi;
        _arquivos = arquivos;
        Previa = new PreviaPessoa(LerParaPreviaAsync, AbrirFichaDaPreviaAsync, () => Linhas,
            () => ((PaginaAtual - 1) * TamanhoPagina, TotalRegistros));
        Indicadores = new FaixaIndicadores(AlternarIndicador);
        Indicadores.DefinirConsulta(c => Filtros.Condicoes().Any(v => v.Campo == c.Campo && v.Operador == c.Operador));
        // Erro que leva ao campo (Lone Contextual, Fase 1): a ficha troca para a aba do campo; a tela rola e põe o foco.
        Validacao.AntesDeIr = IrParaAbaDoErro;
        Validacao.Mudou += () => OnPropertyChanged(nameof(ErrosPorAba));
        Destaques.Mudou += () => OnPropertyChanged(nameof(DestaquesPorAba));
        // A prévia ao lado ocupa parte da largura: as colunas da lista se ajustam ao que sobra.
        Previa.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PreviaPessoa.Visivel)) AjustarColunasAoEspaco();
        };

        // Buscar outro texto volta para a página 1 (a busca no servidor sai logo depois, com uma pequena espera).
        // Mensagens: sucesso vai para a camada global (toast, ViewModelBase.Mostrar); erro e aviso ficam na barra da lista.
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Busca)) _pagina = 1;
            // Voltando da ficha (que pode ter sido alterada): a prévia aberta relê a pessoa e os indicadores ficam para
            // recontar na próxima leitura da lista.
            if (e.PropertyName == nameof(Editando) && !Editando)
            {
                Previa.Reler();
                if (Indicadores.Carregados) _indicadoresContadosEm = DateTime.MinValue;
            }
            if (e.PropertyName is nameof(Mensagem) or nameof(TipoMensagem) or nameof(Editando))
                OnPropertyChanged(nameof(MostrarBarraDaLista));
            // Motivo da alteração: só com alteração; salvou ou descartou, volta a ficar fechado.
            if (e.PropertyName is nameof(PodeDescartar) or nameof(PodeSalvar) or nameof(MotivoAberto))
            {
                if (e.PropertyName == nameof(PodeDescartar) && !PodeDescartar) MotivoAberto = false;
                OnPropertyChanged(nameof(MostrarBotaoMotivo));
                OnPropertyChanged(nameof(MostrarMotivo));
            }
        };

        Filtros.FonteMunicipios = uf => _municipios.ListarDaUfAsync(uf);
        Filtros.Mudou = () =>
        {
            if (_restaurandoContexto) return; // restauração: uma releitura só, no fim (e a visão não fica "alterada")
            if (!_aplicandoVisao && VisaoAtual is not null) VisaoAlterada = true;
            Indicadores.AtualizarMarcados();
            _ = FiltrosMudaramAsync();
        };
        Filtros.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PainelFiltrosPessoas.Aberto)) OnPropertyChanged(nameof(MostrarFiltros));
            if (e.PropertyName == nameof(PainelFiltrosPessoas.TextoBotao)) OnPropertyChanged(nameof(TextoBotaoFiltros));
        };
        Grade.Mudou = mudanca =>
        {
            if (_restaurandoContexto) return;
            if (!_aplicandoVisao && VisaoAtual is not null) VisaoAlterada = true;
            switch (mudanca)
            {
                case MudancaGrade.Ordenacao: _ = RecarregarDaPrimeiraPaginaAsync(); break;
                case MudancaGrade.Colunas: _ = RecarregarAsync(); break;
                // Aparência: só remonta quando as colunas mudaram (ordem, coluna tirada). Densidade e linha de filtro mudam
                // só a grade (altura e cabeçalho), sem trocar as linhas — a lista não volta ao topo.
                default:
                    if (!ConteudoGrade.Colunas.SequenceEqual(Grade.ColunasGrade)) ReconstruirLinhas();
                    break;
            }
            AgendarSalvarColunas();
        };
    }

    protected override bool BuscaNoServidor => true;

    // ---- Lista ----

    /// <summary>
    /// Painel de filtros (catálogo da API): grupos da ficha, campos com caixa de marcar, chips acima da lista. Substitui os
    /// filtros fixos (papel, etiqueta, incluir inativos, município a corrigir), que agora são campos do catálogo.
    /// </summary>
    public PainelFiltrosPessoas Filtros { get; } = new();

    /// <summary>Atalho "Nova etiqueta" na ficha: só para quem gerencia etiquetas (a API confere de novo).</summary>
    public bool PodeCriarEtiqueta => _sessao.Possui(Permissoes.Cadastros.Etiquetas);

    /// <summary>Atalho "Nova profissão" na ficha: só para quem gerencia profissões (a API confere de novo).</summary>
    public bool PodeCriarProfissao => _sessao.Possui(Permissoes.Cadastros.Profissoes);

    public override bool PodeCriar => _sessao.Possui(Permissoes.Pessoas.Criar);

    /// <summary>
    /// Faixa acima da lista quando o perfil tem alcance restrito (Fase 2a-2): diz o que a lista está mostrando, para ninguém
    /// achar que a base "sumiu". Vazio = sem faixa (alcance Tudo). Quem filtra de verdade é a API.
    /// </summary>
    public string AvisoAlcance => AvisoDeAlcance(_sessao.Atual?.Alcance ?? AlcanceComercial.Tudo, _sessao.Atual?.PessoaId);

    public bool TemAvisoAlcance => AvisoAlcance.Length > 0;

    public static string AvisoDeAlcance(AlcanceComercial alcance, Guid? pessoaId) => alcance switch
    {
        AlcanceComercial.MinhaCarteira or AlcanceComercial.MinhaEquipe when pessoaId is null =>
            "Seu usuário não está ligado a uma pessoa do cadastro, então nenhum cadastro aparece. Peça ao administrador para ligar.",
        AlcanceComercial.MinhaCarteira => "Mostrando a sua carteira de clientes (e a de quem você cobre numa ausência).",
        AlcanceComercial.MinhaEquipe => "Mostrando a carteira da sua equipe e das equipes abaixo dela.",
        _ => string.Empty
    };

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

        // Sem a CBO, a ficha oferece só as profissões cadastradas.
        try { _ocupacoesCbo = await _profissoesApi.ListarCboAsync(); }
        catch (Exception ex) when (ex is not SessaoExpiradaException) { _ocupacoesCbo = []; }

        // Sem o cadastro de papéis, a ficha usa os papéis de sistema e os outros períodos voltam intactos.
        try
        {
            _papeis = await _papeisApi.ListarAsync(incluirInativos: true);
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

        // Catálogo do painel de filtros: sem ele, a lista funciona (busca e atalhos) e o painel avisa.
        try
        {
            var catalogo = await _pessoas.CatalogoFiltrosAsync();
            Filtros.Carregar(catalogo);
            Grade.Carregar(catalogo.Colunas.Count > 0 ? catalogo.Colunas : GradePessoas.ColunasBasicas(), catalogo.Layout, Filtros);
            _abasEscolhidas = catalogo.Layout?.Abas;
            CliqueAbreFicha = catalogo.Layout?.CliqueAbreFicha == true;
            Indicadores.DefinirPreferencia(catalogo.Layout?.SemIndicadores == true, catalogo.Layout?.IndicadoresOcultos);
            Indicadores.Carregar(catalogo.Indicadores);
            // Sem números (contagem falhou no servidor ou faixa escondida): tenta pela rota própria depois do intervalo
            // (não logo em seguida: se o servidor acabou de falhar, esperar é melhor), ou ao mostrar a faixa de novo.
            _indicadoresPendentes = catalogo.Indicadores is null;
            _indicadoresContadosEm = DateTime.UtcNow;
            _preferenciaLida = true;
        }
        catch (Exception ex) when (ex is not SessaoExpiradaException)
        {
            Filtros.FalhouAoCarregar("Não foi possível carregar os filtros agora. A busca e os atalhos continuam funcionando.");
            Grade.Carregar(GradePessoas.ColunasBasicas(), null, Filtros);
            _preferenciaLida = false; // não sabemos o que o usuário tinha: não gravar por cima (colunas e abas)
        }

        // Abas: com visão entre as escolhidas, as visões são lidas agora (nome da aba); sem, só quando o editor abrir.
        if (_abasEscolhidas?.Any(id => AbasPessoas.VisaoDe(id) is not null) == true) await CarregarVisoesAsync();
        MontarCatalogoDeAbas();
        _colunasSalvas = JsonSerializer.Serialize(LayoutParaGuardar());
        _listaAberta = true;
    }

    /// <summary>A tela já montou as abas uma vez (antes disso, remontar não relê: a primeira leitura vem logo depois).</summary>
    private bool _listaAberta;
    private bool _preferenciaLida;

    private async Task AtualizarEtiquetasAsync()
    {
        _etiquetas = await _etiquetasApi.ListarAsync(incluirInativas: true);
    }

    protected override string TextoDeBusca(PessoaResumo item) => item.Nome;

    // ---- Lista: atalhos, filtros avançados, paginação e colunas (tela de Pessoas) ----

    /// <summary>
    /// Abas acima da tabela (uma marcada por vez): "Todos" + as escolhidas pelo usuário (naturezas, papéis, visões). Os
    /// filtros avançados ficam no painel "Filtros".
    /// </summary>
    public ObservableCollection<FiltroRapido> FiltrosRapidos { get; } = [FiltroRapido.CriarTodos()];

    /// <summary>Editor das abas ("＋" no fim das abas).</summary>
    public EditorAbas Abas { get; } = new();

    private string _filtroRapido = FiltroRapido.Todos;

    /// <summary>Abas da preferência do usuário (nulo = padrão). Guardadas mesmo as que não existem agora (papel inativo).</summary>
    private List<string>? _abasEscolhidas;

    /// <summary>Visões salvas que o usuário enxerga (lidas quando há aba de visão ou o editor de abas abre).</summary>
    private List<FiltroSalvoDto> _visoes = [];
    private bool _visoesCarregadas;
    private DateTime _visoesContadasEm = DateTime.MinValue;

    /// <summary>De quanto em quanto tempo o contador das abas de visão é refeito (elas não dependem da busca da tela).</summary>
    public TimeSpan IntervaloContagemVisoes { get; set; } = TimeSpan.FromMinutes(1);

    private FiltroRapido? AbaAtual => FiltrosRapidos.FirstOrDefault(f => f.Chave == _filtroRapido);

    [RelayCommand]
    private async Task EscolherFiltroRapidoAsync(FiltroRapido? filtro)
    {
        if (filtro is null || filtro.Chave == _filtroRapido) return;

        // Aba de visão: aplica a visão (troca painel, busca e colunas).
        if (filtro.VisaoId is { } visaoId)
        {
            var visao = _visoes.FirstOrDefault(v => v.Id == visaoId);
            if (visao is null)
            {
                await CarregarVisoesAsync();
                visao = _visoes.FirstOrDefault(v => v.Id == visaoId);
            }
            if (visao is null)
            {
                Mostrar($"A visão \"{filtro.Texto}\" não existe mais (foi removida ou deixou de ser compartilhada).", TipoMensagem.Aviso);
                return;
            }
            await AplicarVisaoAsync(visao, filtro);
            return;
        }

        // Saindo de uma aba de visão: os filtros eram dela; a aba nova começa limpa (como tocar em outra lista).
        var saindoDeVisao = AbaAtual?.EhVisao == true;
        _filtroRapido = filtro.Chave;
        foreach (var f in FiltrosRapidos) f.Selecionado = ReferenceEquals(f, filtro);
        if (saindoDeVisao) SairDaVisao();
        else if (VisaoAtual is not null) VisaoAlterada = true; // a aba também entra na visão

        // Campos só de PJ com a aba "Pessoas físicas" (e vice-versa) ficam desabilitados no painel.
        Filtros.DefinirNatureza(filtro.Natureza);
        _filtrosAtrasados?.Cancel(); // a leitura abaixo já vale para tudo acima (sem uma segunda leitura com espera)
        await RecarregarDaPrimeiraPaginaAsync();
    }

    /// <summary>Saiu de uma aba de visão: painel, busca e aviso eram da visão; a tela volta limpa (quem chama relê).</summary>
    private void SairDaVisao()
    {
        _aplicandoVisao = true;
        try
        {
            Filtros.Limpar();
            Busca = string.Empty;
            CancelarBuscaAtrasada();
            VisaoAtual = null;
            VisaoAlterada = false;
        }
        finally { _aplicandoVisao = false; }
        LimparMensagem();
    }

    /// <summary>Abas guardadas que não existem agora (papel inativo; visão ainda não lida): continuam na preferência.</summary>
    private IEnumerable<string> AbasGuardadas() =>
        (_abasEscolhidas ?? []).Where(id => Abas.Opcao(id) is null && !(AbasPessoas.VisaoDe(id) is not null && _visoesCarregadas)).ToList();

    // ---- Abas escolhidas pelo usuário ----

    /// <summary>Monta as abas possíveis (naturezas, papéis, visões) e as da tela, na ordem escolhida.</summary>
    private void MontarCatalogoDeAbas()
    {
        Abas.Mudou = null;
        Abas.Carregar(CatalogoAbas.Montar(_papeis, _visoes), _abasEscolhidas);
        Abas.Mudou = AbasMudaram;
        var eraVisao = AbaAtual?.EhVisao == true;
        if (MontarAbas() && _listaAberta) VoltarParaTodos(eraVisao); // a aba marcada deixou de existir (papel inativo, visão apagada)
    }

    /// <summary>A aba marcada saiu: "Todos", sem o filtro dela (e sem os filtros da visão, se era uma), relendo a lista.</summary>
    private void VoltarParaTodos(bool eraVisao)
    {
        if (eraVisao) SairDaVisao();
        Filtros.DefinirNatureza(null);
        _filtrosAtrasados?.Cancel();
        _ = RecarregarDaPrimeiraPaginaAsync();
    }

    /// <summary>Refaz as abas da tela (mantém a contagem de cada uma e a marcada; a marcada que saiu volta para "Todos").</summary>
    private bool MontarAbas()
    {
        var anteriores = FiltrosRapidos.ToDictionary(f => f.Chave);
        FiltrosRapidos.Clear();
        FiltrosRapidos.Add(anteriores.TryGetValue(FiltroRapido.Todos, out var todos) ? todos : FiltroRapido.CriarTodos());
        foreach (var id in Abas.Ids)
            if (Abas.Opcao(id) is { } o)
                FiltrosRapidos.Add(anteriores.TryGetValue(id, out var existente) && existente.Texto == o.Nome
                    ? existente
                    : new FiltroRapido(id, o.Nome, o.Dica) { Quantidade = anteriores.GetValueOrDefault(id)?.Quantidade });

        var perdeuMarcada = AbaAtual is null;
        if (perdeuMarcada) _filtroRapido = FiltroRapido.Todos;
        foreach (var f in FiltrosRapidos) f.Selecionado = f.Chave == _filtroRapido;
        return perdeuMarcada;
    }

    /// <summary>O usuário mudou as abas no editor: remonta, guarda a preferência e conta as visões novas.</summary>
    private void AbasMudaram()
    {
        _abasEscolhidas = [.. Abas.Ids, .. AbasGuardadas()];
        var eraVisao = AbaAtual?.EhVisao == true;
        if (MontarAbas()) VoltarParaTodos(eraVisao);
        AgendarSalvarColunas();
        if (FiltrosRapidos.Any(f => f.EhVisao && f.Quantidade is null)) _ = ContarVisoesAsync();
    }

    /// <summary>Abre o editor de abas (lê as visões na hora, para oferecer como abas).</summary>
    [RelayCommand]
    private async Task EditarAbasAsync()
    {
        await CarregarVisoesAsync();
        MontarCatalogoDeAbas();
        Abas.Abrir();
    }

    /// <summary>Lê as visões visíveis ao usuário. Falha: as abas de visão ficam como estavam (tenta de novo depois).</summary>
    private async Task CarregarVisoesAsync()
    {
        try
        {
            _visoes = (await _pessoas.OpcoesConsultaAsync()).Filtros;
            _visoesCarregadas = true;
        }
        catch (Exception ex) when (ex is not SessaoExpiradaException)
        {
            // Sem as visões agora: o editor mostra só naturezas e papéis.
        }
    }

    /// <summary>
    /// Contador das abas de visão: quantas pessoas cada visão traz (com os filtros dela, não os da tela). Falha: as abas
    /// ficam sem número.
    /// </summary>
    private async Task ContarVisoesAsync()
    {
        var ids = FiltrosRapidos.Where(f => f.VisaoId is not null).Select(f => f.VisaoId!.Value).ToList();
        if (ids.Count == 0) return;
        _visoesContadasEm = DateTime.UtcNow;
        try
        {
            var totais = await _pessoas.ContarVisoesAsync(ids);
            foreach (var aba in FiltrosRapidos.Where(f => f.VisaoId is { } v && ids.Contains(v)))
                aba.Quantidade = totais.TryGetValue(aba.VisaoId!.Value, out var total) ? total : null;
        }
        catch (Exception ex) when (ex is not SessaoExpiradaException)
        {
            // Contador é ajuda: sem ele, a aba continua funcionando.
        }
    }

    /// <summary>Preferência guardada: colunas, ordenação, densidade e as abas (as visões salvas não levam as abas).</summary>
    private LayoutListaPessoas LayoutParaGuardar()
    {
        var layout = Grade.Layout();
        layout.Abas = _abasEscolhidas is null ? null : [.. _abasEscolhidas];
        layout.CliqueAbreFicha = CliqueAbreFicha;
        layout.SemIndicadores = Indicadores.Escondida;
        layout.IndicadoresOcultos = Indicadores.Ocultos;
        return layout;
    }

    /// <summary>Painel de filtros aberto (à direita; no celular, sobre a lista).</summary>
    public bool MostrarFiltros => Filtros.Aberto;

    [RelayCommand]
    private void AlternarFiltros() => Filtros.Aberto = !Filtros.Aberto;

    /// <summary>"Filtros" ou "Filtros (3)": quantos filtros do painel estão valendo.</summary>
    public string TextoBotaoFiltros => Filtros.TextoBotao;

    [RelayCommand]
    private void LimparFiltros() => Filtros.Limpar();

    /// <summary>Filtros do painel mudaram: volta para a página 1 e relê, com uma pequena espera (vale a mudança mais nova).</summary>
    private async Task FiltrosMudaramAsync()
    {
        _filtrosAtrasados?.Cancel();
        var cts = _filtrosAtrasados = new CancellationTokenSource();
        try
        {
            await Task.Delay(400, cts.Token);
            // Um aviso de filtro anterior (ex.: valor recusado pela API) não vale mais para os filtros novos.
            LimparMensagem();
            await RecarregarDaPrimeiraPaginaAsync();
        }
        catch (OperationCanceledException)
        {
            // Outra mudança chegou antes: vale a mais nova.
        }
        finally
        {
            if (ReferenceEquals(_filtrosAtrasados, cts)) _filtrosAtrasados = null;
            cts.Dispose();
        }
    }

    private CancellationTokenSource? _filtrosAtrasados;

    // ---- Visões (filtros salvos com nome; do usuário ou compartilhados) ----

    /// <summary>Visão aplicada por último (nula = nenhuma).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoBotaoVisoes))]
    private FiltroSalvoDto? _visaoAtual;

    /// <summary>Os filtros mudaram depois de aplicar a visão ("Visão: X (alterada)").</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoBotaoVisoes))]
    private bool _visaoAlterada;

    private bool _aplicandoVisao;

    public string TextoBotaoVisoes => VisaoAtual is null ? "Visões" : VisaoAlterada ? $"Visão: {VisaoAtual.Nome} (alterada)" : $"Visão: {VisaoAtual.Nome}";

    private const string OpcaoSalvarVisao = "＋ Salvar os filtros atuais como visão…";

    /// <summary>Lista as visões (lidas na hora: podem ter sido criadas por outro usuário) e as ações de salvar e remover.</summary>
    [RelayCommand]
    private async Task VisoesAsync()
    {
        List<FiltroSalvoDto> visoes = [];
        if (!await ExecutarAsync(async () => visoes = (await _pessoas.OpcoesConsultaAsync()).Filtros)) return;

        _visoes = visoes;
        _visoesCarregadas = true;
        var remover = VisaoAtual is { Proprio: true } propria ? $"Remover a visão \"{propria.Nome}\"" : null;
        var fixar = VisaoAtual is { } atual && visoes.Any(v => v.Id == atual.Id) && FiltrosRapidos.All(f => f.VisaoId != atual.Id)
            ? $"Mostrar \"{atual.Nome}\" como aba" : null;
        var opcoes = visoes.Select(v => v.ToString()).ToList();
        opcoes.Add(OpcaoSalvarVisao);
        if (fixar is not null) opcoes.Add(fixar);
        if (remover is not null) opcoes.Add(remover);

        var escolha = await EscolherAsync(visoes.Count == 0 ? "Visões (nenhuma salva ainda)" : "Visões", opcoes);
        if (escolha is null) return;
        if (escolha == OpcaoSalvarVisao) await SalvarVisaoAsync();
        else if (escolha == fixar) await FixarVisaoComoAbaAsync(VisaoAtual!);
        else if (escolha == remover) await RemoverVisaoAsync();
        else if (visoes.FirstOrDefault(v => v.ToString() == escolha) is { } visao) await AplicarVisaoAsync(visao);
    }

    /// <summary>
    /// Filtros da tela como critérios (painel + busca + atalho). O atalho vira condição (PF, clientes, inativos...) para a
    /// visão e a exportação valerem igual à lista.
    /// </summary>
    public CriteriosPessoas CriteriosDaTela()
    {
        var condicoes = Filtros.Condicoes();
        void Incluir(string campo, params string[] valores) =>
            condicoes.Add(new CondicaoFiltro { Campo = campo, Operador = OperadorFiltro.UmDestes, Valores = [.. valores] });
        // A aba vira condição (natureza ou papel); aba de visão não soma nada (os filtros da visão já estão no painel).
        if (AbaAtual?.Natureza is { } natureza) Incluir(CamposFiltroPessoas.Natureza, natureza.ToString());
        if (AbaAtual?.PapelId is { } papel) Incluir(CamposFiltroPessoas.Papeis, papel.ToString("D"));
        return new CriteriosPessoas
        {
            Texto = string.IsNullOrWhiteSpace(Busca) ? null : Busca.Trim(),
            Condicoes = condicoes,
            Layout = Grade.Carregada ? Grade.Layout() : null
        };
    }

    private async Task SalvarVisaoAsync()
    {
        var criterios = CriteriosDaTela();
        if (criterios.Condicoes.Count == 0 && criterios.Texto is null)
        {
            Mostrar("Escolha algum filtro (ou uma busca) antes de salvar a visão.", TipoMensagem.Aviso);
            return;
        }
        var propria = VisaoAtual is { Proprio: true } v ? v : null;
        var nome = await PerguntarAsync("Salvar visão",
            propria is null ? "Nome da visão (ex.: Clientes PJ de Curvelo):" : "Nome da visão (o mesmo nome atualiza esta):",
            "Salvar", "Cancelar", propria?.Nome, 80);
        if (string.IsNullOrWhiteSpace(nome)) return;
        var compartilhar = await ConfirmarAsync("Compartilhar a visão",
            "Deixar esta visão disponível para todos os usuários? Só você poderá alterá-la ou removê-la.", "Compartilhar", "Só para mim");
        var atualizar = propria is not null && TextoBusca.Normalizar(nome) == TextoBusca.Normalizar(propria.Nome);

        FiltroSalvoDto? salva = null;
        if (!await ExecutarAsync(async () => salva = await _pessoas.SalvarFiltroAsync(new FiltroSalvoDto
            {
                Id = atualizar ? propria!.Id : IdSequencial.Novo(),
                Versao = atualizar ? propria!.Versao : null,
                Nome = nome.Trim(),
                Compartilhado = compartilhar,
                Criterios = criterios
            })))
            return;
        VisaoAtual = salva;
        VisaoAlterada = false;
        // A aba da visão (se houver) passa a ter o nome e os filtros novos.
        _visoes.RemoveAll(v => v.Id == salva!.Id);
        _visoes.Add(salva!);
        MontarCatalogoDeAbas();
        if (FiltrosRapidos.FirstOrDefault(f => f.VisaoId == salva!.Id) is { } aba)
        {
            _ = ContarVisoesAsync();
            if (!aba.Selecionado) await AplicarVisaoAsync(salva!, aba);
        }
        Mostrar($"Visão \"{salva!.Nome}\" salva.", TipoMensagem.Sucesso);
    }

    /// <summary>Põe a visão nas abas (no fim), se ainda cabe.</summary>
    private async Task FixarVisaoComoAbaAsync(FiltroSalvoDto visao)
    {
        MontarCatalogoDeAbas(); // as visões acabaram de ser lidas: o catálogo das abas passa a ter esta
        var id = AbasPessoas.Visao(visao.Id);
        if (Abas.Opcao(id) is null) return;
        if (Abas.Ids.Count >= AbasPessoas.Maximo)
        {
            Mostrar($"Já há {AbasPessoas.Maximo} abas: tire uma em \"＋\" (editar abas) antes de pôr outra.", TipoMensagem.Aviso);
            return;
        }
        _abasEscolhidas = [.. Abas.Ids, id, .. AbasGuardadas()];
        MontarCatalogoDeAbas();
        AgendarSalvarColunas();
        _ = ContarVisoesAsync();
        if (FiltrosRapidos.FirstOrDefault(f => f.VisaoId == visao.Id) is not { } aba || aba.Selecionado) return;
        // Visão já aplicada (a opção só aparece assim): só marca a aba, sem reaplicar a gravada — reaplicar descartaria
        // o que o usuário mudou nela ("(alterada)"). A aba anterior, de natureza ou papel, deixa de filtrar: relê.
        if (VisaoAtual?.Id == visao.Id)
        {
            _aplicandoVisao = true;
            try
            {
                _filtroRapido = aba.Chave;
                foreach (var f in FiltrosRapidos) f.Selecionado = ReferenceEquals(f, aba);
                Filtros.DefinirNatureza(null);
            }
            finally { _aplicandoVisao = false; }
            _filtrosAtrasados?.Cancel();
            await RecarregarDaPrimeiraPaginaAsync();
        }
        else await AplicarVisaoAsync(visao, aba);
    }

    private async Task RemoverVisaoAsync()
    {
        if (VisaoAtual is not { Proprio: true } visao) return;
        if (!await ConfirmarAsync("Remover visão", $"Remover a visão \"{visao.Nome}\"? Os filtros da tela continuam como estão.", "Remover", "Cancelar"))
            return;
        if (!await ExecutarAsync(() => _pessoas.DesativarFiltroAsync(visao.Id))) return;
        VisaoAtual = null;
        VisaoAlterada = false;
        // A aba dela (se havia) sai; os filtros da tela continuam como estão, agora em "Todos".
        _visoes.RemoveAll(v => v.Id == visao.Id);
        if (_abasEscolhidas?.Remove(AbasPessoas.Visao(visao.Id)) == true) AgendarSalvarColunas();
        if (AbaAtual?.VisaoId == visao.Id) _filtroRapido = FiltroRapido.Todos; // os filtros ficam; só a aba some
        MontarCatalogoDeAbas();
        Mostrar("Visão removida.", TipoMensagem.Sucesso);
    }

    /// <summary>
    /// Troca os filtros da tela pelos da visão (painel, busca; atalho volta para "Todos") e relê a lista na hora. O aviso
    /// de parte não aplicada vem depois da leitura (a leitura por filtros limpa as mensagens antigas).
    /// </summary>
    public async Task AplicarVisaoAsync(FiltroSalvoDto visao, FiltroRapido? aba = null)
    {
        _aplicandoVisao = true;
        var tudo = true;
        try
        {
            // A aba da visão fica marcada (se ela estiver nas abas); senão, "Todos".
            aba ??= FiltrosRapidos.FirstOrDefault(f => f.VisaoId == visao.Id);
            _filtroRapido = aba?.Chave ?? FiltroRapido.Todos;
            foreach (var f in FiltrosRapidos) f.Selecionado = f.Chave == _filtroRapido;
            Filtros.DefinirNatureza(null);
            Filtros.Limpar();
            foreach (var condicao in visao.Criterios.Condicoes)
                tudo &= Filtros.Aplicar(condicao);
            // Visão com colunas: a lista fica como foi salva (a releitura abaixo já traz os valores das colunas novas).
            if (visao.Criterios.Layout is { } layout && Grade.Carregada)
            {
                Grade.AplicarLayout(layout);
                AgendarSalvarColunas();
            }
            Busca = visao.Criterios.Texto ?? string.Empty;
            CancelarBuscaAtrasada(); // a leitura abaixo já leva a busca da visão
            VisaoAtual = visao;
            VisaoAlterada = false;
        }
        finally { _aplicandoVisao = false; }
        _filtrosAtrasados?.Cancel(); // as mudanças do painel acima já pediram releitura com espera: lê uma vez, agora
        LimparMensagem();
        await RecarregarDaPrimeiraPaginaAsync();
        if (!tudo)
            Mostrar("Parte da visão não pôde ser aplicada (campo sem permissão para você ou opção que não existe mais).", TipoMensagem.Aviso);
    }

    // ---- Ações da lista ("⋯"): exportar e rotina de endereços duplicados ----

    public bool PodeExportar => _sessao.Possui(Permissoes.Pessoas.Exportar);

    private const string AcaoExportar = "Exportar o resultado (CSV)";
    private const string AcaoDuplicados = "Procurar endereços duplicados";
    private const string AcaoVisoes = "Visões salvas…";
    private const string AcaoMostrarIndicadores = "Mostrar a faixa de indicadores";
    private const string AcaoEditarAbas = "Escolher as abas…";

    [RelayCommand]
    private async Task AcoesDaListaAsync()
    {
        // "Visões" também aqui: no celular o botão da barra não aparece (falta espaço).
        var opcoes = new List<string> { AcaoVisoes, AcaoEditarAbas };
        if ((Indicadores.Carregados || _indicadoresPendentes) && Indicadores.Escondida) opcoes.Add(AcaoMostrarIndicadores);
        if (PodeExportar) opcoes.Add(AcaoExportar);
        opcoes.Add(AcaoDuplicados);
        switch (await EscolherAsync("Pessoas", opcoes))
        {
            case AcaoMostrarIndicadores: MostrarFaixaDeIndicadores(); break;
            case AcaoVisoes: await VisoesAsync(); break;
            case AcaoEditarAbas: await EditarAbasAsync(); break;
            case AcaoExportar: await ExportarAsync(); break;
            case AcaoDuplicados: await ProcurarEnderecosDuplicadosAsync(null); break;
        }
    }

    /// <summary>Exporta o que está filtrado na tela (a API confere a permissão e registra na auditoria).</summary>
    private async Task ExportarAsync()
    {
        if (!await ConfirmarAsync("Exportar",
                "Exportar em CSV as pessoas filtradas agora? A exportação fica registrada na auditoria (quem, quando e os filtros).",
                "Exportar", "Cancelar"))
            return;
        ArquivoExportado? arquivo = null;
        if (!await ExecutarAsync(async () =>
            {
                arquivo = await _pessoas.ExportarAsync(CriteriosDaTela());
                var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(arquivo.Conteudo)).ToArray();
                await _arquivos.AbrirAsync(arquivo.NomeArquivo, bytes);
            }))
            return;
        Mostrar($"{arquivo!.Linhas.ToString("N0", TextoTela.Brasil)} pessoa(s) exportada(s) em {arquivo.NomeArquivo}.", TipoMensagem.Sucesso);
    }

    private const string MostrarMaisDuplicados = "Mostrar mais…";

    /// <summary>Pessoas com o mesmo endereço cadastrado mais de uma vez: tocar abre a ficha (a consolidação é lá).</summary>
    private async Task ProcurarEnderecosDuplicadosAsync(Guid? apos)
    {
        Lone.Contracts.Enderecos.PaginaEnderecosDuplicados? pagina = null;
        if (!await ExecutarAsync(async () => pagina = await _pessoas.ListarEnderecosDuplicadosAsync(apos, 50))) return;
        if (pagina!.Itens.Count == 0)
        {
            Mostrar(apos is null ? "Nenhum endereço duplicado encontrado." : "Não há mais endereços duplicados.", TipoMensagem.Sucesso);
            return;
        }
        var linhas = pagina.Itens.Select(i => $"{i.Codigo:000000} · {i.Nome}").ToList();
        if (pagina.ProximoId is not null) linhas.Add(MostrarMaisDuplicados);
        var escolha = await EscolherAsync("Endereços duplicados — toque para abrir a ficha e consolidar", linhas);
        if (escolha is null) return;
        if (escolha == MostrarMaisDuplicados)
        {
            await ProcurarEnderecosDuplicadosAsync(pagina.ProximoId);
            return;
        }
        var item = pagina.Itens[linhas.IndexOf(escolha)];
        Selecionado = new PessoaResumo { Id = item.PessoaId, Codigo = item.Codigo, Nome = item.Nome };
    }

    /// <summary>
    /// Filtro que vai para a API: busca + atalho. As condições do painel vão junto (Filtros.Condicoes). Com o campo
    /// "Situação do cadastro" no painel, é ele que decide as situações (a lista deixa de esconder os inativos).
    /// </summary>
    public FiltroPessoas FiltroAtual()
    {
        var filtro = new FiltroPessoas
        {
            Texto = Busca,
            IncluirInativos = Filtros.TemCondicao(global::Lone.Contracts.Pessoas.CamposFiltroPessoas.Situacao)
        };
        filtro.Natureza = AbaAtual?.Natureza;
        filtro.PapelId = AbaAtual?.PapelId;
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
    public string ResumoPaginacao => Paginacao.Resumo(PaginaAtual, TamanhoPagina, TotalRegistros, Itens.Count, "pessoa", "pessoas");
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
        var condicoes = Filtros.Condicoes();
        var colunas = Grade.ColunasExtras();
        var ordenacao = Grade.Ordenacao;
        var pagina = await _pessoas.ListarPaginaAsync(FiltroAtual(), condicoes, colunas, ordenacao, _pagina, TamanhoPagina);
        // Página que deixou de existir (ex.: filtro reduziu o total): volta para a última que existe.
        if (pagina.Itens.Count == 0 && pagina.Total > 0 && _pagina > 1)
        {
            _pagina = Paginacao.Paginas(pagina.Total, TamanhoPagina);
            pagina = await _pessoas.ListarPaginaAsync(FiltroAtual(), condicoes, colunas, ordenacao, _pagina, TamanhoPagina);
        }
        TotalRegistros = pagina.Total;
        PaginaAtual = pagina.Pagina;
        // Contagem das abas (vem na página 1; nas outras páginas os filtros são os mesmos e a contagem continua valendo).
        // Numa aba de visão, a contagem veio com os filtros da visão, mas tocar em outra aba limpa esses filtros: o número
        // não corresponderia ao que a aba traz. Fica sem número até sair da visão.
        if (pagina.Atalhos is { } atalhos)
        {
            var emVisao = AbaAtual?.EhVisao == true;
            foreach (var aba in FiltrosRapidos.Where(f => !f.EhVisao)) aba.Quantidade = emVisao ? null : aba.QuantidadeEm(atalhos);
        }
        // Abas de visão: contadas à parte (os filtros são os delas), no máximo uma vez por intervalo.
        if (_pagina == 1 && FiltrosRapidos.Any(f => f.EhVisao) && DateTime.UtcNow - _visoesContadasEm >= IntervaloContagemVisoes)
            _ = ContarVisoesAsync();
        // Indicadores (base toda, não dependem da busca): recontados no máximo uma vez por intervalo.
        if (_pagina == 1 && (Indicadores.Carregados || _indicadoresPendentes) && !Indicadores.Escondida &&
            DateTime.UtcNow - _indicadoresContadosEm >= IntervaloIndicadores)
            _ = AtualizarIndicadoresAsync();
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
        ReconstruirLinhas();
        OnPropertyChanged(nameof(ResumoPaginacao));
        OnPropertyChanged(nameof(MostrarEstadoVazio));
    }

    // ---- Colunas da lista (escolha, ordem, ordenação, filtro nas colunas) ----

    /// <summary>Colunas da lista: o nome preso à esquerda e as escolhidas pelo usuário, que rolam para o lado.</summary>
    public GradePessoas Grade { get; } = new();

    /// <summary>As linhas da página com as células das colunas escolhidas (a tela mostra estas, não Itens).</summary>
    public ObservableCollection<LinhaPessoa> Linhas { get; } = new();

    /// <summary>
    /// O que a GradeLista mostra (P2-B2, Etapa 3): colunas e linhas juntas, trocadas de uma vez (um aviso só). Montado
    /// junto com <see cref="Linhas"/>, das mesmas linhas, enquanto a tela atual ainda usa as pilhas.
    /// </summary>
    public global::Lone.Cliente.Grade.ConteudoGrade ConteudoGrade { get; private set; } = global::Lone.Cliente.Grade.ConteudoGrade.Vazio;

    private void ReconstruirLinhas()
    {
        var novas = Itens.Select(Grade.Linha).ToList();
        Linhas.Clear();
        foreach (var linha in novas) Linhas.Add(linha);
        ConteudoGrade = Grade.Conteudo(novas);
        OnPropertyChanged(nameof(ConteudoGrade));
        Previa.Sincronizar(Linhas);
    }

    /// <summary>
    /// Clique na linha: mostra a prévia ao lado (padrão) ou abre a ficha, conforme a escolha do usuário. Sem espaço para a
    /// prévia (celular, janela estreita), abre a ficha.
    /// </summary>
    [RelayCommand]
    private void AbrirLinha(LinhaPessoa? linha)
    {
        if (linha is null) return;
        if (!CliqueAbreFicha && Previa.Cabe) Previa.Mostrar(linha);
        else Selecionado = linha.Pessoa;
    }

    /// <summary>Duplo clique na linha: abre a ficha, qualquer que seja a escolha do clique simples.</summary>
    [RelayCommand]
    private void AbrirFichaDaLinha(LinhaPessoa? linha)
    {
        if (linha is null) return;
        Previa.CancelarLeitura(); // o primeiro toque do duplo clique pediu a prévia; a ficha vem agora (e a prévia relê ao voltar)
        Selecionado = linha.Pessoa;
    }

    // ---- Faixa de indicadores (Etapa 3) ----

    /// <summary>Números da base toda acima da lista (com bloqueio, documentos vencidos/vencendo, com pendência).</summary>
    public FaixaIndicadores Indicadores { get; }

    /// <summary>Intervalo mínimo entre duas contagens dos indicadores (os números não dependem da busca digitada).</summary>
    public TimeSpan IntervaloIndicadores { get; set; } = TimeSpan.FromMinutes(1);

    private DateTime _indicadoresContadosEm = DateTime.MinValue;

    /// <summary>O catálogo veio sem os números: a próxima leitura da lista conta pela rota própria.</summary>
    private bool _indicadoresPendentes;

    /// <summary>A contagem em andamento (os testes esperam por ela).</summary>
    public Task AtualizandoIndicadores { get; private set; } = Task.CompletedTask;

    private Task AtualizarIndicadoresAsync()
    {
        _indicadoresContadosEm = DateTime.UtcNow;
        return AtualizandoIndicadores = ContarIndicadoresAsync();
    }

    private async Task ContarIndicadoresAsync()
    {
        try
        {
            Indicadores.Carregar(await _pessoas.IndicadoresAsync());
            _indicadoresPendentes = false;
        }
        catch (Exception ex) when (ex is not SessaoExpiradaException)
        {
            // Os números são ajuda: sem eles, ficam os últimos e a lista continua funcionando.
        }
    }

    /// <summary>Tocar num indicador põe a condição dele no painel de filtros; tocar de novo tira.</summary>
    private void AlternarIndicador(IndicadorLista indicador)
    {
        var condicao = indicador.Dto.Condicao;
        if (indicador.Marcado)
        {
            if (Filtros.CampoPorId(condicao.Campo) is { } campo) campo.Marcado = false;
        }
        else if (!Filtros.Aplicar(new CondicaoFiltro { Campo = condicao.Campo, Operador = condicao.Operador, Valores = [.. condicao.Valores] }))
        {
            Mostrar($"O filtro \"{indicador.Nome}\" não está disponível para você.", TipoMensagem.Aviso);
        }
        Indicadores.AtualizarMarcados();
    }

    private const string EsconderFaixa = "Esconder a faixa de indicadores";

    /// <summary>"⋯" da faixa: tirar ou pôr cada indicador, ou esconder a faixa (volta pelo "⋯" da lista).</summary>
    [RelayCommand]
    private async Task MenuIndicadoresAsync()
    {
        var opcoes = Indicadores.Todos
            .Select(i => (Indicadores.EstaOculto(i.Id) ? "Mostrar: " : "Tirar da faixa: ") + i.Nome)
            .Append(EsconderFaixa)
            .ToList();
        var escolha = await EscolherAsync("Indicadores", opcoes);
        if (escolha is null) return;
        if (escolha == EsconderFaixa) Indicadores.Escondida = true;
        else if (Indicadores.Todos.ElementAtOrDefault(opcoes.IndexOf(escolha)) is { } escolhido) Indicadores.AlternarOculto(escolhido.Id);
        AgendarSalvarColunas();
    }

    /// <summary>Faixa escondida: volta pelo "⋯" da lista (e reconta se os números ficaram velhos).</summary>
    private void MostrarFaixaDeIndicadores()
    {
        Indicadores.Escondida = false;
        AgendarSalvarColunas();
        if (_indicadoresPendentes || DateTime.UtcNow - _indicadoresContadosEm >= IntervaloIndicadores) _ = AtualizarIndicadoresAsync();
    }

    // ---- Prévia ao lado da lista (Etapa 3) ----

    /// <summary>Prévia da pessoa ao lado da lista (cabeçalho e o Resumo da pessoa), sem abrir a ficha.</summary>
    public PreviaPessoa Previa { get; }

    /// <summary>O clique na linha abre a ficha direto (em vez da prévia). Guardado na preferência do usuário.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoCliqueNaLinha))]
    private bool _cliqueAbreFicha;

    partial void OnCliqueAbreFichaChanged(bool value)
    {
        if (value) Previa.FecharCommand.Execute(null);
        if (_listaAberta) AgendarSalvarColunas();
    }

    public string TextoCliqueNaLinha => CliqueAbreFicha ? "Clique: abre a ficha" : "Clique: mostra a prévia";

    [RelayCommand]
    private void AlternarCliqueNaLinha() => CliqueAbreFicha = !CliqueAbreFicha;

    /// <summary>A prévia lê a pessoa como a ficha lê (mesma rota, mesma permissão), com os cadastros já carregados na tela.</summary>
    private async Task<PessoaFormulario> LerParaPreviaAsync(Guid id, CancellationToken ct)
    {
        var dto = await _pessoas.ObterAsync(id, ct) ?? throw new ValidacaoException(["Este cadastro não existe mais."]);
        return PessoaFormulario.De(dto, _campos, _etiquetas, _profissoes, _papeis, _tiposMeio, _tiposEndereco, _tiposDocumento,
            _camposDocumento, _finalidadesEndereco);
    }

    /// <summary>"Abrir ficha" da prévia ou um item do resumo (já na aba do assunto).</summary>
    private async Task AbrirFichaDaPreviaAsync(Guid id, DestinoFicha? destino)
    {
        if ((Itens.FirstOrDefault(p => p.Id == id) ?? Previa.Linha?.Pessoa) is not { } pessoa || pessoa.Id != id) return;
        Selecionado = pessoa; // abre a ficha (pergunta antes se houver alterações não salvas em outra)
        if (destino is null) return;
        await EsperarFichaAsync(id);
        if (Formulario?.Id == id) IrPara(destino);
    }

    [RelayCommand]
    private Task AcoesDaLinhaGradeAsync(LinhaPessoa? linha) => linha is null ? Task.CompletedTask : AcoesDaLinhaAsync(linha.Pessoa);

    private string _colunasSalvas = string.Empty;
    private CancellationTokenSource? _salvarColunas;

    /// <summary>Espera antes de guardar as colunas (várias mudanças seguidas viram uma gravação só).</summary>
    public TimeSpan EsperaParaSalvarColunas { get; set; } = TimeSpan.FromMilliseconds(800);

    /// <summary>A gravação das colunas em andamento (os testes esperam por ela).</summary>
    public Task SalvandoColunas { get; private set; } = Task.CompletedTask;

    private void AgendarSalvarColunas()
    {
        _salvarColunas?.Cancel();
        var cts = _salvarColunas = new CancellationTokenSource();
        SalvandoColunas = SalvarColunasAsync(cts.Token);
    }

    /// <summary>
    /// Guarda colunas, ordenação e linha de filtro para o usuário. Só grava se mudou; falha não atrapalha a tela (na
    /// próxima mudança tenta de novo).
    /// </summary>
    private async Task SalvarColunasAsync(CancellationToken ct)
    {
        try
        {
            if (EsperaParaSalvarColunas > TimeSpan.Zero) await Task.Delay(EsperaParaSalvarColunas, ct);
            if (!_preferenciaLida) return; // o catálogo (com a preferência) não veio: gravar apagaria a escolha do usuário
            var layout = LayoutParaGuardar();
            var json = JsonSerializer.Serialize(layout);
            if (json == _colunasSalvas) return;
            await _pessoas.SalvarLayoutListaAsync(layout, ct);
            _colunasSalvas = json;
        }
        catch (OperationCanceledException)
        {
            // Outra mudança chegou antes: vale a mais nova.
        }
        catch (Exception)
        {
            // Preferência de apresentação: sem conexão, fica para a próxima mudança.
        }
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

    /// <summary>Altura de cada linha da lista (fixa: a parte presa do nome e a que rola ficam alinhadas).</summary>
    public const double AlturaLinha = LinhaPessoa.AlturaConfortavel;

    /// <summary>Largura mínima para as colunas ao lado do nome (abaixo disso, só o nome com o documento embaixo).</summary>
    public const double LarguraMinimaColunas = 600;

    /// <summary>
    /// Tela estreita (celular): só o nome, com o documento embaixo. Tela larga: o nome preso à esquerda e as colunas
    /// escolhidas rolando para o lado (nenhuma some por falta de espaço).
    /// </summary>
    public void DefinirLarguraDaLista(double largura)
    {
        if (largura <= 0) return;
        _larguraLista = largura;
        // A prévia só vai para o lado se a lista continuar com espaço de sobra para as colunas.
        Previa.Cabe = largura >= LarguraComPrevia + EspacoPrevia + LarguraMinimaListaComPrevia;
        AjustarColunasAoEspaco();
    }

    /// <summary>Largura da prévia com o espaço entre ela e a lista (a tela usa para a coluna dela).</summary>
    public const double EspacoPrevia = 16;
    public const double LarguraComPrevia = PreviaPessoa.Largura;

    /// <summary>Com a prévia aberta, a lista precisa de pelo menos isto (senão a prévia não abre ao lado).</summary>
    public const double LarguraMinimaListaComPrevia = 700;

    private double _larguraLista;

    private void AjustarColunasAoEspaco()
    {
        if (_larguraLista <= 0) return;
        var util = Previa.Visivel ? _larguraLista - LarguraComPrevia - EspacoPrevia : _larguraLista;
        var mostrar = util >= LarguraMinimaColunas;
        if (Grade.MostrarColunas == mostrar) return;
        Grade.MostrarColunas = mostrar;
        ReconstruirLinhas();
    }

    // ---- Ações da linha ("⋯"), respeitando as permissões ----

    private const string AcaoAbrir = "Abrir ficha";
    private const string AcaoPrevia = "Mostrar prévia";
    private const string AcaoHistorico = "Ver histórico";
    private const string AcaoDesativar = "Desativar cadastro";
    private const string AcaoReativar = "Reativar cadastro";

    [RelayCommand]
    private async Task AcoesDaLinhaAsync(PessoaResumo? linha)
    {
        if (linha is null) return;
        var podeInativar = _sessao.Possui(Permissoes.Pessoas.Inativar);
        var linhaDaGrade = Linhas.FirstOrDefault(l => l.Pessoa.Id == linha.Id);
        var opcoes = new List<string> { AcaoAbrir };
        if (Previa.Cabe && linhaDaGrade is not null) opcoes.Add(AcaoPrevia);
        opcoes.Add(AcaoHistorico);
        if (podeInativar && linha.EmUso) opcoes.Add(AcaoDesativar);
        if (podeInativar && linha.Situacao == SituacaoPessoa.Inativo) opcoes.Add(AcaoReativar);

        var escolha = await EscolherAsync(linha.Nome, opcoes);
        if (escolha is null) return;
        if (escolha == AcaoPrevia)
        {
            if (linhaDaGrade is not null) Previa.Mostrar(linhaDaGrade);
            return;
        }

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

    // ---- Avisos da lista: erro e aviso ficam na barra até a próxima ação. O sucesso, que flutuava embaixo da lista e
    // sumia sozinho, passou para a camada global de mensagens (toast), a mesma de todas as telas. ----

    public bool MostrarBarraDaLista => SemFicha && TemMensagem;

    // ---- Configurações de Pessoas: ficam nesta tela (saíram do menu lateral) ----

    /// <summary>A tela navega para a rota (ex.: "papeis", "configuracoes-pessoas").</summary>
    public Func<string, Task>? AbrirTela { get; set; }

    /// <summary>Cadastros de configuração de Pessoas que o usuário pode abrir (o botão "Configurações" some sem nenhum).</summary>
    public IReadOnlyList<ItemConfiguracao> ConfiguracoesPermitidas =>
        [.. ConfiguracoesViewModel.Montar(_sessao.Possui, ModulosConfiguracao.Pessoas).SelectMany(g => g.Itens)];

    public bool PodeConfigurar => ConfiguracoesPermitidas.Count > 0;

    /// <summary>Abre a página "Configurações de Pessoas" (os cadastros em cartões), como o item antigo do menu (decisão do usuário).</summary>
    [RelayCommand]
    private Task ConfiguracoesAsync() =>
        PodeConfigurar && AbrirTela is not null ? AbrirTela(ModulosConfiguracao.Rota(ModulosConfiguracao.Pessoas)) : Task.CompletedTask;

    // ---- Ações rápidas da linha: ligar, WhatsApp, e-mail (contato principal) ----

    /// <summary>Abre um endereço no aparelho ("tel:", "mailto:", "https://wa.me/..."). A tela fornece.</summary>
    public Func<string, Task>? AbrirEndereco { get; set; }

    [RelayCommand]
    private Task LigarAsync(LinhaPessoa? linha) =>
        EnderecoDoTelefone(linha?.Pessoa.TelefonePrincipal, whatsApp: false) is { } uri ? AbrirAsync(uri) : Task.CompletedTask;

    [RelayCommand]
    private Task WhatsAppAsync(LinhaPessoa? linha) =>
        EnderecoDoTelefone(linha?.Pessoa.TelefonePrincipal, whatsApp: true) is { } uri ? AbrirAsync(uri) : Task.CompletedTask;

    [RelayCommand]
    private Task EnviarEmailAsync(LinhaPessoa? linha) =>
        string.IsNullOrWhiteSpace(linha?.Pessoa.EmailPrincipal)
            ? Task.CompletedTask
            : AbrirAsync("mailto:" + linha.Pessoa.EmailPrincipal.Trim());

    /// <summary>
    /// "tel:+5538999887766" ou "https://wa.me/5538999887766". Número com 10 ou 11 dígitos (DDD + número) ganha o 55 do
    /// Brasil; outro tamanho vai como está (já com o código do país ou ramal). Sem dígitos: nada.
    /// </summary>
    public static string? EnderecoDoTelefone(string? telefone, bool whatsApp)
    {
        var internacional = telefone?.TrimStart().StartsWith('+') == true;
        var digitos = Documento.SomenteDigitos(telefone);
        if (!internacional && digitos.Length is 11 or 12 && digitos[0] == '0') digitos = digitos[1..]; // "0" de longa distância
        if (digitos.Length < 8) return null;
        var completo = !internacional && digitos.Length is 10 or 11 ? "55" + digitos : digitos;
        return whatsApp ? "https://wa.me/" + completo : "tel:+" + completo;
    }

    private async Task AbrirAsync(string uri)
    {
        if (AbrirEndereco is null) return;
        try { await AbrirEndereco(uri); }
        catch (Exception) { Mostrar("Não foi possível abrir o aplicativo para este contato neste aparelho.", TipoMensagem.Aviso); }
    }

    // ---- "Ordenar por" (celular: sem cabeçalho de colunas) ----

    private const string OrdemPadrao = "Ordem padrão (nome)";

    [RelayCommand]
    private async Task OrdenarPorAsync()
    {
        var colunas = Grade.ColunasOrdenaveis();
        var escolha = await EscolherAsync("Ordenar por", [OrdemPadrao, .. colunas.Select(c => c.Nome)]);
        if (escolha is null) return;
        if (escolha == OrdemPadrao)
        {
            Grade.OrdenarPor(null, DirecaoOrdenacao.Crescente);
            return;
        }
        var coluna = colunas.First(c => c.Nome == escolha);
        const string crescente = "Crescente (A → Z, menor → maior, mais antiga → mais nova)";
        const string decrescente = "Decrescente (Z → A, maior → menor, mais nova → mais antiga)";
        var direcao = await EscolherAsync(coluna.Nome, [crescente, decrescente]);
        if (direcao is null) return;
        Grade.OrdenarPor(coluna.Id, direcao == crescente ? DirecaoOrdenacao.Crescente : DirecaoOrdenacao.Decrescente);
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

    /// <summary>
    /// Motivo da alteração (opcional, vai para o histórico): só aparece com alteração, primeiro como o botão
    /// "+ Adicionar motivo"; o campo abre ao clicar (padrão da barra da ficha, 03/10/2026).
    /// </summary>
    [ObservableProperty] private bool _motivoAberto;

    public bool MostrarBotaoMotivo => PodeSalvar && PodeDescartar && !MotivoAberto;

    public bool MostrarMotivo => PodeSalvar && PodeDescartar && MotivoAberto;

    [RelayCommand]
    private void AbrirMotivo() => MotivoAberto = true;

    /// <summary>Desistiu do motivo: fecha o campo e apaga o que foi digitado.</summary>
    [RelayCommand]
    private void FecharMotivo()
    {
        if (Formulario is { } f) f.MotivoAlteracao = string.Empty;
        MotivoAberto = false;
    }

    /// <summary>Desativar/reativar: cadastro já gravado e permissão de inativar (a API confere de novo).</summary>
    public bool PodeDesativar => Formulario is { Existente: true, PodeEscolherSituacao: true } && _sessao.Possui(Permissoes.Pessoas.Inativar);
    public bool PodeReativar => Formulario is { Existente: true, EstaInativo: true } && _sessao.Possui(Permissoes.Pessoas.Inativar);

    protected override async Task AbrirAsync(PessoaResumo item)
    {
        var dto = await _pessoas.ObterAsync(item.Id) ?? throw new ValidacaoException(["Este cadastro não existe mais."]);
        Formulario = PessoaFormulario.De(dto, _campos, _etiquetas, _profissoes, _papeis, _tiposMeio, _tiposEndereco, _tiposDocumento, _camposDocumento, _finalidadesEndereco);
        Formulario.OferecerOcupacoesCbo(_ocupacoesCbo);
        await CarregarTerritoriosAsync(item.Id);
    }

    // ---- Territórios do cliente (Fase 2b-1b): por mapa, o território de hoje, desde quando e a origem (só leitura) ----

    public ObservableCollection<string> TerritoriosDoCliente { get; } = new();
    [ObservableProperty] private bool _mostrarTerritorios;

    private async Task CarregarTerritoriosAsync(Guid pessoaId)
    {
        TerritoriosDoCliente.Clear();
        MostrarTerritorios = Lone.Cliente.ViewModels.MenuViewModel.PodeVerOperacoesTerritoriais(_sessao.Possui);
        if (!MostrarTerritorios) return;
        try
        {
            var hoje = DateOnly.FromDateTime(DateTime.Now);
            foreach (var t in (await _territoriosApi.DoClienteAsync(pessoaId, hoje)).Territorios)
                TerritoriosDoCliente.Add($"{t.Mapa}: {t.Caminho} · desde {TextoTela.Data(t.InicioEm)} · {t.OrigemDescricao}" +
                                         (t.Operacao is null ? string.Empty : $" ({t.Operacao})"));
        }
        catch (Exception)
        {
            MostrarTerritorios = false; // a ficha continua sem o bloco (ex.: sem permissão)
        }
    }

    protected override Task NovoItemAsync()
    {
        Formulario = PessoaFormulario.NovaPessoa(_campos, _etiquetas, _profissoes, _papeis, _tiposMeio, _tiposEndereco, _tiposDocumento, _camposDocumento, _finalidadesEndereco);
        Formulario.OferecerOcupacoesCbo(_ocupacoesCbo);
        if (_relacaoParaNova is { } relacao) Formulario.NascerRelacionada(relacao.Pedido, relacao.Tipo, relacao.Outra);
        return Task.CompletedTask;
    }

    /// <summary>Relacionamento com que o próximo cadastro novo nasce ("Cadastrar nova pessoa assim"; Fase 2a-3, E9).</summary>
    private (IncluirRelacionamentoRequisicao Pedido, string Tipo, string Outra)? _relacaoParaNova;

    protected override object? DadosDaFicha() => Formulario?.ParaDto();
    protected override object? FichaObservada => Formulario;
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
        Formulario.OferecerOcupacoesCbo(_ocupacoesCbo);
        SecaoSelecionada = Secoes.FirstOrDefault(s => s.Secao == aba) ?? Secoes[0];
    }

    // ---- Erros que levam ao campo (Lone Contextual, Fase 1) ----

    /// <summary>Resumo dos erros da última tentativa de salvar (fixo acima da ficha). Só muda numa nova conferência.</summary>
    public ResumoValidacao Validacao { get; } = new() { Titulo = "Não foi possível salvar a pessoa" };

    // ---- Destaque de alterações (o que mudou desde a última gravação e de onde veio) ----

    /// <summary>"Destacar alterações": os campos alterados desde a última gravação, com a origem (Receita ou digitado).</summary>
    public DestaquesFicha Destaques { get; } = new();

    private string? _fotoDosDestaques;
    private PessoaDto? _gravadoDosDestaques;

    protected override void AlteracoesAvaliadas()
    {
        AtualizarDestaques();
        ReconferirErros();
    }

    /// <summary>As marcas das abas ("●N" erros, "◆N" destaques) são derivadas da ficha: mudar não é alterar a ficha.</summary>
    protected override bool PropriedadeDaFicha(string? propriedade) =>
        propriedade is not (nameof(ErrosPorAba) or nameof(DestaquesPorAba));

    /// <summary>
    /// Compara a ficha com a gravada, campo a campo (<see cref="AlteracoesDaFicha"/>). Cadastro novo: tudo é novo, então só o
    /// que veio da consulta é destacado. Ajuda visual: um estado intermediário que não monta não pode atrapalhar a digitação.
    /// </summary>
    private void AtualizarDestaques()
    {
        try
        {
            if (Formulario is not { } f || FotoGravada is not { } foto)
            {
                Destaques.Definir([]);
                return;
            }
            if (!ReferenceEquals(foto, _fotoDosDestaques))
            {
                _gravadoDosDestaques = JsonSerializer.Deserialize<PessoaDto>(foto, Lone.Contracts.Comum.OpcoesJson.Padrao);
                _fotoDosDestaques = foto;
            }
            if (_gravadoDosDestaques is null)
            {
                Destaques.Definir([]);
                return;
            }
            var destaques = new List<DestaqueCampo>();
            foreach (var (chave, antes, agora) in AlteracoesDaFicha.Comparar(_gravadoDosDestaques, f.ParaDto()))
            {
                var daReceita = f.OrigemReceita.TryGetValue(chave, out var origem) && AplicacaoReceita.Igual(origem.Valor, agora);
                if (f.Nova && !daReceita) continue;
                destaques.Add(new DestaqueCampo(chave, daReceita ? "Receita" : null, antes, daReceita ? origem.Quando : null));
            }
            Destaques.Definir(destaques);
        }
        catch (Exception)
        {
            Destaques.Definir([]);
        }
    }

    /// <summary>
    /// Gravação com valores trazidos pela Receita e sem motivo escrito pelo usuário: o motivo registra a consulta (vai para
    /// o histórico do cadastro). Só no que vai para a API: a ficha não muda.
    /// </summary>
    private PessoaDto DtoParaGravar(PessoaFormulario formulario)
    {
        var dto = formulario.ParaDto();
        if (dto.MotivoAlteracao is null && Destaques.QuantidadeDaReceita > 0)
            dto.MotivoAlteracao = $"Dados preenchidos pela consulta à Receita Federal ({formulario.ConferenciaReceita.Fonte}) " +
                                  $"em {DateTime.Now:dd/MM/yyyy HH:mm}.";
        return dto;
    }

    /// <summary>Quantos erros há em cada aba (o "●N" nas abas).</summary>
    public IReadOnlyDictionary<SecaoPessoa, int> ErrosPorAba =>
        Validacao.Itens.Select(i => AbaDoCampo.De(i.Erro.Campo, i.Erro.Item)).OfType<SecaoPessoa>()
            .GroupBy(a => a).ToDictionary(g => g.Key, g => g.Count());

    /// <summary>
    /// Quantos campos destacados há em cada aba (o "◆N" nas abas), só com "Destacar alterações" ligado: quem liga o
    /// destaque vê em que aba estão as alterações sem procurar aba por aba. Alteração não é erro: outra marca, outra cor.
    /// </summary>
    public IReadOnlyDictionary<SecaoPessoa, int> DestaquesPorAba =>
        !Destaques.Ligado
            ? new Dictionary<SecaoPessoa, int>()
            : Destaques.Itens.Select(d => AbaDoCampo.De(d.Chave.Campo, d.Chave.Item)).OfType<SecaoPessoa>()
                .GroupBy(a => a).ToDictionary(g => g.Key, g => g.Count());

    /// <summary>Troca para a aba do campo (de um erro ou de uma pendência); falso se a aba não existe para esta pessoa.</summary>
    private bool IrParaAbaDoErro(DestinoCampo destino)
    {
        if (AbaDoCampo.De(destino.Campo, destino.Item) is not { } aba || Secoes.FirstOrDefault(s => s.Secao == aba) is not { } secao) return false;
        SecaoSelecionada = secao;
        return true;
    }

    partial void OnFormularioChanged(PessoaFormulario? oldValue, PessoaFormulario? newValue)
    {
        if (oldValue is not null) Desligar(oldValue);
        Validacao.Limpar(); // outra ficha (ou a mesma relida depois de gravar): os erros eram da tentativa anterior
        Historico.Clear();
        _historicoDe = null;
        _ultimoDoHistorico = null;
        TemMaisHistorico = false;
        ReiniciarFiltroHistorico();
        if (newValue is null)
        {
            Resumo.Atualizar(null, IrPara);
            return;
        }

        newValue.PodeVerDadosSensiveis = _sessao.Possui(Permissoes.Pessoas.VisualizarDadosSensiveis);
        newValue.PodeVerPrivacidade = _sessao.Possui(Permissoes.Pessoas.Privacidade);
        // Papéis e etiquetas: a tela respeita as mesmas permissões da gravação (a API continua conferindo).
        var podeEditar = !newValue.EstaArquivado && _sessao.Possui(newValue.Nova ? Permissoes.Pessoas.Criar : Permissoes.Pessoas.Editar);
        newValue.PapeisFicha.DefinirPermissoes(podeEditar, _sessao.Possui(Permissoes.Pessoas.GerenciarEmpresasDoGrupo));
        newValue.Etiquetas.DefinirPermissao(podeEditar);
        newValue.Privacidade.Acoes.Conceder = ConcederConsentimentoAsync;
        newValue.Privacidade.Acoes.Revogar = RevogarConsentimentoAsync;
        newValue.ConsultaCep = ConsultarCepAsync;
        newValue.ConferenciaCep = ConferirCepAsync;
        newValue.BuscaCepPorEndereco = BuscarCepPorEnderecoAsync;
        newValue.SegundaOpiniaoCep = ConsultarOutraFonteCepAsync;
        newValue.Confirmar = ConfirmarAsync; // diálogo da base (CadastroViewModelBase)
        newValue.ConsolidarNoServidor = ConsolidarEnderecosAsync;
        newValue.TemAlteracoesNaoSalvas = () => TemAlteracoes;
        newValue.ConsultaCnpj = ConsultarCnpjAsync;
        newValue.ConferenciaReceita.AoAplicar = campos =>
        {
            newValue.RegistrarOrigemReceita(campos);
            AtualizarDestaques();
        };
        newValue.AoCompletarDocumento = () => DocumentoCompletoAsync(newValue);
        newValue.FonteMunicipios = uf => _municipios.ListarDaUfAsync(uf);
        newValue.AcoesAnexos.Anexar = AnexarAsync;
        newValue.Situacoes.Acoes.Bloquear = BloquearAsync;
        newValue.Situacoes.Acoes.Liberar = LiberarBloqueioAsync;
        newValue.Situacoes.Acoes.RegistrarInteracao = RegistrarInteracaoAsync;
        newValue.Relacionamentos.Acoes.BuscarPessoa = BuscarPessoaParaRelacionamentoAsync;
        newValue.Relacionamentos.Acoes.Incluir = IncluirRelacionamentoAsync;
        newValue.Relacionamentos.Acoes.CadastrarNova = CadastrarNovaRelacionadaAsync;
        newValue.Relacionamentos.Acoes.AoMudarEscolha = LimparAvisoDoRelacionamento;
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
        // Busca de CEP pelo endereço em andamento na ficha que sai: cancelada (nada dela chega à ficha nova).
        foreach (var endereco in formulario.Enderecos) endereco.CancelarBuscaCepCommand.Execute(null);
        formulario.ConsultaCep = null;
        formulario.ConferenciaCep = null;
        formulario.BuscaCepPorEndereco = null;
        formulario.SegundaOpiniaoCep = null;
        formulario.Confirmar = null;
        formulario.ConsultaCnpj = null;
        formulario.AoCompletarDocumento = null;
        formulario.FonteMunicipios = null;
        formulario.AcoesAnexos.Anexar = null;
        formulario.Situacoes.Acoes.Bloquear = null;
        formulario.Situacoes.Acoes.Liberar = null;
        formulario.Situacoes.Acoes.RegistrarInteracao = null;
        formulario.Relacionamentos.Acoes.BuscarPessoa = null;
        formulario.Relacionamentos.Acoes.Incluir = null;
        formulario.Relacionamentos.Acoes.CadastrarNova = null;
        formulario.Relacionamentos.Acoes.AoMudarEscolha = null;
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

    /// <summary>Resumo da pessoa (painel ao lado ou cartão): refeito ao abrir a ficha, ao trocar de aba e depois de salvar.</summary>
    public ResumoPessoa Resumo { get; } = new();

    /// <summary>
    /// Tocar num item do resumo da pessoa ("Resolver", Lone Contextual Fase 2): com campo, o mesmo caminho do erro que leva ao
    /// campo (aba, rolagem e foco), sem marcar nada; sem campo, só a aba.
    /// </summary>
    private void IrPara(DestinoFicha destino)
    {
        if (destino.Campo is { } campo && Validacao.Levar(new DestinoCampo(campo, destino.Item))) return;
        IrParaAba(destino.Aba);
    }

    /// <summary>Tocar num item do resumo leva à aba onde o assunto é resolvido (se ela existir para esta pessoa).</summary>
    private void IrParaAba(SecaoPessoa aba) =>
        SecaoSelecionada = Secoes.FirstOrDefault(s => s.Secao == aba) ?? SecaoSelecionada;

    /// <summary>Cargas sob demanda: cada uma só na primeira vez que a aba precisa, nesta ficha.</summary>
    private void CarregarDaAba(SecaoOpcao? value)
    {
        Resumo.Atualizar(Formulario, IrPara);
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
            if (!continuar) _ = CarregarOpcoesHistoricoAsync(pessoaId);
            var registros = await _pessoas.ListarHistoricoAsync(pessoaId, continuar ? _ultimoDoHistorico : null, filtro: _filtroHistorico);
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

        // Conferência do app: os erros vão para o resumo fixo (com o campo de cada um) e a ficha leva ao primeiro.
        if (formulario.ValidarLocalmenteComCampos() is { Count: > 0 } erros)
        {
            MostrarErrosDaFicha(erros, doApp: true);
            return;
        }

        if (await ConfirmarSubstituicoesDeVendedorAsync(formulario) is not { } desfazerSubstituicoes) return;

        var eraEmpresaDoGrupo = formulario.PapelEmpresaDoGrupo.Existia && !formulario.Nova;
        var eraNova = formulario.Nova;
        ResultadoSalvarPessoa? resultado = null;
        ValidacaoException? recusa = null;
        var executou = await ExecutarAsync(async () =>
        {
            // Recusa por regra: vai para o resumo da ficha (com os campos que a API informar), não para a barra de mensagens.
            try { resultado = await _pessoas.SalvarAsync(DtoParaGravar(formulario)); }
            catch (ValidacaoException v) { recusa = v; }
        });
        if (!executou || recusa is not null)
        {
            desfazerSubstituicoes(); // não gravou: o vendedor anterior volta como estava (a pergunta aparece de novo)
            if (recusa is not null) MostrarErrosDaFicha(recusa.Itens, doApp: false);
            return;
        }
        Validacao.Limpar();

        // Escolheu uma ocupação da CBO: a API usou ou criou a profissão, que a tela relê para mostrar pelo nome.
        if (resultado!.Pessoa.ProfissaoId is { } profissaoGravada && _profissoes.All(p => p.Id != profissaoGravada))
        {
            try { _profissoes = await _profissoesApi.ListarAsync(incluirInativas: true); }
            catch (Exception ex) when (ex is not SessaoExpiradaException) { /* a ficha mantém a gravada sem o nome */ }
        }

        MostrarGravada(resultado.Pessoa);
        MarcarFichaSemAlteracoes();

        // Confirmação com o nome que o cabeçalho da ficha mostra ("Pessoa salva: Bruno"); contexto = a pessoa, para que
        // duas pessoas salvas em seguida não se juntem num "×2".
        if (resultado.Avisos.Count > 0)
            Mostrar("Salvo, com avisos:" + Environment.NewLine + string.Join(Environment.NewLine, resultado.Avisos), TipoMensagem.Aviso);
        else
            Mostrar(TextosMensagem.ComNome(eraNova ? "Pessoa criada" : "Pessoa salva", formulario.Titulo), TipoMensagem.Sucesso,
                acao: null, contexto: $"pessoa:{resultado.Pessoa.Id}");

        // Empresas do grupo mudaram: a sessão relê as empresas disponíveis (menu "Trocar empresa").
        if (eraEmpresaDoGrupo || resultado.Pessoa.TemPapel(TipoPapel.EmpresaDoGrupo))
            await AtualizarEmpresasDaSessaoAsync();

        await AtualizarListaAposGravarAsync();
    }

    /// <summary>
    /// Erros de validação da ficha: no resumo fixo, com o campo de cada um; a ficha vai para o primeiro que tem campo (os
    /// outros continuam no resumo). Só erros gerais: ficam no resumo, sem levar a lugar nenhum.
    /// </summary>
    private void MostrarErrosDaFicha(IReadOnlyList<ErroValidacao> erros, bool doApp)
    {
        LimparMensagem();
        _itensDosErros = Formulario?.ItensPresentes() ?? [];
        _errosDoApp = doApp;
        Validacao.Definir(erros);
        Validacao.IrParaPrimeiro();
    }

    /// <summary>Os itens da ficha (endereços, documentos...) que existiam quando os erros foram mostrados.</summary>
    private HashSet<Guid> _itensDosErros = [];

    /// <summary>Os erros mostrados vieram da conferência do app (que dá para repetir aqui); falso = vieram da API.</summary>
    private bool _errosDoApp;

    /// <summary>
    /// A cada mudança na ficha, tira do resumo (e das marcas e contadores das abas) só o erro que comprovadamente deixou de
    /// valer: o do item que saiu da ficha (documento, endereço... removido) e, se os erros vieram da conferência do app, o
    /// do campo que a mesma conferência já não aponta. Erro da API que o app não sabe repetir (ex.: número repetido em
    /// outra pessoa) continua até a próxima tentativa de salvar, a não ser que o item dele tenha saído (04/10 e 06/10/2026).
    /// </summary>
    private void ReconferirErros()
    {
        if (!Validacao.Visivel || Formulario is not { } formulario) return;
        HashSet<Guid> presentes;
        List<ErroValidacao>? doAppAgora = null;
        try
        {
            presentes = formulario.ItensPresentes();
            if (_errosDoApp) doAppAgora = [.. formulario.ValidarLocalmenteComCampos()];
        }
        catch (Exception)
        {
            return; // estado intermediário da digitação: fica como está até a próxima mudança
        }
        Validacao.Reconferir(e =>
        {
            if (e.Item is { } item && _itensDosErros.Contains(item) && !presentes.Contains(item)) return false;
            if (doAppAgora is null) return true;
            // Com campo: vale enquanto a conferência ainda apontar o mesmo campo do mesmo item (o texto pode mudar, ex.:
            // "Endereço 2" que virou "Endereço 1"); sem campo (erro geral): enquanto a mesma mensagem continuar.
            return e.Campo is { } campo
                ? doAppAgora.Any(a => a.Campo == campo && a.Item == e.Item)
                : doAppAgora.Any(a => a.Campo is null && a.Mensagem == e.Mensagem);
        });
    }

    /// <summary>
    /// Vendedor novo no lugar de um vigente do mesmo tipo principal/exclusivo (D3): pergunta com o que vai acontecer e, se
    /// o usuário confirmar, encerra o anterior na véspera (nada é apagado). Retroativo não substitui: explica e não grava.
    /// O servidor confere tudo de novo na mesma gravação (e a versão da pessoa barra quem gravou antes).
    /// </summary>
    /// <returns>Como desfazer o que foi aplicado (se a gravação falhar); nulo = não gravar (cancelou ou é retroativo).</returns>
    private async Task<Action?> ConfirmarSubstituicoesDeVendedorAsync(PessoaFormulario formulario)
    {
        var aplicadas = new List<Action>();
        void Desfazer() { foreach (var desfazer in Enumerable.Reverse(aplicadas)) desfazer(); }

        foreach (var substituicao in formulario.SubstituicoesDeVendedor())
        {
            if (substituicao.Impedida)
            {
                Desfazer();
                Mostrar(substituicao.Mensagem, TipoMensagem.Aviso);
                IrParaAba(SecaoPessoa.Comercial);
                return null;
            }
            if (!await ConfirmarAsync(substituicao.Titulo, substituicao.Mensagem, SubstituicaoVendedor.TextoConfirmar, "Cancelar"))
            {
                Desfazer();
                return null;
            }
            aplicadas.Add(substituicao.Aplicar());
        }
        return Desfazer;
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
                ? $"{formulario.Nome} deixará de aparecer nas buscas e operações (continua acessível pelo filtro \"Situação do cadastro\", com todo o histórico, e pode ser reativado). Motivo (opcional):"
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
        Mostrar(desativar ? "Cadastro desativado. Ele continua acessível pelo filtro \"Situação do cadastro\" (Inativo)." : "Cadastro reativado.", TipoMensagem.Sucesso);
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
        if (Formulario is { } f) f.AdicionarContato(new ContatoFormulario { Principal = !f.Contatos.Any(c => c.Ativo) });
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

    /// <summary>
    /// CPF/CNPJ completo na ficha: avisa se já está em outro cadastro; na PJ nova (e sem duplicidade), consulta a Receita
    /// sozinha. Cadastro já gravado só consulta pelo botão (não sobrescreve dados sem o usuário pedir).
    /// </summary>
    private async Task DocumentoCompletoAsync(PessoaFormulario ficha)
    {
        var documento = ficha.DocumentoCompleto;
        DocumentoEmUsoResposta? resposta = null;
        try
        {
            resposta = await _pessoas.DocumentoEmUsoAsync(new DocumentoEmUsoRequisicao
            {
                Natureza = ficha.Natureza.Valor, Documento = documento, IgnorarId = ficha.Id
            });
        }
        catch (Exception)
        {
            // O aviso é uma ajuda: sem ele (rede, permissão), a gravação continua recusando o documento duplicado.
        }

        // O usuário mudou o documento (ou trocou de ficha) enquanto a pergunta ia e voltava: a resposta não vale mais.
        if (!ReferenceEquals(Formulario, ficha) || ficha.DocumentoCompleto != documento) return;

        if (resposta is { EmUso: true })
        {
            ficha.DefinirDocumentoEmUso(resposta);
            return;
        }
        if (ficha.EhJuridica && ficha.Nova)
            await ConsultarCnpjAsync(ficha.Principal);
    }

    /// <summary>
    /// Abre a ficha pedida por outra tela ("Abrir ficha" em transferências, carteira vencendo...). Pergunta antes se houver
    /// alterações não salvas em outra ficha, como ao tocar numa linha.
    /// </summary>
    public void AbrirPessoa(Guid id) => Selecionado = new PessoaResumo { Id = id };

    /// <summary>
    /// Ação "Abrir" do toast de relacionamento: link interno pelo motor de navegação (volta para Pessoas se o usuário já
    /// saiu, abre a ficha, e o Voltar retorna à pessoa de onde veio). Sem motor ligado (testes), o caminho antigo.
    /// </summary>
    private async Task AbrirOutraPessoaAsync(Guid id, string nome)
    {
        if (Navegacao.Conectado)
        {
            await Navegacao.AbrirAsync(new LocalNavegacao(AberturaDePessoa.RotaPessoas, new ReferenciaRegistro(TipoRegistro, id, nome)),
                OrigemNavegacao.Link);
            return;
        }
        AbrirPessoa(id);
        if (AbrirTela is not null) await AbrirTela(AberturaDePessoa.RotaPessoas);
    }

    // ---- Navegação por registros (motor global) ----

    /// <summary>Tipo do registro de pessoa na navegação (endereço interno: lone://pessoas/pessoa/{id}).</summary>
    public const string TipoPessoa = "pessoa";

    protected override string TipoRegistro => TipoPessoa;

    /// <summary>Links e Voltar abrem qualquer pessoa pelo Id (a ficha é lida no servidor), mesmo fora da página carregada.</summary>
    protected override PessoaResumo? CriarItemParaAbrir(Guid id) => new() { Id = id };

    // ---- Estado de navegação (piloto completo da preservação de contexto) ----

    /// <summary>Aplicando um estado guardado: o painel e a grade não disparam releituras nem marcam a visão como alterada.</summary>
    private bool _restaurandoContexto;

    public override EstadoTela CapturarEstado()
    {
        var baseEstado = base.CapturarEstado();
        var ordenacao = Grade.Ordenacao;
        return new EstadoPessoas
        {
            Busca = baseEstado.Busca,
            Registro = baseEstado.Registro,
            Rolagens = baseEstado.Rolagens,
            AbaRapida = _filtroRapido,
            Condicoes = Filtros.Condicoes().Select(Copiar).ToList(),
            VisaoId = VisaoAtual?.Id,
            VisaoAlterada = VisaoAlterada,
            OrdenacaoColuna = ordenacao?.Coluna,
            OrdenacaoDirecao = ordenacao?.Direcao ?? DirecaoOrdenacao.Crescente,
            Pagina = _pagina,
            PreviaId = Previa.Aberta ? Previa.Linha?.Pessoa.Id : null,
            Secao = Editando ? SecaoSelecionada?.Secao : null
        };
    }

    private static CondicaoFiltro Copiar(CondicaoFiltro c) =>
        JsonSerializer.Deserialize<CondicaoFiltro>(JsonSerializer.Serialize(c)) ?? c;

    /// <summary>
    /// Aba, filtros, visão, ordenação, pesquisa e página, nessa ordem. Tolerante: aba que não existe → "Todos"; condição
    /// inválida → só ela fica de fora (mesma aplicação das visões); visão removida → nenhuma; coluna que não ordena mais →
    /// ordem padrão; página que deixou de existir → a última (regra da própria listagem). Idempotente: só o que difere.
    /// </summary>
    protected override async Task<bool> AplicarContextoDaListaAsync(EstadoTela estado)
    {
        if (estado is not EstadoPessoas e) return await base.AplicarContextoDaListaAsync(estado);

        var reler = false;
        _restaurandoContexto = true;
        try
        {
            // Aba de visão/filtro rápido.
            var aba = FiltrosRapidos.FirstOrDefault(f => f.Chave == e.AbaRapida) ?? FiltrosRapidos.FirstOrDefault(f => f.Chave == FiltroRapido.Todos);
            if (aba is not null && aba.Chave != _filtroRapido)
            {
                _filtroRapido = aba.Chave;
                foreach (var f in FiltrosRapidos) f.Selecionado = ReferenceEquals(f, aba);
                Filtros.DefinirNatureza(aba.Natureza);
                reler = true;
            }

            // Filtros do painel. Compara o resultado (não o pedido): uma condição inválida fica de fora sem reler de novo
            // a cada restauração.
            var atuais = JsonSerializer.Serialize(Filtros.Condicoes());
            if (atuais != JsonSerializer.Serialize(e.Condicoes))
            {
                Filtros.Limpar();
                foreach (var condicao in e.Condicoes) Filtros.Aplicar(Copiar(condicao)); // inválida: só ela fica de fora
                if (JsonSerializer.Serialize(Filtros.Condicoes()) != atuais) reler = true;
            }

            // Visão marcada (só o rótulo: os filtros dela já vieram acima).
            if (e.VisaoId is { } visaoId && VisaoAtual?.Id != visaoId)
            {
                if (!_visoesCarregadas) await CarregarVisoesAsync();
                VisaoAtual = _visoes.FirstOrDefault(v => v.Id == visaoId);
            }
            else if (e.VisaoId is null && VisaoAtual is not null) VisaoAtual = null;
            VisaoAlterada = VisaoAtual is not null && e.VisaoAlterada;

            // Ordenação (coluna que não ordena mais → ordem padrão).
            var coluna = e.OrdenacaoColuna is { } c &&
                         (Grade.ColunasOrdenaveis().Any(o => o.Id == c) || Grade.Visiveis.Any(v => v.Id == c)) ? c : null;
            var atual = Grade.Ordenacao;
            if (coluna != atual?.Coluna || (coluna is not null && e.OrdenacaoDirecao != atual?.Direcao))
            {
                Grade.OrdenarPor(coluna, e.OrdenacaoDirecao);
                reler = true;
            }
        }
        finally
        {
            _restaurandoContexto = false;
        }
        _filtrosAtrasados?.Cancel();
        Indicadores.AtualizarMarcados();

        reler |= await base.AplicarContextoDaListaAsync(estado); // pesquisa (volta a página para 1)
        // Página: a pedida; se ela já foi pedida aqui e deu na última válida, fica nessa (não relê de novo).
        var pagina = !reler && _paginaRestaurada is { } r && r.Pedida == e.Pagina && r.Obtida == _pagina
            ? _pagina
            : Math.Max(1, e.Pagina);
        if (pagina != _pagina)
        {
            _pagina = pagina;
            reler = true;
        }
        else if (reler) _pagina = pagina;
        return reler;
    }

    /// <summary>Última página restaurada: a pedida e a que existia (página que deixou de existir → a última válida).</summary>
    private (int Pedida, int Obtida)? _paginaRestaurada;

    /// <summary>Aba da ficha (inexistente → a padrão) e prévia (pessoa fora da página → sem prévia).</summary>
    protected override Task RestaurarDetalhesAsync(EstadoTela estado)
    {
        if (estado is not EstadoPessoas e) return Task.CompletedTask;
        _paginaRestaurada = (e.Pagina, _pagina);
        if (Editando && e.Secao is { } secao && SecaoSelecionada?.Secao != secao &&
            Secoes.FirstOrDefault(s => s.Secao == secao) is { } opcao)
            SecaoSelecionada = opcao;
        if (e.PreviaId is { } id && !(Previa.Aberta && Previa.Linha?.Pessoa.Id == id) &&
            Linhas.FirstOrDefault(l => l.Pessoa.Id == id) is { } linha)
            Previa.Mostrar(linha);
        return Task.CompletedTask;
    }

    /// <summary>Abre o cadastro que já tem o documento (pergunta antes se houver alterações não salvas).</summary>
    [RelayCommand]
    private void AbrirCadastroEmUso()
    {
        if (Formulario?.DocumentoEmUsoId is { } id)
            Selecionado = new PessoaResumo { Id = id };
    }

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

        if (Formulario is not { } ficha) return;
        if (ReferenceEquals(estabelecimento, ficha.Principal))
        {
            // Cadastro gravado com CNPJ de outra raiz: não vira outra empresa (oferece cadastrar como nova pessoa).
            if (ficha.Existente && ficha.CnpjGravado.Length == 14 && !MesmaRaiz(ficha.CnpjGravado, dados.Cnpj))
            {
                await RecusarOutraEmpresaAsync(ficha, dados);
                return;
            }
            // Ficha nova já preenchida por outro CNPJ: pergunta antes de trocar (o mesmo CNPJ de novo: troca sem perguntar).
            if (ficha.Nova && ficha.ConsultaAplicada is { } anterior && anterior.Cnpj != Documento.Normalizar(dados.Cnpj))
            {
                if (!await ConfirmarAsync("Trocar a empresa", MensagemTroca(ficha, anterior, dados), "Trocar", "Cancelar"))
                {
                    ficha.RestaurarCnpj(anterior.Cnpj);
                    return;
                }
                if (!ReferenceEquals(Formulario, ficha)) return; // a ficha mudou enquanto o usuário decidia
            }
        }
        ficha.AplicarCnpj(estabelecimento, dados);
        // O que veio da consulta fica destacado (o usuário desliga no botão da barra da ficha).
        AtualizarDestaques();
        if (Destaques.QuantidadeDaReceita > 0) Destaques.Ligado = true;
        var conferir = ficha.ConferenciaReceita.Visivel
            ? " A Receita trouxe valores diferentes dos atuais: escolha no quadro acima o que trocar."
            : string.Empty;
        Mostrar($"Dados preenchidos pela consulta ({dados.Fonte}). " +
                (estabelecimento.InscricaoVeioDaConsulta
                    ? "A inscrição estadual também foi encontrada. Confira e salve."
                    : "A inscrição estadual não foi encontrada nas fontes públicas: informe-a (o sistema confere o dígito da UF ao salvar).") + conferir +
                (ficha.AvisoSocios.Length > 0 ? " " + ficha.AvisoSocios : string.Empty),
            ficha.AvisoSocios.Length > 0 ? TipoMensagem.Aviso : TipoMensagem.Informacao);
    }

    /// <summary>Mesma raiz de CNPJ (os 8 primeiros dígitos: matriz e filiais da mesma empresa).</summary>
    internal static bool MesmaRaiz(string a, string b)
    {
        var x = Documento.Normalizar(a);
        var y = Documento.Normalizar(b);
        return x.Length >= 8 && y.Length >= 8 && x[..8] == y[..8];
    }

    internal static string MensagemTroca(PessoaFormulario ficha, PessoaFormulario.ConsultaCnpjAplicada anterior, DadosCnpj nova)
    {
        var (substituidos, enderecoMantido) = ficha.PrevisaoDaTroca();
        var empresaAnterior = anterior.RazaoSocial.Length > 0 ? anterior.RazaoSocial : "outra empresa";
        var empresaNova = string.IsNullOrWhiteSpace(nova.RazaoSocial) ? "a nova empresa" : nova.RazaoSocial.Trim();
        var texto = $"A ficha já foi preenchida com os dados de {empresaAnterior} (CNPJ {Documento.Formatar(anterior.Cnpj)}). " +
                    $"Trocar pelos dados de {empresaNova} (CNPJ {Documento.Formatar(nova.Cnpj)})?\n\n" +
                    (substituidos == 1 ? "1 campo vindo da consulta anterior será substituído. "
                        : $"{substituidos} campos vindos da consulta anterior serão substituídos. ") +
                    "O que você digitou será mantido.";
        if (enderecoMantido is not null)
            texto += $"\nO endereço \"{enderecoMantido}\" foi alterado por você e será mantido.";
        return texto;
    }

    /// <summary>
    /// Cadastro gravado consultado com CNPJ de outra raiz: recusa (um cadastro não vira outra empresa) e oferece cadastrar a
    /// empresa como nova pessoa. Se ela já estiver cadastrada, oferece abrir esse cadastro (não nasce em dobro).
    /// </summary>
    private async Task RecusarOutraEmpresaAsync(PessoaFormulario ficha, DadosCnpj dados)
    {
        var cnpj = Documento.Normalizar(dados.Cnpj);
        var empresa = string.IsNullOrWhiteSpace(dados.RazaoSocial) ? "esta empresa" : dados.RazaoSocial.Trim();
        var aceitar = await ConfirmarAsync("Outra empresa",
            $"O CNPJ {Documento.Formatar(cnpj)} é de outra empresa (raiz diferente). Um cadastro gravado não pode virar outra " +
            $"empresa.\n\nDeseja cadastrar {empresa} como uma nova pessoa?",
            "Cadastrar como nova pessoa", "Cancelar");
        ficha.RestaurarCnpj(ficha.CnpjGravado);
        if (!aceitar) return;

        DocumentoEmUsoResposta? emUso = null;
        try
        {
            emUso = await _pessoas.DocumentoEmUsoAsync(new DocumentoEmUsoRequisicao
            {
                Natureza = NaturezaPessoa.Juridica, Documento = cnpj, IgnorarId = ficha.Id
            });
        }
        catch (Exception)
        {
            // Sem a resposta, a ficha nova confere de novo ao receber o CNPJ (e a gravação recusa o duplicado).
        }
        if (emUso is { EmUso: true })
        {
            if (emUso.ForaDoAlcance || emUso.Id is not { } id)
            {
                Mostrar("Já existe um cadastro com este CNPJ, fora do seu acesso. Procure o responsável pelo cadastro.", TipoMensagem.Aviso);
                return;
            }
            var nome = string.IsNullOrWhiteSpace(emUso.Nome) ? empresa : emUso.Nome.Trim();
            if (await ConfirmarAsync("CNPJ já cadastrado",
                    $"Já existe cadastro para este CNPJ: {nome} (código {emUso.Codigo:000000}).", "Abrir cadastro", "Cancelar"))
                Selecionado = new PessoaResumo { Id = id };
            return;
        }

        await NovoCommand.ExecuteAsync(null); // pergunta antes se houver alterações não salvas; nada é salvo sozinho
        if (Formulario is not { Nova: true } nova || ReferenceEquals(nova, ficha)) return;
        nova.Natureza = Opcao.De(OpcoesPessoa.Naturezas, NaturezaPessoa.Juridica);
        nova.Principal.Cnpj = Documento.Formatar(cnpj); // confere o documento e consulta a Receita (PJ nova)
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

    /// <summary>O último aviso de preenchimento do "Novo relacionamento" (some quando o tipo ou a pessoa muda).</summary>
    private string? _avisoDoRelacionamento;

    private void AvisarRelacionamento(string texto)
    {
        _avisoDoRelacionamento = texto;
        Mostrar(texto, TipoMensagem.Aviso);
    }

    /// <summary>
    /// O usuário escolheu o tipo ou a pessoa que o aviso pedia: o aviso sai da tela (só ele; outra mensagem que tenha vindo
    /// depois fica). Ao tentar de novo, a conferência roda outra vez.
    /// </summary>
    private void LimparAvisoDoRelacionamento()
    {
        if (_avisoDoRelacionamento is not null && Mensagem == _avisoDoRelacionamento) LimparMensagem();
        _avisoDoRelacionamento = null;
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
            AvisarRelacionamento(string.Join(Environment.NewLine, erros));
            return;
        }
        if (r.NovoTipo.Valor is { Societario: true } && !PodeAlterarEstruturaEmpresarial)
        {
            AvisarRelacionamento("Vínculos societários (sócio, administrador) exigem a permissão \"Alterar estrutura empresarial\".");
            return;
        }

        PessoaRelacionamentoDto? incluido = null;
        if (!await ExecutarAsync(async () => incluido = await _pessoas.IncluirRelacionamentoAsync(ficha.Id, r.ParaRequisicao())))
            return;
        r.Incluido(incluido!);
        // Piloto da ação do toast: a próxima ação útil é abrir a ficha da outra pessoa (pergunta antes, como sempre, se
        // houver alterações não salvas). Funciona mesmo depois de sair de Pessoas: volta para a tela e abre a ficha.
        var outra = incluido!.OutraPessoaId;
        Mostrar($"Relacionamento registrado: {incluido.Tipo} {incluido.OutraPessoaNome}.", TipoMensagem.Sucesso,
            new AcaoMensagem("Abrir", () => AbrirOutraPessoaAsync(outra, incluido.OutraPessoaNome), $"Abrir a ficha de {incluido.OutraPessoaNome}"),
            contexto: $"pessoa:{ficha.Id}");
    }

    /// <summary>
    /// "Cadastrar nova pessoa assim" (Fase 2a-3, E9): abre um cadastro novo que, ao salvar, já nasce com o relacionamento
    /// escolhido com esta ficha (ex.: contato de um cliente). É o caminho para quem tem alcance restrito cadastrar um contato
    /// ou sócio do cliente dele; a API confere tudo de novo.
    /// </summary>
    private async Task CadastrarNovaRelacionadaAsync()
    {
        if (Formulario is not { Existente: true } ficha)
        {
            Mostrar("Salve o cadastro antes de registrar relacionamentos.", TipoMensagem.Aviso);
            return;
        }
        var r = ficha.Relacionamentos;
        if (r.ValidarParaCadastroNovo() is { Count: > 0 } erros)
        {
            AvisarRelacionamento(string.Join(Environment.NewLine, erros));
            return;
        }
        if (r.NovoTipo.Valor is { Societario: true } && !PodeAlterarEstruturaEmpresarial)
        {
            AvisarRelacionamento("Vínculos societários (sócio, administrador) exigem a permissão \"Alterar estrutura empresarial\".");
            return;
        }
        _relacaoParaNova = (r.ParaCadastroNovo(ficha.Id), r.TipoVistoDaNova(), ficha.Nome);
        try
        {
            await NovoCommand.ExecuteAsync(null);
        }
        finally
        {
            _relacaoParaNova = null;
        }
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

    /// <summary>
    /// "Conferir CEP" (F2): a API confere pelo motor e devolve a decisão; a ficha só mostra. Usar a sugestão é escolha do
    /// usuário e vira alteração não salva (nada é gravado aqui).
    /// </summary>
    private async Task ConferirCepAsync(EnderecoFormulario endereco)
    {
        if (!Cep.TentarCriar(endereco.Cep, out _))
        {
            Mostrar("Digite um CEP válido (8 dígitos) para conferir.", TipoMensagem.Aviso);
            return;
        }

        DecisaoCepDto? decisao = null;
        var pedido = endereco.ParaConferencia();
        if (!await ExecutarAsync(async () => decisao = await _consultas.ConferirCepAsync(pedido)))
            return;
        // O endereço mudou enquanto a conferência estava em andamento: a resposta é de outros dados e não é mostrada.
        if (!endereco.MostrarConferencia(decisao!, pedido))
            Mostrar("O endereço mudou durante a conferência. Clique em \"Conferir CEP\" de novo.", TipoMensagem.Aviso);
    }

    /// <summary>
    /// "Consultar outra fonte" (Checkpoint G): a API compara a resposta da conferência com a de outra fonte. A ficha só
    /// mostra concordâncias e divergências; nenhuma fonte é escolhida e nada muda no endereço.
    /// </summary>
    private async Task ConsultarOutraFonteCepAsync(EnderecoFormulario endereco)
    {
        var cep = new string(endereco.Cep.Where(char.IsAsciiDigit).ToArray());
        if (cep.Length != 8) return;
        SegundaOpiniaoCepDto? opiniao = null;
        if (!await ExecutarAsync(async () => opiniao = await _consultas.ConsultarOutraFonteAsync(cep)))
            return;
        // O CEP mudou (ou a conferência saiu da tela) enquanto a consulta estava em andamento: a resposta não é mostrada.
        endereco.MostrarSegundaOpiniao(opiniao!, cep);
    }

    /// <summary>Busca de CEP pelo endereço indisponível (rede, servidor): amigável, sem detalhe técnico.</summary>
    public const string MensagemBuscaCepIndisponivel =
        "Não foi possível pesquisar CEPs neste momento. Você pode tentar novamente ou informar o CEP manualmente.";

    /// <summary>
    /// "Não sei o CEP" (Checkpoint D; D3 da busca de CEP por endereço): o usuário não sabe o CEP; a API busca pelo endereço
    /// e devolve candidatos. A ficha só mostra; usar um é escolha do usuário e vira alteração não salva (e passa pela
    /// conferência, D1). Faltou UF, município ou logradouro: nada é consultado, a mensagem diz o que falta e a ficha leva
    /// ao campo. Durante a busca a ficha continua usável ("Procurando CEPs…", com "Cancelar"); cancelada, nada muda.
    /// </summary>
    private async Task BuscarCepPorEnderecoAsync(EnderecoFormulario endereco)
    {
        if (endereco.ProcurandoCep) return; // uma busca por vez neste endereço
        if (endereco.FaltaParaBuscarCep() is { } falta)
        {
            endereco.MostrarOrientacaoBuscaCep(falta.Mensagem); // junto da ação (a faixa da ficha pode sair da vista)
            Mostrar(falta.Mensagem, TipoMensagem.Aviso);
            Validacao.Levar(new DestinoCampo(falta.Campo, endereco.Id));
            return;
        }

        var dados = endereco.ParaConferencia(); // os dados com que a busca foi pedida (resposta atrasada é descartada)
        var ct = endereco.IniciarBuscaCep();
        LimparMensagem();
        try
        {
            var decisao = await _consultas.BuscarCepPorEnderecoAsync(endereco.ParaBuscaPorEndereco(), ct);
            if (ct.IsCancellationRequested) return;
            if (!endereco.MostrarConferencia(decisao, dados))
                Mostrar("O endereço mudou durante a busca. Clique em \"Não sei o CEP\" de novo.", TipoMensagem.Aviso);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Cancelada pelo usuário: o endereço fica como estava e nenhum resultado aparece.
        }
        catch (Exception ex) when (ex is ServidorIndisponivelException or ErroDaApiException
                                       or System.Text.Json.JsonException or NotSupportedException)
        {
            // Rede, tempo esgotado, servidor com erro ou resposta que não dá para ler: amigável, sem detalhe técnico.
            Mostrar(MensagemBuscaCepIndisponivel, TipoMensagem.Aviso);
        }
        catch (Exception ex)
        {
            MostrarErro(ex); // dados recusados pela API, sessão expirada, permissão: as mensagens de sempre
        }
        finally
        {
            endereco.TerminarBuscaCep();
        }
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
            endereco.MarcarCepInexistente(); // aviso embaixo do CEP; a gravação também recusa
            return;
        }

        endereco.AplicarCep(dados);
        if (string.IsNullOrWhiteSpace(dados.Logradouro))
            Mostrar($"CEP geral de {dados.Cidade}/{dados.Uf}: esta cidade não tem CEP por rua. Informe o logradouro e o bairro.",
                TipoMensagem.Informacao);
    }
}
